using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PropostaCredito.Infrastructure;
using PropostaCredito.Worker.Consumers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging();
builder.Services.RegisterInfrastructureServices(builder.Configuration);
builder.Services.AddHostedService<PropostaClienteCriadoConsumer>();

var host = builder.Build();

try
{
    var rabbitMqConnection = host.Services.GetRequiredService<RabbitMQ.Client.IConnection>();
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    if (rabbitMqConnection.IsOpen)
    {
        logger.LogInformation(
            "Conex�o com RabbitMQ estabelecida com sucesso em: {Host}",
            rabbitMqConnection.Endpoint.HostName);
    }
    else
    {
        logger.LogWarning("Conex�o com RabbitMQ n�o est� aberta ap�s inicializa��o.");
    }
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(ex, "Falha fatal ao conectar com RabbitMQ na inicializa��o do Worker.");    
    throw;
}


host.Run(); // Inicia o worker e bloqueia at� ser parado (Ctrl+C)