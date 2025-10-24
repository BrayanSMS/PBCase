using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropostaCredito.Application.DTOs;
using PropostaCredito.Application.Services;
using PropostaCredito.Infrastructure.MessageBus;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
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

                _channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: true);
                _channel.ExchangeDeclare(DeadLetterExchange, ExchangeType.Direct, durable: true);
                _channel.QueueDeclare(DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
                _channel.QueueBind(DeadLetterQueueName, DeadLetterExchange, DeadLetterRoutingKey);

                var arguments = new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", DeadLetterExchange },
                    { "x-dead-letter-routing-key", DeadLetterRoutingKey }
                };
                _channel.QueueDeclare(queue: QueueName,
                                      durable: true,
                                      exclusive: false,
                                      autoDelete: false,
                                      arguments: arguments);

                _channel.QueueBind(QueueName, ExchangeName, RoutingKey);
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
            if (_channel == null || _channel.IsClosed)
            {
                _logger.LogError("Canal RabbitMQ não inicializado ou fechado. O Worker não pode iniciar.");
                return;
            }

            stoppingToken.Register(() => _logger.LogInformation("Worker Gracefully stopping..."));

            var consumer = new AsyncEventingBasicConsumer(_channel);

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
                    {
                        throw new JsonException($"Falha ao desserializar a mensagem recebida ou IdCliente vazio. Conteúdo: {messageString}");
                    }

                    await using (var scope = _scopeFactory.CreateAsyncScope())
                    {
                        var propostaService = scope.ServiceProvider.GetRequiredService<IPropostaService>();
                        await propostaService.ProcessarPropostaAsync(message);
                    }

                    if (_channel?.IsOpen ?? false)
                    {
                        _channel.BasicAck(eventArgs.DeliveryTag, false);
                        _logger.LogInformation("Mensagem processada com sucesso (ACK). ClienteId: {ClienteId}", message?.IdCliente);
                    }
                    else
                    {
                        _logger.LogWarning("Canal RabbitMQ fechado antes do ACK para ClienteId: {ClienteId}. A mensagem pode ser reprocessada.", message?.IdCliente);
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Erro ao desserializar mensagem. Enviando para DLQ. Conteúdo: {MessageString}", messageString);
                    if (_channel?.IsOpen ?? false) _channel.BasicNack(eventArgs.DeliveryTag, false, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro INESPERADO ao processar mensagem. Enviando para DLQ. ClienteId: {ClienteId}", message?.IdCliente);
                    if (_channel?.IsOpen ?? false) _channel.BasicNack(eventArgs.DeliveryTag, false, false);
                }
            };

            try
            {
                _channel.BasicConsume(
                    queue: QueueName,
                    autoAck: false,
                    consumerTag: "",
                    noLocal: false,
                    exclusive: false,
                    arguments: null,
                    consumer: consumer);

                _logger.LogInformation("Worker iniciado. Aguardando mensagens na fila '{QueueName}'...", QueueName);

            }
            catch (OperationInterruptedException opEx)
            {
                _logger.LogCritical(opEx, "ERRO AO INICIAR CONSUMO (BasicConsume) para fila {QueueName}. Verifique estado/configuração da fila no RabbitMQ.", QueueName);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Erro inesperado ao tentar iniciar consumo (BasicConsume) para fila {QueueName}.", QueueName);
                throw;
            }


            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                if (_channel == null || _channel.IsClosed)
                {
                    _logger.LogError("Canal RabbitMQ fechado inesperadamente durante execução. O Worker será interrompido.");
                    break;
                }
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