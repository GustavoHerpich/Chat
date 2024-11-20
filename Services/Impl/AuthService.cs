using Chat.Entities;
using Chat.Logging;
using Chat.Models.DTOs;
using Chat.Models.Login;
using Chat.Models.Settings;
using Chat.Repositories;
using Chat.Utils.ResultPattern;
using Chat.Utils.Token;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Chat.Services.Impl;

public class AuthService(
    ILogger<AuthService> _logger,
    IUserRepository _userRepository,
    IOptionsMonitor<AppSettings> appsettingsMonitor) : IAuthService
{
    private readonly AppSettings _settings = appsettingsMonitor.CurrentValue;

    #region public methods
    public async Task<Result<TokenModel>> AuthenticateAsync(LoginRequest loginModel)
    {
        _logger.AuthenticateAttempt(loginModel.Username);

        var userResult = await ValidateUser(loginModel.Username, loginModel.Password);

        if (userResult is null
            || userResult.Value is null)
        {
            _logger.UserNotValidated(loginModel.Username);
            return Error.NotFound("Could not validate the user.");
        }

        if (userResult.IsFailure)
            return userResult.Error!;

        var user = userResult.Value;

        _logger.UserValidated(user.Username);

        var tokenResult = await GenerateToken(user);

        if (tokenResult is null
            || tokenResult.Value is null)
        {
            _logger.TokenGenerationFailed(user.Username);
            return Error.NotFound("Could not validate the user.");
        }

        if (tokenResult.IsFailure)
            return tokenResult.Error!;

        var token = tokenResult.Value;
        
        _logger.UserAuthenticated(user.Username);

        return Result<TokenModel>.Success(token);
    }

    public async Task<Result<User>> RegisterUser(UserModel user)
    {
        _logger.RegisterAttempt(user.Username);

        var existsUserResult = await _userRepository.FindByUsernameAsync(user.Username);
        if (existsUserResult is not null)
        {
            _logger.UserAlreadyExists(user.Username);
            return Error.InvalidArgument("User already exists in our system.");
        }

        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(user.Password);

        var newUser = new User
        {
            Username = user.Username,
            Password = hashedPassword,
            PasswordExpiration = DateTime.UtcNow.AddDays(30),
            Role = user.Role,
        };

        _logger.CreatingUser(newUser.Username);

        newUser = await _userRepository.CreateAsync(newUser);
        _logger.UserCreated(newUser.Username);

        return Result<User>.Success(newUser);
    }

    public async Task<Result<string>> RecoverPassword(RecoverPasswordRequest recoverPassword)
    {
        _logger.RecoverPasswordAttempt(recoverPassword.UserName);

        var userResult = await _userRepository.FindByUsernameAsync(recoverPassword.UserName);
        if (userResult is null)
        {
            _logger.UserNotFound(recoverPassword.UserName);
            return Error.NotFound("User not found.");
        }

        if (userResult.Password == recoverPassword.NewPassword)
        {
            _logger.PasswordsMatch(recoverPassword.UserName);
            return Error.InvalidArgument("New password cannot be the same as the current password.");
        }

        userResult.Password = recoverPassword.NewPassword;
        _logger.UpdatingPassword(recoverPassword.UserName);

        await _userRepository.UpdateAsync(userResult);
        _logger.PasswordUpdated(recoverPassword.UserName);

        return Result<string>.Success("Password changed successfully");
    }

    #endregion

    #region private methods
    private static ClaimsIdentity CreateClaimsIdentity(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        return new ClaimsIdentity(claims, "jwt");
    }

    private Task<Result<TokenModel>> GenerateToken(User user)
    {
        _logger.StartingToGenerateToken(user.Username);

        var createDate = DateTime.UtcNow;
        var jwtBuilder = new JwtBuilder(_settings.APISecret, Convert.ToInt32(_settings.ExpirationToken))
            .SetCreationDate(createDate)
            .AddClaims(CreateClaimsIdentity(user).Claims);

        var token = new JwtSecurityTokenHandler().WriteToken(jwtBuilder.Build());

        var tokenModel = new TokenModel
        {
            Token = token,
            Expiration = createDate.AddMinutes(Convert.ToInt32(_settings.ExpirationToken))
        };

        _logger.TokenGenerated(user.Username, token);

        return Task.FromResult(Result<TokenModel>.Success(tokenModel));
    }

    private async Task<Result<User>> ValidateUser(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.MissingValidationArguments(username);
            return Error.InvalidArgument("Necessary arguments must be provided to validate the user.");
        }

        var login = await _userRepository.FindByUsernameAsync(username);

        if (login is null)
        {
            _logger.UserNotFound(username);
            return Error.NotFound("User not found.");
        }

        if (!BCrypt.Net.BCrypt.Verify(password, login.Password))
        {
            _logger.InvalidPassword(username);
            // Not returned 400 for security reasons
            return Error.NotFound("Incorrect password.");
        }

        if (login.PasswordExpiration < DateTime.UtcNow)
        {
            _logger.PasswordExpired(username);
            return Error.InvalidArgument("User password has expired. Please change your password.");
        }

        _logger.UserValidated(username);

        return Result<User>.Success(login);
    }

    #endregion
}