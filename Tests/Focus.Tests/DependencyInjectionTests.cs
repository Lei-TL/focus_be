using Application.Authentication;
using Application.UserModule.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebApis.Authentication;
using WebApis.Extention;
using Xunit;

namespace Focus.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddProjectDependencies_registers_application_services_as_scoped()
    {
        var services = new ServiceCollection();
        services.AddProjectDependencies(TestConfiguration());
        services.AddHttpContextAccessor();

        Assert.Single(services, x => x.ServiceType == typeof(AuthApplicationService));
        Assert.Single(services, x => x.ServiceType == typeof(ICurrentUser));

        var app = services.First(x => x.ServiceType == typeof(AuthApplicationService));
        var currentUser = services.First(x => x.ServiceType == typeof(ICurrentUser));
        Assert.Equal(ServiceLifetime.Scoped, app.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, currentUser.Lifetime);
        Assert.Equal(typeof(CurrentUser), currentUser.ImplementationType);
    }

    [Fact]
    public void AddProjectDependencies_resolves_in_scope_without_real_database()
    {
        var services = new ServiceCollection();
        services.AddProjectDependencies(TestConfiguration());
        services.AddHttpContextAccessor();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AuthApplicationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICurrentUser>());
        Assert.IsType<CurrentUser>(scope.ServiceProvider.GetRequiredService<ICurrentUser>());
    }

    private static IConfiguration TestConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:FocusDb"] = "Host=127.0.0.1;Port=1;Database=focus_test;Username=focus;Password=test-only",
            ["Jwt:Key"] = new string('k', 48),
            ["Jwt:Issuer"] = "Focus",
            ["Jwt:Audience"] = "Focus.App",
            ["Jwt:AccessMinutes"] = "30",
            ["Jwt:RefreshDays"] = "30"
        }).Build();
}
