using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Repository.Models;

namespace Repository.Services;

public class ElasticService
{
    public const string ArtworkIndexName = "artworks";

    private readonly ElasticsearchClient _client;
    private readonly ILogger<ElasticService> _logger;

    public ElasticService(IConfiguration configuration, ILogger<ElasticService> logger)
    {
        _logger = logger;

        var cloudId = configuration["Elasticsearch:CloudId"];
        var username = configuration["Elasticsearch:Username"];
        var password = configuration["Elasticsearch:Password"];

        if (string.IsNullOrWhiteSpace(cloudId) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Elasticsearch CloudId, Username, and Password are required.");
        }

        var settings = new ElasticsearchClientSettings(
                cloudId,
                new BasicAuthentication(username, password))
            .DefaultIndex(ArtworkIndexName)
            .DefaultFieldNameInferrer(name => name);

        _client = new ElasticsearchClient(settings);
    }

    public async Task EnsureArtworkIndexAsync()
    {
        try
        {
            var exists = await _client.Indices.ExistsAsync(ArtworkIndexName);
            if (exists.Exists)
                return;

            var mappingJson = """
            {
              "mappings": {
                "properties": {
                  "id": { "type": "integer" },
                  "title": { "type": "text" },
                  "description": { "type": "text" },
                  "artistName": { "type": "text" }
                }
              }
            }
            """;

            var response = await _client.Transport.PutAsync<StringResponse>(
                $"/{ArtworkIndexName}",
                PostData.String(mappingJson));

            if (!response.ApiCallDetails.HasSuccessfulStatusCode)
            {
                _logger.LogWarning(
                    "Elasticsearch index creation for {IndexName} returned status {StatusCode}: {Body}",
                    ArtworkIndexName,
                    response.ApiCallDetails.HttpStatusCode,
                    response.Body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Elasticsearch index startup check failed. Application will continue without Elastic search.");
        }
    }

    public async Task<string> GetInfoAsync()
    {
        var response = await _client.InfoAsync();
        return response.Version.Number;
    }

    public async Task IndexArtworkAsync(ArtworkSearchDocument artwork)
    {
        var json = JsonSerializer.Serialize(new
        {
            id = artwork.Id,
            title = artwork.Title,
            description = artwork.Description,
            artistName = artwork.ArtistName
        });

        var response = await _client.Transport.PutAsync<StringResponse>(
            $"/{ArtworkIndexName}/_doc/{artwork.Id}",
            PostData.String(json));

        if (!response.ApiCallDetails.HasSuccessfulStatusCode)
        {
            throw new InvalidOperationException(
                $"Elasticsearch indexing failed with status {response.ApiCallDetails.HttpStatusCode}: {response.Body}");
        }
    }

    public async Task<List<int>> SearchArtworkIdsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<int>();

        var searchJson = JsonSerializer.Serialize(new
        {
            size = 100,
            query = new
            {
                multi_match = new
                {
                    query,
                    fields = new[] { "title^3", "artistName^2", "description" },
                    fuzziness = "AUTO"
                }
            },
            _source = new[] { "id" }
        });

        var response = await _client.Transport.PostAsync<StringResponse>(
            $"/{ArtworkIndexName}/_search",
            PostData.String(searchJson));

        if (!response.ApiCallDetails.HasSuccessfulStatusCode)
        {
            throw new InvalidOperationException(
                $"Elasticsearch search failed with status {response.ApiCallDetails.HttpStatusCode}: {response.Body}");
        }

        using var document = JsonDocument.Parse(response.Body);
        var hits = document.RootElement
            .GetProperty("hits")
            .GetProperty("hits");

        var ids = new List<int>();
        foreach (var hit in hits.EnumerateArray())
        {
            if (hit.TryGetProperty("_source", out var source) &&
                source.TryGetProperty("id", out var idElement) &&
                idElement.TryGetInt32(out var id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }
}
