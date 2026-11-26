using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionSnapshotIntegrationTests
{
    [Theory]
    [InlineData("PlanName")]
    [InlineData("DayName")]
    [InlineData("ExerciseName")]
    [InlineData("ExerciseInstructions")]
    [InlineData("PlannedParameters")]
    [InlineData("AssignmentDeactivation")]
    [InlineData("HistoricalVersion")]
    [InlineData("RetiredExercise")]
    public async Task SourceDataChanges_DoNotModifySessionSnapshot(
        string scenario)
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client = WorkoutSessionTestData.CreateClient(factory);

        var fixture = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client);

        var started = await WorkoutSessionExecutionTestData
            .StartAsync(client, fixture);

        await WorkoutSessionExecutionTestData.SetStatusAsync(
            factory, started.Session.Id, "Completed");

        var before = await WorkoutSessionExecutionTestData
            .GetAsync(client, started.Session.Id);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<FitnessPlatformDbContext>();

            switch (scenario)
            {
                case "PlanName":
                    var plan = await db.WorkoutPlans.SingleAsync(
                        x => x.Id == fixture.Plan.PlanId);

                    plan.Name = "Changed workout plan";
                    break;

                case "DayName":
                    var day = await db.WorkoutPlanDays.SingleAsync(
                        x => x.Id == fixture.Plan.TrainingDayId);

                    day.Name = "Changed workout day";
                    break;

                case "ExerciseName":
                    var exercise = await db.Exercises.SingleAsync(
                        x => x.Id == fixture.Plan.StrengthExerciseId);

                    exercise.Name = "Changed exercise";
                    break;

                case "ExerciseInstructions":
                    var master = await db.Exercises.SingleAsync(
                        x => x.Id == fixture.Plan.StrengthExerciseId);

                    master.Instructions = "Changed instructions";
                    break;

                case "PlannedParameters":
                    var planned = await db.WorkoutPlanExercises
                        .SingleAsync(x =>
                            x.Id == fixture.Plan.StrengthPlanExerciseId);

                    planned.Sets = 5;
                    planned.Reps = 20;
                    planned.RestSeconds = 120;
                    planned.Notes = "Changed planned notes";
                    break;

                case "AssignmentDeactivation":
                    var assignment = await db
                        .MemberWorkoutPlanAssignments
                        .SingleAsync(x => x.Id == fixture.AssignmentId);

                    assignment.Status = "Inactive";
                    assignment.DeactivatedAtUtc =
                        WorkoutSessionTestData.DefaultUtcNow.UtcDateTime;
                    break;

                case "HistoricalVersion":
                    var historical = await db.WorkoutPlans
                        .SingleAsync(x => x.Id == fixture.Plan.PlanId);

                    historical.IsLatestVersion = false;
                    historical.IsActive = false;
                    break;

                case "RetiredExercise":
                    var retired = await db.Exercises.SingleAsync(
                        x => x.Id == fixture.Plan.StrengthExerciseId);

                    retired.IsActive = false;
                    retired.ReviewStatus = "Retired";
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(scenario));
            }

            Assert.True(await db.SaveChangesAsync() > 0);
        }

        var after = await WorkoutSessionExecutionTestData
            .GetAsync(client, started.Session.Id);

        Assert.Equal(
            JsonSerializer.Serialize(before),
            JsonSerializer.Serialize(after));

        Assert.Equal("Completed", after.Session.Status);
        Assert.Equal(fixture.Plan.PlanId, after.Session.WorkoutPlanId);
        Assert.Equal(fixture.Plan.VersionNumber,
            after.Session.WorkoutPlanVersionNumber);
    }
}