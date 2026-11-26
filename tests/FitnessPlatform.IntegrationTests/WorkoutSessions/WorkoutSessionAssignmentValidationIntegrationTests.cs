using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FitnessPlatform.Contracts.WorkoutSessions;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionAssignmentValidationIntegrationTests
{
    private const string StartEndpoint =
        WorkoutSessionTestData.RoutePrefix + "/start";

    private static readonly DateOnly WorkoutDate =
        WorkoutSessionTestData.DefaultWorkoutDate;

    // ============================================================
    // 1. INVALID ASSIGNMENT STATUSES
    // ============================================================

    [Theory]
    [InlineData("Inactive")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task StartWorkout_WithNonActiveAssignment_ReturnsConflict(
        string assignmentStatus)
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client,
                startDate: WorkoutDate.AddDays(-7),
                endDate: WorkoutDate.AddDays(7),
                assignmentStatus: assignmentStatus);

        var request =
            WorkoutSessionTestData.CreateStartRequest(
                fixture);

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertConflictAsync(
            response,
            "WORKOUT_ASSIGNMENT_NOT_ACTIVE",
            "Only active workout plan assignments can be used to start a workout.");

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // 2. FUTURE ASSIGNMENT
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithFutureAssignment_ReturnsConflict()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client,
                startDate: WorkoutDate.AddDays(1),
                endDate: WorkoutDate.AddDays(30));

        var request =
            WorkoutSessionTestData.CreateStartRequest(
                fixture);

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertConflictAsync(
            response,
            "WORKOUT_ASSIGNMENT_NOT_STARTED",
            "The workout plan assignment has not started yet.");

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // 3. EXPIRED ASSIGNMENT
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithExpiredAssignment_ReturnsConflict()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client,
                startDate: WorkoutDate.AddDays(-30),
                endDate: WorkoutDate.AddDays(-1));

        var request =
            WorkoutSessionTestData.CreateStartRequest(
                fixture);

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertConflictAsync(
            response,
            "WORKOUT_ASSIGNMENT_EXPIRED",
            "The workout plan assignment has expired.");

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // 4. VALID ACTIVE ASSIGNMENT DATE RANGES
    // ============================================================

    [Theory]
    [InlineData(-7, 7)]
    [InlineData(0, 7)]
    [InlineData(-7, 0)]
    [InlineData(0, 0)]
    [InlineData(-7, null)]
    [InlineData(-30, 30)]
    public async Task StartWorkout_WithValidAssignmentDateRange_Succeeds(
        int startDateOffset,
        int? endDateOffset)
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var startDate =
            WorkoutDate.AddDays(startDateOffset);

        DateOnly? endDate =
            endDateOffset.HasValue
                ? WorkoutDate.AddDays(
                    endDateOffset.Value)
                : null;

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client,
                startDate: startDate,
                endDate: endDate,
                assignmentStatus: "Active");

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
            WorkoutDate,
            result.Session.WorkoutDate);

        Assert.Equal(
            "Asia/Dubai",
            result.Session.TimeZoneId);

        Assert.Equal(
            "InProgress",
            result.Session.Status);

        Assert.Equal(
            WorkoutSessionTestData.DefaultUtcNow.UtcDateTime,
            result.Session.StartedAtUtc);

        var persisted =
            await WorkoutSessionTestData.GetPersistedSessionAsync(
                factory,
                result.Session.Id);

        Assert.NotNull(persisted);

        Assert.Equal(
            fixture.Member.ProfileId,
            persisted.UserProfileId);

        Assert.Equal(
            fixture.AssignmentId,
            persisted.MemberWorkoutPlanAssignmentId);

        Assert.Equal(
            WorkoutDate,
            persisted.WorkoutDate);

        Assert.Equal(
            "InProgress",
            persisted.Status);

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
    }

    // ============================================================
    // 5. STATUS VALIDATION BEFORE DATE VALIDATION
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithInactiveFutureAssignment_ReturnsStatusConflict()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client,
                startDate: WorkoutDate.AddDays(1),
                endDate: WorkoutDate.AddDays(30),
                assignmentStatus: "Inactive");

        var request =
            WorkoutSessionTestData.CreateStartRequest(
                fixture);

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertConflictAsync(
            response,
            "WORKOUT_ASSIGNMENT_NOT_ACTIVE",
            "Only active workout plan assignments can be used to start a workout.");

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // HELPER: VALIDATE CONFLICT RESPONSE
    // ============================================================

    private static async Task AssertConflictAsync(
        HttpResponseMessage response,
        string expectedCode,
        string expectedError)
    {
        Assert.Equal(
            HttpStatusCode.Conflict,
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
    // HELPER: VERIFY NO UNWANTED SESSIONS
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