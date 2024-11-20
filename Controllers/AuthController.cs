using Chat.Models.DTOs;
using Chat.Models.Login;
using Chat.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chat.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService _authService) : ControllerBase
{
    [HttpPost("Login")]
    [AllowAnonymous]
    public async Task<ActionResult> Login([FromBody] LoginRequest login)
        => await _authService.AuthenticateAsync(login);

    [HttpPost("Register")]
    [AllowAnonymous]
    public async Task<ActionResult> RegisterAsync([FromBody] UserModel user)
        => await _authService.RegisterUser(user);

    [HttpPost("recoverpassword")]
    [AllowAnonymous]
    public async Task<ActionResult> RecoverPasswordAsync([FromBody] RecoverPasswordRequest request)
        => await _authService.RecoverPassword(request);
}
