using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WebApis.Extention;
using Xunit;

namespace Focus.Tests;

// Không cần Docker/database: host dùng config in-memory riêng, cổng động,
// database trỏ cổng không tồn tại (không có request nào chạm DB trong các test này).
public sealed class BootstrapTests
{
    [Fact]
    public void Missing_connection_string_fails_at_registration()
    {
        var services = new ServiceCollection();
        var error = Assert.Throws<InvalidOperationException>(() =>
            services.AddProjectDependencies(ConfigurationWithoutDatabase()));
        Assert.Contains("ConnectionStrings:FocusDb", error.Message);
    }

    [Fact]
    public async Task Invalid_jwt_key_fails_at_host_startup()
    {
        await using var app = BuildApp(JwtEntries("short"));
        await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync());
    }

    [Fact]
    public async Task Missing_jwt_section_fails_at_host_startup()
    {
        await using var app = BuildApp(new Dictionary<string, string?>());
        await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync());
    }

    [Fact]
    public async Task Valid_configuration_boots_through_both_extensions()
    {
        await using var app = BuildApp(JwtEntries(new string('k', 48)));
        await app.StartAsync();
        await app.StopAsync();
    }

    private static WebApplication BuildApp(Dictionary<string, string?> entries)
    {
        entries["ConnectionStrings:FocusDb"] =
            "Host=127.0.0.1;Port=1;Database=focus_test;Username=focus;Password=test-only;Timeout=2";
        entries["urls"] = "http://127.0.0.1:0";
        var builder = WebApplication.CreateBuilder();
        // Cô lập config test: loại mọi nguồn kế thừa (appsettings, user secrets,
        // biến môi trường máy) trước khi thêm in-memory, để test thiếu JWT
        // không bị Jwt__Key ngoài môi trường làm sai lệch.
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(entries);
        builder.Services.AddProjectDependencies(builder.Configuration);
        builder.Services.AddProjectServices();
        return builder.Build();
    }

    private static IConfiguration ConfigurationWithoutDatabase() =>
        new ConfigurationBuilder().AddInMemoryCollection(JwtEntries(new string('k', 48))).Build();

    private static Dictionary<string, string?> JwtEntries(string? key) => new()
    {
        ["Jwt:Key"] = key,
        ["Jwt:Issuer"] = "Focus",
        ["Jwt:Audience"] = "Focus.App",
        ["Jwt:AccessMinutes"] = "30",
        ["Jwt:RefreshDays"] = "30"
    };
}
