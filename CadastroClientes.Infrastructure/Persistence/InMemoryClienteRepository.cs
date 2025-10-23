using System.Collections.Concurrent;
using CadastroClientes.Application.Interfaces;
using CadastroClientes.Domain.Entities;

namespace CadastroClientes.Infrastructure.Persistence
{
    public class InMemoryClienteRepository : IClienteRepository
    {
        private static readonly ConcurrentDictionary<Guid, Cliente> _clientes = new();

        public Task AddAsync(Cliente cliente)
        {
            _clientes.TryAdd(cliente.Id, cliente);
            return Task.CompletedTask;
        }

        public Task<Cliente?> GetByIdAsync(Guid id)
        {
            _clientes.TryGetValue(id, out var cliente);
            return Task.FromResult(cliente);
        }

        public Task<bool> CpfJaCadastradoAsync(string cpf)
        {
            var cpfLimpo = new string(cpf.Where(char.IsDigit).ToArray());

            var existe = _clientes.Values.Any(c => c.Cpf == cpfLimpo);

            return Task.FromResult(existe);
        }
    }
}