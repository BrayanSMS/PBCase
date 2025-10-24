namespace CartaoCredito.Infrastructure.MessageBus
{
    public class RabbitMqConfig
    {
        public const string ConfigSectionName = "RabbitMq";
        public string Hostname { get; set; } = "localhost";
        public string Username { get; set; } = "guest";
        public string Password { get; set; } = "guest";
    }
}