using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessPlatform.IntegrationTests;

public class DatabaseIsolationTests
{
    [Fact]
    public void DbContext_Uses_Isolated_Test_Database()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var databaseName = db.Database.GetDbConnection().Database;

        Assert.Equal(
            "FitnessPlatformIntegrationTestsDb",
            databaseName);
    }
}