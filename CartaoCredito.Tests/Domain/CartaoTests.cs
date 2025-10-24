using CartaoCredito.Domain.Entities;
using FluentAssertions;
using System;

namespace CartaoCredito.Tests.Domain
{
    public class CartaoTests
    {
        private readonly Guid _clienteIdValido = Guid.NewGuid();
        private readonly Guid _propostaIdValido = Guid.NewGuid();
        private const decimal _limiteValido = 5000.00m;

        // --- Testes para o Método de Fábrica 'Create' ---

        [Fact]
        public void Create_ComDadosValidos_DeveCriarCartaoComDadosGerados()
        {
            // Act
            var cartao = Cartao.Create(_clienteIdValido, _propostaIdValido, _limiteValido);

            // Assert
            cartao.Should().NotBeNull();
            cartao.Id.Should().NotBeEmpty();
            cartao.ClienteId.Should().Be(_clienteIdValido);
            cartao.PropostaId.Should().Be(_propostaIdValido);
            cartao.Limite.Should().Be(_limiteValido);
            cartao.DataEmissao.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            // Verifica se os dados gerados têm o formato esperado
            cartao.NumeroCartao.Should().NotBeNullOrEmpty().And.HaveLength(16).And.MatchRegex("^[0-9]+$");
            cartao.Cvv.Should().NotBeNullOrEmpty().And.HaveLength(3).And.MatchRegex("^[0-9]+$");
            cartao.DataValidade.Should().BeAfter(DateTime.UtcNow.AddYears(4).AddMonths(11)); // Aproximadamente 5 anos no futuro
            cartao.DataValidade.Day.Should().Be(DateTime.DaysInMonth(cartao.DataValidade.Year, cartao.DataValidade.Month)); // Último dia do mês
        }

        [Fact]
        public void Create_ComClienteIdInvalido_DeveLancarArgumentException()
        {
            // Act
            Action act = () => Cartao.Create(Guid.Empty, _propostaIdValido, _limiteValido);
            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("Id do cliente inválido.*");
        }

        [Fact]
        public void Create_ComPropostaIdInvalida_DeveLancarArgumentException()
        {
            // Act
            Action act = () => Cartao.Create(_clienteIdValido, Guid.Empty, _limiteValido);
            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("Id da proposta inválido.*");
        }


        [Theory]
        [InlineData(0)]
        [InlineData(-1000)]
        public void Create_ComLimiteInvalido_DeveLancarArgumentException(decimal limiteInvalido)
        {
            // Act
            Action act = () => Cartao.Create(_clienteIdValido, _propostaIdValido, limiteInvalido);
            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("Limite do cartão deve ser positivo.*");
        }
    }
}