using CadastroClientes.Domain.Entities;

namespace CadastroClientes.Application.Interfaces
{
    public interface IClienteRepository
    {
        /// <summary>
        /// Adiciona um novo cliente à persistência.
        /// </summary>
        Task AddAsync(Cliente cliente);

        /// <summary>
        /// Busca um cliente pelo seu Id.
        /// </summary>
        Task<Cliente?> GetByIdAsync(Guid id);

        /// <summary>
        /// Verifica se já existe um cliente com este CPF.
        /// </summary>
        Task<bool> CpfJaCadastradoAsync(string cpf);
    }
}