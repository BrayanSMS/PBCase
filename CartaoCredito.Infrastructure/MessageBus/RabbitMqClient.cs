using CartaoCredito.Application.Interfaces;
using Microsoft.Extensions.Logging;
using CartaoCredito.Application.Interfaces;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace CartaoCredito.Infrastructure.MessageBus
{
    public class RabbitMqClient : IMessageBusClient
    {
        private readonly IConnection _connection;
        private readonly ILogger<RabbitMqClient> _logger;

        // Mantemos o mesmo nome de Exchange para centralizar a comunicação
        // Diferentes routing keys direcionarão para filas diferentes.
        private const string ExchangeName = "clientes_exchange";

        public RabbitMqClient(IConnection connection, ILogger<RabbitMqClient> logger)
        {
            _connection = connection;
            _logger = logger;
        }

        public void Publish(string routingKey, object message)
        {
            try
            {
                using var channel = _connection.CreateModel();

                channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: true);

                var messageJson = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(messageJson);

                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;

                channel.BasicPublish(
                    exchange: ExchangeName,
                    routingKey: routingKey,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation(
                    "Mensagem publicada com sucesso na Exchange '{Exchange}' e RoutingKey '{RoutingKey}'",
                    ExchangeName, routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Falha ao publicar mensagem no RabbitMQ. Exchange: '{Exchange}', RoutingKey: '{RoutingKey}'",
                    ExchangeName, routingKey);
                throw;
            }
        }
    }
}