using System.Collections.Concurrent;
using PropostaCredito.Application.Interfaces;
using PropostaCredito.Domain.Entities;
using System.Linq;
using System.Threading.Tasks;

namespace PropostaCredito.Infrastructure.Persistence
{
    public class InMemoryPropostaRepository : IPropostaRepository
    {
        private static readonly ConcurrentDictionary<Guid, Proposta> _propostas = new();

        public Task AddAsync(Proposta proposta)
        {
            _propostas.TryAdd(proposta.Id, proposta);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Proposta proposta)
        {
            if (_propostas.ContainsKey(proposta.Id))            
                _propostas[proposta.Id] = proposta;
            
            return Task.CompletedTask;
        }

        public Task<Proposta?> GetByClienteIdAsync(Guid clienteId)
        {
            var proposta = _propostas.Values.FirstOrDefault(p => p.ClienteId == clienteId);
            return Task.FromResult(proposta);
        }
    }
}