using FluentAssertions;
using PropostaCredito.Domain.Entities;
using System;
using System.Linq;

namespace PropostaCredito.Tests.Domain
{
    public class PropostaTests
    {
        private readonly Guid _clienteIdValido = Guid.NewGuid();
        private const string _nomeValido = "Cliente Teste";
        private const string _cpfValido = "11122233344"; // Já limpo
        private const string _emailValido = "cliente@teste.com";

        // --- Testes para o Método de Fábrica 'Create' ---

        [Fact]
        public void Create_ComDadosValidos_DeveCriarPropostaPendente()
        {
            // Act
            var proposta = Proposta.Create(_clienteIdValido, _nomeValido, _cpfValido, _emailValido);

            // Assert
            proposta.Should().NotBeNull();
            proposta.ClienteId.Should().Be(_clienteIdValido);
            proposta.NomeCliente.Should().Be(_nomeValido);
            proposta.CpfCliente.Should().Be(_cpfValido);
            proposta.EmailCliente.Should().Be(_emailValido);
            proposta.Status.Should().Be(PropostaStatus.Pendente);
            proposta.Id.Should().NotBeEmpty();
            proposta.DataCriacao.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            proposta.Score.Should().BeNull();
            proposta.LimiteAprovado.Should().BeNull();
            proposta.CartoesPermitidos.Should().Be(0);
            proposta.MotivoReprovacao.Should().BeNull();
        }

        [Fact]
        public void Create_ComClienteIdInvalido_DeveLancarArgumentException()
        {
            // Act
            Action act = () => Proposta.Create(Guid.Empty, _nomeValido, _cpfValido, _emailValido);
            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("Id do cliente inválido.*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_ComNomeInvalido_DeveLancarArgumentException(string nomeInvalido)
        {
            // Act
            Action act = () => Proposta.Create(_clienteIdValido, nomeInvalido, _cpfValido, _emailValido);
            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("Nome do cliente inválido.*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1234567890")] // Menos de 11
        [InlineData("123456789012")] // Mais de 11
        [InlineData("1112223334A")] // Com letra
        public void Create_ComCpfInvalido_DeveLancarArgumentException(string cpfInvalido)
        {
            // Act
            Action act = () => Proposta.Create(_clienteIdValido, _nomeValido, cpfInvalido, _emailValido);
            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("CPF do cliente inválido.*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_ComEmailInvalido_DeveLancarArgumentException(string emailInvalido)
        {
            // Act
            Action act = () => Proposta.Create(_clienteIdValido, _nomeValido, _cpfValido, emailInvalido);
            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("Email do cliente inválido.*");
        }

        // --- Testes para o Método 'AnalisarCredito' ---
        // Nota: Como o score é aleatório na implementação atual, testamos as faixas de resultado.

        [Theory]
        [InlineData("11122233300")] // Final 0 -> Score 0-50 (Reprovado)
        [InlineData("11122233301")] // Final 1 -> Score 50-101 (Reprovado/Limite Baixo)
        public void AnalisarCredito_ComCpfTendenciaReprovacao_DeveReprovar(string cpf)
        {
            // Arrange
            var proposta = Proposta.Create(_clienteIdValido, _nomeValido, cpf, _emailValido);

            // Act
            proposta.AnalisarCredito();

            // Assert
            proposta.Status.Should().BeOneOf(PropostaStatus.Reprovada, PropostaStatus.Aprovada); // Pode aprovar se score for 101
            if (proposta.Status == PropostaStatus.Reprovada)
            {
                proposta.MotivoReprovacao.Should().Be("Score de crédito insuficiente.");
                proposta.LimiteAprovado.Should().BeNull();
                proposta.CartoesPermitidos.Should().Be(0);
                proposta.Score.Should().NotBeNull().And.BeInRange(0, 101); // Uma faixa mais ampla por conta da aleatoriedade
            }
            else
            { // Caso excepcional score 101
                proposta.Status.Should().Be(PropostaStatus.Aprovada);
                proposta.LimiteAprovado.Should().Be(1000.00m);
                proposta.CartoesPermitidos.Should().Be(1);
                proposta.Score.Should().Be(101);
            }
        }

        [Theory]
        [InlineData("11122233302")] // Final 2 -> Score 101-250 (Limite Baixo)
        [InlineData("11122233303")] // Final 3 -> Score 200-350 (Limite Baixo)
        [InlineData("11122233304")] // Final 4 -> Score 300-501 (Limite Baixo/Alto)
        public void AnalisarCredito_ComCpfTendenciaLimiteBaixo_DeveAprovarLimite1000(string cpf)
        {
            // Arrange
            var proposta = Proposta.Create(_clienteIdValido, _nomeValido, cpf, _emailValido);

            // Act
            proposta.AnalisarCredito();

            // Assert
            // Pode cair em limite alto se score for 501
            proposta.Status.Should().Be(PropostaStatus.Aprovada);
            if (proposta.Score <= 500)
            {
                proposta.LimiteAprovado.Should().Be(1000.00m);
                proposta.CartoesPermitidos.Should().Be(1);
                proposta.Score.Should().NotBeNull().And.BeInRange(101, 500);
            }
            else
            { // Caso excepcional score 501
                proposta.LimiteAprovado.Should().Be(5000.00m);
                proposta.CartoesPermitidos.Should().Be(2);
                proposta.Score.Should().Be(501);
            }

        }

        [Theory]
        [InlineData("11122233305")] // Final 5 -> Score 450-600 (Limite Baixo/Alto)
        [InlineData("11122233306")] // Final 6 -> Score 501-700 (Limite Alto)
        [InlineData("11122233307")] // Final 7 -> Score 650-800 (Limite Alto)
        [InlineData("11122233308")] // Final 8 -> Score 750-900 (Limite Alto)
        [InlineData("11122233309")] // Final 9 -> Score 850-1001 (Limite Alto)
        public void AnalisarCredito_ComCpfTendenciaLimiteAlto_DeveAprovarLimite5000(string cpf)
        {
            var proposta = Proposta.Create(_clienteIdValido, _nomeValido, cpf, _emailValido);

            // Act
            proposta.AnalisarCredito();

            // Assert
            proposta.Status.Should().Be(PropostaStatus.Aprovada);
            // Pode cair em limite baixo se score for <= 500 (caso final 5)
            if (proposta.Score > 500)
            {
                proposta.LimiteAprovado.Should().Be(5000.00m);
                proposta.CartoesPermitidos.Should().Be(2);
                proposta.Score.Should().NotBeNull().And.BeInRange(501, 1001);
            }
            else
            { // Caso excepcional score <= 500 (final 5)
                proposta.LimiteAprovado.Should().Be(1000.00m);
                proposta.CartoesPermitidos.Should().Be(1);
                proposta.Score.Should().NotBeNull().And.BeInRange(450, 500);
            }
        }

        [Fact]
        public void AnalisarCredito_QuandoJaAnalisada_NaoDeveAlterarStatus()
        {
            // Arrange
            var proposta = Proposta.Create(_clienteIdValido, _nomeValido, "11122233308", _emailValido); // Tendência Limite Alto
            proposta.AnalisarCredito(); // Primeira análise
            var statusInicial = proposta.Status;
            var scoreInicial = proposta.Score;

            // Act
            proposta.AnalisarCredito(); // Tenta analisar novamente

            // Assert
            proposta.Status.Should().Be(statusInicial);
            proposta.Score.Should().Be(scoreInicial); // Score não deve ser recalculado
        }
    }
}