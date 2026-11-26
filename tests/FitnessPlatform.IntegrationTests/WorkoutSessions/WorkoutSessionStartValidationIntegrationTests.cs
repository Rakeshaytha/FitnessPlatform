using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using FitnessPlatform.Contracts.WorkoutSessions;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionStartValidationIntegrationTests
{
    private const string StartEndpoint =
        WorkoutSessionTestData.RoutePrefix + "/start";

    // ============================================================
    // INVALID ASSIGNMENT AND WORKOUT DAY IDENTIFIERS
    // ============================================================

    [Theory]
    [InlineData(true, false,
        "Member workout plan assignment ID is required.")]
    [InlineData(false, true,
        "Workout plan day ID is required.")]
    [InlineData(true, true,
        "Member workout plan assignment ID is required.")]
    public async Task StartWorkout_WithInvalidIdentifiers_ReturnsBadRequest(
        bool emptyAssignmentId,
        bool emptyWorkoutDayId,
        string expectedError)
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
                fixture);

        if (emptyAssignmentId)
        {
            request.MemberWorkoutPlanAssignmentId =
                Guid.Empty;
        }

        if (emptyWorkoutDayId)
        {
            request.WorkoutPlanDayId =
                Guid.Empty;
        }

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertValidationErrorAsync(
            response,
            expectedError);

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // INVALID TIME ZONE VALUES
    // ============================================================

    [Theory]
    [InlineData(null,
        "Time zone ID is required.")]
    [InlineData("",
        "Time zone ID is required.")]
    [InlineData("   ",
        "Time zone ID is required.")]
    [InlineData("Invalid/WorkoutTimeZone",
        "Invalid or unsupported time zone ID.")]
    [InlineData(" Asia/Dubai",
        "Time zone ID cannot contain leading or trailing whitespace.")]
    [InlineData("Asia/Dubai ",
        "Time zone ID cannot contain leading or trailing whitespace.")]
    public async Task StartWorkout_WithInvalidTimeZone_ReturnsBadRequest(
        string? timeZoneId,
        string expectedError)
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
                fixture);

        request.TimeZoneId = timeZoneId!;

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertValidationErrorAsync(
            response,
            expectedError);

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // TIME ZONE MAXIMUM LENGTH
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithExcessiveTimeZoneLength_ReturnsBadRequest()
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
                timeZoneId: new string('A', 101));

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertValidationErrorAsync(
            response,
            "Time zone ID cannot exceed 100 characters.");

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // NOTES MAXIMUM LENGTH
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithExcessiveNotesLength_ReturnsBadRequest()
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
                notes: new string('N', 1001));

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertValidationErrorAsync(
            response,
            "Notes cannot exceed 1000 characters.");

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // MISSING REQUEST BODY
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithoutRequestBody_ReturnsBadRequest()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client);

        using var response =
            await client.PostAsync(
                StartEndpoint,
                content: null);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // MALFORMED JSON
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithMalformedJson_ReturnsBadRequest()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client);

        using var content = new StringContent(
            "{\"timeZoneId\":",
            Encoding.UTF8,
            "application/json");

        using var response =
            await client.PostAsync(
                StartEndpoint,
                content);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // INVALID GUID JSON FORMAT
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithInvalidGuidFormat_ReturnsBadRequest()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client);

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                new
                {
                    MemberWorkoutPlanAssignmentId =
                        "invalid-guid",

                    WorkoutPlanDayId =
                        fixture.Plan.TrainingDayId,

                    TimeZoneId =
                        "Asia/Dubai"
                });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // OPTIONAL NOTES AND NORMALIZATION
    // ============================================================

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Morning workout  ", "Morning workout")]
    public async Task StartWorkout_WithValidNotes_NormalizesAndPersistsNotes(
        string? notes,
        string? expectedNotes)
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
                notes: notes);

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

        Assert.Equal(
            expectedNotes,
            result.Session.Notes);

        var persisted =
            await WorkoutSessionTestData.GetPersistedSessionAsync(
                factory,
                result.Session.Id);

        Assert.NotNull(persisted);

        Assert.Equal(
            expectedNotes,
            persisted.Notes);

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountSessionsAsync(
                factory,
                fixture.Member.ProfileId));
    }

    // ============================================================
    // NOTES EXACT MAXIMUM LENGTH
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithMaximumAllowedNotesLength_Succeeds()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client);

        var notes =
            new string('N', 1000);

        var request =
            WorkoutSessionTestData.CreateStartRequest(
                fixture,
                notes: notes);

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

        Assert.Equal(
            notes,
            result.Session.Notes);

        var persisted =
            await WorkoutSessionTestData.GetPersistedSessionAsync(
                factory,
                result.Session.Id);

        Assert.NotNull(persisted);

        Assert.Equal(
            notes,
            persisted.Notes);

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountSessionsAsync(
                factory,
                fixture.Member.ProfileId));
    }

    // ============================================================
    // VALID UTC TIME ZONE
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithUtcTimeZone_Succeeds()
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
                timeZoneId: "UTC");

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

        Assert.Equal(
            "UTC",
            result.Session.TimeZoneId);

        Assert.Equal(
            DateOnly.FromDateTime(
                WorkoutSessionTestData.DefaultUtcNow.UtcDateTime),
            result.Session.WorkoutDate);

        var persisted =
            await WorkoutSessionTestData.GetPersistedSessionAsync(
                factory,
                result.Session.Id);

        Assert.NotNull(persisted);

        Assert.Equal(
            "UTC",
            persisted.TimeZoneId);

        Assert.Equal(
            result.Session.WorkoutDate,
            persisted.WorkoutDate);

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountSessionsAsync(
                factory,
                fixture.Member.ProfileId));
    }

    // ============================================================
    // HELPER: VALIDATION ERROR ASSERTION
    // ============================================================

    private static async Task AssertValidationErrorAsync(
        HttpResponseMessage response,
        string expectedError)
    {
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        var content =
            await response.Content.ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(content);

        Assert.True(
            document.RootElement.TryGetProperty(
                "error",
                out var error),
            $"Expected error property. Response: {content}");

        Assert.Equal(
            expectedError,
            error.GetString());
    }

    // ============================================================
    // HELPER: VERIFY NO DATABASE RECORDS CREATED
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