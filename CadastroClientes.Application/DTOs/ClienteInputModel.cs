using System.ComponentModel.DataAnnotations;

namespace CadastroClientes.Application.DTOs
{
    public class ClienteInputModel
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [Length(3, 100, ErrorMessage = "O nome deve ter entre 3 e 100 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O CPF é obrigatório.")]
        public string Cpf { get; set; } = string.Empty;

        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "Formato de e-mail inválido.")]
        public string Email { get; set; } = string.Empty;
    }
}