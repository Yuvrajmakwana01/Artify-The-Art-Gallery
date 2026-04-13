using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Repository.Interfaces;
using Repository.Models;
using Microsoft.Extensions.Configuration;

namespace Repository.Services
{
    public class RabbitMQProducer 
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private const string QueueName = "artwork_notifications";

        public RabbitMQProducer(IConfiguration config)
        {
            var factory = new ConnectionFactory
            {
                HostName = config["RabbitMQ:Host"] ?? "localhost",
                Port     = int.Parse(config["RabbitMQ:Port"] ?? "5672"),
                UserName = config["RabbitMQ:Username"] ?? "guest",
                Password = config["RabbitMQ:Password"] ?? "guest"
            };

            _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            _channel    = _connection.CreateChannelAsync().GetAwaiter().GetResult();

            // Declare queue — safe to call even if queue already exists
            _channel.QueueDeclareAsync(
                queue:      QueueName,
                durable:    true,
                exclusive:  false,
                autoDelete: false,
                arguments:  null
            ).GetAwaiter().GetResult();
        }

        public void SendArtworkNotification(ArtworkNotificationMessage message)
        {
            var json  = JsonSerializer.Serialize(message);
            var body  = Encoding.UTF8.GetBytes(json);

            var props = new BasicProperties
            {
                Persistent = true   // message survives RabbitMQ restart
            };

            _channel.BasicPublishAsync(
                exchange:   string.Empty,
                routingKey: QueueName,
                mandatory:  false,
                basicProperties: props,
                body:       body
            ).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            _channel?.CloseAsync().GetAwaiter().GetResult();
            _connection?.CloseAsync().GetAwaiter().GetResult();
        }
    }
}