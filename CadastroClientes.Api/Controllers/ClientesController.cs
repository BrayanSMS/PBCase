using CadastroClientes.Application.DTOs;
using CadastroClientes.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CadastroClientes.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ClientesController : ControllerBase
    {
        private readonly IClienteService _clienteService;
        private readonly ILogger<ClientesController> _logger;
        
        public ClientesController(
            IClienteService clienteService,
            ILogger<ClientesController> logger)
        {
            _clienteService = clienteService;
            _logger = logger;
        }

        /// <summary>
        /// Cadastra um novo cliente no sistema.
        /// </summary>
        /// <remarks>
        /// Ao cadastrar, o cliente entra em status "EmAnalise" e uma
        /// mensagem é disparada para o microsserviço de Proposta de Crédito.
        /// </remarks>
        /// <param name="inputModel">Dados do cliente (Nome, CPF, Email)</param>
        /// <returns>Retorna os dados do cliente criado (HTTP 201) ou um erro de validação (HTTP 400).</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ClienteViewModel), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PostCliente([FromBody] ClienteInputModel inputModel)
        {
            _logger.LogInformation("Recebida nova requisição de cadastro para o CPF: {CpfMasked}",
                inputModel.Cpf.Substring(0, 3) + ".***.***-**");
            
            var (viewModel, erro) = await _clienteService.CadastrarClienteAsync(inputModel);
            
            if (erro != null)
            {                
                _logger.LogWarning("Falha na validação de negócio: {Erro}", erro);

                return BadRequest(new ProblemDetails
                {
                    Title = "Erro ao cadastrar cliente",
                    Detail = erro,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Cliente {IdCliente} cadastrado com sucesso.", viewModel!.Id);

            return CreatedAtAction(
                nameof(GetClientePorId),
                new { id = viewModel!.Id },
                viewModel);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{id}")]
        public IActionResult GetClientePorId(Guid id)
        {
            return Ok();
        }
    }
}