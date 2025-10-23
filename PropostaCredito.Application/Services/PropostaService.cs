using Microsoft.Extensions.Logging;
using PropostaCredito.Application.DTOs;
using PropostaCredito.Application.Interfaces;
using PropostaCredito.Domain.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PropostaCredito.Application.Services
{
    public class PropostaService : IPropostaService
    {
        private readonly IPropostaRepository _propostaRepository;
        private readonly IMessageBusClient _messageBusClient;
        private readonly ILogger<PropostaService> _logger;

        // Routing keys para publicação dos resultados
        private const string PropostaAprovadaRoutingKey = "proposta.aprovada";
        private const string PropostaReprovadaRoutingKey = "proposta.reprovada";

        public PropostaService(
            IPropostaRepository propostaRepository,
            IMessageBusClient messageBusClient,
            ILogger<PropostaService> logger)
        {
            _propostaRepository = propostaRepository;
            _messageBusClient = messageBusClient;
            _logger = logger;
        }

        public async Task ProcessarPropostaAsync(ClienteCriadoMessage message)
        {
            _logger.LogInformation("Recebida mensagem para processar proposta do cliente ID: {ClienteId}", message.IdCliente);

            if (message.IdCliente == Guid.Empty ||
                string.IsNullOrWhiteSpace(message.Nome) ||
                string.IsNullOrWhiteSpace(message.Cpf) ||
                string.IsNullOrWhiteSpace(message.Email))
            {
                _logger.LogError("Mensagem ClienteCriadoMessage inválida recebida: {@Message}", message);
                return;
            }
            
            var cpfLimpo = new string(message.Cpf.Where(char.IsDigit).ToArray());
            if (cpfLimpo.Length != 11)
            {
                _logger.LogError("CPF inválido na mensagem ClienteCriadoMessage: {Cpf}", message.Cpf);
                return;
            }
            
            Proposta proposta;
            try
            {
                proposta = Proposta.Create(message.IdCliente, message.Nome, cpfLimpo, message.Email);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Erro ao criar entidade Proposta para cliente {ClienteId}. Mensagem inválida?", message.IdCliente);
                return;
            }

            try
            {
                await _propostaRepository.AddAsync(proposta);
                _logger.LogInformation("Proposta {PropostaId} criada para cliente {ClienteId}.", proposta.Id, proposta.ClienteId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar proposta inicial {PropostaId} para cliente {ClienteId}. Mensagem será descartada (ou enviada para DLQ pelo consumer).", proposta.Id, proposta.ClienteId);
                throw; 
            }

            try
            {
                proposta.AnalisarCredito();
                _logger.LogInformation("Análise de crédito concluída para proposta {PropostaId}. Score: {Score}, Status: {Status}",
                    proposta.Id, proposta.Score ?? 0, proposta.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao analisar crédito para proposta {PropostaId}.", proposta.Id);                
                return;
            }

            try
            {
                await _propostaRepository.UpdateAsync(proposta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar proposta {PropostaId} com resultado da análise. Status da publicação pode ficar inconsistente.", proposta.Id);                
            }

            try
            {
                if (proposta.Status == PropostaStatus.Aprovada)
                {
                    var msgAprovada = new PropostaAprovadaMessage
                    {
                        PropostaId = proposta.Id,
                        ClienteId = proposta.ClienteId,
                        CpfCliente = proposta.CpfCliente,
                        LimiteAprovado = proposta.LimiteAprovado ?? 0,
                        CartoesPermitidos = proposta.CartoesPermitidos
                    };
                    _messageBusClient.Publish(PropostaAprovadaRoutingKey, msgAprovada);
                    _logger.LogInformation("Mensagem PropostaAprovadaMessage publicada para proposta {PropostaId}.", proposta.Id);
                }
                else if (proposta.Status == PropostaStatus.Reprovada)
                {
                    var msgReprovada = new PropostaReprovadaMessage
                    {
                        PropostaId = proposta.Id,
                        ClienteId = proposta.ClienteId,
                        CpfCliente = proposta.CpfCliente,
                        MotivoReprovacao = proposta.MotivoReprovacao ?? "Motivo não especificado"
                    };
                    _messageBusClient.Publish(PropostaReprovadaRoutingKey, msgReprovada);
                    _logger.LogInformation("Mensagem PropostaReprovadaMessage publicada para proposta {PropostaId}.", proposta.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao publicar resultado da proposta {PropostaId} no message bus.", proposta.Id);               
                throw;
            }
        }
    }
}