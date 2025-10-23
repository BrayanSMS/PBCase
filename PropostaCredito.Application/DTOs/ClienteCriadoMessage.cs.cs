namespace PropostaCredito.Application.DTOs
{
    public class ClienteCriadoMessage
    {
        public Guid IdCliente { get; set; }
        public string? Nome { get; set; }
        public string? Cpf { get; set; }
        public string? Email { get; set; }
    }
}