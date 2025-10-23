using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using PropostaCredito.Application.DTOs;
using PropostaCredito.Application.Services;
using PropostaCredito.Infrastructure.MessageBus;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PropostaCredito.Worker.Consumers
{
    public class PropostaClienteCriadoConsumer : BackgroundService
    {
        private readonly ILogger<PropostaClienteCriadoConsumer> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConnection _rabbitMqConnection;
        private readonly RabbitMqConfig _rabbitMqConfig;
        private IModel? _channel;

        private const string QueueName = "proposta.analisar";
        private const string ExchangeName = "clientes_exchange";
        private const string RoutingKey = "cliente.criado";

        // Dead Letter Queue (DLQ) para mensagens com erro
        private const string DeadLetterExchange = ExchangeName + ".dlx";
        private const string DeadLetterQueueName = QueueName + ".dlq";
        private const string DeadLetterRoutingKey = RoutingKey + ".dlq";


        public PropostaClienteCriadoConsumer(
            ILogger<PropostaClienteCriadoConsumer> logger,
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

                // Declara a Exchange principal (idem ao publicador)
                _channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: true);

                // Declara a Dead Letter Exchange (DLX)
                _channel.ExchangeDeclare(DeadLetterExchange, ExchangeType.Direct, durable: true);

                // Declara a Dead Letter Queue (DLQ)
                _channel.QueueDeclare(DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
                // Faz o bind da DLQ com a DLX
                _channel.QueueBind(DeadLetterQueueName, DeadLetterExchange, DeadLetterRoutingKey);

                // Declara a Fila principal com argumento para DLX
                var arguments = new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", DeadLetterExchange }, // Nome da DLX
                    { "x-dead-letter-routing-key", DeadLetterRoutingKey } // Routing key para DLX
                };
                _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);

                // Faz o "bind" da fila com a exchange usando a routing key
                _channel.QueueBind(QueueName, ExchangeName, RoutingKey);

                // Configura Quality of Service: processa uma mensagem por vez
                _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                _logger.LogInformation("Worker conectado ao RabbitMQ. Exchange '{Exchange}', Fila '{Queue}', DLQ '{DlqQueue}' declaradas e vinculadas.",
                    ExchangeName, QueueName, DeadLetterQueueName);

            }
            catch (BrokerUnreachableException ex)
            {
                _logger.LogCritical(ex, "Não foi possível conectar ao RabbitMQ em {Host}. Verifique se está rodando.", _rabbitMqConfig.Hostname);
                throw; // Lança a exceção para parar o serviço se a conexão inicial falhar
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
                ClienteCriadoMessage? message = null;

                _logger.LogDebug("Mensagem recebida: {MessageString}", messageString);

                try
                {
                    message = JsonSerializer.Deserialize<ClienteCriadoMessage>(messageString);

                    if (message == null || message.IdCliente == Guid.Empty)
                        throw new JsonException($"Falha ao desserializar a mensagem recebida ou IdCliente vazio. Conteúdo: {messageString}");

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var propostaService = scope.ServiceProvider.GetRequiredService<IPropostaService>();
                        await propostaService.ProcessarPropostaAsync(message);
                    }

                    if (_channel?.IsOpen ?? false)
                    {
                        _channel.BasicAck(eventArgs.DeliveryTag, false);
                        // processedSuccessfully = true; // Removido
                        _logger.LogInformation("Mensagem processada com sucesso (ACK). ClienteId: {ClienteId}", message?.IdCliente);
                    }
                    else
                        _logger.LogWarning("Canal RabbitMQ fechado antes do ACK para ClienteId: {ClienteId}. A mensagem pode ser reprocessada.", message?.IdCliente);

                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Erro ao desserializar mensagem. Enviando para DLQ. Conteúdo: {MessageString}", messageString);                    
                    if (_channel?.IsOpen ?? false)                    
                        _channel.BasicNack(eventArgs.DeliveryTag, false, false);
                    
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro INESPERADO ao processar mensagem. Enviando para DLQ. ClienteId: {ClienteId}", message?.IdCliente);
                    if (_channel?.IsOpen ?? false)
                        _channel.BasicNack(eventArgs.DeliveryTag, false, false);
                }
            };

            // Começa a consumir a fila
            _channel.BasicConsume(QueueName, false, consumer); // autoAck = false

            _logger.LogInformation("Worker iniciado. Aguardando mensagens na fila '{QueueName}'...", QueueName);

            // Mantém o serviço rodando até que seja solicitado o cancelamento
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Pequeno delay para não consumir CPU
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