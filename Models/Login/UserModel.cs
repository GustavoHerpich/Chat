using Chat.Models.Enums;

namespace Chat.Models.Login;

public class UserModel
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}