using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FitnessPlatform.Contracts.WorkoutSessions;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionDayValidationIntegrationTests
{
    private const string StartEndpoint =
        WorkoutSessionTestData.RoutePrefix + "/start";

    // ============================================================
    // 1. VALID TRAINING DAY
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithValidTrainingDay_CreatesSession()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client);

        var result =
            await StartTrainingDayAsync(
                factory,
                client,
                fixture);

        Assert.Equal(
            fixture.Plan.TrainingDayId,
            result.Session.WorkoutPlanDayId);

        Assert.Equal(
            "Strength and Cardio",
            result.Session.WorkoutDayName);

        Assert.Equal(
            1,
            result.Session.WorkoutDayNumber);

        Assert.Equal(
            2,
            result.Session.TotalExercises);

        Assert.Equal(
            2,
            result.Session.PendingExercises);

        Assert.Equal(
            4,
            result.Session.TotalPlannedSets);

        Assert.Equal(
            0,
            result.Session.TotalRecordedSets);

        Assert.Equal(
            0m,
            result.Session.ProgressPercentage);

        Assert.All(
            result.Exercises,
            exercise =>
            {
                Assert.Equal(
                    "Pending",
                    exercise.Status);

                Assert.Empty(
                    exercise.Sets);
            });
    }

    // ============================================================
    // 2. REST DAY
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithRestDay_ReturnsConflict()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client);

        var request =
            WorkoutSessionTestData.CreateStartRequest(
                fixture,
                workoutDayId: fixture.Plan.RestDayId);

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertRestDayConflictAsync(response);

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // 3. TRAINING DAY WITHOUT EXERCISES
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithEmptyTrainingDay_ReturnsConflict()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client,
                includeEmptyTrainingDay: true);

        Assert.True(
            fixture.Plan.EmptyTrainingDayId.HasValue);

        var request =
            WorkoutSessionTestData.CreateStartRequest(
                fixture,
                workoutDayId:
                    fixture.Plan.EmptyTrainingDayId.Value);

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertEmptyDayConflictAsync(response);

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // 4. NONEXISTENT WORKOUT DAY
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithNonexistentDay_ReturnsNotFound()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client);

        var request =
            WorkoutSessionTestData.CreateStartRequest(
                fixture,
                workoutDayId: Guid.NewGuid());

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertDayNotFoundAsync(response);

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // 5 AND 6. TRAINING OR REST DAY FROM ANOTHER PLAN
    // ============================================================

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartWorkout_WithDayFromAnotherPlan_ReturnsNotFound(
        bool useRestDay)
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client);

        var anotherPlan =
            await WorkoutSessionTestData.CreateWorkoutPlanAsync(
                factory);

        var foreignDayId =
            useRestDay
                ? anotherPlan.RestDayId
                : anotherPlan.TrainingDayId;

        var request =
            WorkoutSessionTestData.CreateStartRequest(
                fixture,
                workoutDayId: foreignDayId);

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertDayNotFoundAsync(response);

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // 7. ASSIGNMENT VALIDATION BEFORE DAY VALIDATION
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithInvalidAssignmentAndRestDay_ReturnsAssignmentNotFound()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client);

        var request =
            WorkoutSessionTestData.CreateStartRequest(
                fixture,
                workoutDayId: fixture.Plan.RestDayId);

        request.MemberWorkoutPlanAssignmentId =
            Guid.NewGuid();

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertErrorAsync(
            response,
            HttpStatusCode.NotFound,
            "WORKOUT_ASSIGNMENT_NOT_FOUND",
            "Workout plan assignment not found.");

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // 8. RECOVERY AFTER REJECTED REST DAY
    // ============================================================

    [Fact]
    public async Task StartWorkout_AfterRejectedRestDay_AllowsValidTrainingDay()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client);

        var invalidRequest =
            WorkoutSessionTestData.CreateStartRequest(
                fixture,
                workoutDayId: fixture.Plan.RestDayId);

        using var invalidResponse =
            await client.PostAsJsonAsync(
                StartEndpoint,
                invalidRequest);

        await AssertRestDayConflictAsync(
            invalidResponse);

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);

        var result =
            await StartTrainingDayAsync(
                factory,
                client,
                fixture);

        Assert.Equal(
            fixture.Plan.TrainingDayId,
            result.Session.WorkoutPlanDayId);
    }

    // ============================================================
    // 9. RECOVERY AFTER REJECTED EMPTY TRAINING DAY
    // ============================================================

    [Fact]
    public async Task StartWorkout_AfterRejectedEmptyDay_AllowsValidTrainingDay()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client,
                includeEmptyTrainingDay: true);

        Assert.True(
            fixture.Plan.EmptyTrainingDayId.HasValue);

        var invalidRequest =
            WorkoutSessionTestData.CreateStartRequest(
                fixture,
                workoutDayId:
                    fixture.Plan.EmptyTrainingDayId.Value);

        using var invalidResponse =
            await client.PostAsJsonAsync(
                StartEndpoint,
                invalidRequest);

        await AssertEmptyDayConflictAsync(
            invalidResponse);

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);

        var result =
            await StartTrainingDayAsync(
                factory,
                client,
                fixture);

        Assert.Equal(
            fixture.Plan.TrainingDayId,
            result.Session.WorkoutPlanDayId);
    }

    // ============================================================
    // 10. REST DAY REQUEST WITH EXISTING ACTIVE SESSION
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithExistingSessionAndRestDay_PreservesExistingSession()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client);

        var originalSession =
            await StartTrainingDayAsync(
                factory,
                client,
                fixture);

        var invalidRequest =
            WorkoutSessionTestData.CreateStartRequest(
                fixture,
                workoutDayId: fixture.Plan.RestDayId);

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                invalidRequest);

        await AssertRestDayConflictAsync(response);

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountSessionsAsync(
                factory,
                fixture.Member.ProfileId));

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountUnfinishedSessionsAsync(
                factory,
                fixture.Member.ProfileId));

        var persisted =
            await WorkoutSessionTestData.GetPersistedSessionAsync(
                factory,
                originalSession.Session.Id);

        Assert.NotNull(persisted);

        Assert.Equal(
            originalSession.Session.Id,
            persisted.Id);

        Assert.Equal(
            fixture.Member.ProfileId,
            persisted.UserProfileId);

        Assert.Equal(
            fixture.AssignmentId,
            persisted.MemberWorkoutPlanAssignmentId);

        Assert.Equal(
            fixture.Plan.TrainingDayId,
            persisted.WorkoutPlanDayId);

        Assert.Equal(
            "InProgress",
            persisted.Status);

        Assert.Equal(
            originalSession.Session.RowVersion,
            Convert.ToBase64String(
                persisted.RowVersion));

        Assert.Equal(
            originalSession.Session.UpdatedAtUtc,
            persisted.UpdatedAtUtc);

        Assert.Equal(
            2,
            persisted.Exercises.Count);

        Assert.All(
            persisted.Exercises,
            exercise =>
            {
                Assert.Equal(
                    "Pending",
                    exercise.Status);

                Assert.Empty(
                    exercise.Sets);
            });
    }

    // ============================================================
    // HELPER: START AND VERIFY VALID TRAINING DAY
    // ============================================================

    private static async Task<WorkoutSessionDetailResponse>
        StartTrainingDayAsync(
            FitnessPlatformWebApplicationFactory factory,
            HttpClient client,
            WorkoutSessionTestData.AssignedMember fixture)
    {
        var request =
            WorkoutSessionTestData.CreateStartRequest(
                fixture);

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var result =
            await response.Content
                .ReadFromJsonAsync<WorkoutSessionDetailResponse>();

        Assert.NotNull(result);

        Assert.NotEqual(
            Guid.Empty,
            result.Session.Id);

        Assert.Equal(
            fixture.Member.ProfileId,
            result.Session.UserProfileId);

        Assert.Equal(
            fixture.AssignmentId,
            result.Session.MemberWorkoutPlanAssignmentId);

        Assert.Equal(
            fixture.Plan.PlanId,
            result.Session.WorkoutPlanId);

        Assert.Equal(
            fixture.Plan.TrainingDayId,
            result.Session.WorkoutPlanDayId);

        Assert.Equal(
            "InProgress",
            result.Session.Status);

        Assert.Equal(
            2,
            result.Exercises.Count);

        var persisted =
            await WorkoutSessionTestData.GetPersistedSessionAsync(
                factory,
                result.Session.Id);

        Assert.NotNull(persisted);

        Assert.Equal(
            fixture.Plan.TrainingDayId,
            persisted.WorkoutPlanDayId);

        Assert.Equal(
            2,
            persisted.Exercises.Count);

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountSessionsAsync(
                factory,
                fixture.Member.ProfileId));

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountUnfinishedSessionsAsync(
                factory,
                fixture.Member.ProfileId));

        return result;
    }

    // ============================================================
    // HELPER: REST DAY CONFLICT
    // ============================================================

    private static Task AssertRestDayConflictAsync(
        HttpResponseMessage response)
    {
        return AssertErrorAsync(
            response,
            HttpStatusCode.Conflict,
            "WORKOUT_DAY_IS_REST_DAY",
            "A workout session cannot be started for a rest day.");
    }

    // ============================================================
    // HELPER: EMPTY TRAINING DAY CONFLICT
    // ============================================================

    private static Task AssertEmptyDayConflictAsync(
        HttpResponseMessage response)
    {
        return AssertErrorAsync(
            response,
            HttpStatusCode.Conflict,
            "WORKOUT_DAY_HAS_NO_EXERCISES",
            "The requested workout day does not contain any exercises.");
    }

    // ============================================================
    // HELPER: WORKOUT DAY NOT FOUND
    // ============================================================

    private static Task AssertDayNotFoundAsync(
        HttpResponseMessage response)
    {
        return AssertErrorAsync(
            response,
            HttpStatusCode.NotFound,
            "WORKOUT_DAY_NOT_FOUND",
            "The requested workout day was not found in your assigned workout plan.");
    }

    // ============================================================
    // HELPER: VALIDATE API ERROR RESPONSE
    // ============================================================

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        string expectedError)
    {
        Assert.Equal(
            expectedStatus,
            response.StatusCode);

        var content =
            await response.Content.ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(content);

        Assert.Equal(
            expectedCode,
            document.RootElement
                .GetProperty("code")
                .GetString());

        Assert.Equal(
            expectedError,
            document.RootElement
                .GetProperty("error")
                .GetString());
    }

    // ============================================================
    // HELPER: VERIFY NO UNWANTED SESSION CREATION
    // ============================================================

    private static async Task AssertNoSessionsAsync(
        FitnessPlatformWebApplicationFactory factory,
        Guid profileId)
    {
        Assert.Equal(
            0,
            await WorkoutSessionTestData.CountSessionsAsync(
                factory,
                profileId));

        Assert.Equal(
            0,
            await WorkoutSessionTestData.CountUnfinishedSessionsAsync(
                factory,
                profileId));
    }
}