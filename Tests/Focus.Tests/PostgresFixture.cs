using System.Diagnostics;
using System.Security.Cryptography;
using Infrastructure.Persistence;
using Infrastructure.UserModule.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
namespace Focus.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private string? containerId;
    public WebApplicationFactory<WebApis.Program> Factory { get; private set; } = null!;
    public string ConnectionString { get; private set; } = string.Empty;
    public string Key { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public async Task InitializeAsync()
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        try
        {
            containerId = (await Docker("run", "--rm", "-d", "--name", "focus-test-" + Guid.NewGuid().ToString("N"),
                "-e", "POSTGRES_DB=focus_test", "-e", "POSTGRES_USER=focus", "-e", "POSTGRES_PASSWORD=" + password,
                "-p", "127.0.0.1::5432", "postgres:17")).Trim();
            var portText = (await Docker("port", containerId, "5432/tcp")).Trim();
            var port = portText.Split(':').Last();
            ConnectionString = $"Host=127.0.0.1;Port={port};Database=focus_test;Username=focus;Password={password};Timeout=2";
            var ready = false;
            for (var attempt = 0; attempt < 40; attempt++)
            {
                try
                {
                    await using var connection = new Npgsql.NpgsqlConnection(ConnectionString);
                    await connection.OpenAsync();
                    ready = true;
                    break;
                }
                catch (Npgsql.NpgsqlException) { await Task.Delay(250); }
            }
            if (!ready) throw new InvalidOperationException("Isolated PostgreSQL did not become ready.");
            Factory = new TestApi(ConnectionString, Key);
            using var scope = Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<FocusDbContext>().Database.MigrateAsync();
        }
        catch { await DisposeAsync(); throw; }
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        Npgsql.NpgsqlConnection.ClearAllPools();
        if (!string.IsNullOrWhiteSpace(containerId))
        {
            // Only remove the exact disposable container created by this fixture.
            await Docker("rm", "-f", containerId);
            containerId = null;
        }
    }

    private static async Task<string> Docker(params string[] args)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
        if (process.ExitCode != 0) throw new InvalidOperationException("Docker test setup failed: " + await error);
        return await output;
    }

    private sealed class TestApi(string connection, string key) : WebApplicationFactory<WebApis.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:FocusDb", connection);
            builder.UseSetting("Jwt:Key", key);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:FocusDb"] = connection, ["Jwt:Key"] = key
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FocusDbContext>>();
                services.RemoveAll<FocusDbContext>();
                services.AddDbContext<FocusDbContext>((provider, options) => options.UseNpgsql(connection)
                    .AddInterceptors(provider.GetRequiredService<Infrastructure.Interceptors.TrackingInterceptor>()));
                services.PostConfigure<JwtOptions>(options => { options.Key = key; options.Issuer = "Focus"; options.Audience = "Focus.App"; });
            });
        }
    }
}
