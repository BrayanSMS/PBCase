using CartaoCredito.Application.Interfaces;
using CartaoCredito.Domain.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CartaoCredito.Infrastructure.Persistence
{
    public class InMemoryCartaoRepository : ICartaoRepository
    {
        private static readonly ConcurrentDictionary<Guid, Cartao> _cartoes = new();

        public Task AddAsync(Cartao cartao)
        {
            _cartoes.TryAdd(cartao.Id, cartao);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<Cartao> cartoes)
        {
            foreach (var cartao in cartoes)
            {
                _cartoes.TryAdd(cartao.Id, cartao);
            }
            return Task.CompletedTask;
        }

        public Task<IEnumerable<Cartao>> GetByClienteIdAsync(Guid clienteId)
        {
            var cartoesCliente = _cartoes.Values
                                        .Where(c => c.ClienteId == clienteId)
                                        .ToList()
                                        .AsEnumerable();
            return Task.FromResult(cartoesCliente);
        }
    }
}