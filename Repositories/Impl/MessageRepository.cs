using Chat.Data;
using Chat.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chat.Repositories.Impl
{
    public class MessageRepository(ApplicationDbContext _context) : IMessageRepository
    {
        public async Task<Message> AddMessageAsync(Message message)
        {
            await _context.Messages.AddAsync(message);
            await _context.SaveChangesAsync();
            return message;
        }

        public async Task<List<Message>> GetMessagesByChatIdAsync(string chatId)
        {
            return await _context.Messages
                .Where(m => m.ChatId == chatId)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
        }

        public async Task<ChatSession> GetChatSessionAsync(string chatId)
        {
            return await _context.Chats
                .Include(cs => cs.Messages)
                .Include(cs => cs.Participants)
                .FirstOrDefaultAsync(cs => cs.ChatId == chatId);
        }

        public async Task<List<ChatSession>> GetUserGroupsAsync(string userName)
        {
            return await _context.Chats
                .Where(cs => cs.Participants.Any(p => p.Username == userName))
                .ToListAsync();
        }

        public async Task AddChatSessionAsync(ChatSession chatSession)
        {
            await _context.Chats.AddAsync(chatSession);
            await _context.SaveChangesAsync();
        }
    }
}
