using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;
using System.Text.Json;

namespace Repository.Services
{
    public class RedisService
    {
        private readonly IDatabase _db;
        private readonly IConnectionMultiplexer _mux;

        private const string Prefix = "admin_artworks_";

        public RedisService(IDatabase db, IConnectionMultiplexer mux)
        {
            _db = db;
            _mux = mux;
        }

        // 🔹 GET DATA
        public async Task<T?> GetAsync<T>(string key)
        {
            var value = await _db.StringGetAsync(key);

            if (!value.HasValue)
                return default;

            return JsonSerializer.Deserialize<T>(value!);
        }

        // 🔹 SET DATA
        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            var json = JsonSerializer.Serialize(value);

            await _db.StringSetAsync(
                key,
                json,
                expiry ?? TimeSpan.FromMinutes(5)
            );
        }

        // 🔹 REMOVE SINGLE KEY
        public async Task RemoveAsync(string key)
        {
            await _db.KeyDeleteAsync(key);
        }

        // 🔥 CLEAR ALL ADMIN ARTWORK CACHE
        public async Task ClearAdminArtworkCacheAsync()
        {
            var servers = _mux.GetServers();

            foreach (var server in servers)
            {
                await foreach (var key in server.KeysAsync(pattern: $"{Prefix}*"))
                {
                    await _db.KeyDeleteAsync(key);
                }
            }
        }

        // 🔹 CACHE KEY GENERATOR
        public static string ArtworkCacheKey(string status, int page, int pageSize)
        {
            return $"{Prefix}{status.ToLower()}_{page}_{pageSize}";
        }
    }
}