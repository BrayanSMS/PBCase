using CartaoCredito.Application.DTOs;
using System.Threading.Tasks;

namespace CartaoCredito.Application.Services
{
    public interface ICartaoService
    {
        /// <summary>
        /// Processa a mensagem de proposta aprovada e gera o(s) cartão(ões).
        /// </summary>
        /// <param name="message">Mensagem recebida do RabbitMQ.</param>
        Task ProcessarEmissaoCartaoAsync(PropostaAprovadaMessage message);
    }
}