using System.Net;
using System.Net.Http.Json;

using FitnessPlatform.Contracts.WorkoutSessions;
using FitnessPlatform.Infrastructure.Identity;
using FitnessPlatform.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionTestDataIntegrationTests
{
    // ============================================================
    // 1. IDENTITY USER CREATION
    // ============================================================

    [Fact]
    public async Task CreateMemberAsync_CreatesValidIdentityUser()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        var member =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        using var scope =
            factory.Services.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByIdAsync(
            member.UserId.ToString());

        Assert.NotNull(user);

        Assert.Equal(member.UserId, user.Id);
        Assert.Equal(member.Email, user.Email);
        Assert.Equal(member.Email, user.UserName);

        Assert.True(user.EmailConfirmed);
        Assert.True(user.IsActive);

        Assert.True(
            await userManager.IsInRoleAsync(
                user,
                "Member"));

        Assert.NotEqual(Guid.Empty, member.ProfileId);
    }

    // ============================================================
    // 2. MEMBER WITHOUT PROFILE
    // ============================================================

    [Fact]
    public async Task CreateMemberAsync_WithoutProfile_DoesNotCreateProfile()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        var member =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory,
                createProfile: false);

        Assert.NotEqual(Guid.Empty, member.UserId);
        Assert.Equal(Guid.Empty, member.ProfileId);

        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var profileExists =
            await db.UserProfiles
                .AsNoTracking()
                .AnyAsync(
                    x => x.UserId == member.UserId);

        Assert.False(profileExists);
    }

    // ============================================================
    // 3. PROFILE OWNERSHIP
    // ============================================================

    [Fact]
    public async Task CreateMemberAsync_ProfileIsLinkedToIdentityUser()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        var member =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var profile = await db.UserProfiles
            .AsNoTracking()
            .SingleAsync(
                x => x.Id == member.ProfileId);

        Assert.Equal(member.UserId, profile.UserId);

        Assert.Equal("Workout", profile.FirstName);
        Assert.Equal("IntegrationTest", profile.LastName);

        Assert.Equal(175m, profile.HeightCm);
        Assert.Equal(80m, profile.WeightKg);
        Assert.Equal("AE", profile.CountryCode);

        Assert.Equal(
            new DateOnly(1990, 1, 1),
            profile.DateOfBirth);
    }

    // ============================================================
    // 4. UNIQUE TEST MEMBERS
    // ============================================================

    [Fact]
    public async Task CreateMemberAsync_CreatesUniqueMembersAndProfiles()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        var first =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        var second =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        Assert.NotEqual(first.UserId, second.UserId);
        Assert.NotEqual(first.ProfileId, second.ProfileId);
        Assert.NotEqual(first.Email, second.Email);

        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var profileCount = await db.UserProfiles
            .AsNoTracking()
            .CountAsync(
                x => x.UserId == first.UserId ||
                     x.UserId == second.UserId);

        Assert.Equal(2, profileCount);
    }

    // ============================================================
    // 5 AND 6. ADMIN AND TRAINER ROLES
    // ============================================================

    [Theory]
    [InlineData("Admin")]
    [InlineData("Trainer")]
    public async Task CreateRoleUserAsync_CreatesExpectedRole(
        string role)
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        var testUser = role == "Admin"
            ? await WorkoutSessionTestData.CreateAdminAsync(
                factory)
            : await WorkoutSessionTestData.CreateTrainerAsync(
                factory);

        Assert.Equal(role, testUser.Role);
        Assert.Equal(Guid.Empty, testUser.ProfileId);

        using var scope =
            factory.Services.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByIdAsync(
            testUser.UserId.ToString());

        Assert.NotNull(user);

        var roles =
            await userManager.GetRolesAsync(user);

        Assert.Equal(
            role,
            Assert.Single(roles));
    }

    // ============================================================
    // 7. ACTUAL JWT AUTHENTICATION
    // ============================================================

    [Fact]
    public async Task AuthenticateAsync_ReturnsValidBearerToken()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var member =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        var token =
            await WorkoutSessionTestData.AuthenticateAsync(
                client,
                member.Email);

        Assert.False(
            string.IsNullOrWhiteSpace(token));

        Assert.NotNull(
            client.DefaultRequestHeaders.Authorization);

        Assert.Equal(
            "Bearer",
            client.DefaultRequestHeaders.Authorization.Scheme);

        Assert.Equal(
            token,
            client.DefaultRequestHeaders.Authorization.Parameter);

        using var response = await client.GetAsync(
            $"{WorkoutSessionTestData.RoutePrefix}/active");

        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);
    }

    // ============================================================
    // 8. APPROVED WORKOUT PLAN
    // ============================================================

    [Fact]
    public async Task CreateWorkoutPlanAsync_CreatesApprovedPlan()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        var fixture =
            await WorkoutSessionTestData.CreateWorkoutPlanAsync(
                factory);

        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var plan = await db.WorkoutPlans
            .AsNoTracking()
            .SingleAsync(
                x => x.Id == fixture.PlanId);

        Assert.Equal(fixture.PlanName, plan.Name);

        Assert.Equal("Approved", plan.ReviewStatus);
        Assert.True(plan.IsActive);
        Assert.True(plan.IsLatestVersion);

        Assert.Equal(1, plan.VersionNumber);
        Assert.Equal(12, plan.DurationWeeks);

        Assert.NotEqual(
            Guid.Empty,
            plan.VersionGroupId);

        Assert.Null(plan.PreviousVersionId);

        Assert.Equal(
            WorkoutSessionTestData.DefaultUtcNow.UtcDateTime,
            plan.CreatedAtUtc);
    }

    // ============================================================
    // 9. EXERCISE MASTER FIXTURES
    // ============================================================

    [Fact]
    public async Task CreateWorkoutPlanAsync_CreatesStrengthAndCardioMasters()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        var fixture =
            await WorkoutSessionTestData.CreateWorkoutPlanAsync(
                factory);

        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var strength = await db.Exercises
            .AsNoTracking()
            .SingleAsync(
                x => x.Id == fixture.StrengthExerciseId);

        var cardio = await db.Exercises
            .AsNoTracking()
            .SingleAsync(
                x => x.Id == fixture.CardioExerciseId);

        Assert.Equal("Chest", strength.MuscleGroup);
        Assert.Equal("Barbell", strength.Equipment);

        Assert.Equal("Cardio", cardio.MuscleGroup);
        Assert.Equal("Treadmill", cardio.Equipment);

        Assert.Equal("Approved", strength.ReviewStatus);
        Assert.Equal("Approved", cardio.ReviewStatus);

        Assert.True(strength.IsActive);
        Assert.True(cardio.IsActive);

        Assert.NotEqual(strength.Id, cardio.Id);
        Assert.NotEqual(strength.Code, cardio.Code);

        Assert.False(
            string.IsNullOrWhiteSpace(
                strength.Instructions));

        Assert.False(
            string.IsNullOrWhiteSpace(
                cardio.Instructions));
    }

    // ============================================================
    // 10. PLANNED EXERCISE CONFIGURATION
    // ============================================================

    [Fact]
    public async Task CreateWorkoutPlanAsync_CreatesExpectedExerciseConfiguration()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        var fixture =
            await WorkoutSessionTestData.CreateWorkoutPlanAsync(
                factory);

        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercises = await db.WorkoutPlanExercises
            .AsNoTracking()
            .Where(
                x => x.WorkoutPlanDayId ==
                     fixture.TrainingDayId)
            .OrderBy(x => x.ExerciseOrder)
            .ToListAsync();

        Assert.Equal(2, exercises.Count);

        var strength = exercises[0];
        var cardio = exercises[1];

        Assert.Equal(
            fixture.StrengthPlanExerciseId,
            strength.Id);

        Assert.Equal(
            fixture.StrengthExerciseId,
            strength.ExerciseId);

        Assert.Equal(1, strength.ExerciseOrder);
        Assert.Equal(3, strength.Sets);
        Assert.Equal(12, strength.Reps);
        Assert.Equal(60, strength.RestSeconds);
        Assert.Null(strength.DurationSeconds);

        Assert.Equal(
            "Maintain proper form.",
            strength.Notes);

        Assert.Equal(
            fixture.CardioPlanExerciseId,
            cardio.Id);

        Assert.Equal(
            fixture.CardioExerciseId,
            cardio.ExerciseId);

        Assert.Equal(2, cardio.ExerciseOrder);
        Assert.Equal(1, cardio.Sets);
        Assert.Equal(0, cardio.Reps);
        Assert.Equal(0, cardio.RestSeconds);
        Assert.Equal(600, cardio.DurationSeconds);

        Assert.Equal(
            "Maintain steady pace.",
            cardio.Notes);

        Assert.Equal(
            4,
            exercises.Sum(x => x.Sets));
    }

    // ============================================================
    // 11. REST DAY
    // ============================================================

    [Fact]
    public async Task CreateWorkoutPlanAsync_CreatesRestDayWithoutExercises()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        var fixture =
            await WorkoutSessionTestData.CreateWorkoutPlanAsync(
                factory);

        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var restDay = await db.WorkoutPlanDays
            .AsNoTracking()
            .Include(x => x.Exercises)
            .SingleAsync(
                x => x.Id == fixture.RestDayId);

        Assert.Equal(
            fixture.PlanId,
            restDay.WorkoutPlanId);

        Assert.Equal(2, restDay.DayNumber);
        Assert.Equal("Rest Day", restDay.Name);

        Assert.True(restDay.IsRestDay);

        Assert.Null(
            restDay.EstimatedDurationMinutes);

        Assert.Empty(restDay.Exercises);
    }

    // ============================================================
    // 12. OPTIONAL EMPTY TRAINING DAY
    // ============================================================

    [Fact]
    public async Task CreateWorkoutPlanAsync_OptionalEmptyDay_IsCreatedOnlyWhenRequested()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        var normalPlan =
            await WorkoutSessionTestData.CreateWorkoutPlanAsync(
                factory);

        var defensivePlan =
            await WorkoutSessionTestData.CreateWorkoutPlanAsync(
                factory,
                includeEmptyTrainingDay: true);

        Assert.Null(normalPlan.EmptyTrainingDayId);
        Assert.NotNull(defensivePlan.EmptyTrainingDayId);

        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var normalDayCount =
            await db.WorkoutPlanDays
                .AsNoTracking()
                .CountAsync(
                    x => x.WorkoutPlanId ==
                         normalPlan.PlanId);

        Assert.Equal(2, normalDayCount);

        var defensiveDayCount =
            await db.WorkoutPlanDays
                .AsNoTracking()
                .CountAsync(
                    x => x.WorkoutPlanId ==
                         defensivePlan.PlanId);

        Assert.Equal(3, defensiveDayCount);

        var emptyDay =
            await db.WorkoutPlanDays
                .AsNoTracking()
                .Include(x => x.Exercises)
                .SingleAsync(
                    x => x.Id ==
                         defensivePlan.EmptyTrainingDayId.Value);

        Assert.Equal(3, emptyDay.DayNumber);
        Assert.False(emptyDay.IsRestDay);
        Assert.Empty(emptyDay.Exercises);
    }

    // ============================================================
    // 13. ACTIVE ASSIGNMENT
    // ============================================================

    [Fact]
    public async Task CreateWorkoutAssignmentAsync_CreatesActiveAssignment()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        var member =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        var plan =
            await WorkoutSessionTestData.CreateWorkoutPlanAsync(
                factory);

        var assignmentId =
            await WorkoutSessionTestData.CreateWorkoutAssignmentAsync(
                factory,
                member.ProfileId,
                plan.PlanId);

        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var assignment =
            await db.MemberWorkoutPlanAssignments
                .AsNoTracking()
                .SingleAsync(
                    x => x.Id == assignmentId);

        Assert.Equal(
            member.ProfileId,
            assignment.UserProfileId);

        Assert.Equal(
            plan.PlanId,
            assignment.WorkoutPlanId);

        Assert.Equal("Active", assignment.Status);

        Assert.Equal(
            WorkoutSessionTestData.DefaultWorkoutDate.AddDays(-7),
            assignment.StartDate);

        Assert.Null(assignment.EndDate);
        Assert.Null(assignment.DeactivatedAtUtc);

        Assert.NotEqual(
            Guid.Empty,
            assignment.AssignedByUserId);

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var admin = await userManager.FindByIdAsync(
            assignment.AssignedByUserId.ToString());

        Assert.NotNull(admin);

        Assert.True(
            await userManager.IsInRoleAsync(
                admin,
                "Admin"));
    }

    // ============================================================
    // 14. CUSTOM ASSIGNMENT DATES
    // ============================================================

    [Fact]
    public async Task CreateWorkoutAssignmentAsync_PreservesCustomDates()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        var member =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        var plan =
            await WorkoutSessionTestData.CreateWorkoutPlanAsync(
                factory);

        var startDate =
            new DateOnly(2026, 11, 28);

        var endDate =
            new DateOnly(2026, 12, 31);

        var assignmentId =
            await WorkoutSessionTestData.CreateWorkoutAssignmentAsync(
                factory,
                member.ProfileId,
                plan.PlanId,
                startDate,
                endDate);

        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var assignment =
            await db.MemberWorkoutPlanAssignments
                .AsNoTracking()
                .SingleAsync(
                    x => x.Id == assignmentId);

        Assert.Equal(startDate, assignment.StartDate);
        Assert.Equal(endDate, assignment.EndDate);

        Assert.Equal("Active", assignment.Status);
    }

    // ============================================================
    // 15. INACTIVE ASSIGNMENT
    // ============================================================

    [Fact]
    public async Task CreateWorkoutAssignmentAsync_CreatesInactiveAssignment()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        var member =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        var plan =
            await WorkoutSessionTestData.CreateWorkoutPlanAsync(
                factory);

        var assignmentId =
            await WorkoutSessionTestData.CreateWorkoutAssignmentAsync(
                factory,
                member.ProfileId,
                plan.PlanId,
                status: "Inactive");

        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var assignment =
            await db.MemberWorkoutPlanAssignments
                .AsNoTracking()
                .SingleAsync(
                    x => x.Id == assignmentId);

        Assert.Equal("Inactive", assignment.Status);

        Assert.Equal(
            WorkoutSessionTestData.DefaultUtcNow.UtcDateTime,
            assignment.DeactivatedAtUtc);
    }

    // ============================================================
    // 16. COMPLETE AUTHENTICATED MEMBER
    // ============================================================

    [Fact]
    public async Task PrepareAssignedMemberAsync_CreatesCompleteFixture()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client);

        Assert.NotEqual(
            Guid.Empty,
            fixture.Member.UserId);

        Assert.NotEqual(
            Guid.Empty,
            fixture.Member.ProfileId);

        Assert.NotEqual(
            Guid.Empty,
            fixture.Plan.PlanId);

        Assert.NotEqual(
            Guid.Empty,
            fixture.AssignmentId);

        Assert.NotNull(
            client.DefaultRequestHeaders.Authorization);

        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var assignment =
            await db.MemberWorkoutPlanAssignments
                .AsNoTracking()
                .SingleAsync(
                    x => x.Id == fixture.AssignmentId);

        Assert.Equal(
            fixture.Member.ProfileId,
            assignment.UserProfileId);

        Assert.Equal(
            fixture.Plan.PlanId,
            assignment.WorkoutPlanId);

        Assert.Equal("Active", assignment.Status);

        using var response = await client.GetAsync(
            $"{WorkoutSessionTestData.RoutePrefix}/active");

        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);
    }

    // ============================================================
    // 17. START REQUEST GENERATION
    // ============================================================

    [Fact]
    public async Task CreateStartRequest_PreservesFixtureAndOverrides()
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

        Assert.Equal(
            fixture.AssignmentId,
            request.MemberWorkoutPlanAssignmentId);

        Assert.Equal(
            fixture.Plan.TrainingDayId,
            request.WorkoutPlanDayId);

        Assert.Equal(
            "Asia/Dubai",
            request.TimeZoneId);

        Assert.Null(request.Notes);

        var customRequest =
            WorkoutSessionTestData.CreateStartRequest(
                fixture,
                workoutDayId: fixture.Plan.RestDayId,
                timeZoneId: "UTC",
                notes: "Custom workout notes");

        Assert.Equal(
            fixture.AssignmentId,
            customRequest.MemberWorkoutPlanAssignmentId);

        Assert.Equal(
            fixture.Plan.RestDayId,
            customRequest.WorkoutPlanDayId);

        Assert.Equal("UTC", customRequest.TimeZoneId);

        Assert.Equal(
            "Custom workout notes",
            customRequest.Notes);
    }

    // ============================================================
    // 18. SESSION COUNT
    // ============================================================

    [Fact]
    public async Task CountSessionsAsync_NewMember_ReturnsZero()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        var member =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        var count =
            await WorkoutSessionTestData.CountSessionsAsync(
                factory,
                member.ProfileId);

        Assert.Equal(0, count);
    }

    // ============================================================
    // 19. UNFINISHED SESSION COUNT
    // ============================================================

    [Fact]
    public async Task CountUnfinishedSessionsAsync_NewMember_ReturnsZero()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        var member =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        var count =
            await WorkoutSessionTestData.CountUnfinishedSessionsAsync(
                factory,
                member.ProfileId);

        Assert.Equal(0, count);
    }

    // ============================================================
    // 20. NONEXISTENT SESSION
    // ============================================================

    [Fact]
    public async Task GetPersistedSessionAsync_NonexistentSession_ReturnsNull()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        var session =
            await WorkoutSessionTestData.GetPersistedSessionAsync(
                factory,
                Guid.NewGuid());

        Assert.Null(session);
    }
}