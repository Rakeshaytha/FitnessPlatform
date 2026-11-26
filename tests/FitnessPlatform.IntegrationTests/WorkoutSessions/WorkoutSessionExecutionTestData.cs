using FitnessPlatform.Contracts.WorkoutSessions;
using FitnessPlatform.Domain.Entities;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

internal static class WorkoutSessionExecutionTestData
{
    public const string Route =
        WorkoutSessionTestData.RoutePrefix;

    public static async Task<WorkoutSessionDetailResponse> StartAsync(
        HttpClient client,
        WorkoutSessionTestData.AssignedMember fixture,
        string? notes = null)
    {
        using var response = await client.PostAsJsonAsync(
            $"{Route}/start",
            WorkoutSessionTestData.CreateStartRequest(
                fixture,
                notes: notes));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<WorkoutSessionDetailResponse>();

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Session.Id);

        Assert.Equal(
            $"{Route}/{result.Session.Id}",
            response.Headers.Location?.OriginalString);

        return result;
    }

    public static async Task<WorkoutSessionDetailResponse> GetAsync(
        HttpClient client,
        Guid sessionId)
    {
        using var response = await client.GetAsync(
            $"{Route}/{sessionId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<WorkoutSessionDetailResponse>();

        Assert.NotNull(result);

        return result;
    }

    public static async Task<WorkoutSessionDetailResponse> GetActiveAsync(
        HttpClient client)
    {
        using var response = await client.GetAsync(
            $"{Route}/active");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<WorkoutSessionDetailResponse>();

        Assert.NotNull(result);

        return result;
    }

    public static async Task<WorkoutSession> LoadAsync(
        FitnessPlatformWebApplicationFactory factory,
        Guid sessionId)
    {
        var session = await WorkoutSessionTestData
            .GetPersistedSessionAsync(factory, sessionId);

        Assert.NotNull(session);

        return session;
    }

    public static async Task UpdateAsync(
        FitnessPlatformWebApplicationFactory factory,
        Guid sessionId,
        Action<WorkoutSession> update)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(update);

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        // Load the existing session as a tracked entity graph.
        var session = await db.WorkoutSessions
            .AsTracking()
            .AsSingleQuery()
            .Include(x => x.Exercises)
            .ThenInclude(x => x.Sets)
            .SingleAsync(x => x.Id == sessionId);

        // Capture the identifiers of sets already persisted.
        var existingSetIds = session.Exercises
            .SelectMany(x => x.Sets)
            .Select(x => x.Id)
            .ToHashSet();

        // Apply the requested fixture changes.
        update(session);

        // Explicitly register newly created sets as Added.
        foreach (var exercise in session.Exercises)
        {
            foreach (var set in exercise.Sets)
            {
                if (existingSetIds.Contains(set.Id))
                {
                    continue;
                }

                set.WorkoutSessionExerciseId = exercise.Id;
                set.WorkoutSessionExercise = exercise;

                db.Entry(set).State = EntityState.Added;
            }
        }

        // Detect modifications to existing tracked entities.
        db.ChangeTracker.DetectChanges();

        // Verify that newly created sets will be inserted.
        foreach (var set in session.Exercises.SelectMany(x => x.Sets))
        {
            if (!existingSetIds.Contains(set.Id))
            {
                Assert.Equal(
                    EntityState.Added,
                    db.Entry(set).State);
            }
        }

        // Persist all fixture changes atomically.
        await db.SaveChangesAsync();
    }

    public static async Task SetStatusAsync(
        FitnessPlatformWebApplicationFactory factory,
        Guid sessionId,
        string status)
    {
        await UpdateAsync(factory, sessionId, session =>
        {
            var timestamp = session.StartedAtUtc.AddMinutes(10);

            session.Status = status;
            session.PausedAtUtc = null;
            session.CompletedAtUtc = null;
            session.CancelledAtUtc = null;
            session.TotalDurationSeconds = null;
            session.UpdatedAtUtc = timestamp;

            switch (status)
            {
                case "InProgress":
                    break;

                case "Paused":
                    session.PausedAtUtc = timestamp;
                    break;

                case "Completed":
                    session.CompletedAtUtc = timestamp;
                    session.TotalDurationSeconds = 600;

                    var first = session.Exercises
                        .OrderBy(x => x.ExerciseOrder)
                        .First();

                    first.Status = "Completed";
                    first.StartedAtUtc = session.StartedAtUtc;
                    first.CompletedAtUtc = timestamp;
                    first.UpdatedAtUtc = timestamp;

                    if (!first.Sets.Any())
                    {
                        first.Sets.Add(new WorkoutSessionSet
                        {
                            Id = Guid.NewGuid(),
                            WorkoutSessionExerciseId = first.Id,
                            SetNumber = 1,
                            SetType = "Working",
                            ActualReps = 10,
                            ActualWeightKg = 40m,
                            Status = "Completed",
                            CompletedAtUtc = timestamp,
                            CreatedAtUtc = session.StartedAtUtc,
                            UpdatedAtUtc = timestamp
                        });
                    }

                    foreach (var exercise in session.Exercises
                        .Where(x => x.Id != first.Id))
                    {
                        exercise.Status = "Skipped";
                        exercise.CompletedAtUtc = null;
                        exercise.SkippedAtUtc = timestamp;
                        exercise.SkipReason = "Test fixture";
                        exercise.UpdatedAtUtc = timestamp;
                    }

                    break;

                case "Cancelled":
                    session.CancelledAtUtc = timestamp;
                    session.TotalDurationSeconds = 600;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(status));
            }
        });
    }

    public static async Task AddPerformanceAsync(
        FitnessPlatformWebApplicationFactory factory,
        Guid sessionId)
    {
        await UpdateAsync(factory, sessionId, session =>
        {
            var exercise = session.Exercises
                .OrderBy(x => x.ExerciseOrder)
                .First();

            var timestamp = session.StartedAtUtc.AddMinutes(5);

            exercise.Status = "Completed";
            exercise.StartedAtUtc = session.StartedAtUtc;
            exercise.CompletedAtUtc = timestamp;
            exercise.UpdatedAtUtc = timestamp;

            exercise.Sets.Add(new WorkoutSessionSet
            {
                Id = Guid.NewGuid(),
                WorkoutSessionExerciseId = exercise.Id,
                SetNumber = 2,
                SetType = "Working",
                ActualReps = 8,
                ActualWeightKg = 50m,
                Status = "Completed",
                CompletedAtUtc = timestamp,
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            });

            exercise.Sets.Add(new WorkoutSessionSet
            {
                Id = Guid.NewGuid(),
                WorkoutSessionExerciseId = exercise.Id,
                SetNumber = 1,
                SetType = "Working",
                ActualReps = 10,
                ActualWeightKg = 40m,
                Status = "Completed",
                CompletedAtUtc = timestamp,
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            });

            session.UpdatedAtUtc = timestamp;
        });
    }

    public static async Task DeactivateAssignmentAsync(
        FitnessPlatformWebApplicationFactory factory,
        Guid assignmentId)
    {
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var assignment = await db.MemberWorkoutPlanAssignments
            .SingleAsync(x => x.Id == assignmentId);

        assignment.Status = "Inactive";
        assignment.DeactivatedAtUtc =
            WorkoutSessionTestData.DefaultUtcNow.UtcDateTime;

        assignment.UpdatedAtUtc =
            WorkoutSessionTestData.DefaultUtcNow.UtcDateTime;

        await db.SaveChangesAsync();
    }
}