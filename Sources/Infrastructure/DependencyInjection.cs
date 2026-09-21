using System.Text;
using Application.UserModule.Abstractions;
using Domain.Entities.UserModule;
using Infrastructure.Interceptors;
using Infrastructure.Persistence;
using Infrastructure.UserModule.Persistence;
using Infrastructure.UserModule.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDatabaseService(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("FocusDb");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Configure ConnectionStrings:FocusDb via user secrets or environment.");
        services.AddScoped<TrackingInterceptor>();
        services.AddDbContext<FocusDbContext>((provider, options) =>
            options.UseNpgsql(connection).AddInterceptors(provider.GetRequiredService<TrackingInterceptor>()));
        return services;
    }

    public static IServiceCollection AddUserInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection("Jwt"))
            .Validate(x => !string.IsNullOrWhiteSpace(x.Key) && Encoding.UTF8.GetByteCount(x.Key) >= 32,
                "Jwt:Key must contain at least 32 bytes.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer) && !string.IsNullOrWhiteSpace(x.Audience)
                && x.AccessMinutes > 0 && x.RefreshDays > 0, "Invalid JWT settings.")
            .ValidateOnStart();
        services.AddScoped<IUserAuthStore, UserAuthStore>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IPasswordService, IdentityPasswordService>();
        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<ISeedGenerator, CryptoSeedGenerator>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
