using PropostaCredito.Application.DTOs;

namespace PropostaCredito.Application.Services
{
    public interface IPropostaService
    {
        /// <summary>
        /// Processa a mensagem de cliente criado, realiza a análise de crédito
        /// e publica o resultado.
        /// </summary>
        /// <param name="message">Mensagem recebida do RabbitMQ.</param>
        Task ProcessarPropostaAsync(ClienteCriadoMessage message);
    }
}