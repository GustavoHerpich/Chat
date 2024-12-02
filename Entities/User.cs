using Chat.Models.Enums;

namespace Chat.Entities;

public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public string Password { get; set; }
    public DateTime PasswordExpiration { get; set; }
    public Roles Role { get; set; }
    public List<ChatSession> Chats { get; set; } = [];
}
