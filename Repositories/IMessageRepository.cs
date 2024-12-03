using Chat.Entities;

namespace Chat.Repositories
{
    public interface IMessageRepository
    {
        Task<Message> AddMessageAsync(Message message);
        Task<List<Message>> GetMessagesByChatIdAsync(string chatId);
        Task AddChatSessionAsync(ChatSession chatSession);
        Task<ChatSession> GetChatSessionAsync(string chatId);
        Task<List<ChatSession>> GetUserGroupsAsync(string userName);
        Task<string> GetChatIdByGroupNameAsync(string groupName);
    }
}