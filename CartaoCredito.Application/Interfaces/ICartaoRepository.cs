using CartaoCredito.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CartaoCredito.Application.Interfaces
{
    public interface ICartaoRepository
    {
        /// <summary>
        /// Adiciona um novo cartão à persistência.
        /// </summary>
        Task AddAsync(Cartao cartao);

        /// <summary>
        /// Adiciona múltiplos cartões (útil quando são emitidos 2).
        /// </summary>
        Task AddRangeAsync(IEnumerable<Cartao> cartoes);

        /// <summary>
        /// Busca cartões pelo Id do Cliente.
        /// </summary>
        Task<IEnumerable<Cartao>> GetByClienteIdAsync(Guid clienteId);
    }
}