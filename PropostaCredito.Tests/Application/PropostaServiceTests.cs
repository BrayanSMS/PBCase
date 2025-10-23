using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PropostaCredito.Application.DTOs;
using PropostaCredito.Application.Interfaces;
using PropostaCredito.Application.Services;
using PropostaCredito.Domain.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PropostaCredito.Tests.Application
{
    public class PropostaServiceTests
    {
        private readonly Mock<IPropostaRepository> _mockPropostaRepository;
        private readonly Mock<IMessageBusClient> _mockMessageBusClient;
        private readonly Mock<ILogger<PropostaService>> _mockLogger;
        private readonly PropostaService _propostaService;

        public PropostaServiceTests()
        {
            _mockPropostaRepository = new Mock<IPropostaRepository>();
            _mockMessageBusClient = new Mock<IMessageBusClient>();
            _mockLogger = new Mock<ILogger<PropostaService>>(); // Mock do Logger

            _propostaService = new PropostaService(
                _mockPropostaRepository.Object,
                _mockMessageBusClient.Object,
                _mockLogger.Object // Injeta o mock do Logger
            );
        }

        [Fact]
        public async Task ProcessarPropostaAsync_ComMensagemValidaEAprovada_DeveSalvarPublicarMsgAprovada()
        {
            // Arrange
            var clienteMsg = new ClienteCriadoMessage
            {
                IdCliente = Guid.NewGuid(),
                Nome = "Cliente Aprovado",
                Cpf = "11122233309", // Final 9 -> Tendência Limite Alto (Aprovado 5000)
                Email = "aprovado@teste.com"
            };

            _mockPropostaRepository.Setup(r => r.AddAsync(It.IsAny<Proposta>())).Returns(Task.CompletedTask);
            _mockPropostaRepository.Setup(r => r.UpdateAsync(It.IsAny<Proposta>())).Returns(Task.CompletedTask);

            // Act
            await _propostaService.ProcessarPropostaAsync(clienteMsg);

            // Assert
            _mockPropostaRepository.Verify(r => r.AddAsync(It.Is<Proposta>(p => p.ClienteId == clienteMsg.IdCliente)), Times.Once);
            _mockPropostaRepository.Verify(r => r.UpdateAsync(It.Is<Proposta>(p => p.ClienteId == clienteMsg.IdCliente && p.Status == PropostaStatus.Aprovada)), Times.Once);

            _mockMessageBusClient.Verify(mb => mb.Publish(
                "proposta.aprovada",
                It.Is<PropostaAprovadaMessage>(m =>
                    m.ClienteId == clienteMsg.IdCliente &&
                    m.LimiteAprovado > 0 &&
                    m.CartoesPermitidos > 0
                )), Times.Once);

            _mockMessageBusClient.Verify(mb => mb.Publish("proposta.reprovada", It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public async Task ProcessarPropostaAsync_ComMensagemValidaEReprovada_DeveSalvarPublicarMsgReprovada()
        {
            // Arrange
            var clienteMsg = new ClienteCriadoMessage
            {
                IdCliente = Guid.NewGuid(),
                Nome = "Cliente Reprovado",
                Cpf = "11122233300", // Final 0 -> Tendência Reprovado
                Email = "reprovado@teste.com"
            };

            _mockPropostaRepository.Setup(r => r.AddAsync(It.IsAny<Proposta>())).Returns(Task.CompletedTask);
            _mockPropostaRepository.Setup(r => r.UpdateAsync(It.IsAny<Proposta>())).Returns(Task.CompletedTask);

            // Act
            await _propostaService.ProcessarPropostaAsync(clienteMsg);

            // Assert
            _mockPropostaRepository.Verify(r => r.AddAsync(It.Is<Proposta>(p => p.ClienteId == clienteMsg.IdCliente)), Times.Once);
            _mockPropostaRepository.Verify(r => r.UpdateAsync(It.Is<Proposta>(p => p.ClienteId == clienteMsg.IdCliente && p.Status == PropostaStatus.Reprovada)), Times.Once);

            _mockMessageBusClient.Verify(mb => mb.Publish(
                "proposta.reprovada",
                It.Is<PropostaReprovadaMessage>(m =>
                    m.ClienteId == clienteMsg.IdCliente &&
                    !string.IsNullOrEmpty(m.MotivoReprovacao)
                )), Times.Once);

            _mockMessageBusClient.Verify(mb => mb.Publish("proposta.aprovada", It.IsAny<object>()), Times.Never);
        }

        [Theory]
        [InlineData(null)] // Nome Nulo
        [InlineData("")] // CPF Vazio
        [InlineData("123")] // CPF Inválido
        [InlineData("GuidEmpty")] // IdCliente inválido
        public async Task ProcessarPropostaAsync_ComMensagemInvalida_NaoDeveChamarRepositorioOuMessageBus(string? dadoInvalido)
        {
            // Arrange
            var clienteMsg = new ClienteCriadoMessage
            {
                IdCliente = Guid.NewGuid(), // Começa válido
                Nome = dadoInvalido == null ? null : "Nome Valido",
                Cpf = dadoInvalido == "" || dadoInvalido == "123" ? dadoInvalido : "11122233344",
                Email = "valido@teste.com"
            };
            if (dadoInvalido == "GuidEmpty") clienteMsg.IdCliente = Guid.Empty; // Define Id inválido para o caso específico

            // Act
            await _propostaService.ProcessarPropostaAsync(clienteMsg);

            // Assert
            _mockPropostaRepository.Verify(r => r.AddAsync(It.IsAny<Proposta>()), Times.Never);
            _mockPropostaRepository.Verify(r => r.UpdateAsync(It.IsAny<Proposta>()), Times.Never);
            _mockMessageBusClient.Verify(mb => mb.Publish(It.IsAny<string>(), It.IsAny<object>()), Times.Never);

            _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Error,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("inválida") || v.ToString()!.Contains("inválido")), // Usando ! para suprimir warning
                   It.IsAny<Exception>(),
                   It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
               Times.Once);
        }

        [Fact]
        public async Task ProcessarPropostaAsync_ErroAoSalvarInicial_DeveLancarExcecaoENaoAnalisarOuPublicar() // Nome está correto
        {
            // Arrange
            var clienteMsg = new ClienteCriadoMessage
            {
                IdCliente = Guid.NewGuid(),
                Nome = "Cliente Erro Salvar",
                Cpf = "11122233344",
                Email = "erro@teste.com"
            };

            var simulatedException = new Exception("Erro de BD simulado");

            _mockPropostaRepository.Setup(r => r.AddAsync(It.IsAny<Proposta>()))
                                   .ThrowsAsync(simulatedException);

            // Act
            Func<Task> act = async () => await _propostaService.ProcessarPropostaAsync(clienteMsg);

            // Assert
            // *** ESTA É A LINHA CRÍTICA QUE PRECISA ESTAR CORRETA ***
            await act.Should().ThrowAsync<Exception>() // <<-- VERIFIQUE SE ESTÁ ASSIM
                     .WithMessage("Erro de BD simulado");

            _mockPropostaRepository.Verify(r => r.AddAsync(It.Is<Proposta>(p => p.ClienteId == clienteMsg.IdCliente)), Times.Once);
            _mockPropostaRepository.Verify(r => r.UpdateAsync(It.IsAny<Proposta>()), Times.Never);
            _mockMessageBusClient.Verify(mb => mb.Publish(It.IsAny<string>(), It.IsAny<object>()), Times.Never);

            _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Error,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erro ao salvar proposta inicial")), // Usando ! para suprimir warning
                   It.Is<Exception>(ex => ex == simulatedException),
                   It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
               Times.Once);
        }
    }
}