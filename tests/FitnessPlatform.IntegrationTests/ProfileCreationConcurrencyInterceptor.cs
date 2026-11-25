using FitnessPlatform.Domain.Entities;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FitnessPlatform.IntegrationTests;

public sealed class ProfileCreationConcurrencyInterceptor
    : SaveChangesInterceptor
{
    private readonly Guid _targetUserId;

    private readonly TaskCompletionSource<bool> _bothRequestsReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TimeSpan _timeout =
        TimeSpan.FromSeconds(30);

    private int _arrivalCount;

    public int ArrivalCount =>
        Volatile.Read(ref _arrivalCount);

    public ProfileCreationConcurrencyInterceptor(
        Guid targetUserId)
    {
        if (targetUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Target user ID cannot be empty.",
                nameof(targetUserId));
        }

        _targetUserId = targetUserId;
    }

    public override async ValueTask<InterceptionResult<int>>
        SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not FitnessPlatformDbContext db)
        {
            return result;
        }

        var containsTargetProfile = db.ChangeTracker
            .Entries<UserProfile>()
            .Any(entry =>
                entry.State == EntityState.Added &&
                entry.Entity.UserId == _targetUserId);

        if (!containsTargetProfile)
        {
            return result;
        }

        var arrivalCount =
            Interlocked.Increment(ref _arrivalCount);

        if (arrivalCount == 2)
        {
            _bothRequestsReady.TrySetResult(true);
        }
        else if (arrivalCount > 2)
        {
            throw new InvalidOperationException(
                "More than two target profile creation operations " +
                "reached the concurrency interceptor.");
        }

        await _bothRequestsReady.Task.WaitAsync(
            _timeout,
            cancellationToken);

        return result;
    }
}