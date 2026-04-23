using System.Text.Json;
using StackExchange.Redis;

namespace Repository.Services
{
    public class RedisService
    {
        private const string AdminArtworkPrefix = "admin_artworks_";
        private const int DefaultNotificationLimit = 100;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private readonly IDatabase _db;
        private readonly IConnectionMultiplexer _mux;

        public RedisService(IConnectionMultiplexer mux)
        {
            _mux = mux;
            _db = _mux.GetDatabase();
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var value = await _db.StringGetAsync(key);

            if (!value.HasValue)
                return default;

            return JsonSerializer.Deserialize<T>(value!, JsonOptions);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await _db.StringSetAsync(key, json, expiry ?? TimeSpan.FromMinutes(5));
        }

        public async Task RemoveAsync(string key)
        {
            await _db.KeyDeleteAsync(key);
        }

        public async Task ClearAdminArtworkCacheAsync()
        {
            var endpoints = _mux.GetEndPoints();

            foreach (var endpoint in endpoints)
            {
                var server = _mux.GetServer(endpoint);

                await foreach (var key in server.KeysAsync(pattern: $"{AdminArtworkPrefix}*"))
                    await _db.KeyDeleteAsync(key);
            }
        }

        public static string ArtworkCacheKey(string status, int page, int pageSize)
        {
            return $"{AdminArtworkPrefix}{status.ToLowerInvariant()}_{page}_{pageSize}";
        }

        public async Task StoreNotificationAsync(
            RabbitService.NotificationMessage notification,
            int keepLatest = DefaultNotificationLimit)
        {
            if (notification == null)
                throw new ArgumentNullException(nameof(notification));

            if (string.IsNullOrWhiteSpace(notification.Id))
                notification.Id = Guid.NewGuid().ToString("N");

            if (notification.CreatedAtUtc == default)
                notification.CreatedAtUtc = DateTime.UtcNow;

            var listKey = NotificationListKey(notification.RecipientType, notification.RecipientId);
            var countKey = NotificationCountKey(notification.RecipientType, notification.RecipientId);
            var json = JsonSerializer.Serialize(notification, JsonOptions);

            await _db.ListLeftPushAsync(listKey, json);
            await _db.ListTrimAsync(listKey, 0, keepLatest - 1);
            await _db.StringIncrementAsync(countKey);
        }

        public async Task<IReadOnlyList<RabbitService.NotificationMessage>> GetNotificationsAsync(
            string recipientType,
            string recipientId,
            int take = 20)
        {
            var listKey = NotificationListKey(recipientType, recipientId);
            var values = await _db.ListRangeAsync(listKey, 0, Math.Max(take, 1) - 1);
            var notifications = new List<RabbitService.NotificationMessage>();

            foreach (var value in values)
            {
                if (!value.HasValue)
                    continue;

                var notification = JsonSerializer.Deserialize<RabbitService.NotificationMessage>(
                    value!,
                    JsonOptions);

                if (notification != null)
                    notifications.Add(notification);
            }

            return notifications;
        }

        public async Task<long> GetNotificationCountAsync(string recipientType, string recipientId)
        {
            var value = await _db.StringGetAsync(NotificationCountKey(recipientType, recipientId));
            return value.HasValue && long.TryParse(value!, out var count) ? count : 0;
        }

        public async Task ResetNotificationCountAsync(string recipientType, string recipientId)
        {
            await _db.StringSetAsync(NotificationCountKey(recipientType, recipientId), 0);
        }

        public async Task ClearNotificationsAsync(string recipientType, string recipientId)
        {
            await _db.KeyDeleteAsync(new RedisKey[]
            {
                NotificationListKey(recipientType, recipientId),
                NotificationCountKey(recipientType, recipientId)
            });
        }

        public async Task<bool> MarkAsReadAsync(
            string recipientType,
            string recipientId,
            string notificationId)
        {
            var listKey  = NotificationListKey(recipientType, recipientId);
            var countKey = NotificationCountKey(recipientType, recipientId);
            var values   = await _db.ListRangeAsync(listKey);

            foreach (var value in values)
            {
                if (!value.HasValue) continue;

                var notification = JsonSerializer.Deserialize<RabbitService.NotificationMessage>(
                    value!, JsonOptions);

                if (notification?.Id != notificationId) continue;

                await _db.ListRemoveAsync(listKey, value, 1);

                var current = await _db.StringGetAsync(countKey);
                if (current.HasValue && long.TryParse(current!, out var count) && count > 0)
                    await _db.StringDecrementAsync(countKey);

                return true;
            }
            return false;
        }

        public async Task SetOtpAsync(string email, string otp)
        {
            var key = $"otp:{NormalizeEmail(email)}";
            await _db.StringSetAsync(key, otp, TimeSpan.FromMinutes(5));
        }

        public async Task SetOtpVerifiedAsync(string email)
        {
            var key = $"otp_verified:{NormalizeEmail(email)}";
            await _db.StringSetAsync(key, "true", TimeSpan.FromMinutes(10));
        }

        public async Task<bool> IsOtpVerifiedAsync(string email)
        {
            var key = $"otp_verified:{NormalizeEmail(email)}";
            var value = await _db.StringGetAsync(key);
            return value == "true";
        }

        public async Task<string?> GetOtpAsync(string email)
        {
            var key = $"otp:{NormalizeEmail(email)}";
            return await _db.StringGetAsync(key);
        }

        public async Task DeleteOtpAsync(string email)
        {
            var key = $"otp:{NormalizeEmail(email)}";
            await _db.KeyDeleteAsync(key);
        }

        public async Task DeleteVerifiedFlagAsync(string email)
        {
            var key = $"otp_verified:{NormalizeEmail(email)}";
            await _db.KeyDeleteAsync(key);
        }

        private static string NotificationListKey(string recipientType, string recipientId)
        {
            return $"notifications:{NormalizeKeyPart(recipientType)}:{NormalizeKeyPart(recipientId)}";
        }

        private static string NotificationCountKey(string recipientType, string recipientId)
        {
            return $"{NotificationListKey(recipientType, recipientId)}:count";
        }

        private static string NormalizeKeyPart(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "unknown"
                : value.Trim().ToLowerInvariant();
        }

        private static string NormalizeEmail(string email)
        {
            return email.ToLowerInvariant().Trim();
        }
    }
}
