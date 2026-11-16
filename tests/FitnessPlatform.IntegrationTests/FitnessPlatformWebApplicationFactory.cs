using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Data.SqlClient;

namespace FitnessPlatform.IntegrationTests;

public class FitnessPlatformWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private const string TestDatabaseName =
        "FitnessPlatformIntegrationTestsDb";

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
                options.UseSqlServer(connectionString));
        });
    }
}