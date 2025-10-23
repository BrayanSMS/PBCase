namespace PropostaCredito.Application.DTOs
{
    public class PropostaReprovadaMessage
    {
        public Guid PropostaId { get; set; }
        public Guid ClienteId { get; set; }
        public string? CpfCliente { get; set; }
        public string? MotivoReprovacao { get; set; }
    }
}