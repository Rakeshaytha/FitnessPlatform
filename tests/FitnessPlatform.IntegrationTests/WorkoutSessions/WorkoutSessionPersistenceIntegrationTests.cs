using FitnessPlatform.Domain.Entities;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionPersistenceIntegrationTests
{
    [Theory]
    [InlineData("InProgress", "InProgress")]
    [InlineData("InProgress", "Paused")]
    [InlineData("Paused", "InProgress")]
    [InlineData("Paused", "Paused")]
    public async Task Database_RejectsDuplicateUnfinishedSession(
        string existingStatus,
        string duplicateStatus)
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client = WorkoutSessionTestData.CreateClient(factory);

        var fixture = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client);

        var first = await WorkoutSessionExecutionTestData
            .StartAsync(client, fixture);

        await WorkoutSessionExecutionTestData.SetStatusAsync(
            factory, first.Session.Id, existingStatus);

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var duplicate = new WorkoutSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = fixture.Member.ProfileId,
            MemberWorkoutPlanAssignmentId = fixture.AssignmentId,
            WorkoutPlanId = fixture.Plan.PlanId,
            WorkoutPlanDayId = fixture.Plan.TrainingDayId,
            WorkoutPlanName = first.Session.WorkoutPlanName,
            WorkoutPlanVersionNumber = first.Session.WorkoutPlanVersionNumber,
            WorkoutDayName = first.Session.WorkoutDayName,
            WorkoutDayNumber = first.Session.WorkoutDayNumber,
            WorkoutDate = first.Session.WorkoutDate,
            TimeZoneId = first.Session.TimeZoneId,
            Status = duplicateStatus,
            StartedAtUtc = first.Session.StartedAtUtc,
            PausedAtUtc = duplicateStatus == "Paused"
                ? first.Session.StartedAtUtc.AddMinutes(1)
                : null,
            CreatedAtUtc = first.Session.CreatedAtUtc,
            UpdatedAtUtc = first.Session.UpdatedAtUtc
        };

        db.WorkoutSessions.Add(duplicate);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync());

        var sql = Assert.IsType<SqlException>(
            exception.InnerException);

        Assert.Contains(sql.Errors.Cast<SqlError>(),
            x => x.Number is 2601 or 2627);

        Assert.Contains(
            "UX_WorkoutSessions_UnfinishedUserProfile",
            sql.Message);

        Assert.Equal(1, await WorkoutSessionTestData
            .CountSessionsAsync(factory, fixture.Member.ProfileId));
    }

    [Theory]
    [InlineData("InvalidStatus")]
    [InlineData("InvalidLifecycle")]
    [InlineData("DuplicateExerciseOrder")]
    [InlineData("DuplicatePlanExercise")]
    public async Task Database_RejectsInvalidWorkoutData(
        string scenario)
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client = WorkoutSessionTestData.CreateClient(factory);

        var fixture = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client);

        var result = await WorkoutSessionExecutionTestData
            .StartAsync(client, fixture);

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var session = await db.WorkoutSessions
            .Include(x => x.Exercises)
            .SingleAsync(x => x.Id == result.Session.Id);

        var exercises = session.Exercises
            .OrderBy(x => x.ExerciseOrder)
            .ToArray();

        switch (scenario)
        {
            case "InvalidStatus":
                session.Status = "Unknown";
                break;

            case "InvalidLifecycle":
                session.Status = "Paused";
                session.PausedAtUtc = null;
                break;

            case "DuplicateExerciseOrder":
                exercises[1].ExerciseOrder =
                    exercises[0].ExerciseOrder;
                break;

            case "DuplicatePlanExercise":
                exercises[1].WorkoutPlanExerciseId =
                    exercises[0].WorkoutPlanExerciseId;
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scenario));
        }

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync());

        var sql = Assert.IsType<SqlException>(
            exception.InnerException);

        if (scenario.StartsWith("Duplicate"))
        {
            Assert.Contains(sql.Errors.Cast<SqlError>(),
                x => x.Number is 2601 or 2627);
        }
        else
        {
            Assert.Contains(sql.Errors.Cast<SqlError>(),
                x => x.Number == 547);
        }

        var saved = await WorkoutSessionExecutionTestData
            .LoadAsync(factory, result.Session.Id);

        Assert.Equal("InProgress", saved.Status);
        Assert.Null(saved.PausedAtUtc);
        Assert.Equal(2, saved.Exercises.Count);
        Assert.Equal(2, saved.Exercises
            .Select(x => x.ExerciseOrder).Distinct().Count());
        Assert.Equal(2, saved.Exercises
            .Select(x => x.WorkoutPlanExerciseId).Distinct().Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureAfterSave_RollsBackCompleteSession(
        bool retry)
    {
        var interceptor = new WorkoutStartFailureInterceptor();

        using var factory = new FitnessPlatformWebApplicationFactory(
            interceptor,
            new WorkoutSessionTestTimeProvider(
                WorkoutSessionTestData.DefaultUtcNow));

        using var client = WorkoutSessionTestData.CreateClient(factory);

        var fixture = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client);

        interceptor.Arm(fixture.Member.ProfileId);

        using var response = await client.PostAsJsonAsync(
            $"{WorkoutSessionTestData.RoutePrefix}/start",
            WorkoutSessionTestData.CreateStartRequest(fixture));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, interceptor.TriggerCount);
        Assert.NotEqual(Guid.Empty, interceptor.SessionId);

        Assert.Equal(0, await WorkoutSessionTestData
            .CountSessionsAsync(factory, fixture.Member.ProfileId));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<FitnessPlatformDbContext>();

            Assert.False(await db.WorkoutSessions.AnyAsync(
                x => x.Id == interceptor.SessionId));

            Assert.False(await db.WorkoutSessionExercises.AnyAsync(
                x => x.WorkoutSessionId == interceptor.SessionId));
        }

        if (retry)
        {
            var result = await WorkoutSessionExecutionTestData
                .StartAsync(client, fixture);

            Assert.NotEqual(interceptor.SessionId, result.Session.Id);

            Assert.Equal(1, await WorkoutSessionTestData
                .CountSessionsAsync(
                    factory, fixture.Member.ProfileId));

            Assert.Equal(2, result.Exercises.Count);
        }
    }

    private sealed class WorkoutStartFailureInterceptor
        : SaveChangesInterceptor
    {
        private Guid _profileId;
        private int _triggerCount;

        public int TriggerCount => Volatile.Read(ref _triggerCount);

        public Guid SessionId { get; private set; }

        public void Arm(Guid profileId)
        {
            _profileId = profileId;
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is FitnessPlatformDbContext db
                && db.Database.CurrentTransaction is not null
                && result > 0
                && _profileId != Guid.Empty)
            {
                var session = db.ChangeTracker
                    .Entries<WorkoutSession>()
                    .Select(x => x.Entity)
                    .FirstOrDefault(
                        x => x.UserProfileId == _profileId);

                if (session is not null
                    && Interlocked.CompareExchange(
                        ref _triggerCount, 1, 0) == 0)
                {
                    SessionId = session.Id;

                    throw new DbUpdateConcurrencyException(
                        "Simulated workout persistence failure.");
                }
            }

            return base.SavedChangesAsync(
                eventData, result, cancellationToken);
        }
    }
}