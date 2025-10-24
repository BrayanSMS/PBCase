using CartaoCredito.Application.DTOs;
using CartaoCredito.Application.Services;
using CartaoCredito.Infrastructure.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CartaoCredito.Worker.Consumers
{
    public class CartaoPropostaAprovadaConsumer : BackgroundService
    {
        private readonly ILogger<CartaoPropostaAprovadaConsumer> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConnection _rabbitMqConnection;
        private readonly RabbitMqConfig _rabbitMqConfig;
        private IModel? _channel;

        private const string QueueName = "cartao.emitir";
        private const string ExchangeName = "clientes_exchange";
        private const string RoutingKey = "proposta.aprovada";

        // Configuração DLQ
        private const string DeadLetterExchange = ExchangeName + ".dlx";
        private const string DeadLetterQueueName = QueueName + ".dlq";
        private const string DeadLetterRoutingKey = RoutingKey + ".dlq"; 

        public CartaoPropostaAprovadaConsumer(
            ILogger<CartaoPropostaAprovadaConsumer> logger,
            IServiceScopeFactory scopeFactory,
            IConnection rabbitMqConnection,
            IOptions<RabbitMqConfig> rabbitMqOptions)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _rabbitMqConnection = rabbitMqConnection;
            _rabbitMqConfig = rabbitMqOptions.Value;

            InitializeRabbitMq();
        }

        private void InitializeRabbitMq()
        {
            try
            {
                _channel = _rabbitMqConnection.CreateModel();

                // Garante que a Exchange principal exista
                _channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: true);

                // Garante que a DLX exista
                _channel.ExchangeDeclare(DeadLetterExchange, ExchangeType.Direct, durable: true);

                // Declara a DLQ
                _channel.QueueDeclare(DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

                // Faz o bind da DLQ na DLX com a routing key específica
                _channel.QueueBind(DeadLetterQueueName, DeadLetterExchange, DeadLetterRoutingKey);

                // Declara a Fila principal com argumento para DLX e routing key específica
                var arguments = new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", DeadLetterExchange },
                    { "x-dead-letter-routing-key", DeadLetterRoutingKey } // Direciona erros para a routing key correta na DLX
                };
                _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);
                _channel.QueueBind(QueueName, ExchangeName, RoutingKey);

                // QoS
                _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                _logger.LogInformation("Worker conectado ao RabbitMQ. Exchange '{Exchange}', Fila '{Queue}', DLQ '{DlqQueue}' declaradas e vinculadas.",
                    ExchangeName, QueueName, DeadLetterQueueName);
            }
            catch (BrokerUnreachableException ex)
            {
                _logger.LogCritical(ex, "Não foi possível conectar ao RabbitMQ em {Host}. Verifique se está rodando.", _rabbitMqConfig.Hostname);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Erro inesperado ao inicializar RabbitMQ.");
                throw;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_channel == null)
            {
                _logger.LogError("Canal RabbitMQ não inicializado. O Worker não pode iniciar.");
                return;
            }

            stoppingToken.Register(() => _logger.LogInformation("Worker Gracefully stopping..."));

            var consumer = new EventingBasicConsumer(_channel);

            consumer.Received += async (sender, eventArgs) =>
            {
                var body = eventArgs.Body.ToArray();
                var messageString = Encoding.UTF8.GetString(body);
                PropostaAprovadaMessage? message = null;

                _logger.LogDebug("Mensagem recebida: {MessageString}", messageString);

                try
                {
                    message = JsonSerializer.Deserialize<PropostaAprovadaMessage>(messageString);

                    if (message == null || message.ClienteId == Guid.Empty || message.PropostaId == Guid.Empty)
                    {
                        throw new JsonException($"Falha ao desserializar PropostaAprovadaMessage ou IDs inválidos. Conteúdo: {messageString}");
                    }

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var cartaoService = scope.ServiceProvider.GetRequiredService<ICartaoService>();
                        await cartaoService.ProcessarEmissaoCartaoAsync(message);
                    }

                    if (_channel?.IsOpen ?? false)
                    {
                        _channel.BasicAck(eventArgs.DeliveryTag, false);
                        _logger.LogInformation("Mensagem processada com sucesso (ACK). PropostaId: {PropostaId}", message?.PropostaId);
                    }
                    else                    
                        _logger.LogWarning("Canal RabbitMQ fechado antes do ACK para PropostaId: {PropostaId}.", message?.PropostaId);                    
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Erro ao desserializar mensagem PropostaAprovada. Enviando para DLQ. Conteúdo: {MessageString}", messageString);
                    if (_channel?.IsOpen ?? false) _channel.BasicNack(eventArgs.DeliveryTag, false, false);
                }
                // Captura exceções lançadas pelo CartaoService (ArgumentException, erro de BD, etc.)
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar mensagem PropostaAprovada. Enviando para DLQ. PropostaId: {PropostaId}", message?.PropostaId);
                    if (_channel?.IsOpen ?? false) _channel.BasicNack(eventArgs.DeliveryTag, false, false);
                }
            };

            _channel.BasicConsume(QueueName, false, consumer);

            _logger.LogInformation("Worker iniciado. Aguardando mensagens na fila '{QueueName}'...", QueueName);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }

            _logger.LogInformation("Worker está parando.");
        }

        public override void Dispose()
        {
            if (_channel?.IsOpen ?? false)
            {
                _channel.Close();
                _channel.Dispose();
                _logger.LogInformation("Canal RabbitMQ fechado.");
            }
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}