using System.Text.Json;
using StackExchange.Redis;

namespace Repository.Services
{
    public class RedisService
    {
        private readonly IDatabase _db;
        private readonly IConnectionMultiplexer _mux;

        private const string Prefix = "admin_artworks_";

        // ✅ FIXED CONSTRUCTOR
        public RedisService(IConnectionMultiplexer mux)
        {
            _mux = mux;
            _db = _mux.GetDatabase(); // ✅ correct way
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
            var endpoints = _mux.GetEndPoints();

            foreach (var endpoint in endpoints)
            {
                var server = _mux.GetServer(endpoint);

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


            //redis service for user forgot password otp
            // 🔹 SET OTP
            public async Task SetOtpAsync(string email, string otp)
            {
                var key = $"otp:{email.ToLower().Trim()}";
                await _db.StringSetAsync(key, otp, TimeSpan.FromMinutes(5));
            }
            public async Task SetOtpVerifiedAsync(string email)
{
    // Yahan ToLower() aur Trim() zaroor lagayein
    var key = $"otp_verified:{email.ToLower().Trim()}"; 
    await _db.StringSetAsync(key, "true", TimeSpan.FromMinutes(10));
}

           // 🔹 CHECK VERIFIED FLAG
public async Task<bool> IsOtpVerifiedAsync(string email)
{
    // Yahan bhi ToLower() aur Trim() lagayein
    var key = $"otp_verified:{email.ToLower().Trim()}";
    var val = await _db.StringGetAsync(key);
    return val == "true";
}

            // 🔹 GET OTP
            public async Task<string?> GetOtpAsync(string email)
            {
                var key = $"otp:{email.ToLower().Trim()}";
                return await _db.StringGetAsync(key);
            }

            // 🔹 DELETE OTP
            public async Task DeleteOtpAsync(string email)
            {
                var key = $"otp:{email.ToLower().Trim()}";
                await _db.KeyDeleteAsync(key);
            }
            
            // 🔹 DELETE VERIFIED FLAG (Cleanup ke waqt kaam aayega)
            public async Task DeleteVerifiedFlagAsync(string email)
            {
                var key = $"otp_verified:{email.ToLower().Trim()}";
                await _db.KeyDeleteAsync(key);
            }
        }
}