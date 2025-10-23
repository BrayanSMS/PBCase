using CadastroClientes.Application.DTOs;

namespace CadastroClientes.Application.Services
{
    using Result = ValueTuple<ClienteViewModel?, string?>;

    public interface IClienteService
    {
        /// <summary>
        /// Organiza o cadastro de um novo cliente.
        /// </summary>
        /// <returns>
        /// Uma tupla (ValueTuple) contendo:
        /// (ClienteViewModel? viewModel, string? erro)
        /// Se sucesso, viewModel não será nulo e erro será nulo.
        /// Se falha, viewModel será nulo e erro conterá a mensagem.
        /// </returns>
        Task<Result> CadastrarClienteAsync(ClienteInputModel inputModel);
    }
}