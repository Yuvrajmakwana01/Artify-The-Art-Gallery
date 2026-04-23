using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Repository.Services
{
    public class RabbitService : IDisposable, IAsyncDisposable
    {
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan HealthyDelay = TimeSpan.FromMinutes(1);

        public static class Queues
        {
            public const string ArtistNotifications = "artist_notifications";
            public const string AdminNotifications = "admin_notifications";
            public const string BuyerNotifications = "buyer_notifications";
        }

        public sealed class NotificationMessage
        {
            public string Id { get; set; } = Guid.NewGuid().ToString("N");
            public string RecipientType { get; set; } = string.Empty;
            public string RecipientId { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public Dictionary<string, string> Data { get; set; } = new();
            public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private readonly RedisService _redis;
        private readonly ILogger<RabbitService> _logger;
        private readonly ConnectionFactory _factory;
        private readonly string[] _queueNames;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private readonly SemaphoreSlim _publishLock = new(1, 1);
        private readonly SemaphoreSlim _consumerLock = new(1, 1);

        private IConnection? _connection;
        private IChannel? _publishChannel;
        private IChannel? _consumerChannel;
        private bool _consumerStarted;
        private bool _disposed;

        public bool IsConsumerStarted =>
            _consumerStarted &&
            _connection?.IsOpen == true &&
            _consumerChannel?.IsOpen == true;

        public RabbitService(
            IConfiguration configuration,
            RedisService redis,
            ILogger<RabbitService> logger)
        {
            _redis = redis;
            _logger = logger;

            var hostName = configuration["RabbitMQ:Host"];
            var userName = configuration["RabbitMQ:Username"];
            var password = configuration["RabbitMQ:Password"];
            var virtualHost = configuration["RabbitMQ:VirtualHost"];

            if (string.IsNullOrWhiteSpace(hostName))
                throw new InvalidOperationException("RabbitMQ:Host is missing in configuration.");

            if (!int.TryParse(configuration["RabbitMQ:Port"], out var port))
                throw new InvalidOperationException("RabbitMQ:Port is missing or invalid in configuration.");

            if (string.IsNullOrWhiteSpace(userName))
                throw new InvalidOperationException("RabbitMQ:Username is missing in configuration.");

            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("RabbitMQ:Password is missing in configuration.");

            if (string.IsNullOrWhiteSpace(virtualHost))
                throw new InvalidOperationException("RabbitMQ:VirtualHost is missing in configuration.");

            var useSsl = bool.TryParse(configuration["RabbitMQ:UseSsl"], out var configuredSsl)
                ? configuredSsl
                : port == 5671;

            _factory = new ConnectionFactory
            {
                HostName = hostName,
                Port = port,
                UserName = userName,
                Password = password,
                VirtualHost = virtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                ClientProvidedName = "artify-notification-service"
            };

            if (useSsl)
            {
                _factory.Ssl = new SslOption
                {
                    Enabled = true,
                    ServerName = hostName,
                    Version = SslProtocols.Tls12
                };
            }

            _queueNames = new[]
            {
                Queues.ArtistNotifications,
                Queues.AdminNotifications,
                Queues.BuyerNotifications
            };
        }

        public void Publish(string queueName, NotificationMessage message)
        {
            PublishAsync(queueName, message).GetAwaiter().GetResult();
        }

        public async Task PublishAsync(
            string queueName,
            NotificationMessage message,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(queueName))
                throw new ArgumentException("Queue name is required.", nameof(queueName));

            NormalizeMessage(message, queueName);
            await EnsureConnectedAsync(cancellationToken);

            if (_publishChannel == null)
                throw new InvalidOperationException("RabbitMQ publish channel is not available.");

            var json = JsonSerializer.Serialize(message, JsonOptions);
            var body = Encoding.UTF8.GetBytes(json);
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                Type = message.Type,
                MessageId = message.Id,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await _publishLock.WaitAsync(cancellationToken);
            try
            {
                await DeclareQueueAsync(_publishChannel, queueName, cancellationToken);
                await _publishChannel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: queueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "Published {Type} notification {NotificationId} to {Queue}.",
                    message.Type,
                    message.Id,
                    queueName);
            }
            finally
            {
                _publishLock.Release();
            }
        }

        public Task PublishArtworkModerationNotificationAsync(
            int artistId,
            string artworkTitle,
            string message,
            string status,
            CancellationToken cancellationToken = default)
        {
            return PublishAsync(
                Queues.ArtistNotifications,
                new NotificationMessage
                {
                    RecipientType = "artist",
                    RecipientId = artistId.ToString(),
                    Type = $"artwork_{status.ToLowerInvariant()}",
                    Title = artworkTitle,
                    Message = message,
                    Data = new Dictionary<string, string>
                    {
                        ["artworkTitle"] = artworkTitle,
                        ["status"] = status
                    }
                },
                cancellationToken);
        }

        public Task PublishLikeNotificationAsync(
            int artistId,
            int artworkId,
            string artworkTitle,
            int likedByUserId,
            CancellationToken cancellationToken = default)
        {
            return PublishAsync(
                Queues.ArtistNotifications,
                new NotificationMessage
                {
                    RecipientType = "artist",
                    RecipientId = artistId.ToString(),
                    Type = "like",
                    Title = "New like on your artwork",
                    Message = $"Someone liked your artwork '{artworkTitle}'.",
                    Data = new Dictionary<string, string>
                    {
                        ["artworkId"] = artworkId.ToString(),
                        ["artworkTitle"] = artworkTitle,
                        ["likedByUserId"] = likedByUserId.ToString()
                    }
                },
                cancellationToken);
        }

        public Task PublishRegisterNotificationAsync(
            int userId,
            string userName,
            string role,
            CancellationToken cancellationToken = default)
        {
            return PublishAsync(
                Queues.AdminNotifications,
                new NotificationMessage
                {
                    RecipientType = "admin",
                    RecipientId = "all",
                    Type = "register",
                    Title = "New registration",
                    Message = $"{role} '{userName}' registered on Artify.",
                    Data = new Dictionary<string, string>
                    {
                        ["userId"] = userId.ToString(),
                        ["userName"] = userName,
                        ["role"] = role
                    }
                },
                cancellationToken);
        }

        public Task PublishPaymentNotificationAsync(
            int buyerId,
            int orderId,
            decimal amount,
            string currency,
            CancellationToken cancellationToken = default)
        {
            return PublishAsync(
                Queues.BuyerNotifications,
                new NotificationMessage
                {
                    RecipientType = "buyer",
                    RecipientId = buyerId.ToString(),
                    Type = "payment",
                    Title = "Payment successful",
                    Message = $"Your payment for order #{orderId} was successful.",
                    Data = new Dictionary<string, string>
                    {
                        ["buyerId"] = buyerId.ToString(),
                        ["orderId"] = orderId.ToString(),
                        ["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
                        ["currency"] = NormalizeCurrency(currency)
                    }
                },
                cancellationToken);
        }

        public Task PublishAdminPaymentNotificationAsync(
            int orderId,
            int buyerId,
            string buyerName,
            decimal amount,
            string currency,
            int artworkCount,
            CancellationToken cancellationToken = default)
        {
            var normalizedCurrency = NormalizeCurrency(currency);
            var safeArtworkCount = Math.Max(artworkCount, 1);
            var artworkLabel = safeArtworkCount == 1 ? "artwork" : "artworks";
            var buyerDisplayName = string.IsNullOrWhiteSpace(buyerName)
                ? $"Buyer #{buyerId}"
                : buyerName.Trim();

            return PublishAsync(
                Queues.AdminNotifications,
                new NotificationMessage
                {
                    RecipientType = "admin",
                    RecipientId = "all",
                    Type = "payment",
                    Title = "New payment received",
                    Message = $"{buyerDisplayName} completed payment for order #{orderId}. Total: {normalizedCurrency} {amount:0.00} for {safeArtworkCount} {artworkLabel}.",
                    Data = new Dictionary<string, string>
                    {
                        ["buyerId"] = buyerId.ToString(),
                        ["buyerName"] = buyerDisplayName,
                        ["orderId"] = orderId.ToString(),
                        ["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
                        ["currency"] = normalizedCurrency,
                        ["artworkCount"] = safeArtworkCount.ToString()
                    }
                },
                cancellationToken);
        }

        public Task PublishArtistPaymentNotificationAsync(
            int artistId,
            int orderId,
            decimal amount,
            string currency,
            int artworkCount,
            string artworkSummary,
            CancellationToken cancellationToken = default)
        {
            var normalizedCurrency = NormalizeCurrency(currency);
            var safeArtworkCount = Math.Max(artworkCount, 1);
            var isSingleArtwork = safeArtworkCount == 1;
            var safeArtworkSummary = string.IsNullOrWhiteSpace(artworkSummary)
                ? "New order received"
                : artworkSummary.Trim();
            var title = isSingleArtwork ? safeArtworkSummary : "New order received";
            var message = isSingleArtwork
                ? $"Your artwork '{safeArtworkSummary}' was purchased in order #{orderId}. Total: {normalizedCurrency} {amount:0.00}."
                : $"{safeArtworkCount} of your artworks were purchased in order #{orderId}. Total: {normalizedCurrency} {amount:0.00}.";

            return PublishAsync(
                Queues.ArtistNotifications,
                new NotificationMessage
                {
                    RecipientType = "artist",
                    RecipientId = artistId.ToString(),
                    Type = "payment_received",
                    Title = title,
                    Message = message,
                    Data = new Dictionary<string, string>
                    {
                        ["artistId"] = artistId.ToString(),
                        ["orderId"] = orderId.ToString(),
                        ["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
                        ["currency"] = normalizedCurrency,
                        ["artworkCount"] = safeArtworkCount.ToString(),
                        ["artworkSummary"] = safeArtworkSummary
                    }
                },
                cancellationToken);
        }

        public void StartConsumer()
        {
            StartConsumerAsync().GetAwaiter().GetResult();
        }

        public async Task RunConsumerLoopAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("RabbitMQ consumer service started.");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await StartConsumerAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while ensuring RabbitMQ consumer is running.");
                }

                var delay = IsConsumerStarted ? HealthyDelay : RetryDelay;

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        public async Task StartConsumerAsync(CancellationToken cancellationToken = default)
        {
            if (IsConsumerStarted)
                return;

            await _consumerLock.WaitAsync(cancellationToken);
            try
            {
                if (IsConsumerStarted)
                    return;

                await EnsureConnectedAsync(cancellationToken);

                if (_connection == null)
                    throw new InvalidOperationException("RabbitMQ connection is not available.");

                _consumerChannel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
                await _consumerChannel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: 10,
                    global: false,
                    cancellationToken: cancellationToken);

                foreach (var queueName in _queueNames)
                {
                    await DeclareQueueAsync(_consumerChannel, queueName, cancellationToken);

                    // RabbitMQ.Client 7.x uses AsyncEventingBasicConsumer for event-based consumers.
                    var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
                    consumer.ReceivedAsync += async (_, eventArgs) =>
                    {
                        await HandleReceivedMessageAsync(queueName, eventArgs, cancellationToken);
                    };

                    await _consumerChannel.BasicConsumeAsync(
                        queue: queueName,
                        autoAck: false,
                        consumer: consumer,
                        cancellationToken: cancellationToken);

                    _logger.LogInformation("RabbitMQ consumer started for queue {Queue}.", queueName);
                }

                _consumerStarted = true;
            }
            catch (Exception ex)
            {
                _consumerStarted = false;

                try
                {
                    if (_consumerChannel?.IsOpen == true)
                        await _consumerChannel.CloseAsync(cancellationToken: cancellationToken);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to close RabbitMQ consumer channel after startup failure.");
                }
                finally
                {
                    _consumerChannel = null;
                }

                _logger.LogError(ex, "RabbitMQ consumer startup failed. API will continue without notification consumption.");
            }
            finally
            {
                _consumerLock.Release();
            }
        }

        // private async Task HandleReceivedMessageAsync(
        //     string queueName,
        //     BasicDeliverEventArgs eventArgs,
        //     CancellationToken cancellationToken)
        // {
        //     try
        //     {
        //         var json = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
        //         var message = JsonSerializer.Deserialize<NotificationMessage>(json, JsonOptions)
        //             ?? throw new JsonException("Notification message payload was empty.");

        //         NormalizeMessage(message, queueName);
        //         await _redis.StoreNotificationAsync(message);

        //         await AckMessageAsync(eventArgs.DeliveryTag, cancellationToken);

        //         _logger.LogInformation(
        //             "Consumed {Type} notification {NotificationId} from {Queue}.",
        //             message.Type,
        //             message.Id,
        //             queueName);
        //     }
        //     catch (JsonException ex)
        //     {
        //         _logger.LogError(
        //             ex,
        //             "Invalid notification payload received from queue {Queue}. Message will be discarded.",
        //             queueName);

        //         await NackMessageAsync(
        //             deliveryTag: eventArgs.DeliveryTag,
        //             requeue: false,
        //             cancellationToken: cancellationToken);
        //     }
        //     catch (Exception ex)
        //     {
        //         var shouldRequeue = !eventArgs.Redelivered;

        //         _logger.LogError(
        //             ex,
        //             shouldRequeue
        //                 ? "Failed to consume notification from queue {Queue}. Message will be requeued once."
        //                 : "Failed to consume notification from queue {Queue} again after redelivery. Message will be discarded.",
        //             queueName);

        //         await NackMessageAsync(
        //             deliveryTag: eventArgs.DeliveryTag,
        //             requeue: shouldRequeue,
        //             cancellationToken: cancellationToken);
        //     }
        // }

         private async Task HandleReceivedMessageAsync(
    string queueName,
    BasicDeliverEventArgs eventArgs,
    CancellationToken cancellationToken)
{
    try
    {
        var json = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
        var notification = JsonSerializer.Deserialize<NotificationMessage>(json, JsonOptions);

        if (notification != null)
        {
            NormalizeMessage(notification, queueName); // ← ADD THIS LINE
            // Redis mein store karo
            await _redis.StoreNotificationAsync(notification);

            // ✅ ACK — queue se message hatao (yahi nahi tha isliye 0 dikh raha tha)
            await _consumerChannel.BasicAckAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false,
                cancellationToken: cancellationToken);
        }
        else
        {
            // Invalid message — reject karo, requeue mat karo
            await _consumerChannel.BasicNackAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken: cancellationToken);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing message from queue {Queue}", queueName);

        // Error pe requeue: true — dobara try hoga
        await _consumerChannel.BasicNackAsync(
            deliveryTag: eventArgs.DeliveryTag,
            multiple: false,
            requeue: true,
            cancellationToken: cancellationToken);
    }
}

        // private async Task AckMessageAsync(ulong deliveryTag, CancellationToken cancellationToken)
        // {
        //     if (_consumerChannel?.IsOpen != true)
        //         return;

        //     await _consumerChannel.BasicAckAsync(
        //         deliveryTag: deliveryTag,
        //         multiple: false,
        //         cancellationToken: cancellationToken);
        // }

        // private async Task NackMessageAsync(
        //     ulong deliveryTag,
        //     bool requeue,
        //     CancellationToken cancellationToken)
        // {
        //     if (_consumerChannel?.IsOpen != true)
        //         return;

        //     await _consumerChannel.BasicNackAsync(
        //         deliveryTag: deliveryTag,
        //         multiple: false,
        //         requeue: requeue,
        //         cancellationToken: cancellationToken);
        // }

        private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            if (_connection?.IsOpen == true && _publishChannel?.IsOpen == true)
                return;

            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                if (_connection?.IsOpen != true)
                    _connection = await _factory.CreateConnectionAsync(cancellationToken);

                if (_publishChannel?.IsOpen != true)
                {
                    _publishChannel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

                    foreach (var queueName in _queueNames)
                        await DeclareQueueAsync(_publishChannel, queueName, cancellationToken);
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private static Task DeclareQueueAsync(
            IChannel channel,
            string queueName,
            CancellationToken cancellationToken)
        {
            return channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);
        }

        private static void NormalizeMessage(NotificationMessage message, string queueName)
        {
            if (string.IsNullOrWhiteSpace(message.Id))
                message.Id = Guid.NewGuid().ToString("N");

            if (message.CreatedAtUtc == default)
                message.CreatedAtUtc = DateTime.UtcNow;

            if (message.CreatedAtUtc.Kind == DateTimeKind.Unspecified)
                message.CreatedAtUtc = DateTime.SpecifyKind(message.CreatedAtUtc, DateTimeKind.Utc);

            if (string.IsNullOrWhiteSpace(message.RecipientType))
                message.RecipientType = ResolveRecipientType(queueName);

            if (string.IsNullOrWhiteSpace(message.RecipientId))
                message.RecipientId = message.RecipientType == "admin" ? "all" : "unknown";

            if (string.IsNullOrWhiteSpace(message.Type))
                message.Type = queueName.Replace("_notifications", string.Empty);

            message.Data ??= new Dictionary<string, string>();
        }

        private static string ResolveRecipientType(string queueName)
        {
            return queueName switch
            {
                Queues.ArtistNotifications => "artist",
                Queues.AdminNotifications => "admin",
                Queues.BuyerNotifications => "buyer",
                _ => "user"
            };
        }

        private static string NormalizeCurrency(string? currency)
        {
            return string.IsNullOrWhiteSpace(currency)
                ? "USD"
                : currency.Trim().ToUpperInvariant();
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            _connectionLock.Dispose();
            _publishLock.Dispose();
            _consumerLock.Dispose();

            if (_consumerChannel != null)
                await _consumerChannel.CloseAsync();

            if (_publishChannel != null)
                await _publishChannel.CloseAsync();

            if (_connection != null)
                await _connection.CloseAsync();

            GC.SuppressFinalize(this);
        }
    }
}
