using System.Security.Cryptography;
using System.Text;

namespace CadastroClientes.Domain.Entities
{
    
    public class Cliente
    {        
        private Cliente(string nome, string cpf, string email)
        {
            Id = Guid.NewGuid();
            Nome = nome;
            Cpf = CpfLimpo(cpf);
            Email = email;
            DataCadastro = DateTime.UtcNow;
            Status = ClienteStatus.EmAnalise;
        }

        public Guid Id { get; private set; }
        public string Nome { get; private set; }
        public string Cpf { get; private set; }
        public string Email { get; private set; }
        public DateTime DataCadastro { get; private set; }
        public ClienteStatus Status { get; private set; }
        
        public static Cliente Create(string nome, string cpf, string email)
        {            
            if (string.IsNullOrWhiteSpace(nome))
                throw new ArgumentException("Nome não pode ser nulo ou vazio.", nameof(nome));

            if (!IsCpfValido(cpf))
                throw new ArgumentException("CPF inválido.", nameof(cpf));

            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email inválido.", nameof(email));

            return new Cliente(nome, cpf, email);
        }
        
        public void AprovarAnalise()
        {
            if (Status == ClienteStatus.EmAnalise)
                Status = ClienteStatus.AnaliseAprovada;
        }

        public void ReprovarAnalise()
        {
            if (Status == ClienteStatus.EmAnalise)
                Status = ClienteStatus.AnaliseReprovada;
        }

        private static bool IsCpfValido(string cpf)
        {
            var cpfLimpo = CpfLimpo(cpf);
            return !string.IsNullOrWhiteSpace(cpfLimpo) && cpfLimpo.Length == 11 && cpfLimpo.All(char.IsDigit);
        }

        private static string CpfLimpo(string cpf)
        {
            return new string((cpf ?? string.Empty).Where(char.IsDigit).ToArray());
        }
    }

    public enum ClienteStatus
    {
        EmAnalise = 1,
        AnaliseAprovada = 2,
        AnaliseReprovada = 3,
        CadastroConcluido = 4
    }
}