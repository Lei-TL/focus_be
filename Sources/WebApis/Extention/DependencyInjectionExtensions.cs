using Application.Authentication;
using Application.UserModule.Services;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebApis.Authentication;

namespace WebApis.Extention;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddProjectDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabaseService(configuration);
        services.AddUserInfrastructure(configuration);
        services.AddScoped<AuthApplicationService>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        return services;
    }
}
