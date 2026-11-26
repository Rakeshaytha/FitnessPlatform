using FitnessPlatform.Contracts.WorkoutSessions;
using FitnessPlatform.Domain.Entities;
using FitnessPlatform.Infrastructure.Identity;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

internal static class WorkoutSessionTestData
{
    // ============================================================
    // TEST CONSTANTS
    // ============================================================

    public const string RoutePrefix =
        "/api/member/workout-sessions";

    public const string DefaultTimeZoneId =
        "Asia/Dubai";

    public static readonly DateTimeOffset DefaultUtcNow =
        new(2026, 11, 26, 21, 30, 0, TimeSpan.Zero);

    public static readonly DateOnly DefaultWorkoutDate =
        new(2026, 11, 27);

    // ============================================================
    // TEST DATA RECORDS
    // ============================================================

    public sealed record TestUser(
        Guid UserId,
        Guid ProfileId,
        string Email,
        string Role);

    public sealed record TestWorkoutPlan(
        Guid PlanId,
        string PlanName,
        int VersionNumber,
        Guid TrainingDayId,
        Guid RestDayId,
        Guid? EmptyTrainingDayId,
        Guid StrengthExerciseId,
        Guid CardioExerciseId,
        Guid StrengthPlanExerciseId,
        Guid CardioPlanExerciseId);

    public sealed record AssignedMember(
        TestUser Member,
        TestWorkoutPlan Plan,
        Guid AssignmentId);

    private sealed class LoginResponse
    {
        public string AccessToken { get; set; } =
            string.Empty;
    }

    // ============================================================
    // APPLICATION FACTORY
    // ============================================================

    public static FitnessPlatformWebApplicationFactory
        CreateFactory(DateTimeOffset? utcNow = null)
    {
        var timeProvider =
            new WorkoutSessionTestTimeProvider(
                utcNow ?? DefaultUtcNow);

        return new FitnessPlatformWebApplicationFactory(
            timeProvider);
    }

    // ============================================================
    // HTTP CLIENT
    // ============================================================

    public static HttpClient CreateClient(
        FitnessPlatformWebApplicationFactory factory)
    {
        return factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
    }

    // ============================================================
    // IDENTITY USER CREATION
    // ============================================================

    public static async Task<TestUser> CreateUserAsync(
        FitnessPlatformWebApplicationFactory factory,
        string role = "Member",
        bool createProfile = true)
    {
        await TestIdentitySeeder.SeedAsync(
            factory.Services);

        using var scope =
            factory.Services.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var email =
            $"workout-{role.ToLowerInvariant()}-{Guid.NewGuid():N}@fitnessplatform.local";

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult =
            await userManager.CreateAsync(
                user,
                TestIdentitySeeder.TestPassword);

        Assert.True(
            createResult.Succeeded,
            string.Join(
                ", ",
                createResult.Errors.Select(
                    x => x.Description)));

        var roleResult =
            await userManager.AddToRoleAsync(
                user,
                role);

        Assert.True(
            roleResult.Succeeded,
            string.Join(
                ", ",
                roleResult.Errors.Select(
                    x => x.Description)));

        if (!createProfile)
        {
            return new TestUser(
                user.Id,
                Guid.Empty,
                email,
                role);
        }

        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FirstName = "Workout",
            LastName = "IntegrationTest",
            DateOfBirth = new DateOnly(1990, 1, 1),
            HeightCm = 175,
            WeightKg = 80,
            CountryCode = "AE",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.UserProfiles.Add(profile);

        await db.SaveChangesAsync();

        return new TestUser(
            user.Id,
            profile.Id,
            email,
            role);
    }

    // ============================================================
    // ROLE-SPECIFIC USERS
    // ============================================================

    public static Task<TestUser> CreateMemberAsync(
        FitnessPlatformWebApplicationFactory factory,
        bool createProfile = true)
    {
        return CreateUserAsync(
            factory,
            "Member",
            createProfile);
    }

    public static Task<TestUser> CreateAdminAsync(
        FitnessPlatformWebApplicationFactory factory)
    {
        return CreateUserAsync(
            factory,
            "Admin",
            createProfile: false);
    }

    public static Task<TestUser> CreateTrainerAsync(
        FitnessPlatformWebApplicationFactory factory)
    {
        return CreateUserAsync(
            factory,
            "Trainer",
            createProfile: false);
    }

    // ============================================================
    // AUTHENTICATION
    // ============================================================

    public static async Task<string> AuthenticateAsync(
        HttpClient client,
        string email)
    {
        client.DefaultRequestHeaders.Authorization = null;

        using var response =
            await client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    Email = email,
                    Password =
                        TestIdentitySeeder.TestPassword
                });

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var login = await response.Content
            .ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(login);

        Assert.False(
            string.IsNullOrWhiteSpace(
                login.AccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        return login.AccessToken;
    }

    // ============================================================
    // WORKOUT PLAN CREATION
    // ============================================================

    public static async Task<TestWorkoutPlan>
        CreateWorkoutPlanAsync(
            FitnessPlatformWebApplicationFactory factory,
            bool includeEmptyTrainingDay = false,
            string reviewStatus = "Approved",
            bool isActive = true,
            bool isLatestVersion = true)
    {
        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var utcNow = DefaultUtcNow.UtcDateTime;

        var plan = new WorkoutPlan
        {
            Id = Guid.NewGuid(),

            Name =
                $"Workout Session Test {Guid.NewGuid():N}",

            Description =
                "Workout execution integration test plan.",

            GoalCode = "WEIGHT_LOSS",
            ExperienceLevel = "Beginner",
            TrainingLocation = "Gym",

            DaysPerWeek = 3,
            DurationWeeks = 12,

            ReviewStatus = reviewStatus,
            IsActive = isActive,

            VersionNumber = 1,
            VersionGroupId = Guid.NewGuid(),
            IsLatestVersion = isLatestVersion,

            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        // --------------------------------------------------------
        // Strength exercise master
        // --------------------------------------------------------

        var strengthExercise = new Exercise
        {
            Id = Guid.NewGuid(),
            Code = $"EX-{Guid.NewGuid():N}",

            Name = "Integration Test Bench Press",
            Description = "Test strength exercise.",

            MuscleGroup = "Chest",
            Equipment = "Barbell",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",

            Instructions =
                "Perform controlled repetitions.",

            ReviewStatus = "Approved",
            IsActive = true
        };

        // --------------------------------------------------------
        // Cardio exercise master
        // --------------------------------------------------------

        var cardioExercise = new Exercise
        {
            Id = Guid.NewGuid(),
            Code = $"EX-{Guid.NewGuid():N}",

            Name = "Integration Test Treadmill Running",
            Description = "Test cardio exercise.",

            MuscleGroup = "Cardio",
            Equipment = "Treadmill",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",

            Instructions =
                "Maintain a steady running pace.",

            ReviewStatus = "Approved",
            IsActive = true
        };

        // --------------------------------------------------------
        // Training day
        // --------------------------------------------------------

        var trainingDay = new WorkoutPlanDay
        {
            Id = Guid.NewGuid(),
            WorkoutPlanId = plan.Id,

            DayNumber = 1,
            Name = "Strength and Cardio",

            Description =
                "Combined strength and cardio training.",

            IsRestDay = false,
            EstimatedDurationMinutes = 60
        };

        // --------------------------------------------------------
        // Rest day
        // --------------------------------------------------------

        var restDay = new WorkoutPlanDay
        {
            Id = Guid.NewGuid(),
            WorkoutPlanId = plan.Id,

            DayNumber = 2,
            Name = "Rest Day",
            Description = "Recovery day.",

            IsRestDay = true,
            EstimatedDurationMinutes = null
        };

        // --------------------------------------------------------
        // Optional empty training day
        // --------------------------------------------------------

        WorkoutPlanDay? emptyTrainingDay = null;

        if (includeEmptyTrainingDay)
        {
            emptyTrainingDay = new WorkoutPlanDay
            {
                Id = Guid.NewGuid(),
                WorkoutPlanId = plan.Id,

                DayNumber = 3,
                Name = "Empty Training Day",

                Description =
                    "Defensive validation test fixture.",

                IsRestDay = false,
                EstimatedDurationMinutes = 30
            };
        }

        // --------------------------------------------------------
        // Planned strength exercise
        // --------------------------------------------------------

        var strengthPlanExercise =
            new WorkoutPlanExercise
            {
                Id = Guid.NewGuid(),

                WorkoutPlanDayId = trainingDay.Id,
                ExerciseId = strengthExercise.Id,

                ExerciseOrder = 1,

                Sets = 3,
                Reps = 12,
                RestSeconds = 60,
                DurationSeconds = null,

                Notes = "Maintain proper form."
            };

        // --------------------------------------------------------
        // Planned cardio exercise
        // --------------------------------------------------------

        var cardioPlanExercise =
            new WorkoutPlanExercise
            {
                Id = Guid.NewGuid(),

                WorkoutPlanDayId = trainingDay.Id,
                ExerciseId = cardioExercise.Id,

                ExerciseOrder = 2,

                Sets = 1,
                Reps = 0,
                RestSeconds = 0,
                DurationSeconds = 600,

                Notes = "Maintain steady pace."
            };

        // --------------------------------------------------------
        // Persist complete workout plan
        // --------------------------------------------------------

        db.WorkoutPlans.Add(plan);

        db.Exercises.AddRange(
            strengthExercise,
            cardioExercise);

        db.WorkoutPlanDays.AddRange(
            trainingDay,
            restDay);

        if (emptyTrainingDay is not null)
        {
            db.WorkoutPlanDays.Add(
                emptyTrainingDay);
        }

        db.WorkoutPlanExercises.AddRange(
            strengthPlanExercise,
            cardioPlanExercise);

        await db.SaveChangesAsync();

        return new TestWorkoutPlan(
            plan.Id,
            plan.Name,
            plan.VersionNumber,
            trainingDay.Id,
            restDay.Id,
            emptyTrainingDay?.Id,
            strengthExercise.Id,
            cardioExercise.Id,
            strengthPlanExercise.Id,
            cardioPlanExercise.Id);
    }

    // ============================================================
    // WORKOUT PLAN ASSIGNMENT
    // ============================================================

    public static async Task<Guid> CreateWorkoutAssignmentAsync(
        FitnessPlatformWebApplicationFactory factory,
        Guid profileId,
        Guid planId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string status = "Active")
    {
        await TestIdentitySeeder.SeedAsync(
            factory.Services);

        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var admin =
            await userManager.FindByEmailAsync(
                TestIdentitySeeder.AdminEmail);

        Assert.NotNull(admin);

        var utcNow = DefaultUtcNow.UtcDateTime;

        var assignment =
            new MemberWorkoutPlanAssignment
            {
                Id = Guid.NewGuid(),

                UserProfileId = profileId,
                WorkoutPlanId = planId,

                AssignedByUserId = admin.Id,

                Status = status,

                StartDate =
                    startDate ??
                    DefaultWorkoutDate.AddDays(-7),

                EndDate = endDate,

                AssignedAtUtc = utcNow,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow,

                DeactivatedAtUtc =
                    status == "Inactive"
                        ? utcNow
                        : null,

                Notes =
                    "Workout session integration test."
            };

        db.MemberWorkoutPlanAssignments.Add(
            assignment);

        await db.SaveChangesAsync();

        return assignment.Id;
    }

    // ============================================================
    // PREPARE COMPLETE AUTHENTICATED MEMBER
    // ============================================================

    public static async Task<AssignedMember>
        PrepareAssignedMemberAsync(
            FitnessPlatformWebApplicationFactory factory,
            HttpClient memberClient,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            string assignmentStatus = "Active",
            bool includeEmptyTrainingDay = false)
    {
        var member =
            await CreateMemberAsync(factory);

        var plan =
            await CreateWorkoutPlanAsync(
                factory,
                includeEmptyTrainingDay);

        var assignmentId =
            await CreateWorkoutAssignmentAsync(
                factory,
                member.ProfileId,
                plan.PlanId,
                startDate,
                endDate,
                assignmentStatus);

        await AuthenticateAsync(
            memberClient,
            member.Email);

        return new AssignedMember(
            member,
            plan,
            assignmentId);
    }

    // ============================================================
    // START WORKOUT REQUEST
    // ============================================================

    public static StartWorkoutSessionRequest
        CreateStartRequest(
            AssignedMember fixture,
            Guid? workoutDayId = null,
            string timeZoneId = DefaultTimeZoneId,
            string? notes = null)
    {
        return new StartWorkoutSessionRequest
        {
            MemberWorkoutPlanAssignmentId =
                fixture.AssignmentId,

            WorkoutPlanDayId =
                workoutDayId ??
                fixture.Plan.TrainingDayId,

            TimeZoneId = timeZoneId,
            Notes = notes
        };
    }

    // ============================================================
    // RETRIEVE PERSISTED SESSION
    // ============================================================

    public static async Task<WorkoutSession?>
        GetPersistedSessionAsync(
            FitnessPlatformWebApplicationFactory factory,
            Guid sessionId)
    {
        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        return await db.WorkoutSessions
            .AsNoTracking()
            .AsSingleQuery()
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .SingleOrDefaultAsync(
                x => x.Id == sessionId);
    }

    // ============================================================
    // COUNT ALL MEMBER SESSIONS
    // ============================================================

    public static async Task<int> CountSessionsAsync(
        FitnessPlatformWebApplicationFactory factory,
        Guid profileId)
    {
        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        return await db.WorkoutSessions
            .AsNoTracking()
            .CountAsync(
                x => x.UserProfileId == profileId);
    }

    // ============================================================
    // COUNT UNFINISHED MEMBER SESSIONS
    // ============================================================

    public static async Task<int>
        CountUnfinishedSessionsAsync(
            FitnessPlatformWebApplicationFactory factory,
            Guid profileId)
    {
        using var scope =
            factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        return await db.WorkoutSessions
            .AsNoTracking()
            .CountAsync(
                x => x.UserProfileId == profileId &&
                    (
                        x.Status == "InProgress" ||
                        x.Status == "Paused"
                    ));
    }
}