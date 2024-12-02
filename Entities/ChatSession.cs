namespace Chat.Entities;

public class ChatSession
{
    public required string ChatId { get; set; }
    public string GroupName { get; set; }
    public List<Message> Messages { get; set; } = [];
    public List<User> Participants { get; set; } = [];
    public void AddMessage(Message message)
    {
        Messages.Add(message);
    }
    public void AddParticipant(User user)
    {
        if (!Participants.Exists(u => u.Id == user.Id))
        {
            Participants.Add(user);
        }
    }
}
