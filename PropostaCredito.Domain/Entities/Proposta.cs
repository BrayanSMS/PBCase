using System;
using System.Linq;

namespace PropostaCredito.Domain.Entities
{
    public class Proposta
    {
        private Proposta(Guid clienteId, string nomeCliente, string cpfCliente, string emailCliente)
        {
            Id = Guid.NewGuid();
            ClienteId = clienteId;
            NomeCliente = nomeCliente;
            CpfCliente = cpfCliente;
            EmailCliente = emailCliente;
            DataCriacao = DateTime.UtcNow;
            Status = PropostaStatus.Pendente;
            Score = null;
            LimiteAprovado = null;
            CartoesPermitidos = 0;
            MotivoReprovacao = null;
        }

        public Guid Id { get; private set; }
        public Guid ClienteId { get; private set; }
        public string NomeCliente { get; private set; }
        public string CpfCliente { get; private set; }
        public string EmailCliente { get; private set; }
        public DateTime DataCriacao { get; private set; }
        public PropostaStatus Status { get; private set; }
        public int? Score { get; private set; }
        public decimal? LimiteAprovado { get; private set; }
        public int CartoesPermitidos { get; private set; }
        public string? MotivoReprovacao { get; private set; }

        /// <summary>
        /// Método de Fábrica para criar uma proposta inicial.
        /// </summary>
        public static Proposta Create(Guid clienteId, string nomeCliente, string cpfCliente, string emailCliente)
        {            
            if (clienteId == Guid.Empty)
                throw new ArgumentException("Id do cliente inválido.", nameof(clienteId));
            if (string.IsNullOrWhiteSpace(nomeCliente))
                throw new ArgumentException("Nome do cliente inválido.", nameof(nomeCliente));
            if (string.IsNullOrWhiteSpace(cpfCliente) || cpfCliente.Length != 11 || !cpfCliente.All(char.IsDigit))
                throw new ArgumentException("CPF do cliente inválido.", nameof(cpfCliente));
            if (string.IsNullOrWhiteSpace(emailCliente)) // Simples verificação de e-mail não vazio
                throw new ArgumentException("Email do cliente inválido.", nameof(emailCliente));

            return new Proposta(clienteId, nomeCliente, cpfCliente, emailCliente);
        }

        /// <summary>
        /// Processa a análise de crédito, calcula o score e define o resultado.
        /// </summary>
        public void AnalisarCredito()
        {
            if (Status != PropostaStatus.Pendente)                            
                return;

            // Simulação da lógica de Score (em um cenário real, chamaria um serviço externo)
            // Irá gerar um score aleatório baseado no último dígito do CPF
            var ultimoDigitoCpf = int.Parse(CpfCliente.Substring(CpfCliente.Length - 1));
            // Mapeia o dígito para faixas de score
            Score = ultimoDigitoCpf switch
            {
                0 => Random.Shared.Next(0, 50),       // Tendência a Reprovar
                1 => Random.Shared.Next(50, 101),     // Tendência a Reprovar
                2 => Random.Shared.Next(101, 250),    // Tendência a Limite Baixo
                3 => Random.Shared.Next(200, 350),    // Tendência a Limite Baixo
                4 => Random.Shared.Next(300, 501),    // Tendência a Limite Baixo
                5 => Random.Shared.Next(450, 600),    // Tendência a Limite Alto
                6 => Random.Shared.Next(501, 700),    // Tendência a Limite Alto
                7 => Random.Shared.Next(650, 800),    // Tendência a Limite Alto
                8 => Random.Shared.Next(750, 900),    // Tendência a Limite Alto
                _ => Random.Shared.Next(850, 1001)    // Tendência a Limite Alto (dígito 9)
            }; 

            if (Score <= 100)            
                Reprovar("Score de crédito insuficiente.");            
            else if (Score <= 500)            
                Aprovar(1000.00m, 1);            
            else            
                Aprovar(5000.00m, 2);            
        }

        private void Aprovar(decimal limite, int cartoes)
        {
            Status = PropostaStatus.Aprovada;
            LimiteAprovado = limite;
            CartoesPermitidos = cartoes;
            MotivoReprovacao = null;
        }

        private void Reprovar(string motivo)
        {
            Status = PropostaStatus.Reprovada;
            LimiteAprovado = null;
            CartoesPermitidos = 0;
            MotivoReprovacao = motivo;
        }
    }

    public enum PropostaStatus
    {
        Pendente = 1,
        Aprovada = 2,
        Reprovada = 3
    }
}