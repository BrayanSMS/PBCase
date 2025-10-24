using CadastroClientes.Application.DTOs;
using CadastroClientes.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CadastroClientes.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")] // Rota: /api/v1/clientes
    public class ClientesController : ControllerBase
    {
        private readonly IClienteService _clienteService;
        private readonly ILogger<ClientesController> _logger;

        // O IClienteService é injetado via DI (configurado no Program.cs
        // e no DependencyInjectionConfig.cs)
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
            // O [ApiController] e as DataAnnotations (ex: [Required])
            // no ClienteInputModel já tratam validações básicas
            // (ex: campos nulos ou email inválido).

            _logger.LogInformation("Recebida nova requisição de cadastro para o CPF: {CpfMasked}",
                inputModel.Cpf.Substring(0, 3) + ".***.***-**");

            // 1. Delega a lógica para o Serviço de Aplicação
            var (viewModel, erro) = await _clienteService.CadastrarClienteAsync(inputModel);

            // 2. Mapeia o resultado
            if (erro != null)
            {
                // Se o 'erro' não for nulo, significa que uma regra de negócio falhou
                // (ex: CPF duplicado ou inválido)
                _logger.LogWarning("Falha na validação de negócio: {Erro}", erro);

                // Retornamos um HTTP 400 (Bad Request)
                // Usamos "ProblemDetails" para um formato de erro padrão.
                return BadRequest(new ProblemDetails
                {
                    Title = "Erro ao cadastrar cliente",
                    Detail = erro,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Se chegamos aqui, 'viewModel' não é nulo e o cadastro foi um sucesso.
            _logger.LogInformation("Cliente {IdCliente} cadastrado com sucesso.", viewModel!.Id);

            // Retornamos HTTP 201 (Created)
            // É uma boa prática REST retornar um header "Location"
            // indicando a URL para buscar o recurso recém-criado
            // (embora não tenhamos implementado o GET ainda).
            return CreatedAtAction(
                nameof(GetClientePorId), // Nome do método GET (que ainda não criamos)
                new { id = viewModel!.Id },
                viewModel);
        }

        // Método "fantasma" apenas para o CreatedAtAction funcionar.
        // Não precisamos implementá-lo para este desafio.
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{id}")]
        public IActionResult GetClientePorId(Guid id)
        {
            return Ok();
        }
    }
}