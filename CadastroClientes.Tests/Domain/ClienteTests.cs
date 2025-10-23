using CadastroClientes.Domain.Entities;
using FluentAssertions;

namespace CadastroClientes.Tests.Domain
{
    public class ClienteTests
    {
        [Fact] // Indica que é um método de teste
        public void Create_ComDadosValidos_DeveCriarClienteComStatusEmAnalise()
        {
            string nomeValido = "Brayan S.";
            string cpfValido = "123.456.789-00";
            string emailValido = "teste@teste.com";

            // Act (Ação)
            var cliente = Cliente.Create(nomeValido, cpfValido, emailValido);

            // Assert (Verificação)
            cliente.Should().NotBeNull(); // Garante que o cliente foi criado
            cliente.Nome.Should().Be(nomeValido);
            cliente.Cpf.Should().Be("12345678900"); // CPF deve ser armazenado limpo
            cliente.Email.Should().Be(emailValido);
            cliente.Status.Should().Be(ClienteStatus.EmAnalise);
            cliente.Id.Should().NotBeEmpty(); // Deve ter um Guid gerado
            cliente.DataCadastro.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5)); // Data/hora aproximada
        }

        [Theory] // Indica um teste parametrizado
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_ComNomeInvalido_DeveLancarArgumentException(string nomeInvalido)
        {            
            string cpfValido = "123.456.789-00";
            string emailValido = "teste@teste.com";

            // Act
            // Usamos uma Action para capturar a exceção
            Action act = () => Cliente.Create(nomeInvalido, cpfValido, emailValido);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("Nome não pode ser nulo ou vazio.*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1234567890")] // Menos de 11 dígitos
        [InlineData("123.456.789-AB")] // Com letras
        [InlineData("111111111111")] // Mais de 11 dígitos
        public void Create_ComCpfInvalido_DeveLancarArgumentException(string cpfInvalido)
        {
            // Arrange
            string nomeValido = "Brayan S.";
            string emailValido = "teste@teste.com";

            // Act
            Action act = () => Cliente.Create(nomeValido, cpfInvalido, emailValido);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("CPF inválido.*");
        }

        [Theory]
        [InlineData("email-invalido")]
        [InlineData("")]
        // Poderíamos adicionar mais casos de e-mail inválido, mas o básico é testar vazio/nulo
        public void Create_ComEmailInvalido_DeveLancarArgumentException(string emailInvalido)
        {
            // Arrange
            string nomeValido = "Brayan S.";
            string cpfValido = "123.456.789-00";

            // Act
            // Neste caso, a validação de email na entidade é muito simples (apenas IsNullOrWhiteSpace).
            // A validação de formato [EmailAddress] está no DTO.
            // Para testar a validação da entidade:
            if (string.IsNullOrWhiteSpace(emailInvalido))
            {
                Action act = () => Cliente.Create(nomeValido, cpfValido, emailInvalido);
                // Assert
                act.Should().Throw<ArgumentException>()
                  .WithMessage("Email inválido.*");
            }
            else
            {
                var cliente = Cliente.Create(nomeValido, cpfValido, emailInvalido);
                cliente.Should().NotBeNull();
            }
        }

        // --- Testes para Mudança de Status (Exemplo) ---
        [Fact]
        public void AprovarAnalise_QuandoStatusEmAnalise_DeveMudarStatusParaAnaliseAprovada()
        {
            // Arrange
            var cliente = Cliente.Create("Nome", "12345678900", "email@valido.com");

            // Act
            cliente.AprovarAnalise();

            // Assert
            cliente.Status.Should().Be(ClienteStatus.AnaliseAprovada);
        }

        [Fact]
        public void ReprovarAnalise_QuandoStatusEmAnalise_DeveMudarStatusParaAnaliseReprovada()
        {
            // Arrange
            var cliente = Cliente.Create("Nome", "12345678900", "email@valido.com");

            // Act
            cliente.ReprovarAnalise();

            // Assert
            cliente.Status.Should().Be(ClienteStatus.AnaliseReprovada);
        }
    }
}