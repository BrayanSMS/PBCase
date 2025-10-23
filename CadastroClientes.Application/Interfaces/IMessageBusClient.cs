namespace CadastroClientes.Application.Interfaces
{
    public interface IMessageBusClient
    {
        /// <summary>
        /// Publica uma mensagem em uma fila/tópico/exchange específico.
        /// </summary>
        /// <param name="routingKey">A "chave de roteamento" ou "tópico" da mensagem.</param>
        /// <param name="message">O objeto (payload) a ser serializado e enviado.</param>
        void Publish(string routingKey, object message);
    }
}