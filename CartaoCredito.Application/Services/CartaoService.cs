using CartaoCredito.Application.DTOs;
using CartaoCredito.Application.Interfaces;
using CartaoCredito.Domain.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;

namespace CartaoCredito.Application.Services
{
    public class CartaoService : ICartaoService
    {
        private readonly ICartaoRepository _cartaoRepository;
        private readonly ILogger<CartaoService> _logger;

        public CartaoService(
            ICartaoRepository cartaoRepository,
            ILogger<CartaoService> logger)
        {
            _cartaoRepository = cartaoRepository;
            _logger = logger;
        }

        public async Task ProcessarEmissaoCartaoAsync(PropostaAprovadaMessage message)
        {
            _logger.LogInformation(
                "Recebida mensagem para emitir {CartoesPermitidos} cartão(ões) com limite {Limite:C} para Proposta ID: {PropostaId}, Cliente ID: {ClienteId}",
                message.CartoesPermitidos,
                message.LimiteAprovado,
                message.PropostaId,
                message.ClienteId);

            if (message.ClienteId == Guid.Empty || message.PropostaId == Guid.Empty || message.LimiteAprovado <= 0 || message.CartoesPermitidos <= 0)
            {
                _logger.LogError("Mensagem PropostaAprovadaMessage inválida recebida: {@Message}", message);
                throw new ArgumentException("Mensagem PropostaAprovadaMessage inválida.");
            }

            var cartoesParaSalvar = new List<Cartao>();
            try
            {
                for (int i = 0; i < message.CartoesPermitidos; i++)
                {
                    var novoCartao = Cartao.Create(message.ClienteId, message.PropostaId, message.LimiteAprovado);
                    cartoesParaSalvar.Add(novoCartao);

                    _logger.LogInformation(
                        "Cartão {NumeroCartao} (Parcial) com limite {Limite:C} gerado para Cliente {ClienteId} (Proposta {PropostaId})",
                       novoCartao.NumeroCartao.Substring(novoCartao.NumeroCartao.Length - 4),
                       novoCartao.Limite,
                       novoCartao.ClienteId,
                       novoCartao.PropostaId);
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Erro ao criar entidade Cartao para Proposta {PropostaId}.", message.PropostaId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao gerar dados do cartão para Proposta {PropostaId}.", message.PropostaId);
                throw;
            }

            if (cartoesParaSalvar.Any())
            {
                try
                {
                    if (cartoesParaSalvar.Count == 1)
                    {
                        await _cartaoRepository.AddAsync(cartoesParaSalvar[0]);
                    }
                    else
                    {
                        await _cartaoRepository.AddRangeAsync(cartoesParaSalvar);
                    }
                    _logger.LogInformation("{Count} cartão(ões) salvo(s) com sucesso para Proposta {PropostaId}.", cartoesParaSalvar.Count, message.PropostaId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao salvar cartão(ões) para Proposta {PropostaId}.", message.PropostaId);
                    throw;
                }
            }
            else
            {
                _logger.LogWarning("Nenhum cartão foi gerado para Proposta {PropostaId}, embora a mensagem fosse válida.", message.PropostaId);
            }            
        }
    }
}