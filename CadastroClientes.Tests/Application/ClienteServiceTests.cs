using CadastroClientes.Application.DTOs;
using CadastroClientes.Application.Interfaces;
using CadastroClientes.Application.Services;
using CadastroClientes.Domain.Entities;
using FluentAssertions;
using Moq;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace CadastroClientes.Tests.Application
{
    // Definimos o tipo de retorno 'Result' para facilitar a leitura.
    using Result = ValueTuple<ClienteViewModel?, string?>;

    public class ClienteServiceTests
    {
        private readonly Mock<IClienteRepository> _mockClienteRepository;
        private readonly Mock<IMessageBusClient> _mockMessageBusClient;
        private readonly ClienteService _clienteService; // A classe que estamos testando

        public ClienteServiceTests()
        {
            _mockClienteRepository = new Mock<IClienteRepository>();
            _mockMessageBusClient = new Mock<IMessageBusClient>();

            _clienteService = new ClienteService(
                _mockClienteRepository.Object,
                _mockMessageBusClient.Object
            );
        }

        [Fact]
        public async Task CadastrarClienteAsync_ComDadosValidosENaoDuplicados_DeveRetornarViewModelEChamarRepositorioEMessageBus()
        {
            // Arrange (Organização)
            var inputModel = new ClienteInputModel
            {
                Nome = "Brayan S.",
                Cpf = "111.222.333-44",
                Email = "unico@teste.com"
            };
            var cpfLimpo = "11122233344";

            // Configurar o Mock do Repositório:
            _mockClienteRepository.Setup(repo => repo.CpfJaCadastradoAsync(cpfLimpo))
                                  .ReturnsAsync(false);
            _mockClienteRepository.Setup(repo => repo.AddAsync(It.IsAny<Cliente>()))
                                  .Returns(Task.CompletedTask);

            // Act (Ação)
            var (viewModel, erro) = await _clienteService.CadastrarClienteAsync(inputModel);

            // Assert (Verificação)
            erro.Should().BeNull();
            viewModel.Should().NotBeNull();
            viewModel!.Nome.Should().Be(inputModel.Nome);
            viewModel.Email.Should().Be(inputModel.Email);
            viewModel.Status.Should().Be(ClienteStatus.EmAnalise.ToString());

            _mockClienteRepository.Verify(repo => repo.CpfJaCadastradoAsync(cpfLimpo), Times.Once);
            _mockClienteRepository.Verify(repo => repo.AddAsync(It.Is<Cliente>(c => c.Cpf == cpfLimpo)), Times.Once);
            _mockMessageBusClient.Verify(mb => mb.Publish("cliente.criado", It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task CadastrarClienteAsync_ComCpfJaExistente_DeveRetornarErroENaoChamarAddOuPublish()
        {
            // Arrange
            var inputModel = new ClienteInputModel
            {
                Nome = "Outro Nome",
                Cpf = "111.222.333-44", // CPF que já existe
                Email = "outro@teste.com"
            };
            var cpfLimpo = "11122233344";

            _mockClienteRepository.Setup(repo => repo.CpfJaCadastradoAsync(cpfLimpo))
                                  .ReturnsAsync(true);

            // Act
            var (viewModel, erro) = await _clienteService.CadastrarClienteAsync(inputModel);

            // Assert
            erro.Should().NotBeNullOrEmpty().And.Contain("CPF já cadastrado");
            viewModel.Should().BeNull();

            _mockClienteRepository.Verify(repo => repo.CpfJaCadastradoAsync(cpfLimpo), Times.Once);
            _mockClienteRepository.Verify(repo => repo.AddAsync(It.IsAny<Cliente>()), Times.Never);
            _mockMessageBusClient.Verify(mb => mb.Publish(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
        }

        [Theory]
        [InlineData("")] // Nome inválido
        [InlineData("12345")] // CPF inválido
        public async Task CadastrarClienteAsync_ComDadosInvalidosParaEntidade_DeveRetornarErroENaoChamarAddOuPublish(string dadoInvalido)
        {
            // Arrange
            var inputModel = new ClienteInputModel
            {
                Nome = dadoInvalido == "" ? "" : "Nome Valido",
                Cpf = dadoInvalido == "12345" ? "12345" : "111.222.333-44",
                Email = "valido@teste.com"
            };

            var cpfLimpoParaNomeInvalido = "11122233344";
            var cpfLimpoParaCpfInvalido = "12345"; // CPF inválido limpo

            _mockClienteRepository.Setup(repo => repo.CpfJaCadastradoAsync(cpfLimpoParaNomeInvalido))
                                  .ReturnsAsync(false);
            _mockClienteRepository.Setup(repo => repo.CpfJaCadastradoAsync(cpfLimpoParaCpfInvalido))
                                  .ReturnsAsync(false);


            // Act
            var (viewModel, erro) = await _clienteService.CadastrarClienteAsync(inputModel);

            // Assert
            erro.Should().NotBeNullOrEmpty();
            viewModel.Should().BeNull();

            // Verificar se CpfJaCadastradoAsync foi chamado UMA VEZ
            // com o CPF correspondente ao cenário de teste.
            if (dadoInvalido == "") // Caso Nome Inválido
            {
                // Verificamos se foi chamado com o CPF correto para este caso
                _mockClienteRepository.Verify(repo => repo.CpfJaCadastradoAsync(cpfLimpoParaNomeInvalido), Times.Once);
                // CORRIGIDO: Removida a verificação desnecessária do outro CPF
            }
            else // Caso CPF Inválido ("12345")
            {
                // Verificamos se foi chamado com o CPF correto para este caso
                _mockClienteRepository.Verify(repo => repo.CpfJaCadastradoAsync(cpfLimpoParaCpfInvalido), Times.Once);
                // Garantimos que não foi chamado com o CPF do outro caso
                _mockClienteRepository.Verify(repo => repo.CpfJaCadastradoAsync(cpfLimpoParaNomeInvalido), Times.Never);
            }

            // Nenhum destes deve ser chamado se a validação/criação da entidade falhar
            _mockClienteRepository.Verify(repo => repo.AddAsync(It.IsAny<Cliente>()), Times.Never);
            _mockMessageBusClient.Verify(mb => mb.Publish(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
        }
    }
}