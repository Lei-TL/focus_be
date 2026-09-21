using Application.UserModule.Services;
using Domain.Entities.UserModule;
using Xunit;
namespace Focus.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Inner_layers_do_not_reference_outer_layers()
    {
        var domain = typeof(User).Assembly.GetReferencedAssemblies().Select(x => x.Name!).ToArray();
        var application = typeof(AuthApplicationService).Assembly.GetReferencedAssemblies().Select(x => x.Name!).ToArray();
        Assert.DoesNotContain(domain, x => x is "Application" or "Infrastructure" or "WebApis" || x.StartsWith("Microsoft.EntityFrameworkCore"));
        Assert.DoesNotContain(application, x => x is "Infrastructure" or "WebApis" || x.StartsWith("Microsoft.AspNetCore") || x.StartsWith("Microsoft.EntityFrameworkCore") || x.Contains("IdentityModel"));
    }

    [Fact]
    public void Business_endpoints_only_depend_on_application_and_http()
    {
        var root = FindRoot();
        var files = Directory.GetFiles(Path.Combine(root, "Sources", "WebApis", "Endpoints"), "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            foreach (var forbidden in new[] { "using Infrastructure", "using Domain", "DbContext", "SaveChanges", "BeginTransaction", "PasswordHasher", "JwtSecurityToken", "ExecuteUpdate" })
                Assert.DoesNotContain(forbidden, source);
        }
    }

    [Fact]
    public void User_entities_follow_the_agreed_folder_format()
    {
        var root = FindRoot();
        Assert.True(File.Exists(Path.Combine(root, "Sources/Domain/Entities/UserModule/User.cs")));
        Assert.Equal("Domain.Entities.UserModule", typeof(User).Namespace);
    }

    internal static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Sources/Web_Apis.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Project root not found.");
    }
}
