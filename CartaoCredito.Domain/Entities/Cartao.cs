using System;
using System.Text;

namespace CartaoCredito.Domain.Entities
{
    public class Cartao
    {
        private Cartao(Guid clienteId, Guid propostaId, decimal limite)
        {
            Id = Guid.NewGuid();
            ClienteId = clienteId;
            PropostaId = propostaId;
            Limite = limite;
            DataEmissao = DateTime.UtcNow;

            // Gera dados fictícios do cartão
            NumeroCartao = GerarNumeroCartaoFicticio();
            Cvv = GerarCvvFicticio();
            DataValidade = GerarDataValidadeFicticia();
        }

        public Guid Id { get; private set; }
        public Guid ClienteId { get; private set; }
        public Guid PropostaId { get; private set; }
        public string NumeroCartao { get; private set; }
        public string Cvv { get; private set; }
        public DateTime DataValidade { get; private set; }
        public decimal Limite { get; private set; }
        public DateTime DataEmissao { get; private set; }

        /// <summary>
        /// Método de Fábrica para criar um novo cartão.
        /// </summary>
        public static Cartao Create(Guid clienteId, Guid propostaId, decimal limite)
        {
            // Validações básicas
            if (clienteId == Guid.Empty)
                throw new ArgumentException("Id do cliente inválido.", nameof(clienteId));
            if (propostaId == Guid.Empty)
                throw new ArgumentException("Id da proposta inválido.", nameof(propostaId));
            if (limite <= 0)
                throw new ArgumentException("Limite do cartão deve ser positivo.", nameof(limite));

            return new Cartao(clienteId, propostaId, limite);
        }

        private static string GerarNumeroCartaoFicticio()
        {
            // Gera um número de 16 dígitos aleatório (não segue regras de bandeira)
            // Exemplo simples: Prefixo fixo + aleatório
            var random = Random.Shared;
            var builder = new StringBuilder("5500"); // Prefixo exemplo
            for (int i = 0; i < 12; i++)
            {
                builder.Append(random.Next(0, 10));
            }
            return builder.ToString();
        }

        private static string GerarCvvFicticio()
        {
            // Gera um CVV de 3 dígitos aleatório
            return Random.Shared.Next(100, 1000).ToString();
        }

        private static DateTime GerarDataValidadeFicticia()
        {
            // Define validade para 5 anos a partir da emissão, no último dia do mês
            var dataEmissao = DateTime.UtcNow;
            var dataValidade = dataEmissao.AddYears(5);
            // Ajusta para o último dia do mês
            return new DateTime(dataValidade.Year, dataValidade.Month, DateTime.DaysInMonth(dataValidade.Year, dataValidade.Month), 23, 59, 59, DateTimeKind.Utc);
        }
    }
}