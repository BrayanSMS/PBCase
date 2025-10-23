using CadastroClientes.Application.DTOs;
using CadastroClientes.Application.Interfaces;
using CadastroClientes.Domain.Entities;

namespace CadastroClientes.Application.Services
{
    using Result = ValueTuple<ClienteViewModel?, string?>;

    public class ClienteService : IClienteService
    {
        private readonly IClienteRepository _clienteRepository;
        private readonly IMessageBusClient _messageBusClient;
       
        private const string ClienteCriadoRoutingKey = "cliente.criado";

        public ClienteService(
            IClienteRepository clienteRepository,
            IMessageBusClient messageBusClient)
        {
            _clienteRepository = clienteRepository;
            _messageBusClient = messageBusClient;
        }

        public async Task<Result> CadastrarClienteAsync(ClienteInputModel inputModel)
        {
            var cpfLimpo = new string(inputModel.Cpf.Where(char.IsDigit).ToArray());
            if (await _clienteRepository.CpfJaCadastradoAsync(cpfLimpo))                            
                return (null, "CPF já cadastrado no sistema.");            

            Cliente cliente;
            try
            {                
                cliente = Cliente.Create(inputModel.Nome, inputModel.Cpf, inputModel.Email);
            }
            catch (ArgumentException ex)
            {                
                return (null, ex.Message);
            }
            
            await _clienteRepository.AddAsync(cliente);

            var mensagem = new ClienteCriadoMessage
            {
                IdCliente = cliente.Id,
                Nome = cliente.Nome,
                Email = cliente.Email,
                Cpf = cliente.Cpf
            };

            _messageBusClient.Publish(ClienteCriadoRoutingKey, mensagem);
            
            var viewModel = ClienteViewModel.FromEntity(cliente);
            
            return (viewModel, null);
        }
    }

    /// <summary>
    /// DTO específico para a mensagem publicada no RabbitMQ.
    /// Contém apenas os dados necessários para o próximo microsserviço.
    /// </summary>
    public class ClienteCriadoMessage
    {
        public Guid IdCliente { get; set; }
        public string Nome { get; set; }
        public string Cpf { get; set; }
        public string Email { get; set; }
    }
}