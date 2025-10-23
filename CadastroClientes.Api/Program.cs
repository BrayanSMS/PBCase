using CadastroClientes.Infrastructure; 

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "PB.Desafio - Cadastro de Clientes API",
        Version = "v1",
        Description = "API para cadastro de clientes e publica��o de eventos."
    });
});

builder.Services.RegisterInfrastructureServices(builder.Configuration);
builder.Services.AddLogging();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CadastroClientes.Api V1");
        c.RoutePrefix = string.Empty; // Acessar o Swagger na raiz (/)
    });
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

try
{
    var rabbitMqConnection = app.Services.GetRequiredService<RabbitMQ.Client.IConnection>();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    if (rabbitMqConnection.IsOpen)
    {
        logger.LogInformation(
            "Conex�o com RabbitMQ estabelecida com sucesso em: {Host}",
            rabbitMqConnection.Endpoint.HostName);
    }
    else    
        logger.LogWarning("Conex�o com RabbitMQ n�o est� aberta.");    
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(ex, "Falha fatal ao conectar com RabbitMQ na inicializa��o.");

    throw;
}

app.Run();