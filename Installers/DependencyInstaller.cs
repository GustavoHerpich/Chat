using Chat.Repositories;
using Chat.Repositories.Impl;
using Chat.Services;
using Chat.Services.Impl;

namespace Chat.Installers;

public static class DependencyInstaller
{
    public static void AddDependencies(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
    }
}