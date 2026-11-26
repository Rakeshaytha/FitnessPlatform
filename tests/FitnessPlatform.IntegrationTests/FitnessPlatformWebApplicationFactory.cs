using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FitnessPlatform.IntegrationTests;

public class FitnessPlatformWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private const string TestDatabaseName =
        "FitnessPlatformIntegrationTestsDb";

    private readonly IInterceptor? _interceptor;

    private readonly TimeProvider? _timeProvider;
    public FitnessPlatformWebApplicationFactory()
    {
    }

    public FitnessPlatformWebApplicationFactory(
        IInterceptor interceptor)
    {
        _interceptor = interceptor;
    }

    public FitnessPlatformWebApplicationFactory(
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
    }

    public FitnessPlatformWebApplicationFactory(
        IInterceptor interceptor,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _interceptor = interceptor;
        _timeProvider = timeProvider;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting(
      "IntegrationTesting:Enabled",
      "true");

        builder.UseSetting(
            "ConnectionStrings:FitnessPlatformDb",
            "Server=(localdb)\\MSSQLLocalDB;" +
            "Database=FitnessPlatformIntegrationTestsDb;" +
            "Trusted_Connection=True;" +
            "TrustServerCertificate=True;");

        builder.UseSetting(
            "Jwt:Issuer",
            "FitnessPlatform.IntegrationTests");

        builder.UseSetting(
            "Jwt:Audience",
            "FitnessPlatform.IntegrationTests");
        builder.UseSetting("Jwt:ExpirationMinutes", "60");

        builder.UseSetting(
            "Jwt:Key",
            Convert.ToBase64String(
                Enumerable.Repeat((byte)42, 64).ToArray()));

        builder.ConfigureServices(services =>
        {
            // Override server time only when explicitly requested
            // by an integration test.
            if (_timeProvider is not null)
            {
                services.RemoveAll<TimeProvider>();

                services.AddSingleton<TimeProvider>(
                    _timeProvider);
            }

            services.RemoveAll<DbContextOptions<FitnessPlatformDbContext>>();
            services.RemoveAll<FitnessPlatformDbContext>();
            var connectionString =
                "Server=(localdb)\\MSSQLLocalDB;" +
                $"Database={TestDatabaseName};" +
                "Trusted_Connection=True;" +
                "TrustServerCertificate=True;";

            var databaseName =
                new SqlConnectionStringBuilder(connectionString)
                    .InitialCatalog;

            if (!string.Equals(
                    databaseName,
                    TestDatabaseName,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Integration tests must use the isolated test database.");
            }

            services.AddDbContext<FitnessPlatformDbContext>(options =>
            {
                options.UseSqlServer(connectionString);

                if (_interceptor is not null)
                {
                    options.AddInterceptors(_interceptor);
                }
            });

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<FitnessPlatformDbContext>();

            db.Database.Migrate();
        });
    }
}