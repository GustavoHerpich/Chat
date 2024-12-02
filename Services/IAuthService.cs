using Chat.Entities;
using Chat.Models.DTOs;
using Chat.Models.Login;
using Chat.Utils.ResultPattern;

namespace Chat.Services;

public interface IAuthService
{
    Task<Result<User>> RegisterUser(UserModel user);
    Task<Result<TokenModel>> AuthenticateAsync(LoginRequest loginModel);
    Task<Result<string>> RecoverPassword(RecoverPasswordRequest recoverPassword);
    Task<Result<User>> GetUser(string userName);
}
