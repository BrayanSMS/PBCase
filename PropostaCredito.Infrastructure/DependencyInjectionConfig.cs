using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using PropostaCredito.Application.Interfaces;
using PropostaCredito.Application.Services;
using PropostaCredito.Infrastructure.MessageBus;
using PropostaCredito.Infrastructure.Persistence;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;


namespace PropostaCredito.Infrastructure 
{
    public static class DependencyInjectionConfig
    {
        public static IServiceCollection RegisterInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {           
            services.AddSingleton<IPropostaRepository, InMemoryPropostaRepository>();

            services.Configure<RabbitMqConfig>(
                configuration.GetSection(RabbitMqConfig.ConfigSectionName));

            services.AddSingleton<IConnection>(sp =>
            {
                var config = sp.GetRequiredService<IOptions<RabbitMqConfig>>().Value;                
                var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("RabbitMqConnection");

                var factory = new ConnectionFactory
                {
                    HostName = config.Hostname,
                    UserName = config.Username,
                    Password = config.Password,
                    DispatchConsumersAsync = true
                };

                var retryPolicy = Policy
                    .Handle<SocketException>()
                    .Or<BrokerUnreachableException>()
                    .WaitAndRetry(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                        (ex, time) => logger.LogWarning(ex, "Não foi possível conectar ao RabbitMQ. Tentando novamente em {Tempo}s...", time.TotalSeconds)
                    );

                logger.LogInformation("Tentando conectar ao RabbitMQ em {Hostname}...", config.Hostname);
                return retryPolicy.Execute(() => factory.CreateConnection());
            });

            services.AddSingleton<IMessageBusClient, RabbitMqClient>();
            services.AddScoped<IPropostaService, PropostaService>();

            return services;
        }
    }
}