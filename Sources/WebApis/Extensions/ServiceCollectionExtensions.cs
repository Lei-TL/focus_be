namespace Focus_Be.Extensions;

using Infrastructure.FocusDbContext;
using Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("FocusDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing connection string 'FocusDb'. Configure ConnectionStrings:FocusDb " +
                "via user secrets or the ConnectionStrings__FocusDb environment variable.");
        }

        services.AddScoped<TrackingInterceptor>();
        services.AddDbContext<FocusDbContext>((provider, options) =>
            options.UseNpgsql(connectionString)
                .AddInterceptors(provider.GetRequiredService<TrackingInterceptor>()));

        return services;
    }
}
