using Application.Authentication;
using Application.UserModule.Services;
using Application.WorkItemModule.Services;
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
        services.AddWorkItemInfrastructure();
        services.AddScoped<AuthApplicationService>();
        services.AddScoped<WorkItemApplicationService>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        return services;
    }
}
