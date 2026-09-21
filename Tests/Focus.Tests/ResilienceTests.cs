using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Focus.Tests;

// Không cần Docker: hai host test riêng dùng database không tới được.
// Health 200 đã được AuthIntegrationTests bao phủ nên ở đây chỉ thêm 503.
public sealed class HealthDegradedTests
{
    [Fact]
    public async Task Health_returns_503_when_database_is_unreachable()
    {
        using var factory = new UnreachableDbApi();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unhealthy", body.GetProperty("status").GetString());
    }

    private sealed class UnreachableDbApi : WebApplicationFactory<WebApis.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            // Program.Main tự tạo WebApplication.CreateBuilder nên config riêng
            // phải đi qua UseSetting (pattern PostgresFixture đang dùng đạt).
            foreach (var (key, value) in TestSettings.UnreachableDb())
                builder.UseSetting(key, value);
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(TestSettings.UnreachableDb()));
        }
    }
}

public sealed class AuthRateLimitTests
{
    [Fact]
    public async Task Auth_endpoints_reject_requests_beyond_30_per_minute()
    {
        using var factory = new IsolatedRateLimitApi();
        using var client = factory.CreateClient();
        var rejected = 0;
        const int total = 35;
        for (var i = 0; i < total; i++)
        {
            // Body sai validation nên trả 400 mà không chạm database;
            // rate limiter chạy trước endpoint nên mọi request đều bị đếm.
            using var response = await client.PostAsJsonAsync("/auth/register",
                new { email = "bad", password = "x", displayName = "" });
            if (response.StatusCode == (HttpStatusCode)429) rejected++;
            else Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Equal(30, total - rejected);
        Assert.Equal(5, rejected);
    }

    private sealed class IsolatedRateLimitApi : WebApplicationFactory<WebApis.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            foreach (var (key, value) in TestSettings.UnreachableDb())
                builder.UseSetting(key, value);
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(TestSettings.UnreachableDb()));
        }
    }
}

internal static class TestSettings
{
    public static Dictionary<string, string?> UnreachableDb() => new()
    {
        ["ConnectionStrings:FocusDb"] =
            "Host=127.0.0.1;Port=1;Database=focus_test;Username=focus;Password=test-only;Timeout=2",
        ["Jwt:Key"] = new string('k', 48),
        ["Jwt:Issuer"] = "Focus",
        ["Jwt:Audience"] = "Focus.App",
        ["Jwt:AccessMinutes"] = "30",
        ["Jwt:RefreshDays"] = "30"
    };
}
