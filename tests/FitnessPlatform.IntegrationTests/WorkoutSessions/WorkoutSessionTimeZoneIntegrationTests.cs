using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FitnessPlatform.Contracts.WorkoutSessions;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionTimeZoneIntegrationTests
{
    private const string StartEndpoint =
        WorkoutSessionTestData.RoutePrefix + "/start";

    // ============================================================
    // 1. WORKOUT DATE ACROSS MULTIPLE TIME ZONES
    // ============================================================

    [Theory]
    [InlineData("UTC", 2026, 11, 26)]
    [InlineData("Asia/Dubai", 2026, 11, 27)]
    [InlineData("Asia/Kolkata", 2026, 11, 27)]
    [InlineData("America/New_York", 2026, 11, 26)]
    [InlineData("Europe/London", 2026, 11, 26)]
    [InlineData("Pacific/Auckland", 2026, 11, 27)]
    public async Task StartWorkout_WithSupportedTimeZone_UsesCorrectLocalDate(
        string timeZoneId,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var utcNow =
            WorkoutSessionTestData.DefaultUtcNow;

        using var factory =
            WorkoutSessionTestData.CreateFactory(
                utcNow);

        using var client =
            WorkoutSessionTestData.CreateClient(
                factory);

        var expectedDate = new DateOnly(
            expectedYear,
            expectedMonth,
            expectedDay);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client,
                startDate: expectedDate.AddDays(-7),
                endDate: expectedDate.AddDays(7));

        var result =
            await StartAndAssertSessionAsync(
                factory,
                client,
                fixture,
                timeZoneId,
                expectedDate,
                utcNow);

        Assert.Equal(
            expectedDate,
            result.Session.WorkoutDate);
    }

    // ============================================================
    // 2. UAE AND INDIA MIDNIGHT BOUNDARIES
    // ============================================================

    [Theory]
    [InlineData(
        "Asia/Dubai",
        19, 59, 59,
        26)]
    [InlineData(
        "Asia/Dubai",
        20, 0, 0,
        27)]
    [InlineData(
        "Asia/Kolkata",
        18, 29, 59,
        26)]
    [InlineData(
        "Asia/Kolkata",
        18, 30, 0,
        27)]
    public async Task StartWorkout_AroundLocalMidnight_UsesCorrectWorkoutDate(
        string timeZoneId,
        int utcHour,
        int utcMinute,
        int utcSecond,
        int expectedDay)
    {
        var utcNow = new DateTimeOffset(
            2026,
            11,
            26,
            utcHour,
            utcMinute,
            utcSecond,
            TimeSpan.Zero);

        var expectedDate = new DateOnly(
            2026,
            11,
            expectedDay);

        using var factory =
            WorkoutSessionTestData.CreateFactory(
                utcNow);

        using var client =
            WorkoutSessionTestData.CreateClient(
                factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client,
                startDate: new DateOnly(2026, 11, 20),
                endDate: new DateOnly(2026, 12, 4));

        await StartAndAssertSessionAsync(
            factory,
            client,
            fixture,
            timeZoneId,
            expectedDate,
            utcNow);
    }

    // ============================================================
    // 3. DAYLIGHT SAVING TIME
    // ============================================================

    [Theory]
    [InlineData(7, 15)]
    [InlineData(1, 14)]
    public async Task StartWorkout_NewYorkSeasonalOffset_UsesCorrectLocalDate(
        int month,
        int expectedDay)
    {
        var utcNow = new DateTimeOffset(
            2026,
            month,
            15,
            4,
            30,
            0,
            TimeSpan.Zero);

        var expectedDate = new DateOnly(
            2026,
            month,
            expectedDay);

        using var factory =
            WorkoutSessionTestData.CreateFactory(
                utcNow);

        using var client =
            WorkoutSessionTestData.CreateClient(
                factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client,
                startDate: expectedDate.AddDays(-7),
                endDate: expectedDate.AddDays(7));

        await StartAndAssertSessionAsync(
            factory,
            client,
            fixture,
            "America/New_York",
            expectedDate,
            utcNow);
    }

    // ============================================================
    // 4. SAME UTC TIMESTAMP WITH DIFFERENT LOCAL DATES
    // ============================================================

    [Fact]
    public async Task StartWorkout_DifferentMemberTimeZones_ProducesIndependentLocalDates()
    {
        var utcNow =
            WorkoutSessionTestData.DefaultUtcNow;

        using var factory =
            WorkoutSessionTestData.CreateFactory(
                utcNow);

        using var dubaiClient =
            WorkoutSessionTestData.CreateClient(
                factory);

        using var utcClient =
            WorkoutSessionTestData.CreateClient(
                factory);

        var dubaiMember =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                dubaiClient,
                startDate: new DateOnly(2026, 11, 20),
                endDate: new DateOnly(2026, 12, 4));

        var utcMember =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                utcClient,
                startDate: new DateOnly(2026, 11, 20),
                endDate: new DateOnly(2026, 12, 4));

        var dubaiResult =
            await StartAndAssertSessionAsync(
                factory,
                dubaiClient,
                dubaiMember,
                "Asia/Dubai",
                new DateOnly(2026, 11, 27),
                utcNow);

        var utcResult =
            await StartAndAssertSessionAsync(
                factory,
                utcClient,
                utcMember,
                "UTC",
                new DateOnly(2026, 11, 26),
                utcNow);

        Assert.NotEqual(
            dubaiResult.Session.Id,
            utcResult.Session.Id);

        Assert.NotEqual(
            dubaiResult.Session.UserProfileId,
            utcResult.Session.UserProfileId);

        Assert.NotEqual(
            dubaiResult.Session.WorkoutDate,
            utcResult.Session.WorkoutDate);

        Assert.Equal(
            dubaiResult.Session.StartedAtUtc,
            utcResult.Session.StartedAtUtc);

        Assert.Equal(
            dubaiResult.Session.CreatedAtUtc,
            utcResult.Session.CreatedAtUtc);
    }

    // ============================================================
    // 5. ASSIGNMENT START DATE BASED ON LOCAL DATE
    // ============================================================

    [Theory]
    [InlineData("Asia/Dubai", true)]
    [InlineData("UTC", false)]
    public async Task StartWorkout_AssignmentStartDate_UsesMemberLocalDate(
        string timeZoneId,
        bool shouldSucceed)
    {
        var utcNow =
            WorkoutSessionTestData.DefaultUtcNow;

        using var factory =
            WorkoutSessionTestData.CreateFactory(
                utcNow);

        using var client =
            WorkoutSessionTestData.CreateClient(
                factory);

        var assignmentStartDate =
            new DateOnly(2026, 11, 27);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client,
                startDate: assignmentStartDate,
                endDate: assignmentStartDate.AddDays(30));

        if (shouldSucceed)
        {
            await StartAndAssertSessionAsync(
                factory,
                client,
                fixture,
                timeZoneId,
                new DateOnly(2026, 11, 27),
                utcNow);

            return;
        }

        var request =
            WorkoutSessionTestData.CreateStartRequest(
                fixture,
                timeZoneId: timeZoneId);

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
    // 6. ASSIGNMENT EXPIRY BASED ON LOCAL DATE
    // ============================================================

    [Theory]
    [InlineData("Asia/Dubai", false)]
    [InlineData("UTC", true)]
    public async Task StartWorkout_AssignmentEndDate_UsesMemberLocalDate(
        string timeZoneId,
        bool shouldSucceed)
    {
        var utcNow =
            WorkoutSessionTestData.DefaultUtcNow;

        using var factory =
            WorkoutSessionTestData.CreateFactory(
                utcNow);

        using var client =
            WorkoutSessionTestData.CreateClient(
                factory);

        var assignmentEndDate =
            new DateOnly(2026, 11, 26);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client,
                startDate: assignmentEndDate.AddDays(-30),
                endDate: assignmentEndDate);

        if (shouldSucceed)
        {
            await StartAndAssertSessionAsync(
                factory,
                client,
                fixture,
                timeZoneId,
                new DateOnly(2026, 11, 26),
                utcNow);

            return;
        }

        var request =
            WorkoutSessionTestData.CreateStartRequest(
                fixture,
                timeZoneId: timeZoneId);

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
    // HELPER: CREATE AND VERIFY WORKOUT SESSION
    // ============================================================

    private static async Task<WorkoutSessionDetailResponse>
        StartAndAssertSessionAsync(
            FitnessPlatformWebApplicationFactory factory,
            HttpClient client,
            WorkoutSessionTestData.AssignedMember fixture,
            string timeZoneId,
            DateOnly expectedWorkoutDate,
            DateTimeOffset expectedUtcNow)
    {
        var request =
            WorkoutSessionTestData.CreateStartRequest(
                fixture,
                timeZoneId: timeZoneId);

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
            expectedWorkoutDate,
            result.Session.WorkoutDate);

        Assert.Equal(
            timeZoneId,
            result.Session.TimeZoneId);

        Assert.Equal(
            "InProgress",
            result.Session.Status);

        Assert.Equal(
            expectedUtcNow.UtcDateTime,
            result.Session.StartedAtUtc);

        Assert.Equal(
            expectedUtcNow.UtcDateTime,
            result.Session.CreatedAtUtc);

        Assert.Equal(
            expectedUtcNow.UtcDateTime,
            result.Session.UpdatedAtUtc);

        var persisted =
            await WorkoutSessionTestData.GetPersistedSessionAsync(
                factory,
                result.Session.Id);

        Assert.NotNull(persisted);

        Assert.Equal(
            expectedWorkoutDate,
            persisted.WorkoutDate);

        Assert.Equal(
            timeZoneId,
            persisted.TimeZoneId);

        Assert.Equal(
            expectedUtcNow.UtcDateTime,
            persisted.StartedAtUtc);

        Assert.Equal(
            expectedUtcNow.UtcDateTime,
            persisted.CreatedAtUtc);

        Assert.Equal(
            expectedUtcNow.UtcDateTime,
            persisted.UpdatedAtUtc);

        Assert.Equal(
            2,
            persisted.Exercises.Count);

        Assert.All(
            persisted.Exercises,
            exercise =>
            {
                Assert.Equal(
                    expectedUtcNow.UtcDateTime,
                    exercise.CreatedAtUtc);

                Assert.Equal(
                    expectedUtcNow.UtcDateTime,
                    exercise.UpdatedAtUtc);

                Assert.Equal(
                    "Pending",
                    exercise.Status);
            });

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
    // HELPER: CONFLICT RESPONSE VALIDATION
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