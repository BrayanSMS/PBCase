using CartaoCredito.Infrastructure;
using CartaoCredito.Worker.Consumers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using RabbitMQ.Client;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging();
builder.Services.RegisterInfrastructureServices(builder.Configuration);
builder.Services.AddHostedService<CartaoPropostaAprovadaConsumer>();

var host = builder.Build();

try
{
    var rabbitMqConnection = host.Services.GetRequiredService<IConnection>();
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    if (rabbitMqConnection.IsOpen)
    {
        logger.LogInformation(
            "Conex�o com RabbitMQ estabelecida com sucesso em: {Host}",
            rabbitMqConnection.Endpoint.HostName);
    }
    else
        logger.LogWarning("Conex�o com RabbitMQ n�o est� aberta ap�s inicializa��o.");

}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(ex, "Falha fatal ao conectar com RabbitMQ na inicializa��o do Worker.");
    throw;
}

host.Run();