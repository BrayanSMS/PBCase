namespace CadastroClientes.Application.DTOs
{
    public class ClienteViewModel
    {
        public Guid Id { get; set; }
        public string Nome { get; set; }
        public string Email { get; set; }
        public string Status { get; set; }
        public DateTime DataCadastro { get; set; }

        public static ClienteViewModel FromEntity(Domain.Entities.Cliente cliente)
        {
            return new ClienteViewModel
            {
                Id = cliente.Id,
                Nome = cliente.Nome,
                Email = cliente.Email,
                Status = cliente.Status.ToString(),
                DataCadastro = cliente.DataCadastro
            };
        }
    }
}