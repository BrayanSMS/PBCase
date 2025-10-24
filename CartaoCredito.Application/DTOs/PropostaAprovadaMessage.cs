using System;
using System.Text.Json.Serialization;

namespace CartaoCredito.Application.DTOs
{
    public class PropostaAprovadaMessage
    {
        public Guid PropostaId { get; set; }
        public Guid ClienteId { get; set; }
        public string? CpfCliente { get; set; }
        public decimal LimiteAprovado { get; set; }
        public int CartoesPermitidos { get; set; }
    }
}