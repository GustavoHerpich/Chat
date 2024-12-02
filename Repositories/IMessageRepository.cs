using Chat.Entities;

namespace Chat.Repositories
{
    public interface IMessageRepository
    {
        Task<Message> AddMessageAsync(Message message);
        Task<List<Message>> GetMessagesByChatIdAsync(string chatId);
        Task AddChatSessionAsync(ChatSession chatSession);
        Task<ChatSession> GetChatSessionAsync(string chatId);
    }
}