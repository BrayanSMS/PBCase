using PropostaCredito.Domain.Entities;

namespace PropostaCredito.Application.Interfaces
{
    public interface IPropostaRepository
    {
        /// <summary>
        /// Adiciona uma nova proposta à persistência.
        /// </summary>
        Task AddAsync(Proposta proposta);

        /// <summary>
        /// Atualiza uma proposta existente (após análise).
        /// </summary>
        Task UpdateAsync(Proposta proposta);

        /// <summary>
        /// Busca uma proposta pelo Id do Cliente (para evitar duplicidade, se necessário).
        /// </summary>
        Task<Proposta?> GetByClienteIdAsync(Guid clienteId);
    }
}