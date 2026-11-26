using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionCreationIntegrationTests
{
    [Theory]
    [InlineData("Identifiers")]
    [InlineData("PlanSnapshot")]
    [InlineData("DaySnapshot")]
    [InlineData("StrengthSnapshot")]
    [InlineData("CardioSnapshot")]
    [InlineData("ExerciseOrdering")]
    [InlineData("SessionDefaults")]
    [InlineData("ExerciseDefaults")]
    [InlineData("RowVersion")]
    [InlineData("Progress")]
    [InlineData("Persistence")]
    [InlineData("Notes")]
    public async Task StartWorkout_CreatesCorrectSession(
        string scenario)
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client = WorkoutSessionTestData.CreateClient(factory);

        var fixture = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client);

        var result = await WorkoutSessionExecutionTestData
            .StartAsync(client, fixture, "  Test workout  ");

        var session = result.Session;

        switch (scenario)
        {
            case "Identifiers":
                Assert.Equal(fixture.Member.ProfileId,
                    session.UserProfileId);
                Assert.Equal(fixture.AssignmentId,
                    session.MemberWorkoutPlanAssignmentId);
                Assert.Equal(fixture.Plan.PlanId,
                    session.WorkoutPlanId);
                Assert.Equal(fixture.Plan.TrainingDayId,
                    session.WorkoutPlanDayId);
                break;

            case "PlanSnapshot":
                Assert.Equal(fixture.Plan.PlanName,
                    session.WorkoutPlanName);
                Assert.Equal(fixture.Plan.VersionNumber,
                    session.WorkoutPlanVersionNumber);
                break;

            case "DaySnapshot":
                Assert.Equal("Strength and Cardio",
                    session.WorkoutDayName);
                Assert.Equal(1, session.WorkoutDayNumber);
                break;

            case "StrengthSnapshot":
                var strength = Assert.Single(result.Exercises,
                    x => x.ExerciseId == fixture.Plan.StrengthExerciseId);

                Assert.Equal(fixture.Plan.StrengthPlanExerciseId,
                    strength.WorkoutPlanExerciseId);
                Assert.Equal("Integration Test Bench Press",
                    strength.ExerciseName);
                Assert.Equal("Chest", strength.MuscleGroup);
                Assert.Equal("Barbell", strength.Equipment);
                Assert.Equal("Perform controlled repetitions.",
                    strength.ExerciseInstructions);
                Assert.Equal(3, strength.PlannedSets);
                Assert.Equal(12, strength.PlannedReps);
                Assert.Equal(60, strength.PlannedRestSeconds);
                Assert.Null(strength.PlannedDurationSeconds);
                Assert.Equal("Maintain proper form.",
                    strength.PlannedNotes);
                break;

            case "CardioSnapshot":
                var cardio = Assert.Single(result.Exercises,
                    x => x.ExerciseId == fixture.Plan.CardioExerciseId);

                Assert.Equal(fixture.Plan.CardioPlanExerciseId,
                    cardio.WorkoutPlanExerciseId);
                Assert.Equal("Integration Test Treadmill Running",
                    cardio.ExerciseName);
                Assert.Equal("Cardio", cardio.MuscleGroup);
                Assert.Equal("Treadmill", cardio.Equipment);
                Assert.Equal(1, cardio.PlannedSets);
                Assert.Equal(0, cardio.PlannedReps);
                Assert.Equal(600, cardio.PlannedDurationSeconds);
                break;

            case "ExerciseOrdering":
                Assert.Equal(new[] { 1, 2 },
                    result.Exercises.Select(x => x.ExerciseOrder));
                break;

            case "SessionDefaults":
                Assert.Equal("InProgress", session.Status);
                Assert.Null(session.CompletedAtUtc);
                Assert.Null(session.CancelledAtUtc);
                Assert.Null(session.PausedAtUtc);
                Assert.Null(session.TotalDurationSeconds);
                Assert.Equal(0, session.TotalPausedSeconds);
                Assert.Equal(
                    WorkoutSessionTestData.DefaultUtcNow.UtcDateTime,
                    session.StartedAtUtc);
                Assert.Equal(session.StartedAtUtc,
                    session.CreatedAtUtc);
                Assert.Equal(session.StartedAtUtc,
                    session.UpdatedAtUtc);
                break;

            case "ExerciseDefaults":
                Assert.Equal(2, result.Exercises.Count);

                Assert.All(result.Exercises, exercise =>
                {
                    Assert.Equal("Pending", exercise.Status);
                    Assert.Null(exercise.StartedAtUtc);
                    Assert.Null(exercise.CompletedAtUtc);
                    Assert.Null(exercise.SkippedAtUtc);
                    Assert.Null(exercise.Notes);
                    Assert.Null(exercise.SkipReason);
                    Assert.Empty(exercise.Sets);
                    Assert.Equal(session.Id,
                        exercise.WorkoutSessionId);
                });
                break;

            case "RowVersion":
                Assert.Equal(8,
                    Convert.FromBase64String(session.RowVersion).Length);

                var persisted = await WorkoutSessionExecutionTestData
                    .LoadAsync(factory, session.Id);

                Assert.Equal(session.RowVersion,
                    Convert.ToBase64String(persisted.RowVersion));
                break;

            case "Progress":
                Assert.Equal(2, session.TotalExercises);
                Assert.Equal(2, session.PendingExercises);
                Assert.Equal(0, session.InProgressExercises);
                Assert.Equal(0, session.CompletedExercises);
                Assert.Equal(0, session.SkippedExercises);
                Assert.Equal(4, session.TotalPlannedSets);
                Assert.Equal(0, session.TotalRecordedSets);
                Assert.Equal(0, session.CompletedSets);
                Assert.Equal(0, session.SkippedSets);
                Assert.Equal(0m, session.ProgressPercentage);

                Assert.All(result.Exercises, exercise =>
                {
                    Assert.Equal(0L, exercise.TotalActualReps);
                    Assert.Equal(0m, exercise.TotalVolumeKg);
                    Assert.Equal(0L,
                        exercise.TotalActualDurationSeconds);
                    Assert.Equal(0m,
                        exercise.TotalActualDistanceMeters);
                });
                break;

            case "Persistence":
                var saved = await WorkoutSessionExecutionTestData
                    .LoadAsync(factory, session.Id);

                Assert.Equal(session.UserProfileId,
                    saved.UserProfileId);
                Assert.Equal(session.WorkoutPlanId,
                    saved.WorkoutPlanId);
                Assert.Equal(session.WorkoutDate,
                    saved.WorkoutDate);
                Assert.Equal(2, saved.Exercises.Count);

                Assert.Equal(
                    result.Exercises.Select(x => x.Id).OrderBy(x => x),
                    saved.Exercises.Select(x => x.Id).OrderBy(x => x));

                Assert.All(saved.Exercises,
                    x => Assert.Empty(x.Sets));
                break;

            case "Notes":
                Assert.Equal("Test workout", session.Notes);

                var stored = await WorkoutSessionExecutionTestData
                    .LoadAsync(factory, session.Id);

                Assert.Equal("Test workout", stored.Notes);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scenario));
        }

        Assert.Equal(1, await WorkoutSessionTestData
            .CountSessionsAsync(factory, fixture.Member.ProfileId));
    }
}