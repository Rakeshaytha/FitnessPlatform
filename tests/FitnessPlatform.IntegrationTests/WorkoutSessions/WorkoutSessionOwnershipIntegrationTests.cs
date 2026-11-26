using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FitnessPlatform.Contracts.WorkoutSessions;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionOwnershipIntegrationTests
{
    private const string StartEndpoint =
        WorkoutSessionTestData.RoutePrefix + "/start";

    // ============================================================
    // 1. MEMBER WITHOUT PROFILE
    // ============================================================

    [Fact]
    public async Task StartWorkout_MemberWithoutProfile_ReturnsNotFound()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var member =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory,
                createProfile: false);

        var owner =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        var plan =
            await WorkoutSessionTestData.CreateWorkoutPlanAsync(
                factory);

        var assignmentId =
            await WorkoutSessionTestData.CreateWorkoutAssignmentAsync(
                factory,
                owner.ProfileId,
                plan.PlanId);

        await WorkoutSessionTestData.AuthenticateAsync(
            client,
            member.Email);

        var request = new StartWorkoutSessionRequest
        {
            MemberWorkoutPlanAssignmentId = assignmentId,
            WorkoutPlanDayId = plan.TrainingDayId,
            TimeZoneId = "Asia/Dubai"
        };

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        var content =
            await response.Content.ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(content);

        Assert.Equal(
            "Member profile not found.",
            document.RootElement
                .GetProperty("error")
                .GetString());

        await AssertNoSessionsAsync(
            factory,
            owner.ProfileId);
    }

    // ============================================================
    // 2. MEMBER WITHOUT ASSIGNMENT
    // ============================================================

    [Fact]
    public async Task StartWorkout_MemberWithoutAssignment_ReturnsNotFound()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var member =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        var plan =
            await WorkoutSessionTestData.CreateWorkoutPlanAsync(
                factory);

        await WorkoutSessionTestData.AuthenticateAsync(
            client,
            member.Email);

        var request = new StartWorkoutSessionRequest
        {
            MemberWorkoutPlanAssignmentId = Guid.NewGuid(),
            WorkoutPlanDayId = plan.TrainingDayId,
            TimeZoneId = "Asia/Dubai"
        };

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertAssignmentNotFoundAsync(response);

        await AssertNoSessionsAsync(
            factory,
            member.ProfileId);
    }

    // ============================================================
    // 3. NONEXISTENT ASSIGNMENT
    // ============================================================

    [Fact]
    public async Task StartWorkout_NonexistentAssignment_ReturnsNotFound()
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

        request.MemberWorkoutPlanAssignmentId =
            Guid.NewGuid();

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertAssignmentNotFoundAsync(response);

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // 4. ANOTHER MEMBER'S ASSIGNMENT
    // ============================================================

    [Fact]
    public async Task StartWorkout_AnotherMembersAssignment_ReturnsNotFound()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var ownerClient =
            WorkoutSessionTestData.CreateClient(factory);

        using var otherClient =
            WorkoutSessionTestData.CreateClient(factory);

        var owner =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                ownerClient);

        var otherMember =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        await WorkoutSessionTestData.AuthenticateAsync(
            otherClient,
            otherMember.Email);

        var request = new StartWorkoutSessionRequest
        {
            MemberWorkoutPlanAssignmentId =
                owner.AssignmentId,

            WorkoutPlanDayId =
                Guid.NewGuid(),

            TimeZoneId =
                "Asia/Dubai"
        };

        using var response =
            await otherClient.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertAssignmentNotFoundAsync(response);

        await AssertNoSessionsAsync(
            factory,
            owner.Member.ProfileId);

        await AssertNoSessionsAsync(
            factory,
            otherMember.ProfileId);
    }

    // ============================================================
    // 5. ANOTHER MEMBER'S COMPLETE ASSIGNMENT
    // ============================================================

    [Fact]
    public async Task StartWorkout_AnotherMembersAssignmentAndMatchingDay_ReturnsNotFound()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var ownerClient =
            WorkoutSessionTestData.CreateClient(factory);

        using var otherClient =
            WorkoutSessionTestData.CreateClient(factory);

        var owner =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                ownerClient);

        var otherMember =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        await WorkoutSessionTestData.AuthenticateAsync(
            otherClient,
            otherMember.Email);

        var request =
            WorkoutSessionTestData.CreateStartRequest(
                owner);

        using var response =
            await otherClient.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertAssignmentNotFoundAsync(response);

        await AssertNoSessionsAsync(
            factory,
            owner.Member.ProfileId);

        await AssertNoSessionsAsync(
            factory,
            otherMember.ProfileId);
    }

    // ============================================================
    // 6. FOREIGN ASSIGNMENT WITH OWN WORKOUT DAY
    // ============================================================

    [Fact]
    public async Task StartWorkout_AnotherMembersAssignmentWithOwnDay_ReturnsNotFound()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var firstClient =
            WorkoutSessionTestData.CreateClient(factory);

        using var secondClient =
            WorkoutSessionTestData.CreateClient(factory);

        var first =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                firstClient);

        var second =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                secondClient);

        var request =
            WorkoutSessionTestData.CreateStartRequest(
                second);

        request.MemberWorkoutPlanAssignmentId =
            first.AssignmentId;

        using var response =
            await secondClient.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertAssignmentNotFoundAsync(response);

        await AssertNoSessionsAsync(
            factory,
            first.Member.ProfileId);

        await AssertNoSessionsAsync(
            factory,
            second.Member.ProfileId);
    }

    // ============================================================
    // 7. OWN ASSIGNMENT WITH FOREIGN WORKOUT DAY
    // ============================================================

    [Fact]
    public async Task StartWorkout_OwnAssignmentWithAnotherPlansDay_ReturnsNotFound()
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

        var request =
            WorkoutSessionTestData.CreateStartRequest(
                fixture,
                workoutDayId:
                    anotherPlan.TrainingDayId);

        using var response =
            await client.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertWorkoutDayNotFoundAsync(response);

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // 8. NONEXISTENT WORKOUT DAY
    // ============================================================

    [Fact]
    public async Task StartWorkout_OwnAssignmentWithNonexistentDay_ReturnsNotFound()
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

        await AssertWorkoutDayNotFoundAsync(response);

        await AssertNoSessionsAsync(
            factory,
            fixture.Member.ProfileId);
    }

    // ============================================================
    // 9. VALID ASSIGNMENT OWNERSHIP
    // ============================================================

    [Fact]
    public async Task StartWorkout_OwnAssignment_CreatesSessionForAuthenticatedMember()
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
            fixture.Plan.PlanId,
            persisted.WorkoutPlanId);

        Assert.Equal(
            fixture.Plan.TrainingDayId,
            persisted.WorkoutPlanDayId);

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountSessionsAsync(
                factory,
                fixture.Member.ProfileId));
    }

    // ============================================================
    // 10. TWO MEMBERS SHARING THE SAME WORKOUT PLAN
    // ============================================================

    [Fact]
    public async Task StartWorkout_TwoMembersWithSamePlan_CreatesIndependentSessions()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var firstClient =
            WorkoutSessionTestData.CreateClient(factory);

        using var secondClient =
            WorkoutSessionTestData.CreateClient(factory);

        var firstMember =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        var secondMember =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        var sharedPlan =
            await WorkoutSessionTestData.CreateWorkoutPlanAsync(
                factory);

        var firstAssignmentId =
            await WorkoutSessionTestData.CreateWorkoutAssignmentAsync(
                factory,
                firstMember.ProfileId,
                sharedPlan.PlanId);

        var secondAssignmentId =
            await WorkoutSessionTestData.CreateWorkoutAssignmentAsync(
                factory,
                secondMember.ProfileId,
                sharedPlan.PlanId);

        await WorkoutSessionTestData.AuthenticateAsync(
            firstClient,
            firstMember.Email);

        await WorkoutSessionTestData.AuthenticateAsync(
            secondClient,
            secondMember.Email);

        var firstFixture =
            new WorkoutSessionTestData.AssignedMember(
                firstMember,
                sharedPlan,
                firstAssignmentId);

        var secondFixture =
            new WorkoutSessionTestData.AssignedMember(
                secondMember,
                sharedPlan,
                secondAssignmentId);

        var firstResult =
            await StartSessionAsync(
                firstClient,
                firstFixture);

        var secondResult =
            await StartSessionAsync(
                secondClient,
                secondFixture);

        Assert.NotEqual(
            firstResult.Session.Id,
            secondResult.Session.Id);

        Assert.Equal(
            firstMember.ProfileId,
            firstResult.Session.UserProfileId);

        Assert.Equal(
            secondMember.ProfileId,
            secondResult.Session.UserProfileId);

        Assert.Equal(
            firstAssignmentId,
            firstResult.Session.MemberWorkoutPlanAssignmentId);

        Assert.Equal(
            secondAssignmentId,
            secondResult.Session.MemberWorkoutPlanAssignmentId);

        Assert.Equal(
            sharedPlan.PlanId,
            firstResult.Session.WorkoutPlanId);

        Assert.Equal(
            sharedPlan.PlanId,
            secondResult.Session.WorkoutPlanId);

        Assert.Equal(
            sharedPlan.TrainingDayId,
            firstResult.Session.WorkoutPlanDayId);

        Assert.Equal(
            sharedPlan.TrainingDayId,
            secondResult.Session.WorkoutPlanDayId);

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountSessionsAsync(
                factory,
                firstMember.ProfileId));

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountSessionsAsync(
                factory,
                secondMember.ProfileId));

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountUnfinishedSessionsAsync(
                factory,
                firstMember.ProfileId));

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountUnfinishedSessionsAsync(
                factory,
                secondMember.ProfileId));

        Assert.Equal(
            2,
            firstResult.Exercises.Count);

        Assert.Equal(
            2,
            secondResult.Exercises.Count);

        var firstExerciseIds =
            firstResult.Exercises
                .Select(x => x.Id)
                .ToHashSet();

        Assert.All(
            secondResult.Exercises,
            exercise =>
                Assert.DoesNotContain(
                    exercise.Id,
                    firstExerciseIds));
    }

    // ============================================================
    // 11. OWNERSHIP VALIDATION BEFORE DUPLICATE SESSION CHECK
    // ============================================================

    [Fact]
    public async Task StartWorkout_WithExistingSession_CannotUseAnotherMembersAssignment()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var firstClient =
            WorkoutSessionTestData.CreateClient(factory);

        using var secondClient =
            WorkoutSessionTestData.CreateClient(factory);

        var first =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                firstClient);

        var second =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                secondClient);

        var originalSession =
            await StartSessionAsync(
                firstClient,
                first);

        var unauthorizedRequest =
            WorkoutSessionTestData.CreateStartRequest(
                second);

        using var response =
            await firstClient.PostAsJsonAsync(
                StartEndpoint,
                unauthorizedRequest);

        await AssertAssignmentNotFoundAsync(response);

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountSessionsAsync(
                factory,
                first.Member.ProfileId));

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountUnfinishedSessionsAsync(
                factory,
                first.Member.ProfileId));

        await AssertNoSessionsAsync(
            factory,
            second.Member.ProfileId);

        var persisted =
            await WorkoutSessionTestData.GetPersistedSessionAsync(
                factory,
                originalSession.Session.Id);

        Assert.NotNull(persisted);

        Assert.Equal(
            first.Member.ProfileId,
            persisted.UserProfileId);

        Assert.Equal(
            first.AssignmentId,
            persisted.MemberWorkoutPlanAssignmentId);

        Assert.Equal(
            "InProgress",
            persisted.Status);

        Assert.Equal(
            originalSession.Session.RowVersion,
            Convert.ToBase64String(
                persisted.RowVersion));
    }

    // ============================================================
    // 12. ATTEMPTED OWNERSHIP OVERRIDE THROUGH JSON
    // ============================================================

    [Fact]
    public async Task StartWorkout_AdditionalOwnershipProperties_CannotOverrideAuthenticatedMember()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var ownerClient =
            WorkoutSessionTestData.CreateClient(factory);

        using var otherClient =
            WorkoutSessionTestData.CreateClient(factory);

        var owner =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                ownerClient);

        var otherMember =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        await WorkoutSessionTestData.AuthenticateAsync(
            otherClient,
            otherMember.Email);

        var request = new
        {
            MemberWorkoutPlanAssignmentId =
                owner.AssignmentId,

            WorkoutPlanDayId =
                owner.Plan.TrainingDayId,

            TimeZoneId =
                "Asia/Dubai",

            UserProfileId =
                owner.Member.ProfileId,

            UserId =
                owner.Member.UserId,

            WorkoutPlanId =
                owner.Plan.PlanId
        };

        using var response =
            await otherClient.PostAsJsonAsync(
                StartEndpoint,
                request);

        await AssertAssignmentNotFoundAsync(response);

        await AssertNoSessionsAsync(
            factory,
            owner.Member.ProfileId);

        await AssertNoSessionsAsync(
            factory,
            otherMember.ProfileId);
    }

    // ============================================================
    // HELPER: START VALID WORKOUT SESSION
    // ============================================================

    private static async Task<WorkoutSessionDetailResponse>
        StartSessionAsync(
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

        return result;
    }

    // ============================================================
    // HELPER: ASSIGNMENT NOT FOUND
    // ============================================================

    private static Task AssertAssignmentNotFoundAsync(
        HttpResponseMessage response)
    {
        return AssertNotFoundAsync(
            response,
            "WORKOUT_ASSIGNMENT_NOT_FOUND",
            "Workout plan assignment not found.");
    }

    // ============================================================
    // HELPER: WORKOUT DAY NOT FOUND
    // ============================================================

    private static Task AssertWorkoutDayNotFoundAsync(
        HttpResponseMessage response)
    {
        return AssertNotFoundAsync(
            response,
            "WORKOUT_DAY_NOT_FOUND",
            "The requested workout day was not found in your assigned workout plan.");
    }

    // ============================================================
    // HELPER: ERROR RESPONSE VALIDATION
    // ============================================================

    private static async Task AssertNotFoundAsync(
        HttpResponseMessage response,
        string expectedCode,
        string expectedError)
    {
        Assert.Equal(
            HttpStatusCode.NotFound,
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
    // HELPER: VERIFY NO UNAUTHORIZED SESSION CREATION
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