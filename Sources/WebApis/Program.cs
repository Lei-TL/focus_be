using FastEndpoints;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WebApis.Extention;

namespace WebApis;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddProjectDependencies(builder.Configuration);
        builder.Services.AddProjectServices();
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseFastEndpoints(options =>
        {
            options.Errors.ResponseBuilder = (failures, _, status) => new HttpValidationProblemDetails(
                failures.GroupBy(x => x.PropertyName).ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray()))
                { Status = status };
        });
        app.MapHealthChecks("/health", new HealthCheckOptions {
            ResponseWriter = (context, report) => context.Response.WriteAsJsonAsync(
                new { status = report.Status == HealthStatus.Healthy ? "healthy" : "unhealthy" })
        });
        app.Run();
    }
}
