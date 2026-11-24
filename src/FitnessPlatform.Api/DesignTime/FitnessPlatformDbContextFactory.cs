using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FitnessPlatform.Api.DesignTime;

public sealed class FitnessPlatformDbContextFactory
    : IDesignTimeDbContextFactory<FitnessPlatformDbContext>
{
    public FitnessPlatformDbContext CreateDbContext(string[] args)
    {
        // Determine the current environment.
        var environment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        // Locate the API configuration files.
        var currentDirectory = Directory.GetCurrentDirectory();

        var apiProjectDirectory = Path.Combine(
            currentDirectory,
            "src",
            "FitnessPlatform.Api");

        string basePath;

        if (File.Exists(
            Path.Combine(apiProjectDirectory, "appsettings.json")))
        {
            basePath = apiProjectDirectory;
        }
        else if (File.Exists(
            Path.Combine(currentDirectory, "appsettings.json")))
        {
            basePath = currentDirectory;
        }
        else
        {
            throw new InvalidOperationException(
                "Unable to locate FitnessPlatform API configuration files.");
        }

        // Load application configuration.
        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(
                "appsettings.json",
                optional: false,
                reloadOnChange: false)
            .AddJsonFile(
                $"appsettings.{environment}.json",
                optional: true,
                reloadOnChange: false);

        // Load development secrets.
        if (string.Equals(
            environment,
            "Development",
            StringComparison.OrdinalIgnoreCase))
        {
            configurationBuilder.AddUserSecrets<
                FitnessPlatformDbContextFactory>(
                    optional: true);
        }

        // Environment variables override configuration files.
        configurationBuilder.AddEnvironmentVariables();

        var configuration = configurationBuilder.Build();

        // Retrieve the database connection string.
        var connectionString = configuration
            .GetConnectionString("FitnessPlatformDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'FitnessPlatformDb' is missing.");
        }

        // Configure EF Core independently of API startup.
        var optionsBuilder =
            new DbContextOptionsBuilder<FitnessPlatformDbContext>();

        optionsBuilder.UseSqlServer(connectionString);

        return new FitnessPlatformDbContext(
            optionsBuilder.Options);
    }
}