using CartaoCredito.Application.DTOs;
using CartaoCredito.Application.Interfaces;
using CartaoCredito.Application.Services;
using CartaoCredito.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CartaoCredito.Tests.Application
{
    public class CartaoServiceTests
    {
        private readonly Mock<ICartaoRepository> _mockCartaoRepository;
        private readonly Mock<ILogger<CartaoService>> _mockLogger;
        private readonly CartaoService _cartaoService;

        public CartaoServiceTests()
        {
            _mockCartaoRepository = new Mock<ICartaoRepository>();
            _mockLogger = new Mock<ILogger<CartaoService>>();

            _cartaoService = new CartaoService(
                _mockCartaoRepository.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task ProcessarEmissaoCartaoAsync_ComMensagemValidaParaUmCartao_DeveSalvarUmCartao()
        {
            // Arrange
            var propostaMsg = new PropostaAprovadaMessage
            {
                PropostaId = Guid.NewGuid(),
                ClienteId = Guid.NewGuid(),
                CpfCliente = "11122233344",
                LimiteAprovado = 1000.00m,
                CartoesPermitidos = 1 // <<-- Um cartão
            };

            // Configura AddAsync para retornar Task.CompletedTask
            _mockCartaoRepository.Setup(r => r.AddAsync(It.IsAny<Cartao>()))
                                   .Returns(Task.CompletedTask);

            // Act
            await _cartaoService.ProcessarEmissaoCartaoAsync(propostaMsg);

            // Assert
            // Verifica se AddAsync foi chamado UMA vez com um cartão válido
            _mockCartaoRepository.Verify(r => r.AddAsync(
                It.Is<Cartao>(c =>
                    c.ClienteId == propostaMsg.ClienteId &&
                    c.PropostaId == propostaMsg.PropostaId &&
                    c.Limite == propostaMsg.LimiteAprovado)
                ), Times.Once);

            // Verifica se AddRangeAsync NUNCA foi chamado
            _mockCartaoRepository.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Cartao>>()), Times.Never);

            // Verifica log de informação (opcional)
            _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Information,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("1 cartão(ões) salvo(s)")),
                   null, // Sem exceção
                   It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
               Times.Once);
        }

        [Fact]
        public async Task ProcessarEmissaoCartaoAsync_ComMensagemValidaParaDoisCartoes_DeveSalvarDoisCartoes()
        {
            // Arrange
            var propostaMsg = new PropostaAprovadaMessage
            {
                PropostaId = Guid.NewGuid(),
                ClienteId = Guid.NewGuid(),
                CpfCliente = "11122233355",
                LimiteAprovado = 5000.00m,
                CartoesPermitidos = 2 // <<-- Dois cartões
            };

            // Configura AddRangeAsync para retornar Task.CompletedTask
            _mockCartaoRepository.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Cartao>>()))
                                   .Returns(Task.CompletedTask);

            // Act
            await _cartaoService.ProcessarEmissaoCartaoAsync(propostaMsg);

            // Assert
            // Verifica se AddAsync NUNCA foi chamado
            _mockCartaoRepository.Verify(r => r.AddAsync(It.IsAny<Cartao>()), Times.Never);

            // Verifica se AddRangeAsync foi chamado UMA vez com uma lista contendo DOIS cartões válidos
            _mockCartaoRepository.Verify(r => r.AddRangeAsync(
                It.Is<IEnumerable<Cartao>>(list =>
                    list.Count() == 2 && // Verifica se a lista tem 2 itens
                    list.All(c => // Verifica se TODOS os itens da lista são válidos
                        c.ClienteId == propostaMsg.ClienteId &&
                        c.PropostaId == propostaMsg.PropostaId &&
                        c.Limite == propostaMsg.LimiteAprovado)
                )), Times.Once);

            // Verifica log de informação (opcional)
            _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Information,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("2 cartão(ões) salvo(s)")),
                   null, // Sem exceção
                   It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
               Times.Once);
        }

        [Theory]
        [InlineData(0)] // Limite inválido
        [InlineData(-1)] // Limite inválido
        [InlineData(1000, 0)] // Cartões inválidos
        [InlineData(1000, -1)] // Cartões inválidos
        public async Task ProcessarEmissaoCartaoAsync_ComDadosInvalidosNaMensagem_DeveLancarArgumentException(decimal limite, int cartoes = 1) // Default para cartoes se não for o foco do teste
        {
            // Arrange
            var propostaMsg = new PropostaAprovadaMessage
            {
                PropostaId = Guid.NewGuid(),
                ClienteId = Guid.NewGuid(),
                CpfCliente = "11122233344",
                LimiteAprovado = limite, // Dado inválido
                CartoesPermitidos = cartoes // Dado inválido
            };
            if (limite > 0 && cartoes <= 0) // Ajusta para o caso de teste de cartões inválidos
            {
                propostaMsg.LimiteAprovado = 1000; // Define um limite válido
            }


            // Act
            Func<Task> act = async () => await _cartaoService.ProcessarEmissaoCartaoAsync(propostaMsg);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                     .WithMessage("Mensagem PropostaAprovadaMessage inválida.");

            // Garante que nenhum método do repositório foi chamado
            _mockCartaoRepository.Verify(r => r.AddAsync(It.IsAny<Cartao>()), Times.Never);
            _mockCartaoRepository.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Cartao>>()), Times.Never);

            // Verifica log de erro (opcional)
            _mockLogger.Verify(
              x => x.Log(
                  LogLevel.Error,
                  It.IsAny<EventId>(),
                  It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("inválida recebida")),
                  null, // Sem exceção explícita no log, mas a ArgumentException foi lançada
                  It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
              Times.Once);
        }

        [Fact]
        public async Task ProcessarEmissaoCartaoAsync_ErroAoSalvarCartao_DeveLancarExcecao()
        {
            // Arrange
            var propostaMsg = new PropostaAprovadaMessage
            {
                PropostaId = Guid.NewGuid(),
                ClienteId = Guid.NewGuid(),
                CpfCliente = "11122233344",
                LimiteAprovado = 1000.00m,
                CartoesPermitidos = 1
            };
            var simulatedException = new Exception("Erro de BD simulado ao salvar");

            // Simula erro no AddAsync
            _mockCartaoRepository.Setup(r => r.AddAsync(It.IsAny<Cartao>()))
                                   .ThrowsAsync(simulatedException);

            // Act
            Func<Task> act = async () => await _cartaoService.ProcessarEmissaoCartaoAsync(propostaMsg);

            // Assert
            await act.Should().ThrowAsync<Exception>()
                     .WithMessage("Erro de BD simulado ao salvar");

            // Verifica que AddAsync foi chamado
            _mockCartaoRepository.Verify(r => r.AddAsync(It.IsAny<Cartao>()), Times.Once);
            // Verifica que AddRangeAsync não foi chamado
            _mockCartaoRepository.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Cartao>>()), Times.Never);

            // Verifica log de erro (opcional)
            _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Error,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erro ao salvar cartão(ões)")),
                   It.Is<Exception>(ex => ex == simulatedException),
                   It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
               Times.Once);
        }
    }
}