using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace API.Services;

public class PaypalService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public PaypalService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> CreateOrderAsync(decimal total)
    {
        var token = await GetAccessTokenAsync();
        var payload = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    amount = new
                    {
                        currency_code = GetCurrency(),
                        value = total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GetBaseUrl()}/v2/checkout/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("id").GetString()
               ?? throw new InvalidOperationException("PayPal order id missing.");
    }

    public async Task<bool> CaptureOrderAsync(string paypalOrderId)
    {
        var token = await GetAccessTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GetBaseUrl()}/v2/checkout/orders/{paypalOrderId}/capture");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) return false;

        using var document = JsonDocument.Parse(body);
        var status = document.RootElement.GetProperty("status").GetString();
        return string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var clientId = _configuration["PayPal:ClientId"] ?? throw new InvalidOperationException("PayPal:ClientId is missing.");
        var secret = _configuration["PayPal:Secret"] ?? throw new InvalidOperationException("PayPal:Secret is missing.");
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GetBaseUrl()}/v1/oauth2/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials"
        });

        using var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("access_token").GetString()
               ?? throw new InvalidOperationException("PayPal access token missing.");
    }

    private string GetBaseUrl()
    {
        var mode = _configuration["PayPal:Mode"] ?? "Sandbox";
        return string.Equals(mode, "Live", StringComparison.OrdinalIgnoreCase)
            ? "https://api-m.paypal.com"
            : "https://api-m.sandbox.paypal.com";
    }

    private string GetCurrency() => _configuration["PayPal:Currency"] ?? "USD";
}
