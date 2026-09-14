namespace Focus_BE.WebAPIs;

public static class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        ConfigureServices(builder.Services, builder.Configuration);

        WebApplication app = builder.Build();

        app.Run();

    }

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        Focus_Be.Extensions.ServiceCollectionExtensions.AddDatabaseService(services, configuration);
    }
}

