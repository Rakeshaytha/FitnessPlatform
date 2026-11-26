namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionTestTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    public WorkoutSessionTestTimeProvider(
        DateTimeOffset utcNow)
    {
        _utcNow = utcNow.ToUniversalTime();
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }
}