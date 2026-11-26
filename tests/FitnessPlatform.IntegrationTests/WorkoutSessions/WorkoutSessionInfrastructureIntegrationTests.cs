using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionInfrastructureIntegrationTests
{
    private static readonly DateTimeOffset TestUtcNow =
        new(2026, 11, 26, 21, 30, 0, TimeSpan.Zero);

    [Fact]
    public void TestTimeProvider_ReturnsConfiguredUtcTime()
    {
        // Arrange
        var timeProvider =
            new WorkoutSessionTestTimeProvider(TestUtcNow);

        // Act
        var result = timeProvider.GetUtcNow();

        // Assert
        Assert.Equal(TestUtcNow, result);
        Assert.Equal(TimeSpan.Zero, result.Offset);
    }

    [Fact]
    public void TestTimeProvider_NormalizesNonUtcTimestamp()
    {
        // Arrange
        var dubaiTime = new DateTimeOffset(
            2026,
            11,
            27,
            1,
            30,
            0,
            TimeSpan.FromHours(4));

        var timeProvider =
            new WorkoutSessionTestTimeProvider(dubaiTime);

        // Act
        var result = timeProvider.GetUtcNow();

        // Assert
        Assert.Equal(TestUtcNow, result);
        Assert.Equal(TimeSpan.Zero, result.Offset);
    }

    [Fact]
    public void DefaultFactory_UsesSystemTimeProvider()
    {
        // Arrange
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        // Act
        var timeProvider = factory.Services
            .GetRequiredService<TimeProvider>();

        // Assert
        Assert.Same(TimeProvider.System, timeProvider);
    }

    [Fact]
    public void Factory_WithCustomTimeProvider_UsesConfiguredProvider()
    {
        // Arrange
        var testTimeProvider =
            new WorkoutSessionTestTimeProvider(TestUtcNow);

        using var factory =
            new FitnessPlatformWebApplicationFactory(
                testTimeProvider);

        // Act
        var timeProvider = factory.Services
            .GetRequiredService<TimeProvider>();

        // Assert
        Assert.Same(testTimeProvider, timeProvider);

        Assert.Equal(
            TestUtcNow,
            timeProvider.GetUtcNow());
    }

    [Fact]
    public void Factory_WithInterceptorAndTimeProvider_UsesConfiguredServices()
    {
        // Arrange
        var interceptor =
            new RollbackFailureInterceptor();

        var testTimeProvider =
            new WorkoutSessionTestTimeProvider(TestUtcNow);

        using var factory =
            new FitnessPlatformWebApplicationFactory(
                interceptor,
                testTimeProvider);

        // Act
        var timeProvider = factory.Services
            .GetRequiredService<TimeProvider>();

        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var connection =
            db.Database.GetDbConnection();

        // Assert
        Assert.Same(testTimeProvider, timeProvider);

        Assert.Equal(
            TestUtcNow,
            timeProvider.GetUtcNow());

        Assert.Equal(
            "FitnessPlatformIntegrationTestsDb",
            connection.Database);

        Assert.True(db.Database.IsSqlServer());

        Assert.Equal(0, interceptor.TriggerCount);
    }

    [Fact]
    public async Task Factory_UsesIsolatedSqlServerDatabase()
    {
        // Arrange
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var connection =
            db.Database.GetDbConnection();

        // Act
        var canConnect =
            await db.Database.CanConnectAsync();

        // Assert
        Assert.True(canConnect);

        Assert.True(db.Database.IsSqlServer());

        Assert.Equal(
            "FitnessPlatformIntegrationTestsDb",
            connection.Database);

        Assert.Contains(
            "(localdb)",
            connection.DataSource,
            StringComparison.OrdinalIgnoreCase);
    }
}