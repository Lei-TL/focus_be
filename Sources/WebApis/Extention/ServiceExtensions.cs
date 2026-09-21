using System.Text;
using System.Threading.RateLimiting;
using FastEndpoints;
using Infrastructure.Persistence;
using Infrastructure.UserModule.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace WebApis.Extention;

public static class ServiceExtensions
{
    public static IServiceCollection AddProjectServices(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, configured) =>
            {
                var settings = configured.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters {
                    ValidateIssuer = true, ValidIssuer = settings.Issuer,
                    ValidateAudience = true, ValidAudience = settings.Audience,
                    ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256], NameClaimType = "sub", RoleClaimType = "role"
                };
            });
        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        services.AddFastEndpoints(options => options.Assemblies = [typeof(Program).Assembly]);
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        return services;
    }
}
