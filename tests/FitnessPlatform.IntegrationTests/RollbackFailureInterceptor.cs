using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FitnessPlatform.IntegrationTests;

public sealed class RollbackFailureInterceptor : SaveChangesInterceptor
{
    private Func<FitnessPlatformDbContext, bool>? _failureCondition;

    private int _triggerCount;

    public int TriggerCount => Volatile.Read(ref _triggerCount);

    public void Arm(
        Func<FitnessPlatformDbContext, bool> failureCondition)
    {
        ArgumentNullException.ThrowIfNull(failureCondition);

        Interlocked.Exchange(ref _triggerCount, 0);

        _failureCondition = failureCondition;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is FitnessPlatformDbContext db &&
            db.Database.CurrentTransaction is not null &&
            result > 0 &&
            _failureCondition is not null &&
            _failureCondition(db))
        {
            if (Interlocked.CompareExchange(
                    ref _triggerCount, 1, 0) == 0)
            {
                throw new DbUpdateConcurrencyException(
                    "Integration test: simulated failure after database save and before transaction commit.");
            }
        }

        return base.SavedChangesAsync(
            eventData,
            result,
            cancellationToken);
    }
}