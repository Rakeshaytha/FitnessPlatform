
using FitnessPlatform.Contracts.Exercises;
using FitnessPlatform.Contracts.WorkoutPlans;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;


namespace FitnessPlatform.IntegrationTests;

public class WorkoutPlanIntegrationTests
{
    [Fact]
    public async Task Admin_CanCreateAndRetrieveCompleteWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // 1. Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // 2. Create workout plan
        var planRequest = new CreateWorkoutPlanRequest
        {
            Name = $"Integration Test Plan {Guid.NewGuid():N}",
            Description = "Automated workout plan integration test.",
            GoalCode = "WEIGHT_LOSS",
            ExperienceLevel = "Beginner",
            TrainingLocation = "Gym",
            DaysPerWeek = 5,
            DurationWeeks = 12
        };

        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            planRequest);

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.NotEqual(Guid.Empty, plan.Id);
        Assert.Equal("Draft", plan.ReviewStatus);

        // 3. Create workout day
        var dayRequest = new CreateWorkoutPlanDayRequest
        {
            DayNumber = 1,
            Name = "Chest & Triceps",
            Description = "Upper-body strength workout.",
            IsRestDay = false,
            EstimatedDurationMinutes = 60
        };

        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            dayRequest);

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);
        Assert.NotEqual(Guid.Empty, day.Id);

        // 4. Create exercise
        var exerciseRequest = new SaveExerciseRequest
        {
            Code = $"BENCH-{Guid.NewGuid():N}",
            Name = "Integration Test Bench Press",
            Description = "Chest strength exercise.",
            MuscleGroup = "Chest",
            Equipment = "Barbell",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform controlled repetitions."
        };

        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            exerciseRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);
        Assert.NotEqual(Guid.Empty, exercise.Id);

        // 5. Assign exercise to workout day
        var assignmentRequest = new AddWorkoutPlanExerciseRequest
        {
            ExerciseId = exercise.Id,
            ExerciseOrder = 1,
            Sets = 3,
            Reps = 12,
            RestSeconds = 90,
            Notes = "Maintain proper form."
        };

        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            assignmentRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            assignmentResponse.StatusCode);

        // 6. Retrieve complete workout plan
        var getResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var details = await getResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(details);
        Assert.Equal(plan.Id, details.Id);
        Assert.Equal(planRequest.Name, details.Name);
        Assert.Equal("WEIGHT_LOSS", details.GoalCode);
        Assert.Equal(5, details.DaysPerWeek);
        Assert.Equal(12, details.DurationWeeks);

        // 7. Verify nested workout day
        var retrievedDay = Assert.Single(details.Days);

        Assert.Equal(day.Id, retrievedDay.Id);
        Assert.Equal(1, retrievedDay.DayNumber);
        Assert.Equal("Chest & Triceps", retrievedDay.Name);

        // 8. Verify nested exercise
        var retrievedExercise = Assert.Single(retrievedDay.Exercises);

        Assert.Equal(exercise.Id, retrievedExercise.ExerciseId);
        Assert.Equal(exercise.Code, retrievedExercise.ExerciseCode);
        Assert.Equal(exercise.Name, retrievedExercise.ExerciseName);
        Assert.Equal(1, retrievedExercise.ExerciseOrder);
        Assert.Equal(3, retrievedExercise.Sets);
        Assert.Equal(12, retrievedExercise.Reps);
        Assert.Equal(90, retrievedExercise.RestSeconds);
    }

    private sealed class LoginTestResponse
    {
        public string AccessToken { get; set; } = string.Empty;
    }


    [Fact]
    public async Task Admin_GetNonexistentWorkoutPlan_ReturnsNotFound()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Request a workout plan that does not exist
        var nonexistentPlanId = Guid.NewGuid();

        var response = await client.GetAsync(
            $"/api/admin/workout-plans/{nonexistentPlanId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    [Fact]
    public async Task UnauthenticatedUser_CannotAccessWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Do not provide an authentication token
        var planId = Guid.NewGuid();

        var response = await client.GetAsync(
            $"/api/admin/workout-plans/{planId}");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }


    [Fact]
    public async Task Member_CannotAccessAdminWorkoutPlan_ReturnsForbidden()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Login as Member
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));

        // Step 2: Attach Member JWT
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 3: Attempt to access Admin-only Workout Plan API
        var planId = Guid.NewGuid();

        var response = await client.GetAsync(
            $"/api/admin/workout-plans/{planId}");

        // Step 4: Member must be denied
        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }


    [Fact]
    public async Task Admin_CannotAssignDuplicateExerciseOrder_ReturnsConflict()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Login as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Duplicate Order Test {Guid.NewGuid():N}",
                Description = "Test duplicate exercise ordering.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 5,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Add workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"DUP-{Guid.NewGuid():N}",
                Name = "Duplicate Order Test Exercise",
                MuscleGroup = "Chest",
                Equipment = "Barbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Assign exercise with order 1
        var assignment = new AddWorkoutPlanExerciseRequest
        {
            ExerciseId = exercise.Id,
            ExerciseOrder = 1,
            Sets = 3,
            Reps = 12,
            RestSeconds = 90
        };

        var firstResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            assignment);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Step 6: Attempt duplicate order 1
        var duplicateResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            assignment);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        // Step 7: Verify only one exercise was saved
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(details);

        var savedDay = Assert.Single(details.Days);
        var savedExercise = Assert.Single(savedDay.Exercises);

        Assert.Equal(1, savedExercise.ExerciseOrder);
        Assert.Equal(exercise.Id, savedExercise.ExerciseId);
    }

    [Fact]
    public async Task Admin_CannotAssignExerciseToRestDay()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Rest Day Test {Guid.NewGuid():N}",
                Description = "Rest day validation test.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 5,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create a rest day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Rest Day",
                Description = "Recovery and rest.",
                IsRestDay = true,
                EstimatedDurationMinutes = 0
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);
        Assert.True(day.IsRestDay);

        // Step 4: Create an exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"REST-{Guid.NewGuid():N}",
                Name = "Rest Day Test Exercise",
                MuscleGroup = "Chest",
                Equipment = "Barbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Attempt to assign exercise to rest day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 90
            });

        // Step 6: Verify the request is rejected
        Assert.Equal(
            HttpStatusCode.BadRequest,
            assignmentResponse.StatusCode);

        // Step 7: Verify no exercise was assigned
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(details);

        var savedDay = Assert.Single(details.Days);

        Assert.True(savedDay.IsRestDay);
        Assert.Empty(savedDay.Exercises);
    }

    [Fact]
    public async Task Admin_CannotAssignExerciseWithZeroSets()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Invalid Sets Test {Guid.NewGuid():N}",
                Description = "Validation integration test.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 5,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Strength Workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Attempt invalid assignment with zero sets
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = Guid.NewGuid(),
                ExerciseOrder = 1,
                Sets = 0,
                Reps = 12,
                RestSeconds = 90
            });

        // Step 5: Verify invalid request is rejected
        Assert.Equal(
            HttpStatusCode.BadRequest,
            assignmentResponse.StatusCode);

        // Step 6: Verify no exercise was assigned
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(details);

        var savedDay = Assert.Single(details.Days);

        Assert.Empty(savedDay.Exercises);
    }

    [Theory]
    [InlineData(0, 3, 12, 90, null)]
    [InlineData(1, 21, 12, 90, null)]
    [InlineData(1, 3, -1, 90, null)]
    [InlineData(1, 3, 501, 90, null)]
    [InlineData(1, 3, 12, 3601, null)]
    [InlineData(1, 3, 12, 90, 14401)]
    public async Task Admin_InvalidExerciseAssignment_ReturnsBadRequest(
        int exerciseOrder,
        int sets,
        int reps,
        int restSeconds,
        int? durationSeconds)
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Validation Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 5,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Validation Test Day",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Submit invalid exercise assignment
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = Guid.NewGuid(),
                ExerciseOrder = exerciseOrder,
                Sets = sets,
                Reps = reps,
                RestSeconds = restSeconds,
                DurationSeconds = durationSeconds
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            assignmentResponse.StatusCode);

        // Verify no assignment was saved
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(details);

        var savedDay = Assert.Single(details.Days);

        Assert.Empty(savedDay.Exercises);
    }

    [Fact]
    public async Task Admin_CannotAssignNonexistentExercise_ReturnsNotFound()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Missing Exercise Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 5,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create a workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Strength Workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Assign a nonexistent exercise
        var missingExerciseId = Guid.NewGuid();

        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = missingExerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 90
            });

        // Step 5: Verify HTTP 404
        Assert.Equal(
            HttpStatusCode.NotFound,
            assignmentResponse.StatusCode);

        // Step 6: Verify no assignment was saved
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(details);

        var savedDay = Assert.Single(details.Days);

        Assert.Empty(savedDay.Exercises);
    }

    [Fact]
    public async Task Admin_CannotAssignExerciseToDayFromAnotherPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create Workout Plan A
        var planAResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Workout Plan A {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 5,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planAResponse.StatusCode);

        var planA = await planAResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(planA);

        // Step 3: Create Workout Plan B
        var planBResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Workout Plan B {Guid.NewGuid():N}",
                GoalCode = "MUSCLE_GAIN",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 4,
                DurationWeeks = 8
            });

        Assert.Equal(HttpStatusCode.Created, planBResponse.StatusCode);

        var planB = await planBResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(planB);
        Assert.NotEqual(planA.Id, planB.Id);

        // Step 4: Create workout day under Plan B
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{planB.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Plan B Strength Day",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);
        Assert.Equal(planB.Id, day.WorkoutPlanId);

        // Step 5: Attempt assignment using Plan A and Plan B's day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{planA.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = Guid.NewGuid(),
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 90
            });

        // Step 6: Verify cross-plan assignment is rejected
        Assert.Equal(
            HttpStatusCode.NotFound,
            assignmentResponse.StatusCode);

        // Step 7: Verify Plan B day has no exercises
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{planB.Id}");

        Assert.NotNull(details);

        var savedDay = Assert.Single(details.Days);

        Assert.Equal(day.Id, savedDay.Id);
        Assert.Empty(savedDay.Exercises);
    }

    [Fact]
    public async Task Admin_CannotCreateDuplicateWorkoutDay_ReturnsConflict()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Duplicate Day Test {Guid.NewGuid():N}",
                Description = "Verify duplicate workout day validation.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 5,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create Day 1
        var dayRequest = new CreateWorkoutPlanDayRequest
        {
            DayNumber = 1,
            Name = "Chest & Triceps",
            Description = "Upper-body workout.",
            IsRestDay = false,
            EstimatedDurationMinutes = 60
        };

        var firstResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            dayRequest);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Step 4: Attempt to create Day 1 again
        var duplicateResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Leg Day",
                Description = "Duplicate day number.",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        // Step 5: Verify duplicate is rejected
        Assert.Equal(
            HttpStatusCode.Conflict,
            duplicateResponse.StatusCode);

        // Step 6: Verify only one workout day exists
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(details);

        var savedDay = Assert.Single(details.Days);

        Assert.Equal(1, savedDay.DayNumber);
        Assert.Equal("Chest & Triceps", savedDay.Name);
    }

    [Fact]
    public async Task Admin_CanSubmitWorkoutPlanForReview()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Review Submission Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Create training day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise in Exercise Master
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"REVIEW-{Guid.NewGuid():N}",
                Name = "Review Test Squat",
                Description = "Exercise for review submission testing",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise to training day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // Step 7: Verify persisted review status
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(details);
        Assert.Equal("In Review", details.ReviewStatus);

        var savedDay = Assert.Single(details.Days);

        Assert.Single(savedDay.Exercises);
    }

    [Fact]
    public async Task Admin_CannotAddExerciseToInReviewPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Review Submission Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Create training day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise in Exercise Master
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"REVIEW-{Guid.NewGuid():N}",
                Name = "Review Test Squat",
                Description = "Exercise for review submission testing",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise to training day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);


        // Step 7: Attempt to add another exercise after submission
        var blockedExerciseResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 2,
                Sets = 3,
                Reps = 10,
                RestSeconds = 60
            });

        Assert.Equal(
            HttpStatusCode.Conflict,
            blockedExerciseResponse.StatusCode);

        // Step 8: Verify the workout plan remains unchanged
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(details);
        Assert.Equal("In Review", details.ReviewStatus);

        var savedDay = Assert.Single(details.Days);
        var savedExercise = Assert.Single(savedDay.Exercises);

        Assert.Equal(1, savedExercise.ExerciseOrder);

    }

    [Fact]
    public async Task Admin_CannotAddWorkoutDayToInReviewPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Review Submission Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Create training day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise in Exercise Master
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"REVIEW-{Guid.NewGuid():N}",
                Name = "Review Test Squat",
                Description = "Exercise for review submission testing",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise to training day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);


        // Step 7: Attempt to add another day after submission
        var blockedDayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 2,
                Name = "Additional Training Day",
                IsRestDay = false,
                EstimatedDurationMinutes = 45
            });

        Assert.Equal(
            HttpStatusCode.Conflict,
            blockedDayResponse.StatusCode);

        // Step 8: Verify the plan remains unchanged
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(details);
        Assert.Equal("In Review", details.ReviewStatus);

        var savedDay = Assert.Single(details.Days);
        Assert.Equal(1, savedDay.DayNumber);
        Assert.Single(savedDay.Exercises);

    }

    [Fact]
    public async Task Admin_CannotUpdateInReviewWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Review Submission Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Create training day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise in Exercise Master
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"REVIEW-{Guid.NewGuid():N}",
                Name = "Review Test Squat",
                Description = "Exercise for review submission testing",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise to training day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // Verify workout plan is now In Review
        var reviewPlan = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewPlan);
        Assert.Equal("In Review", reviewPlan.ReviewStatus);

        // Step 6: Attempt to update a workout plan under review
        var updateRequest = new UpdateWorkoutPlanRequest
        {
            Name = "Unauthorized Updated Plan",
            Description = "This update should be rejected.",
            GoalCode = "MUSCLE_GAIN",
            ExperienceLevel = "Advanced",
            TrainingLocation = "Home",
            DaysPerWeek = 5,
            DurationWeeks = 16
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}",
            updateRequest);

        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);

        // Step 7: Retrieve the workout plan
        var savedPlan = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        // Step 8: Verify the update was rejected
        Assert.Equal(plan.Name, savedPlan.Name);
        Assert.Equal(plan.Description, savedPlan.Description);
        Assert.Equal(plan.GoalCode, savedPlan.GoalCode);
        Assert.Equal(plan.ExperienceLevel, savedPlan.ExperienceLevel);
        Assert.Equal(plan.TrainingLocation, savedPlan.TrainingLocation);
        Assert.Equal(plan.DaysPerWeek, savedPlan.DaysPerWeek);
        Assert.Equal(plan.DurationWeeks, savedPlan.DurationWeeks);

        // Step 9: Verify the plan remains under review
        Assert.Equal("In Review", savedPlan.ReviewStatus);

    }


    [Fact]
    public async Task Admin_CannotSubmitWorkoutPlanWithoutDays()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan without days
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Empty Plan Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Attempt submission without workout days
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            submitResponse.StatusCode);

        // Step 4: Verify review status remains Draft
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(details);
        Assert.Equal("Draft", details.ReviewStatus);
        Assert.Empty(details.Days);
    }

    [Fact]
    public async Task Admin_CannotSubmitWorkoutPlanWithEmptyTrainingDay()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Empty Training Day Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Add training day without exercises
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest and Triceps",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        // Step 4: Attempt to submit incomplete workout plan
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            submitResponse.StatusCode);

        // Step 5: Verify status remains Draft
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(details);
        Assert.Equal("Draft", details.ReviewStatus);

        var savedDay = Assert.Single(details.Days);

        Assert.False(savedDay.IsRestDay);
        Assert.Empty(savedDay.Exercises);
    }

    [Fact]
    public async Task Admin_CannotSubmitWorkoutPlanTwice()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Duplicate Review Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"DUP-REVIEW-{Guid.NewGuid():N}",
                Name = "Duplicate Review Test Exercise",
                Description = "Exercise for duplicate submission test",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        // Step 6: First submission
        var firstSubmitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(HttpStatusCode.OK, firstSubmitResponse.StatusCode);

        // Step 7: Duplicate submission
        var secondSubmitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(
            HttpStatusCode.Conflict,
            secondSubmitResponse.StatusCode);

        // Step 8: Verify status remains In Review
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(details);
        Assert.Equal("In Review", details.ReviewStatus);
    }

    [Fact]
    public async Task Admin_CanApproveWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Approval Test {Guid.NewGuid():N}",
                Description = "Workout plan approval integration test.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(
            HttpStatusCode.Created,
            planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Add workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Strength",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"APPROVAL-{Guid.NewGuid():N}",
                Name = "Approval Test Squat",
                Description = "Exercise for workout plan approval.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(
            HttpStatusCode.Created,
            exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // Step 7: Verify In Review status
        var reviewDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewDetails);
        Assert.Equal("In Review", reviewDetails.ReviewStatus);

        // Step 8: Approve workout plan
        var approveResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // Step 9: Verify Approved status persisted
        var approvedDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(approvedDetails);

        Assert.Equal(
            "Approved",
            approvedDetails.ReviewStatus);

        Assert.True(approvedDetails.IsActive);

        // Step 10: Verify workout day and exercise remain intact
        var savedDay = Assert.Single(approvedDetails.Days);

        Assert.Equal(day.Id, savedDay.Id);

        var savedExercise = Assert.Single(savedDay.Exercises);

        Assert.Equal(exerciseId, savedExercise.ExerciseId);
    }

    [Fact]
    public async Task Admin_CannotUpdateApprovedWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Approval Test {Guid.NewGuid():N}",
                Description = "Workout plan approval integration test.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(
            HttpStatusCode.Created,
            planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Add workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Strength",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"APPROVAL-{Guid.NewGuid():N}",
                Name = "Approval Test Squat",
                Description = "Exercise for workout plan approval.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(
            HttpStatusCode.Created,
            exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // Step 8: Verify the workout plan is Approved

        var approveResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            approveResponse.StatusCode);


        // Step 7: Verify the workout plan is Approved
        var approvedPlan = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(approvedPlan);
        Assert.Equal("Approved", approvedPlan.ReviewStatus);

        // Step 8: Attempt to update the Approved workout plan
        var updateRequest = new UpdateWorkoutPlanRequest
        {
            Name = "Unauthorized Approved Plan Update",
            Description = "This update must be blocked.",
            GoalCode = "MUSCLE_GAIN",
            ExperienceLevel = "Advanced",
            TrainingLocation = "Home",
            DaysPerWeek = 5,
            DurationWeeks = 16
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}",
            updateRequest);

        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);

        // Step 9: Retrieve the plan after the rejected update
        var savedPlan = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        // Step 10: Verify all original values remain unchanged
        Assert.Equal(approvedPlan.Name, savedPlan.Name);
        Assert.Equal(approvedPlan.Description, savedPlan.Description);
        Assert.Equal(approvedPlan.GoalCode, savedPlan.GoalCode);
        Assert.Equal(approvedPlan.ExperienceLevel, savedPlan.ExperienceLevel);
        Assert.Equal(approvedPlan.TrainingLocation, savedPlan.TrainingLocation);
        Assert.Equal(approvedPlan.DaysPerWeek, savedPlan.DaysPerWeek);
        Assert.Equal(approvedPlan.DurationWeeks, savedPlan.DurationWeeks);

        // Step 11: Verify approval status is preserved
        Assert.Equal("Approved", savedPlan.ReviewStatus);
        Assert.Equal(approvedPlan.RejectionReason, savedPlan.RejectionReason);
        Assert.Equal(approvedPlan.UpdatedAtUtc, savedPlan.UpdatedAtUtc);
    }


    [Fact]
    public async Task Admin_CannotAddWorkoutDayToApprovedPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Approval Test {Guid.NewGuid():N}",
                Description = "Workout plan approval integration test.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(
            HttpStatusCode.Created,
            planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Add workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Strength",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"APPROVAL-{Guid.NewGuid():N}",
                Name = "Approval Test Squat",
                Description = "Exercise for workout plan approval.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(
            HttpStatusCode.Created,
            exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // Step 7: Verify In Review status
        var reviewDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewDetails);
        Assert.Equal("In Review", reviewDetails.ReviewStatus);

        // Step 8: Approve workout plan
        var approveResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // Step 9: Verify Approved status persisted
        var approvedDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(approvedDetails);

        Assert.Equal(
            "Approved",
            approvedDetails.ReviewStatus);

        Assert.True(approvedDetails.IsActive);

        // Step 10: Verify workout day and exercise remain intact
        var savedDay = Assert.Single(approvedDetails.Days);

        Assert.Equal(day.Id, savedDay.Id);

        var savedExercise = Assert.Single(savedDay.Exercises);

        Assert.Equal(exerciseId, savedExercise.ExerciseId);

        // Step 11: Attempt to add another workout day to Approved plan
        var blockedDayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 2,
                Name = "Additional Strength Training",
                IsRestDay = false,
                EstimatedDurationMinutes = 45
            });

        Assert.Equal(
            HttpStatusCode.Conflict,
            blockedDayResponse.StatusCode);

        // Step 12: Verify Approved plan remains unchanged
        var finalDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(finalDetails);
        Assert.Equal("Approved", finalDetails.ReviewStatus);
        Assert.True(finalDetails.IsActive);

        var finalDay = Assert.Single(finalDetails.Days);
        Assert.Equal(day.Id, finalDay.Id);

        var finalExercise = Assert.Single(finalDay.Exercises);
        Assert.Equal(exerciseId, finalExercise.ExerciseId);
    }

    [Fact]
    public async Task Admin_CannotAddExerciseToApprovedPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Approval Test {Guid.NewGuid():N}",
                Description = "Workout plan approval integration test.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(
            HttpStatusCode.Created,
            planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Add workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Strength",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"APPROVAL-{Guid.NewGuid():N}",
                Name = "Approval Test Squat",
                Description = "Exercise for workout plan approval.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(
            HttpStatusCode.Created,
            exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // Step 7: Verify In Review status
        var reviewDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewDetails);
        Assert.Equal("In Review", reviewDetails.ReviewStatus);



        // Step 8: Approve workout plan
        var approveResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // Step 9: Verify Approved status persisted
        var approvedDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(approvedDetails);

        Assert.Equal(
            "Approved",
            approvedDetails.ReviewStatus);

        Assert.True(approvedDetails.IsActive);

        // Step 10: Verify workout day and exercise remain intact
        var savedDay = Assert.Single(approvedDetails.Days);

        Assert.Equal(day.Id, savedDay.Id);

        var savedExercise = Assert.Single(savedDay.Exercises);

        Assert.Equal(exerciseId, savedExercise.ExerciseId);

        // Step 11: Attempt to add another exercise to Approved plan
        var blockedExerciseResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 2,
                Sets = 3,
                Reps = 10,
                RestSeconds = 60
            });

        Assert.Equal(
            HttpStatusCode.Conflict,
            blockedExerciseResponse.StatusCode);

        // Step 12: Verify Approved plan remains unchanged
        var finalDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(finalDetails);
        Assert.Equal("Approved", finalDetails.ReviewStatus);
        Assert.True(finalDetails.IsActive);

        var finalDay = Assert.Single(finalDetails.Days);
        Assert.Equal(day.Id, finalDay.Id);

        var finalExercise = Assert.Single(finalDay.Exercises);
        Assert.Equal(exerciseId, finalExercise.ExerciseId);
        Assert.Equal(1, finalExercise.ExerciseOrder);
    }

    [Fact]
    public async Task Admin_CanRejectWorkoutPlan_WithReason()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Approval Test {Guid.NewGuid():N}",
                Description = "Workout plan approval integration test.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(
            HttpStatusCode.Created,
            planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Add workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Strength",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"APPROVAL-{Guid.NewGuid():N}",
                Name = "Approval Test Squat",
                Description = "Exercise for workout plan approval.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(
            HttpStatusCode.Created,
            exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // Step 7: Verify In Review status
        var reviewDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewDetails);
        Assert.Equal("In Review", reviewDetails.ReviewStatus);

        // Step 8: Reject workout plan with reason
        const string reason = "Workout plan requires additional recovery days.";
        var rejectResponse = await client.PostAsJsonAsync($"/api/admin/workout-plans/{plan.Id}/reject", new RejectWorkoutPlanRequest { Reason = reason });
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>($"/api/admin/workout-plans/{plan.Id}");
        Assert.NotNull(details);
        Assert.Equal("Rejected", details.ReviewStatus);
        Assert.Equal(reason, details.RejectionReason);
    }

    [Fact]
    public async Task Admin_CanUpdateRejectedWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Approval Test {Guid.NewGuid():N}",
                Description = "Workout plan approval integration test.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(
            HttpStatusCode.Created,
            planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Add workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Strength",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"APPROVAL-{Guid.NewGuid():N}",
                Name = "Approval Test Squat",
                Description = "Exercise for workout plan approval.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(
            HttpStatusCode.Created,
            exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // Step 7: Verify In Review status
        var reviewDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewDetails);
        Assert.Equal("In Review", reviewDetails.ReviewStatus);

        // Step 8: Reject workout plan with reason
        const string reason = "Workout plan requires additional recovery days.";
        var rejectResponse = await client.PostAsJsonAsync($"/api/admin/workout-plans/{plan.Id}/reject", new RejectWorkoutPlanRequest { Reason = reason });
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>($"/api/admin/workout-plans/{plan.Id}");
        Assert.NotNull(details);
        Assert.Equal("Rejected", details.ReviewStatus);
        Assert.Equal(reason, details.RejectionReason);

        // Step 9: Update the rejected workout plan
        var updateRequest = new UpdateWorkoutPlanRequest
        {
            Name = "Updated Rejected Workout Plan",
            Description = "Updated after administrator review",
            GoalCode = "MUSCLE_GAIN",
            ExperienceLevel = "Intermediate",
            TrainingLocation = "Home",
            DaysPerWeek = 4,
            DurationWeeks = 10
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}",
            updateRequest);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updatedPlan = await updateResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(updatedPlan);

        // Step 10: Verify the update response
        Assert.Equal(updateRequest.Name, updatedPlan.Name);
        Assert.Equal(updateRequest.Description, updatedPlan.Description);
        Assert.Equal(updateRequest.GoalCode, updatedPlan.GoalCode);
        Assert.Equal(updateRequest.ExperienceLevel, updatedPlan.ExperienceLevel);
        Assert.Equal(updateRequest.TrainingLocation, updatedPlan.TrainingLocation);
        Assert.Equal(updateRequest.DaysPerWeek, updatedPlan.DaysPerWeek);
        Assert.Equal(updateRequest.DurationWeeks, updatedPlan.DurationWeeks);

        Assert.Equal("Rejected", updatedPlan.ReviewStatus);
        Assert.Equal(reason, updatedPlan.RejectionReason);

        // Step 11: Retrieve the plan and verify persistence
        var savedPlan = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        Assert.Equal(updateRequest.Name, savedPlan.Name);
        Assert.Equal(updateRequest.Description, savedPlan.Description);
        Assert.Equal(updateRequest.GoalCode, savedPlan.GoalCode);
        Assert.Equal(updateRequest.ExperienceLevel, savedPlan.ExperienceLevel);
        Assert.Equal(updateRequest.TrainingLocation, savedPlan.TrainingLocation);
        Assert.Equal(updateRequest.DaysPerWeek, savedPlan.DaysPerWeek);
        Assert.Equal(updateRequest.DurationWeeks, savedPlan.DurationWeeks);

        Assert.Equal("Rejected", savedPlan.ReviewStatus);
        Assert.Equal(reason, savedPlan.RejectionReason);
    }


    [Fact]
    public async Task Admin_CanAddWorkoutDayToRejectedPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Approval Test {Guid.NewGuid():N}",
                Description = "Workout plan approval integration test.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(
            HttpStatusCode.Created,
            planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Add workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Strength",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"APPROVAL-{Guid.NewGuid():N}",
                Name = "Approval Test Squat",
                Description = "Exercise for workout plan approval.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(
            HttpStatusCode.Created,
            exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // Step 7: Verify In Review status
        var reviewDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewDetails);
        Assert.Equal("In Review", reviewDetails.ReviewStatus);

        // Step 8: Reject workout plan with reason
        const string reason = "Workout plan requires additional recovery days.";
        var rejectResponse = await client.PostAsJsonAsync($"/api/admin/workout-plans/{plan.Id}/reject", new RejectWorkoutPlanRequest { Reason = reason });
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>($"/api/admin/workout-plans/{plan.Id}");
        Assert.NotNull(details);
        Assert.Equal("Rejected", details.ReviewStatus);
        Assert.Equal(reason, details.RejectionReason);

        // Step 9: Add another workout day after rejection
        var newDayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 2,
                Name = "Additional Recovery Day",
                Description = "Recovery day added after review feedback.",
                IsRestDay = true,
                EstimatedDurationMinutes = 30
            });

        Assert.Equal(
            HttpStatusCode.Created,
            newDayResponse.StatusCode);

        var newDay = await newDayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(newDay);
        Assert.Equal(2, newDay.DayNumber);
        Assert.True(newDay.IsRestDay);

        // Step 10: Verify rejected plan retains its status and reason
        var updatedDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(updatedDetails);
        Assert.Equal("Rejected", updatedDetails.ReviewStatus);
        Assert.Equal(reason, updatedDetails.RejectionReason);

        Assert.Equal(2, updatedDetails.Days.Count());

        Assert.Contains(
            updatedDetails.Days,
            d => d.Id == newDay.Id && d.DayNumber == 2);

        Assert.Contains(
            updatedDetails.Days,
            d => d.Id == day.Id && d.DayNumber == 1);
    }

    [Fact]
    public async Task Admin_CanAddExerciseToRejectedPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Approval Test {Guid.NewGuid():N}",
                Description = "Workout plan approval integration test.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(
            HttpStatusCode.Created,
            planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Add workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Strength",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"APPROVAL-{Guid.NewGuid():N}",
                Name = "Approval Test Squat",
                Description = "Exercise for workout plan approval.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(
            HttpStatusCode.Created,
            exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // Step 7: Verify In Review status
        var reviewDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewDetails);
        Assert.Equal("In Review", reviewDetails.ReviewStatus);

        // Step 8: Reject workout plan with reason
        const string reason = "Workout plan requires additional recovery days.";
        var rejectResponse = await client.PostAsJsonAsync($"/api/admin/workout-plans/{plan.Id}/reject", new RejectWorkoutPlanRequest { Reason = reason });
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>($"/api/admin/workout-plans/{plan.Id}");
        Assert.NotNull(details);
        Assert.Equal("Rejected", details.ReviewStatus);
        Assert.Equal(reason, details.RejectionReason);

        // Step 9: Add another exercise after rejection
        var newExerciseResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 2,
                Sets = 3,
                Reps = 10,
                RestSeconds = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            newExerciseResponse.StatusCode);

        // Step 10: Verify both exercises are saved
        var updatedDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(updatedDetails);
        Assert.Equal("Rejected", updatedDetails.ReviewStatus);
        Assert.Equal(reason, updatedDetails.RejectionReason);

        var savedDay = Assert.Single(updatedDetails.Days);

        Assert.Equal(day.Id, savedDay.Id);
        Assert.Equal(2, savedDay.Exercises.Count());

        Assert.Contains(
            savedDay.Exercises,
            e => e.ExerciseId == exerciseId &&
                 e.ExerciseOrder == 1);

        Assert.Contains(
            savedDay.Exercises,
            e => e.ExerciseId == exerciseId &&
                 e.ExerciseOrder == 2);
    }


    [Fact]
    public async Task Admin_CannotApproveDraftWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Draft Approval Test {Guid.NewGuid():N}",
                Description = "Verify Draft plans cannot be approved.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(
            HttpStatusCode.Created,
            planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Attempt to approve Draft workout plan
        var approveResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        // Step 4: Verify approval is rejected
        Assert.Equal(
            HttpStatusCode.Conflict,
            approveResponse.StatusCode);

        // Step 5: Verify review status remains Draft
        var details =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(details);
        Assert.Equal("Draft", details.ReviewStatus);
    }

    [Fact]
    public async Task Admin_CannotRejectWorkoutPlanWithoutReason()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Approval Test {Guid.NewGuid():N}",
                Description = "Workout plan approval integration test.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(
            HttpStatusCode.Created,
            planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Add workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Strength",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"APPROVAL-{Guid.NewGuid():N}",
                Name = "Approval Test Squat",
                Description = "Exercise for workout plan approval.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(
            HttpStatusCode.Created,
            exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // Step 7: Verify In Review status
        var reviewDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewDetails);
        Assert.Equal("In Review", reviewDetails.ReviewStatus);

        // Step 8: Attempt rejection without a reason
        var rejectResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/reject",
            new RejectWorkoutPlanRequest
            {
                Reason = ""
            });

        Assert.Equal(HttpStatusCode.BadRequest, rejectResponse.StatusCode);

        // Step 9: Verify workout plan remains In Review
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(details);
        Assert.Equal("In Review", details.ReviewStatus);
        Assert.Null(details.RejectionReason);
    }


    [Fact]
    public async Task Admin_CannotApproveWorkoutPlanTwice()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Duplicate Approval Test {Guid.NewGuid():N}",
                Description = "Verify approved plans cannot be approved again.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Add workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Strength",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"REAPPROVE-{Guid.NewGuid():N}",
                Name = "Reapproval Test Squat",
                Description = "Exercise for duplicate approval testing.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        // Step 6: Submit for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // Step 7: First approval
        var firstApprovalResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        Assert.Equal(HttpStatusCode.OK, firstApprovalResponse.StatusCode);

        // Step 8: Attempt duplicate approval
        var secondApprovalResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        Assert.Equal(
            HttpStatusCode.Conflict,
            secondApprovalResponse.StatusCode);

        // Step 9: Verify status remains Approved
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(details);

        Assert.Equal("Approved", details.ReviewStatus);

        // Step 10: Verify workout data remains intact
        var savedDay = Assert.Single(details.Days);

        Assert.Equal(day.Id, savedDay.Id);

        var savedExercise = Assert.Single(savedDay.Exercises);

        Assert.Equal(exerciseId, savedExercise.ExerciseId);
    }

    [Fact]
    public async Task Admin_CannotRejectDraftWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan in Draft status
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Draft Rejection Test {Guid.NewGuid():N}",
                Description = "Verify Draft plans cannot be rejected.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Attempt to reject Draft plan
        var rejectResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/reject",
            new RejectWorkoutPlanRequest
            {
                Reason = "Workout plan needs improvements."
            });

        Assert.Equal(HttpStatusCode.Conflict, rejectResponse.StatusCode);

        // Step 4: Verify plan remains Draft
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(details);
        Assert.Equal("Draft", details.ReviewStatus);
        Assert.Null(details.RejectionReason);
    }

    [Fact]
    public async Task Admin_CannotRejectApprovedWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Approval Test {Guid.NewGuid():N}",
                Description = "Workout plan approval integration test.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(
            HttpStatusCode.Created,
            planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Add workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Strength",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"APPROVAL-{Guid.NewGuid():N}",
                Name = "Approval Test Squat",
                Description = "Exercise for workout plan approval.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(
            HttpStatusCode.Created,
            exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // Step 7: Verify In Review status
        var reviewDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewDetails);
        Assert.Equal("In Review", reviewDetails.ReviewStatus);

        // Step 8: Approve workout plan
        var approveResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // Step 9: Verify Approved status persisted
        var approvedDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(approvedDetails);

        Assert.Equal(
            "Approved",
            approvedDetails.ReviewStatus);

        Assert.True(approvedDetails.IsActive);

        // Step 10: Verify workout day and exercise remain intact
        var savedDay = Assert.Single(approvedDetails.Days);

        Assert.Equal(day.Id, savedDay.Id);

        var savedExercise = Assert.Single(savedDay.Exercises);

        Assert.Equal(exerciseId, savedExercise.ExerciseId);

        // Attempt to reject an already approved workout plan
        var rejectResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/reject",
            new RejectWorkoutPlanRequest
            {
                Reason = "Attempting to reject an approved plan."
            });

        Assert.Equal(HttpStatusCode.Conflict, rejectResponse.StatusCode);

        // Verify that the approved status remains unchanged
        var finalDetails = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(finalDetails);
        Assert.Equal("Approved", finalDetails.ReviewStatus);
        Assert.Null(finalDetails.RejectionReason);
        Assert.True(finalDetails.IsActive);
    }

    [Fact]
    public async Task Admin_CanResubmitRejectedWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Approval Test {Guid.NewGuid():N}",
                Description = "Workout plan approval integration test.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(
            HttpStatusCode.Created,
            planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Add workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Strength",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"APPROVAL-{Guid.NewGuid():N}",
                Name = "Approval Test Squat",
                Description = "Exercise for workout plan approval.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(
            HttpStatusCode.Created,
            exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // Step 7: Verify In Review status
        var reviewDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewDetails);
        Assert.Equal("In Review", reviewDetails.ReviewStatus);

        // Step 8: Reject workout plan with reason
        const string reason = "Workout plan requires additional recovery days.";
        var rejectResponse = await client.PostAsJsonAsync($"/api/admin/workout-plans/{plan.Id}/reject", new RejectWorkoutPlanRequest { Reason = reason });
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        var details = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>($"/api/admin/workout-plans/{plan.Id}");
        Assert.NotNull(details);
        Assert.Equal("Rejected", details.ReviewStatus);
        Assert.Equal(reason, details.RejectionReason);

        // Step 10: Resubmit the rejected workout plan
        var resubmitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(HttpStatusCode.OK, resubmitResponse.StatusCode);

        // Step 11: Verify the plan returned to In Review
        var resubmittedDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(resubmittedDetails);

        Assert.Equal("In Review", resubmittedDetails.ReviewStatus);

        // Step 12: Verify the previous rejection reason was cleared
        Assert.Null(resubmittedDetails.RejectionReason);

        // Step 13: Verify workout days and exercises were preserved
        var savedDay = Assert.Single(resubmittedDetails.Days);

        Assert.Equal(day.Id, savedDay.Id);

        var savedExercise = Assert.Single(savedDay.Exercises);

        Assert.Equal(exerciseId, savedExercise.ExerciseId);
    }

    [Fact]
    public async Task Admin_CannotResubmitApprovedWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Approval Test {Guid.NewGuid():N}",
                Description = "Workout plan approval integration test.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(
            HttpStatusCode.Created,
            planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Add workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Strength",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"APPROVAL-{Guid.NewGuid():N}",
                Name = "Approval Test Squat",
                Description = "Exercise for workout plan approval.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(
            HttpStatusCode.Created,
            exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // Step 7: Verify In Review status
        var reviewDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewDetails);
        Assert.Equal("In Review", reviewDetails.ReviewStatus);

        // Step 8: Approve workout plan
        var approveResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // Step 9: Verify plan is Approved before attempting resubmission
        var approvedDetails =
            await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(approvedDetails);
        Assert.Equal("Approved", approvedDetails.ReviewStatus);

        // Attempt to resubmit an already approved workout plan
        var resubmitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(HttpStatusCode.Conflict, resubmitResponse.StatusCode);

        // Verify the approved status remains unchanged
        var finalDetails = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(finalDetails);
        Assert.Equal("Approved", finalDetails.ReviewStatus);
        Assert.Null(finalDetails.RejectionReason);
        Assert.True(finalDetails.IsActive);
    }

    [Fact]
    public async Task Admin_CanUpdateDraftWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create a Draft workout plan
        var createResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Original Plan {Guid.NewGuid():N}",
                Description = "Original description",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 8
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var plan = await createResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Update the workout plan
        var updateRequest = new UpdateWorkoutPlanRequest
        {
            Name = "Updated Strength Training Plan",
            Description = "Updated workout plan description",
            GoalCode = "MUSCLE_GAIN",
            ExperienceLevel = "Intermediate",
            TrainingLocation = "Home",
            DaysPerWeek = 5,
            DurationWeeks = 12
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}",
            updateRequest);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updatedPlan = await updateResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(updatedPlan);

        // Step 4: Verify the update response
        Assert.Equal(plan.Id, updatedPlan.Id);
        Assert.Equal(updateRequest.Name, updatedPlan.Name);
        Assert.Equal(updateRequest.Description, updatedPlan.Description);
        Assert.Equal(updateRequest.GoalCode, updatedPlan.GoalCode);
        Assert.Equal(updateRequest.ExperienceLevel, updatedPlan.ExperienceLevel);
        Assert.Equal(updateRequest.TrainingLocation, updatedPlan.TrainingLocation);
        Assert.Equal(updateRequest.DaysPerWeek, updatedPlan.DaysPerWeek);
        Assert.Equal(updateRequest.DurationWeeks, updatedPlan.DurationWeeks);

        Assert.Equal("Draft", updatedPlan.ReviewStatus);
        Assert.Null(updatedPlan.RejectionReason);
        Assert.True(updatedPlan.IsActive);
        Assert.True(updatedPlan.UpdatedAtUtc >= plan.UpdatedAtUtc);

        // Step 5: Retrieve the plan and verify persistence
        var savedPlan = await client.GetFromJsonAsync<WorkoutPlanDetailResponse>(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        Assert.Equal(plan.Id, savedPlan.Id);
        Assert.Equal(updateRequest.Name, savedPlan.Name);
        Assert.Equal(updateRequest.Description, savedPlan.Description);
        Assert.Equal(updateRequest.GoalCode, savedPlan.GoalCode);
        Assert.Equal(updateRequest.ExperienceLevel, savedPlan.ExperienceLevel);
        Assert.Equal(updateRequest.TrainingLocation, savedPlan.TrainingLocation);
        Assert.Equal(updateRequest.DaysPerWeek, savedPlan.DaysPerWeek);
        Assert.Equal(updateRequest.DurationWeeks, savedPlan.DurationWeeks);

        Assert.Equal("Draft", savedPlan.ReviewStatus);
        Assert.Null(savedPlan.RejectionReason);
    }

    [Fact]
    public async Task Admin_CannotUpdateInactiveWorkoutPlan()
    {
        // Step 1: Create test client and authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create a Draft workout plan
        var createRequest = new CreateWorkoutPlanRequest
        {
            Name = "Inactive Workout Plan Test",
            Description = "Original workout plan description.",
            GoalCode = "WEIGHT_LOSS",
            ExperienceLevel = "Beginner",
            TrainingLocation = "Gym",
            DaysPerWeek = 4,
            DurationWeeks = 12
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            createRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var plan = await createResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.True(plan.IsActive);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Deactivate the workout plan
        var deactivateResponse = await client.PatchAsync(
            $"/api/admin/workout-plans/{plan.Id}/deactivate",
            null);

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        // Step 4: Verify the workout plan is inactive
        var inactivePlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(inactivePlan);
        Assert.False(inactivePlan.IsActive);
        Assert.Equal("Draft", inactivePlan.ReviewStatus);

        // Step 5: Attempt to update the inactive workout plan
        var updateRequest = new UpdateWorkoutPlanRequest
        {
            Name = "Modified Inactive Workout Plan",
            Description = "This update must be blocked.",
            GoalCode = "MUSCLE_GAIN",
            ExperienceLevel = "Advanced",
            TrainingLocation = "Home",
            DaysPerWeek = 5,
            DurationWeeks = 16
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}",
            updateRequest);

        // Step 6: Verify the API rejects the update
        Assert.Equal(
            HttpStatusCode.Conflict,
            updateResponse.StatusCode);

        // Step 7: Verify original values remain unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        Assert.Equal(plan.Id, savedPlan.Id);
        Assert.Equal(createRequest.Name, savedPlan.Name);
        Assert.Equal(createRequest.Description, savedPlan.Description);
        Assert.Equal(createRequest.GoalCode, savedPlan.GoalCode);
        Assert.Equal(createRequest.ExperienceLevel, savedPlan.ExperienceLevel);
        Assert.Equal(createRequest.TrainingLocation, savedPlan.TrainingLocation);
        Assert.Equal(createRequest.DaysPerWeek, savedPlan.DaysPerWeek);
        Assert.Equal(createRequest.DurationWeeks, savedPlan.DurationWeeks);

        Assert.False(savedPlan.IsActive);
        Assert.Equal("Draft", savedPlan.ReviewStatus);
        Assert.Null(savedPlan.RejectionReason);
        Assert.Equal(inactivePlan.UpdatedAtUtc, savedPlan.UpdatedAtUtc);
    }

    [Fact]
    public async Task Admin_CannotUpdateWorkoutPlan_WithMissingRequiredFields()
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create a valid Draft workout plan
        var createRequest = new CreateWorkoutPlanRequest
        {
            Name = "Validation Test Workout Plan",
            Description = "Original description.",
            GoalCode = "WEIGHT_LOSS",
            ExperienceLevel = "Beginner",
            TrainingLocation = "Gym",
            DaysPerWeek = 4,
            DurationWeeks = 12
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            createRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var plan = await createResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Attempt update with missing required fields
        var updateRequest = new UpdateWorkoutPlanRequest
        {
            Name = "",
            Description = "This update should be rejected.",
            GoalCode = "",
            ExperienceLevel = "",
            TrainingLocation = "",
            DaysPerWeek = 4,
            DurationWeeks = 12
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}",
            updateRequest);

        // Step 4: Verify validation failure
        Assert.Equal(
            HttpStatusCode.BadRequest,
            updateResponse.StatusCode);

        // Step 5: Verify original data remains unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        Assert.Equal(createRequest.Name, savedPlan.Name);
        Assert.Equal(createRequest.Description, savedPlan.Description);
        Assert.Equal(createRequest.GoalCode, savedPlan.GoalCode);
        Assert.Equal(createRequest.ExperienceLevel, savedPlan.ExperienceLevel);
        Assert.Equal(createRequest.TrainingLocation, savedPlan.TrainingLocation);
        Assert.Equal(createRequest.DaysPerWeek, savedPlan.DaysPerWeek);
        Assert.Equal(createRequest.DurationWeeks, savedPlan.DurationWeeks);

        Assert.True(savedPlan.IsActive);
        Assert.Equal("Draft", savedPlan.ReviewStatus);
        Assert.Equal(plan.UpdatedAtUtc, savedPlan.UpdatedAtUtc);
    }
    [Fact]
    public async Task Admin_CannotUpdateWorkoutPlan_WithInvalidNumericRanges()
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create a valid Draft workout plan
        var createRequest = new CreateWorkoutPlanRequest
        {
            Name = "Numeric Validation Workout Plan",
            Description = "Original workout plan description.",
            GoalCode = "WEIGHT_LOSS",
            ExperienceLevel = "Beginner",
            TrainingLocation = "Gym",
            DaysPerWeek = 4,
            DurationWeeks = 12
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            createRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var plan = await createResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Define invalid numeric combinations
        var invalidRanges = new[]
        {
            (DaysPerWeek: 0, DurationWeeks: 12),
            (DaysPerWeek: 8, DurationWeeks: 12),
            (DaysPerWeek: 4, DurationWeeks: 0),
            (DaysPerWeek: 4, DurationWeeks: 53)
        };

        // Step 4: Verify each invalid update is rejected
        foreach (var invalidRange in invalidRanges)
        {
            var updateRequest = new UpdateWorkoutPlanRequest
            {
                Name = "Modified Numeric Validation Plan",
                Description = "This update must be rejected.",
                GoalCode = "MUSCLE_GAIN",
                ExperienceLevel = "Advanced",
                TrainingLocation = "Home",
                DaysPerWeek = invalidRange.DaysPerWeek,
                DurationWeeks = invalidRange.DurationWeeks
            };

            var updateResponse = await client.PutAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}",
                updateRequest);

            Assert.Equal(
                HttpStatusCode.BadRequest,
                updateResponse.StatusCode);
        }

        // Step 5: Verify original workout plan remains unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        Assert.Equal(plan.Id, savedPlan.Id);
        Assert.Equal(createRequest.Name, savedPlan.Name);
        Assert.Equal(createRequest.Description, savedPlan.Description);
        Assert.Equal(createRequest.GoalCode, savedPlan.GoalCode);
        Assert.Equal(createRequest.ExperienceLevel, savedPlan.ExperienceLevel);
        Assert.Equal(createRequest.TrainingLocation, savedPlan.TrainingLocation);
        Assert.Equal(createRequest.DaysPerWeek, savedPlan.DaysPerWeek);
        Assert.Equal(createRequest.DurationWeeks, savedPlan.DurationWeeks);

        Assert.True(savedPlan.IsActive);
        Assert.Equal("Draft", savedPlan.ReviewStatus);
        Assert.Equal(plan.UpdatedAtUtc, savedPlan.UpdatedAtUtc);
    }

    [Fact]
    public async Task Admin_CannotUpdateWorkoutPlan_WithExcessiveFieldLengths()
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create a valid Draft workout plan
        var createRequest = new CreateWorkoutPlanRequest
        {
            Name = "Field Length Validation Plan",
            Description = "Original workout plan description.",
            GoalCode = "WEIGHT_LOSS",
            ExperienceLevel = "Beginner",
            TrainingLocation = "Gym",
            DaysPerWeek = 4,
            DurationWeeks = 12
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            createRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var plan = await createResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Define invalid update requests
        var invalidRequests = new[]
        {
            new UpdateWorkoutPlanRequest
            {
                Name = new string('A', 201),
                Description = "Valid description",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 4,
                DurationWeeks = 12
            },
            new UpdateWorkoutPlanRequest
            {
                Name = "Valid Name",
                Description = new string('A', 2001),
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 4,
                DurationWeeks = 12
            },
            new UpdateWorkoutPlanRequest
            {
                Name = "Valid Name",
                Description = "Valid description",
                GoalCode = new string('A', 51),
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 4,
                DurationWeeks = 12
            },
            new UpdateWorkoutPlanRequest
            {
                Name = "Valid Name",
                Description = "Valid description",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = new string('A', 51),
                TrainingLocation = "Gym",
                DaysPerWeek = 4,
                DurationWeeks = 12
            },
            new UpdateWorkoutPlanRequest
            {
                Name = "Valid Name",
                Description = "Valid description",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = new string('A', 51),
                DaysPerWeek = 4,
                DurationWeeks = 12
            }
        };

        // Step 4: Verify every invalid update is rejected
        foreach (var updateRequest in invalidRequests)
        {
            var updateResponse = await client.PutAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}",
                updateRequest);

            Assert.Equal(
                HttpStatusCode.BadRequest,
                updateResponse.StatusCode);
        }

        // Step 5: Verify original workout plan remains unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        Assert.Equal(plan.Id, savedPlan.Id);
        Assert.Equal(createRequest.Name, savedPlan.Name);
        Assert.Equal(createRequest.Description, savedPlan.Description);
        Assert.Equal(createRequest.GoalCode, savedPlan.GoalCode);
        Assert.Equal(createRequest.ExperienceLevel, savedPlan.ExperienceLevel);
        Assert.Equal(createRequest.TrainingLocation, savedPlan.TrainingLocation);
        Assert.Equal(createRequest.DaysPerWeek, savedPlan.DaysPerWeek);
        Assert.Equal(createRequest.DurationWeeks, savedPlan.DurationWeeks);

        Assert.True(savedPlan.IsActive);
        Assert.Equal("Draft", savedPlan.ReviewStatus);
        Assert.Equal(plan.UpdatedAtUtc, savedPlan.UpdatedAtUtc);
    }
    [Fact]
    public async Task Admin_CannotUpdateNonexistentWorkoutPlan()
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(
            HttpStatusCode.OK,
            loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Generate a nonexistent workout plan ID
        var nonexistentPlanId = Guid.NewGuid();

        // Step 3: Prepare a valid update request
        var updateRequest = new UpdateWorkoutPlanRequest
        {
            Name = "Nonexistent Workout Plan",
            Description = "This workout plan does not exist.",
            GoalCode = "WEIGHT_LOSS",
            ExperienceLevel = "Beginner",
            TrainingLocation = "Gym",
            DaysPerWeek = 4,
            DurationWeeks = 12
        };

        // Step 4: Attempt to update the nonexistent workout plan
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{nonexistentPlanId}",
            updateRequest);

        // Step 5: Verify the API returns 404 Not Found
        Assert.Equal(
            HttpStatusCode.NotFound,
            updateResponse.StatusCode);

        // Step 6: Verify the workout plan was not created
        var getResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{nonexistentPlanId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            getResponse.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedUser_CannotUpdateWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // No authentication token
        var planId = Guid.NewGuid();

        var updateRequest = new UpdateWorkoutPlanRequest
        {
            Name = "Unauthorized Update",
            Description = "This update must be rejected.",
            GoalCode = "WEIGHT_LOSS",
            ExperienceLevel = "Beginner",
            TrainingLocation = "Gym",
            DaysPerWeek = 4,
            DurationWeeks = 12
        };

        var response = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{planId}",
            updateRequest);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Member_CannotUpdateWorkoutPlan_ReturnsForbidden()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Login as Member
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(
            HttpStatusCode.OK,
            loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        // Step 2: Attach Member JWT
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 3: Prepare a valid update request
        var planId = Guid.NewGuid();

        var updateRequest = new UpdateWorkoutPlanRequest
        {
            Name = "Member Unauthorized Update",
            Description = "Members cannot update workout plans.",
            GoalCode = "WEIGHT_LOSS",
            ExperienceLevel = "Beginner",
            TrainingLocation = "Gym",
            DaysPerWeek = 4,
            DurationWeeks = 12
        };

        // Step 4: Attempt to update workout plan
        var response = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{planId}",
            updateRequest);

        // Step 5: Verify access is forbidden
        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }
    [Fact]
    public async Task Admin_CanUpdateDraftWorkoutPlanDay()
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create a Draft workout plan
        var createRequest = new CreateWorkoutPlanRequest
        {
            Name = "Workout Day Update Test",
            Description = "Testing workout day editing.",
            GoalCode = "WEIGHT_LOSS",
            ExperienceLevel = "Beginner",
            TrainingLocation = "Gym",
            DaysPerWeek = 4,
            DurationWeeks = 12
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            createRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var plan = await createResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Add a workout day
        var dayRequest = new CreateWorkoutPlanDayRequest
        {
            DayNumber = 1,
            Name = "Original Workout Day",
            Description = "Original day description.",
            IsRestDay = false,
            EstimatedDurationMinutes = 45
        };

        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            dayRequest);

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Update the workout day
        var updateRequest = new UpdateWorkoutPlanDayRequest
        {
            DayNumber = 2,
            Name = "Updated Workout Day",
            Description = "Updated workout day description.",
            IsRestDay = true,
            EstimatedDurationMinutes = 30
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}",
            updateRequest);

        // Step 5: Verify successful update
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updatedDay = await updateResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(updatedDay);

        Assert.Equal(day.Id, updatedDay.Id);
        Assert.Equal(plan.Id, updatedDay.WorkoutPlanId);
        Assert.Equal(updateRequest.DayNumber, updatedDay.DayNumber);
        Assert.Equal(updateRequest.Name, updatedDay.Name);
        Assert.Equal(updateRequest.Description, updatedDay.Description);
        Assert.Equal(updateRequest.IsRestDay, updatedDay.IsRestDay);
        Assert.Equal(
            updateRequest.EstimatedDurationMinutes,
            updatedDay.EstimatedDurationMinutes);

        // Step 6: Retrieve the plan to verify persistence
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);
        Assert.Equal("Draft", savedPlan.ReviewStatus);
        Assert.True(savedPlan.IsActive);

        var savedDay = Assert.Single(savedPlan.Days);

        Assert.Equal(day.Id, savedDay.Id);
        Assert.Equal(updateRequest.DayNumber, savedDay.DayNumber);
        Assert.Equal(updateRequest.Name, savedDay.Name);
        Assert.Equal(updateRequest.Description, savedDay.Description);
        Assert.Equal(updateRequest.IsRestDay, savedDay.IsRestDay);
        Assert.Equal(
            updateRequest.EstimatedDurationMinutes,
            savedDay.EstimatedDurationMinutes);
    }
    [Fact]
    public async Task Admin_CannotUpdateWorkoutDay_InReviewPlan()
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create Draft workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Workout Day Review Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Original Full Body Workout",
                Description = "Original workout description",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise in Exercise Master
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"DAY-REVIEW-{Guid.NewGuid():N}",
                Name = "Workout Day Review Squat",
                Description = "Exercise for workout day review testing",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise to workout day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // Step 7: Verify plan is In Review
        var submittedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(submittedPlan);
        Assert.Equal("In Review", submittedPlan.ReviewStatus);

        // Step 8: Attempt to update workout day
        var updateRequest = new UpdateWorkoutPlanDayRequest
        {
            DayNumber = 2,
            Name = "Modified Workout Day",
            Description = "This update should be rejected",
            IsRestDay = true,
            EstimatedDurationMinutes = 30
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}",
            updateRequest);

        // Step 9: Verify update is blocked
        Assert.Equal(
            HttpStatusCode.Conflict,
            updateResponse.StatusCode);

        // Step 10: Verify original workout day remains unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);
        Assert.Equal("In Review", savedPlan.ReviewStatus);

        var savedDay = Assert.Single(savedPlan.Days);

        Assert.Equal(day.Id, savedDay.Id);
        Assert.Equal(1, savedDay.DayNumber);
        Assert.Equal("Original Full Body Workout", savedDay.Name);
        Assert.Equal("Original workout description", savedDay.Description);
        Assert.False(savedDay.IsRestDay);
        Assert.Equal(60, savedDay.EstimatedDurationMinutes);

        Assert.Single(savedDay.Exercises);
    }
    [Fact]
    public async Task Admin_CannotUpdateWorkoutDay_ApprovedPlan()
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create Draft workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Approved Day Update Test {Guid.NewGuid():N}",
                Description = "Testing workout day editing restrictions.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Add workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Original Strength Workout",
                Description = "Original workout day description",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"APPROVED-DAY-{Guid.NewGuid():N}",
                Name = "Approved Day Test Squat",
                Description = "Exercise for approved workout day testing",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise to workout day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // Step 7: Approve workout plan
        var approveResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // Step 8: Verify Approved status
        var approvedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(approvedPlan);
        Assert.Equal("Approved", approvedPlan.ReviewStatus);
        Assert.True(approvedPlan.IsActive);

        // Step 9: Attempt to update workout day
        var updateRequest = new UpdateWorkoutPlanDayRequest
        {
            DayNumber = 2,
            Name = "Modified Approved Workout",
            Description = "This update should be rejected",
            IsRestDay = true,
            EstimatedDurationMinutes = 30
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}",
            updateRequest);

        // Step 10: Verify update is blocked
        Assert.Equal(
            HttpStatusCode.Conflict,
            updateResponse.StatusCode);

        // Step 11: Retrieve plan again
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        // Step 12: Verify plan remains Approved
        Assert.Equal("Approved", savedPlan.ReviewStatus);
        Assert.True(savedPlan.IsActive);

        // Step 13: Verify original workout day remains unchanged
        var savedDay = Assert.Single(savedPlan.Days);

        Assert.Equal(day.Id, savedDay.Id);
        Assert.Equal(1, savedDay.DayNumber);
        Assert.Equal("Original Strength Workout", savedDay.Name);
        Assert.Equal(
            "Original workout day description",
            savedDay.Description);
        Assert.False(savedDay.IsRestDay);
        Assert.Equal(60, savedDay.EstimatedDurationMinutes);

        // Step 14: Verify exercise assignment is preserved
        Assert.Single(savedDay.Exercises);
    }
    [Fact]
    public async Task Admin_CannotUpdateWorkoutDay_InactivePlan()
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create Draft workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Inactive Day Update Test {Guid.NewGuid():N}",
                Description = "Testing inactive workout day restrictions.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Original Cardio Workout",
                Description = "Original cardio workout description",
                IsRestDay = false,
                EstimatedDurationMinutes = 45
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Deactivate workout plan
        var deactivateResponse = await client.PatchAsync(
            $"/api/admin/workout-plans/{plan.Id}/deactivate",
            null);

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        // Step 5: Verify workout plan is inactive
        var inactivePlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(inactivePlan);
        Assert.False(inactivePlan.IsActive);

        // Step 6: Attempt to update workout day
        var updateRequest = new UpdateWorkoutPlanDayRequest
        {
            DayNumber = 2,
            Name = "Modified Cardio Workout",
            Description = "This update should be rejected",
            IsRestDay = true,
            EstimatedDurationMinutes = 30
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}",
            updateRequest);

        // Step 7: Verify update is blocked
        Assert.Equal(
            HttpStatusCode.Conflict,
            updateResponse.StatusCode);

        // Step 8: Retrieve workout plan again
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        // Step 9: Verify workout plan remains inactive
        Assert.False(savedPlan.IsActive);
        Assert.Equal("Draft", savedPlan.ReviewStatus);

        // Step 10: Verify workout day remains unchanged
        var savedDay = Assert.Single(savedPlan.Days);

        Assert.Equal(day.Id, savedDay.Id);
        Assert.Equal(1, savedDay.DayNumber);
        Assert.Equal("Original Cardio Workout", savedDay.Name);
        Assert.Equal(
            "Original cardio workout description",
            savedDay.Description);
        Assert.False(savedDay.IsRestDay);
        Assert.Equal(45, savedDay.EstimatedDurationMinutes);
    }
    [Fact]
    public async Task Admin_CannotUpdateWorkoutDay_WithDuplicateDayNumber()
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create Draft workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Duplicate Day Update Test {Guid.NewGuid():N}",
                Description = "Testing duplicate workout day numbers.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Create Day 1
        var day1Response = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Workout",
                Description = "Original chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, day1Response.StatusCode);

        var day1 = await day1Response.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day1);

        // Step 4: Create Day 2
        var day2Response = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 2,
                Name = "Back Workout",
                Description = "Original back workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 45
            });

        Assert.Equal(HttpStatusCode.Created, day2Response.StatusCode);

        var day2 = await day2Response.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day2);

        // Step 5: Attempt to change Day 2 to Day 1
        var updateRequest = new UpdateWorkoutPlanDayRequest
        {
            DayNumber = 1,
            Name = "Modified Back Workout",
            Description = "This update should be rejected",
            IsRestDay = true,
            EstimatedDurationMinutes = 30
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day2.Id}",
            updateRequest);

        // Step 6: Verify duplicate day number is rejected
        Assert.Equal(
            HttpStatusCode.Conflict,
            updateResponse.StatusCode);

        // Step 7: Retrieve workout plan
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);
        Assert.Equal("Draft", savedPlan.ReviewStatus);

        // Step 8: Verify both workout days are unchanged
        Assert.Equal(2, savedPlan.Days.Count());

        var savedDay1 = Assert.Single(
            savedPlan.Days, d => d.Id == day1.Id);

        var savedDay2 = Assert.Single(
            savedPlan.Days, d => d.Id == day2.Id);

        Assert.Equal(1, savedDay1.DayNumber);
        Assert.Equal("Chest Workout", savedDay1.Name);
        Assert.Equal("Original chest workout", savedDay1.Description);
        Assert.False(savedDay1.IsRestDay);
        Assert.Equal(60, savedDay1.EstimatedDurationMinutes);

        Assert.Equal(2, savedDay2.DayNumber);
        Assert.Equal("Back Workout", savedDay2.Name);
        Assert.Equal("Original back workout", savedDay2.Description);
        Assert.False(savedDay2.IsRestDay);
        Assert.Equal(45, savedDay2.EstimatedDurationMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public async Task Admin_CannotUpdateWorkoutDay_WithInvalidDayNumber(
    int invalidDayNumber)
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create Draft workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Invalid Day Number Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create valid workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Original Workout Day",
                Description = "Original description",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Attempt update with invalid day number
        var updateRequest = new UpdateWorkoutPlanDayRequest
        {
            DayNumber = invalidDayNumber,
            Name = "Modified Workout Day",
            Description = "Invalid day number update",
            IsRestDay = true,
            EstimatedDurationMinutes = 30
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}",
            updateRequest);

        // Step 5: Verify Bad Request
        Assert.Equal(
            HttpStatusCode.BadRequest,
            updateResponse.StatusCode);

        // Step 6: Verify original workout day remains unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);

        Assert.Equal(day.Id, savedDay.Id);
        Assert.Equal(1, savedDay.DayNumber);
        Assert.Equal("Original Workout Day", savedDay.Name);
        Assert.Equal("Original description", savedDay.Description);
        Assert.False(savedDay.IsRestDay);
        Assert.Equal(60, savedDay.EstimatedDurationMinutes);
    }
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Admin_CannotUpdateWorkoutDay_WithInvalidName(
    string? invalidName)
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create Draft workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Invalid Day Name Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create valid workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Original Workout Day",
                Description = "Original description",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Attempt update with invalid name
        var updateRequest = new UpdateWorkoutPlanDayRequest
        {
            DayNumber = 1,
            Name = invalidName!,
            Description = "This update should be rejected",
            IsRestDay = true,
            EstimatedDurationMinutes = 30
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}",
            updateRequest);

        // Step 5: Verify Bad Request
        Assert.Equal(
            HttpStatusCode.BadRequest,
            updateResponse.StatusCode);

        // Step 6: Verify original workout day remains unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);

        Assert.Equal(day.Id, savedDay.Id);
        Assert.Equal(1, savedDay.DayNumber);
        Assert.Equal("Original Workout Day", savedDay.Name);
        Assert.Equal("Original description", savedDay.Description);
        Assert.False(savedDay.IsRestDay);
        Assert.Equal(60, savedDay.EstimatedDurationMinutes);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Admin_CannotUpdateWorkoutDay_WithExcessiveFieldLengths(
    bool invalidName)
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create Draft workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Excessive Field Length Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create valid workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Original Workout Day",
                Description = "Original description",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Prepare invalid field lengths
        var updateRequest = new UpdateWorkoutPlanDayRequest
        {
            DayNumber = 1,
            Name = invalidName
                ? new string('N', 201)
                : "Valid Workout Day",
            Description = invalidName
                ? "Valid description"
                : new string('D', 2001),
            IsRestDay = true,
            EstimatedDurationMinutes = 30
        };

        // Step 5: Attempt update
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}",
            updateRequest);

        // Step 6: Verify Bad Request
        Assert.Equal(
            HttpStatusCode.BadRequest,
            updateResponse.StatusCode);

        // Step 7: Verify original workout day remains unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);

        Assert.Equal(day.Id, savedDay.Id);
        Assert.Equal(1, savedDay.DayNumber);
        Assert.Equal("Original Workout Day", savedDay.Name);
        Assert.Equal("Original description", savedDay.Description);
        Assert.False(savedDay.IsRestDay);
        Assert.Equal(60, savedDay.EstimatedDurationMinutes);
    }
    [Theory]
    [InlineData(-1)]
    [InlineData(301)]
    public async Task Admin_CannotUpdateWorkoutDay_WithInvalidDuration(
    int invalidDuration)
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create Draft workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Invalid Duration Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create valid workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Original Workout Day",
                Description = "Original description",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Attempt update with invalid duration
        var updateRequest = new UpdateWorkoutPlanDayRequest
        {
            DayNumber = 1,
            Name = "Modified Workout Day",
            Description = "This update should be rejected",
            IsRestDay = true,
            EstimatedDurationMinutes = invalidDuration
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}",
            updateRequest);

        // Step 5: Verify Bad Request
        Assert.Equal(
            HttpStatusCode.BadRequest,
            updateResponse.StatusCode);

        // Step 6: Verify original workout day remains unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);

        Assert.Equal(day.Id, savedDay.Id);
        Assert.Equal(1, savedDay.DayNumber);
        Assert.Equal("Original Workout Day", savedDay.Name);
        Assert.Equal("Original description", savedDay.Description);
        Assert.False(savedDay.IsRestDay);
        Assert.Equal(60, savedDay.EstimatedDurationMinutes);
    }

    [Fact]
    public async Task Admin_CannotUpdateWorkoutDay_WhenPlanDoesNotExist()
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Generate nonexistent plan and day IDs
        var nonexistentPlanId = Guid.NewGuid();
        var nonexistentDayId = Guid.NewGuid();

        // Step 3: Prepare valid workout day update request
        var updateRequest = new UpdateWorkoutPlanDayRequest
        {
            DayNumber = 1,
            Name = "Updated Workout Day",
            Description = "Testing nonexistent workout plan",
            IsRestDay = false,
            EstimatedDurationMinutes = 45
        };

        // Step 4: Attempt to update a day in a nonexistent plan
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{nonexistentPlanId}/days/{nonexistentDayId}",
            updateRequest);

        // Step 5: Verify 404 Not Found
        Assert.Equal(
            HttpStatusCode.NotFound,
            updateResponse.StatusCode);
    }
    [Fact]
    public async Task Admin_CannotUpdateWorkoutDay_WhenDayDoesNotExist()
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create Draft workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Missing Workout Day Test {Guid.NewGuid():N}",
                Description = "Testing nonexistent workout day validation.",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Generate a nonexistent workout day ID
        var nonexistentDayId = Guid.NewGuid();

        // Step 4: Prepare valid workout day update request
        var updateRequest = new UpdateWorkoutPlanDayRequest
        {
            DayNumber = 1,
            Name = "Updated Workout Day",
            Description = "This workout day does not exist.",
            IsRestDay = false,
            EstimatedDurationMinutes = 45
        };

        // Step 5: Attempt to update nonexistent workout day
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{nonexistentDayId}",
            updateRequest);

        // Step 6: Verify 404 Not Found
        Assert.Equal(
            HttpStatusCode.NotFound,
            updateResponse.StatusCode);

        // Step 7: Retrieve workout plan
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        // Step 8: Verify workout plan remains unchanged
        Assert.Equal("Draft", savedPlan.ReviewStatus);
        Assert.True(savedPlan.IsActive);
        Assert.Empty(savedPlan.Days);
    }
    [Fact]
    public async Task Admin_CannotUpdateWorkoutDay_BelongingToDifferentPlan()
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create Plan A
        var planAResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Ownership Plan A {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planAResponse.StatusCode);

        var planA = await planAResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(planA);

        // Step 3: Create Plan B
        var planBResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Ownership Plan B {Guid.NewGuid():N}",
                GoalCode = "MUSCLE_GAIN",
                ExperienceLevel = "Intermediate",
                TrainingLocation = "Gym",
                DaysPerWeek = 4,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planBResponse.StatusCode);

        var planB = await planBResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(planB);
        Assert.NotEqual(planA.Id, planB.Id);

        // Step 4: Add workout day to Plan A
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{planA.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Original Chest Workout",
                Description = "Belongs to Plan A",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);
        Assert.Equal(planA.Id, day.WorkoutPlanId);

        // Step 5: Attempt to update Plan A's day using Plan B's ID
        var updateRequest = new UpdateWorkoutPlanDayRequest
        {
            DayNumber = 2,
            Name = "Unauthorized Workout Update",
            Description = "This update must be rejected",
            IsRestDay = true,
            EstimatedDurationMinutes = 30
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{planB.Id}/days/{day.Id}",
            updateRequest);

        // Step 6: Verify 404 Not Found
        Assert.Equal(
            HttpStatusCode.NotFound,
            updateResponse.StatusCode);

        // Step 7: Retrieve Plan A
        var savedPlanA = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{planA.Id}");

        Assert.NotNull(savedPlanA);

        // Step 8: Verify original workout day remains unchanged
        var savedDay = Assert.Single(savedPlanA.Days);

        Assert.Equal(day.Id, savedDay.Id);
        Assert.Equal(planA.Id, savedDay.WorkoutPlanId);
        Assert.Equal(1, savedDay.DayNumber);
        Assert.Equal("Original Chest Workout", savedDay.Name);
        Assert.Equal("Belongs to Plan A", savedDay.Description);
        Assert.False(savedDay.IsRestDay);
        Assert.Equal(60, savedDay.EstimatedDurationMinutes);

        // Step 9: Verify Plan B remains unchanged
        var savedPlanB = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{planB.Id}");

        Assert.NotNull(savedPlanB);
        Assert.Empty(savedPlanB.Days);
    }
    [Fact]
    public async Task UnauthenticatedUser_CannotUpdateWorkoutDay()
    {
        // Step 1: Initialize test application
        using var factory = new FitnessPlatformWebApplicationFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 2: Generate workout plan and day IDs
        var planId = Guid.NewGuid();
        var dayId = Guid.NewGuid();

        // Step 3: Prepare valid workout day update request
        var updateRequest = new UpdateWorkoutPlanDayRequest
        {
            DayNumber = 1,
            Name = "Unauthorized Workout Update",
            Description = "This request must be rejected.",
            IsRestDay = false,
            EstimatedDurationMinutes = 45
        };

        // Step 4: Attempt update without authentication
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{planId}/days/{dayId}",
            updateRequest);

        // Step 5: Verify 401 Unauthorized
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            updateResponse.StatusCode);
    }
    [Fact]
    public async Task Member_CannotUpdateWorkoutDay()
    {
        // Step 1: Initialize test application
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 2: Authenticate as regular member
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(
            HttpStatusCode.OK,
            loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 3: Generate workout plan and day IDs
        var planId = Guid.NewGuid();
        var dayId = Guid.NewGuid();

        // Step 4: Prepare valid workout day update request
        var updateRequest = new UpdateWorkoutPlanDayRequest
        {
            DayNumber = 1,
            Name = "Member Workout Update",
            Description = "Members should not be allowed to update workout days.",
            IsRestDay = false,
            EstimatedDurationMinutes = 45
        };

        // Step 5: Attempt update using Member credentials
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{planId}/days/{dayId}",
            updateRequest);

        // Step 6: Verify 403 Forbidden
        Assert.Equal(
            HttpStatusCode.Forbidden,
            updateResponse.StatusCode);
    }
    [Fact]
    public async Task Admin_CanUpdateWorkoutDay_InRejectedPlan()
    {
        // Step 1: Authenticate as Admin
        using var factory = new FitnessPlatformWebApplicationFactory();
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create Draft workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Rejected Plan Edit Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Add workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Original Workout Day",
                Description = "Original description",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise in Exercise Master
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"DAY-REJECT-{Guid.NewGuid():N}",
                Name = "Workout Day Rejection Test Squat",
                Description = "Exercise for rejected workout plan testing",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 4.1: Assign exercise to workout day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        // Step 5: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.True(
            submitResponse.IsSuccessStatusCode,
            await submitResponse.Content.ReadAsStringAsync());

        // Step 6: Reject the workout plan
        var rejectResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/reject",
            new
            {
                Reason = "Workout day needs improvement"
            });

        Assert.True(
            rejectResponse.IsSuccessStatusCode,
            await rejectResponse.Content.ReadAsStringAsync());

        // Step 7: Verify Rejected status
        var rejectedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(rejectedPlan);
        Assert.Equal("Rejected", rejectedPlan.ReviewStatus);

        // Step 8: Update workout day
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}",
            new UpdateWorkoutPlanDayRequest
            {
                DayNumber = 2,
                Name = "Updated Workout Day",
                Description = "Improved after review rejection",
                IsRestDay = false,
                EstimatedDurationMinutes = 75
            });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // Step 9: Verify changes persisted
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);
        Assert.Equal("Rejected", savedPlan.ReviewStatus);

        var savedDay = Assert.Single(savedPlan.Days);

        Assert.Equal(day.Id, savedDay.Id);
        Assert.Equal(2, savedDay.DayNumber);
        Assert.Equal("Updated Workout Day", savedDay.Name);
        Assert.Equal(
            "Improved after review rejection",
            savedDay.Description);
        Assert.False(savedDay.IsRestDay);
        Assert.Equal(75, savedDay.EstimatedDurationMinutes);
    }
    
    [Fact]
    public async Task Admin_CanUpdateWorkoutPlanExerciseAssignment()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Exercise Update Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Workout",
                Description = "Chest training session",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise in Exercise Master
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"EX-UPD-{Guid.NewGuid():N}",
                Name = "Exercise Assignment Update Test",
                Description = "Exercise for assignment editing",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Assign exercise to workout day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60,
                DurationSeconds = null,
                Notes = "Original instructions"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);
        Assert.NotEqual(Guid.Empty, assignment.Id);

        // Step 6: Update the existing exercise assignment
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/{assignment.Id}",
            new UpdateWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 2,
                Sets = 4,
                Reps = 10,
                RestSeconds = 90,
                DurationSeconds = 600,
                Notes = "  Updated exercise instructions  "
            });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // Step 7: Verify updated response
        var updatedAssignment = await updateResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(updatedAssignment);

        Assert.Equal(assignment.Id, updatedAssignment.Id);
        Assert.Equal(day.Id, updatedAssignment.WorkoutPlanDayId);
        Assert.Equal(exercise.Id, updatedAssignment.ExerciseId);
        Assert.Equal(2, updatedAssignment.ExerciseOrder);
        Assert.Equal(4, updatedAssignment.Sets);
        Assert.Equal(10, updatedAssignment.Reps);
        Assert.Equal(90, updatedAssignment.RestSeconds);
        Assert.Equal(600, updatedAssignment.DurationSeconds);
        Assert.Equal(
            "Updated exercise instructions",
            updatedAssignment.Notes);

        // Step 8: Retrieve workout plan again
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);
        var savedAssignment = Assert.Single(savedDay.Exercises);

        // Step 9: Verify database persistence
        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(exercise.Id, savedAssignment.ExerciseId);
        Assert.Equal(2, savedAssignment.ExerciseOrder);
        Assert.Equal(4, savedAssignment.Sets);
        Assert.Equal(10, savedAssignment.Reps);
        Assert.Equal(90, savedAssignment.RestSeconds);
        Assert.Equal(600, savedAssignment.DurationSeconds);
        Assert.Equal(
            "Updated exercise instructions",
            savedAssignment.Notes);
    }

    [Fact]
    public async Task Admin_CannotUpdateWorkoutPlanExercise_WithInvalidExerciseId()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Exercise Update Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Workout",
                Description = "Chest training session",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise in Exercise Master
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"EX-UPD-{Guid.NewGuid():N}",
                Name = "Exercise Assignment Update Test",
                Description = "Exercise for assignment editing",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Assign exercise to workout day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60,
                DurationSeconds = null,
                Notes = "Original instructions"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);
        Assert.NotEqual(Guid.Empty, assignment.Id);

        // Step 6: Attempt to update assignment with nonexistent Exercise ID
        var invalidExerciseId = Guid.NewGuid();

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/{assignment.Id}",
            new UpdateWorkoutPlanExerciseRequest
            {
                ExerciseId = invalidExerciseId,
                ExerciseOrder = 2,
                Sets = 4,
                Reps = 10,
                RestSeconds = 90,
                DurationSeconds = 600,
                Notes = "Invalid exercise reference"
            });

        // Verify Exercise Master validation
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);

        // Step 7: Retrieve workout plan after rejected update
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);
        var savedAssignment = Assert.Single(savedDay.Exercises);

        // Step 8: Verify original assignment remains unchanged
        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(exercise.Id, savedAssignment.ExerciseId);
        Assert.Equal(1, savedAssignment.ExerciseOrder);
        Assert.Equal(3, savedAssignment.Sets);
        Assert.Equal(12, savedAssignment.Reps);
        Assert.Equal(60, savedAssignment.RestSeconds);
        Assert.Null(savedAssignment.DurationSeconds);
        Assert.Equal("Original instructions", savedAssignment.Notes);

        // Confirm invalid exercise was not assigned
        Assert.NotEqual(invalidExerciseId, savedAssignment.ExerciseId);
    }

    [Fact]
    public async Task Admin_CannotUpdateWorkoutPlanExercise_WithNotesExceedingMaximumLength()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Exercise Update Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Workout",
                Description = "Chest training session",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise in Exercise Master
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"EX-UPD-{Guid.NewGuid():N}",
                Name = "Exercise Assignment Update Test",
                Description = "Exercise for assignment editing",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Assign exercise to workout day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60,
                DurationSeconds = null,
                Notes = "Original instructions"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);
        Assert.NotEqual(Guid.Empty, assignment.Id);

        // Step 6: Attempt to update assignment with notes exceeding 2000 characters
        var invalidNotes = new string('A', 2001);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/{assignment.Id}",
            new UpdateWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 2,
                Sets = 4,
                Reps = 10,
                RestSeconds = 90,
                DurationSeconds = 600,
                Notes = invalidNotes
            });

        // Verify notes length validation
        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);

        // Step 7: Retrieve workout plan after rejected update
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);
        var savedAssignment = Assert.Single(savedDay.Exercises);

        // Step 8: Verify original assignment remains unchanged
        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(exercise.Id, savedAssignment.ExerciseId);
        Assert.Equal(1, savedAssignment.ExerciseOrder);
        Assert.Equal(3, savedAssignment.Sets);
        Assert.Equal(12, savedAssignment.Reps);
        Assert.Equal(60, savedAssignment.RestSeconds);
        Assert.Null(savedAssignment.DurationSeconds);
        Assert.Equal("Original instructions", savedAssignment.Notes);


    }

    [Fact]
    public async Task Admin_CannotUpdateWorkoutPlanExercise_WhenPlanIsInReview()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"In Review Exercise Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Workout",
                Description = "Chest training",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"REVIEW-EX-{Guid.NewGuid():N}",
                Name = "Review Restriction Exercise",
                Description = "Exercise for review restriction testing",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.True(
            submitResponse.IsSuccessStatusCode,
            await submitResponse.Content.ReadAsStringAsync());

        // Step 7: Verify plan is In Review
        var reviewPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewPlan);
        Assert.Equal("In Review", reviewPlan.ReviewStatus);

        // Step 8: Attempt to update exercise assignment
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/{assignment.Id}",
            new UpdateWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 5,
                Reps = 15,
                RestSeconds = 90,
                Notes = "This update should be rejected"
            });

        // Step 9: Verify update is blocked
        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);

        // Step 10: Verify original assignment remains unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);
        var savedAssignment = Assert.Single(savedDay.Exercises);

        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(3, savedAssignment.Sets);
        Assert.Equal(12, savedAssignment.Reps);
        Assert.Equal(60, savedAssignment.RestSeconds);
    }

    [Fact]
    public async Task Admin_CannotDeleteWorkoutPlanExercise_WhenPlanIsInReview()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"In Review Exercise Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Workout",
                Description = "Chest training",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"REVIEW-EX-{Guid.NewGuid():N}",
                Name = "Review Restriction Exercise",
                Description = "Exercise for review restriction testing",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.True(
            submitResponse.IsSuccessStatusCode,
            await submitResponse.Content.ReadAsStringAsync());

        // Step 7: Verify plan is In Review
        var reviewPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewPlan);
        Assert.Equal("In Review", reviewPlan.ReviewStatus);

        // Attempt to delete assignment while plan is In Review
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/{assignment.Id}");

        // Verify deletion is blocked

        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);


        // Step 10: Verify original assignment remains unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);
        var savedAssignment = Assert.Single(savedDay.Exercises);

        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(3, savedAssignment.Sets);
        Assert.Equal(12, savedAssignment.Reps);
        Assert.Equal(60, savedAssignment.RestSeconds);
    }


    [Fact]
    public async Task Admin_CanUpdateWorkoutPlanExercise_WhenPlanIsRejected()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"In Review Exercise Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Workout",
                Description = "Chest training",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"REVIEW-EX-{Guid.NewGuid():N}",
                Name = "Review Restriction Exercise",
                Description = "Exercise for review restriction testing",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.True(
            submitResponse.IsSuccessStatusCode,
            await submitResponse.Content.ReadAsStringAsync());

        // Step 7: Verify plan is In Review
        var reviewPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewPlan);
        Assert.Equal("In Review", reviewPlan.ReviewStatus);

        // Reject the workout plan
        var rejectResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/reject",
            new
            {
                Reason = "Exercise assignment needs improvement"
            });

        Assert.True(
            rejectResponse.IsSuccessStatusCode,
            await rejectResponse.Content.ReadAsStringAsync());

        // Verify Rejected status
        var rejectedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(rejectedPlan);
        Assert.Equal("Rejected", rejectedPlan.ReviewStatus);

        // Step 8: Attempt to update exercise assignment
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/{assignment.Id}",
            new UpdateWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 5,
                Reps = 15,
                RestSeconds = 90,
                Notes = "This update should be rejected"
            });

        // Step 9: Verify update is blocked
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // Step 10: Verify original assignment remains unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);
        var savedAssignment = Assert.Single(savedDay.Exercises);

        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(exercise.Id, savedAssignment.ExerciseId);
        Assert.Equal(1, savedAssignment.ExerciseOrder);
        Assert.Equal(5, savedAssignment.Sets);
        Assert.Equal(15, savedAssignment.Reps);
        Assert.Equal(90, savedAssignment.RestSeconds);
        Assert.Equal(
            "This update should be rejected",
            savedAssignment.Notes);
    }
    [Fact]
    public async Task Admin_CanDeleteWorkoutPlanExercise_WhenPlanIsRejected()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"In Review Exercise Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Workout",
                Description = "Chest training",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"REVIEW-EX-{Guid.NewGuid():N}",
                Name = "Review Restriction Exercise",
                Description = "Exercise for review restriction testing",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.True(
            submitResponse.IsSuccessStatusCode,
            await submitResponse.Content.ReadAsStringAsync());

        // Step 7: Verify plan is In Review
        var reviewPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewPlan);
        Assert.Equal("In Review", reviewPlan.ReviewStatus);

        // Reject the workout plan
        var rejectResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/reject",
            new
            {
                Reason = "Exercise assignment needs improvement"
            });

        Assert.True(
            rejectResponse.IsSuccessStatusCode,
            await rejectResponse.Content.ReadAsStringAsync());

        // Verify Rejected status
        var rejectedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(rejectedPlan);
        Assert.Equal("Rejected", rejectedPlan.ReviewStatus);

        // Attempt to delete assignment from a Rejected workout plan
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/{assignment.Id}");

        // Step 9: Verify deletion is successful
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Step 10: Retrieve workout plan after deletion
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        // Step 11: Verify workout day still exists
        var savedDay = Assert.Single(savedPlan.Days);

        Assert.Equal(day.Id, savedDay.Id);

        // Step 12: Verify exercise assignment has been deleted
        Assert.Empty(savedDay.Exercises);

        // Step 13: Verify workout plan remains Rejected
        Assert.Equal("Rejected", savedPlan.ReviewStatus);
    }
    [Fact]
    public async Task Admin_CannotUpdateWorkoutPlanExercise_WhenPlanIsApproved()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"In Review Exercise Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Workout",
                Description = "Chest training",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"REVIEW-EX-{Guid.NewGuid():N}",
                Name = "Review Restriction Exercise",
                Description = "Exercise for review restriction testing",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.True(
            submitResponse.IsSuccessStatusCode,
            await submitResponse.Content.ReadAsStringAsync());

        // Step 7: Verify plan is In Review
        var reviewPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewPlan);
        Assert.Equal("In Review", reviewPlan.ReviewStatus);

        // Step 8: Approve workout plan
        var approveResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        Assert.True(
            approveResponse.IsSuccessStatusCode,
            await approveResponse.Content.ReadAsStringAsync());

        // Verify workout plan is Approved
        var approvedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(approvedPlan);
        Assert.Equal("Approved", approvedPlan.ReviewStatus);

        // Step 8: Attempt to update exercise assignment
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/{assignment.Id}",
            new UpdateWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 5,
                Reps = 15,
                RestSeconds = 90,
                Notes = "This update should be rejected"
            });

        // Step 9: Verify update is blocked
        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);

        // Step 10: Verify original assignment remains unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);
        var savedAssignment = Assert.Single(savedDay.Exercises);

        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(3, savedAssignment.Sets);
        Assert.Equal(12, savedAssignment.Reps);
        Assert.Equal(60, savedAssignment.RestSeconds);
    }
    [Fact]
    public async Task Admin_CannotDeleteWorkoutPlanExercise_WhenPlanIsApproved()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"In Review Exercise Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Workout",
                Description = "Chest training",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"REVIEW-EX-{Guid.NewGuid():N}",
                Name = "Review Restriction Exercise",
                Description = "Exercise for review restriction testing",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.True(
            submitResponse.IsSuccessStatusCode,
            await submitResponse.Content.ReadAsStringAsync());

        // Step 7: Verify plan is In Review
        var reviewPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewPlan);
        Assert.Equal("In Review", reviewPlan.ReviewStatus);

        // Step 8: Approve workout plan
        var approveResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        Assert.True(
            approveResponse.IsSuccessStatusCode,
            await approveResponse.Content.ReadAsStringAsync());

        // Verify workout plan is Approved
        var approvedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(approvedPlan);
        Assert.Equal("Approved", approvedPlan.ReviewStatus);

        // Attempt to delete assignment from Approved workout plan
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/{assignment.Id}");

        // Verify deletion is blocked
        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);

        // Step 9: Verify update is blocked
        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);

        // Step 10: Verify original assignment remains unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);
        var savedAssignment = Assert.Single(savedDay.Exercises);

        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(3, savedAssignment.Sets);
        Assert.Equal(12, savedAssignment.Reps);
        Assert.Equal(60, savedAssignment.RestSeconds);
    }


    [Fact]
    public async Task Admin_CannotUpdateWorkoutPlanExercise_WhenPlanIsInactive()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"In Review Exercise Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Workout",
                Description = "Chest training",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"REVIEW-EX-{Guid.NewGuid():N}",
                Name = "Review Restriction Exercise",
                Description = "Exercise for review restriction testing",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.True(
            submitResponse.IsSuccessStatusCode,
            await submitResponse.Content.ReadAsStringAsync());

        // Step 7: Verify plan is In Review
        var reviewPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewPlan);
        Assert.Equal("In Review", reviewPlan.ReviewStatus);

        // Step 8: Approve workout plan
        var approveResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        Assert.True(
            approveResponse.IsSuccessStatusCode,
            await approveResponse.Content.ReadAsStringAsync());

        // Verify workout plan is Approved
        var approvedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(approvedPlan);
        Assert.Equal("Approved", approvedPlan.ReviewStatus);

        // Deactivate the workout plan
        var deactivateResponse = await client.PatchAsync(
            $"/api/admin/workout-plans/{plan.Id}/deactivate",
            null);

        Assert.True(
            deactivateResponse.IsSuccessStatusCode,
            await deactivateResponse.Content.ReadAsStringAsync());

        // Verify the plan is inactive
        var inactivePlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(inactivePlan);
        Assert.False(inactivePlan.IsActive);

        // Step 8: Attempt to update exercise assignment
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/{assignment.Id}",
            new UpdateWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 5,
                Reps = 15,
                RestSeconds = 90,
                Notes = "This update should be rejected"
            });

        // Step 9: Verify update is blocked
        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);

        // Step 10: Verify original assignment remains unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);
        var savedAssignment = Assert.Single(savedDay.Exercises);

        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(3, savedAssignment.Sets);
        Assert.Equal(12, savedAssignment.Reps);
        Assert.Equal(60, savedAssignment.RestSeconds);
    }

    [Fact]
    public async Task Admin_CannotDeleteWorkoutPlanExercise_WhenPlanIsInactive()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"In Review Exercise Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Workout",
                Description = "Chest training",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"REVIEW-EX-{Guid.NewGuid():N}",
                Name = "Review Restriction Exercise",
                Description = "Exercise for review restriction testing",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Assign exercise
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.True(
            submitResponse.IsSuccessStatusCode,
            await submitResponse.Content.ReadAsStringAsync());

        // Step 7: Verify plan is In Review
        var reviewPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(reviewPlan);
        Assert.Equal("In Review", reviewPlan.ReviewStatus);

        // Step 8: Approve workout plan
        var approveResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        Assert.True(
            approveResponse.IsSuccessStatusCode,
            await approveResponse.Content.ReadAsStringAsync());

        // Verify workout plan is Approved
        var approvedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(approvedPlan);
        Assert.Equal("Approved", approvedPlan.ReviewStatus);

        // Deactivate the workout plan
        var deactivateResponse = await client.PatchAsync(
            $"/api/admin/workout-plans/{plan.Id}/deactivate",
            null);

        Assert.True(
            deactivateResponse.IsSuccessStatusCode,
            await deactivateResponse.Content.ReadAsStringAsync());

        // Verify the plan is inactive
        var inactivePlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(inactivePlan);
        Assert.False(inactivePlan.IsActive);

        // Attempt to delete assignment from an inactive workout plan
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/{assignment.Id}");

        // Verify deletion is blocked
        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);

        // Step 10: Verify original assignment remains unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);
        var savedAssignment = Assert.Single(savedDay.Exercises);

        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(3, savedAssignment.Sets);
        Assert.Equal(12, savedAssignment.Reps);
        Assert.Equal(60, savedAssignment.RestSeconds);
    }


    [Fact]
    public async Task Admin_CannotUpdateWorkoutPlanExercise_WithDuplicateOrder()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Duplicate Order Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Workout",
                Description = "Duplicate exercise order validation",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"DUP-ORDER-{Guid.NewGuid():N}",
                Name = "Duplicate Order Test Exercise",
                Description = "Exercise for duplicate order validation",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Assign first exercise with order 1
        var firstAssignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(
            HttpStatusCode.Created,
            firstAssignmentResponse.StatusCode);

        var firstAssignment = await firstAssignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(firstAssignment);

        // Step 6: Assign second exercise with order 2
        var secondAssignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 2,
                Sets = 4,
                Reps = 10,
                RestSeconds = 90
            });

        Assert.Equal(
            HttpStatusCode.Created,
            secondAssignmentResponse.StatusCode);

        var secondAssignment = await secondAssignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(secondAssignment);

        // Step 7: Attempt to change second assignment order to 1
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/{secondAssignment.Id}",
            new UpdateWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 5,
                Reps = 15,
                RestSeconds = 120,
                Notes = "Duplicate order attempt"
            });

        // Step 8: Verify duplicate order is rejected
        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);

        // Step 9: Retrieve workout plan again
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);

        Assert.Equal(2, savedDay.Exercises.Count);

        // Step 10: Verify original exercise orders remain unchanged
        var savedFirstAssignment = Assert.Single(
            savedDay.Exercises,
            e => e.Id == firstAssignment.Id);

        var savedSecondAssignment = Assert.Single(
            savedDay.Exercises,
            e => e.Id == secondAssignment.Id);

        Assert.Equal(1, savedFirstAssignment.ExerciseOrder);
        Assert.Equal(2, savedSecondAssignment.ExerciseOrder);

        Assert.Equal(4, savedSecondAssignment.Sets);
        Assert.Equal(10, savedSecondAssignment.Reps);
        Assert.Equal(90, savedSecondAssignment.RestSeconds);
    }

    [Theory]
    [InlineData(0, 3, 12, 60, null)]
    [InlineData(-1, 3, 12, 60, null)]
    [InlineData(1, 0, 12, 60, null)]
    [InlineData(1, 21, 12, 60, null)]
    [InlineData(1, 3, -1, 60, null)]
    [InlineData(1, 3, 501, 60, null)]
    [InlineData(1, 3, 12, -1, null)]
    [InlineData(1, 3, 12, 3601, null)]
    [InlineData(1, 3, 12, 60, 0)]
    [InlineData(1, 3, 12, 60, 14401)]
    public async Task Admin_CannotUpdateWorkoutPlanExercise_WithInvalidNumericValues(
    int exerciseOrder,
    int sets,
    int reps,
    int restSeconds,
    int? durationSeconds)
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Numeric Validation {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Validation Workout",
                Description = "Exercise assignment validation",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create Exercise Master record
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"NUM-VAL-{Guid.NewGuid():N}",
                Name = "Numeric Validation Exercise",
                Description = "Exercise for validation testing",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Create valid exercise assignment
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60,
                Notes = "Original assignment"
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);

        // Step 6: Attempt update with invalid numeric values
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/{assignment.Id}",
            new UpdateWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = exerciseOrder,
                Sets = sets,
                Reps = reps,
                RestSeconds = restSeconds,
                DurationSeconds = durationSeconds,
                Notes = "Invalid numeric update"
            });

        // Step 7: Verify validation rejects the request
        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);

        // Step 8: Verify original assignment remains unchanged
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);
        var savedAssignment = Assert.Single(savedDay.Exercises);

        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(exercise.Id, savedAssignment.ExerciseId);
        Assert.Equal(1, savedAssignment.ExerciseOrder);
        Assert.Equal(3, savedAssignment.Sets);
        Assert.Equal(12, savedAssignment.Reps);
        Assert.Equal(60, savedAssignment.RestSeconds);
        Assert.Null(savedAssignment.DurationSeconds);
        Assert.Equal("Original assignment", savedAssignment.Notes);
    }

    [Theory]
    [InlineData("Plan")]
    [InlineData("Day")]
    [InlineData("Assignment")]
    public async Task Admin_CannotUpdateWorkoutPlanExercise_WithMissingIds(
    string missingEntity)
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Missing ID Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Missing ID Test Day",
                Description = "Testing invalid identifiers",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"MISS-ID-{Guid.NewGuid():N}",
                Name = "Missing ID Test Exercise",
                Description = "Exercise for missing ID validation",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Create valid assignment
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60,
                Notes = "Original assignment"
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);

        // Step 6: Replace one identifier with a nonexistent ID
        var requestPlanId = missingEntity == "Plan"
            ? Guid.NewGuid()
            : plan.Id;

        var requestDayId = missingEntity == "Day"
            ? Guid.NewGuid()
            : day.Id;

        var requestAssignmentId = missingEntity == "Assignment"
            ? Guid.NewGuid()
            : assignment.Id;

        // Step 7: Attempt update
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{requestPlanId}/days/{requestDayId}/exercises/{requestAssignmentId}",
            new UpdateWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 2,
                Sets = 5,
                Reps = 15,
                RestSeconds = 90,
                Notes = "This update should not be saved"
            });

        // Step 8: Verify 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);

        // Step 9: Retrieve original workout plan
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);
        var savedAssignment = Assert.Single(savedDay.Exercises);

        // Step 10: Verify original assignment remains unchanged
        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(exercise.Id, savedAssignment.ExerciseId);
        Assert.Equal(1, savedAssignment.ExerciseOrder);
        Assert.Equal(3, savedAssignment.Sets);
        Assert.Equal(12, savedAssignment.Reps);
        Assert.Equal(60, savedAssignment.RestSeconds);
        Assert.Equal("Original assignment", savedAssignment.Notes);
    }

    [Theory]
    [InlineData("Plan")]
    [InlineData("Day")]
    [InlineData("Assignment")]
    public async Task Admin_CannotDeleteWorkoutPlanExercise_WithMissingIds(
    string missingEntity)
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Missing ID Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Missing ID Test Day",
                Description = "Testing invalid identifiers",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"MISS-ID-{Guid.NewGuid():N}",
                Name = "Missing ID Test Exercise",
                Description = "Exercise for missing ID validation",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Create valid assignment
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60,
                Notes = "Original assignment"
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);

        // Step 6: Replace one identifier with a nonexistent ID
        var requestPlanId = missingEntity == "Plan"
            ? Guid.NewGuid()
            : plan.Id;

        var requestDayId = missingEntity == "Day"
            ? Guid.NewGuid()
            : day.Id;

        var requestAssignmentId = missingEntity == "Assignment"
            ? Guid.NewGuid()
            : assignment.Id;

        // Attempt deletion using a missing identifier
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{requestPlanId}/days/{requestDayId}/exercises/{requestAssignmentId}");

        // Step 8: Verify 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        // Step 9: Retrieve original workout plan
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);
        var savedAssignment = Assert.Single(savedDay.Exercises);

        // Step 10: Verify original assignment remains unchanged
        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(exercise.Id, savedAssignment.ExerciseId);
        Assert.Equal(1, savedAssignment.ExerciseOrder);
        Assert.Equal(3, savedAssignment.Sets);
        Assert.Equal(12, savedAssignment.Reps);
        Assert.Equal(60, savedAssignment.RestSeconds);
        Assert.Equal("Original assignment", savedAssignment.Notes);
    }



    [Theory]
    [InlineData("WrongPlan")]
    [InlineData("WrongDay")]
    [InlineData("WrongPlanAndDay")]
    public async Task Admin_CannotUpdateWorkoutPlanExercise_WithIncorrectOwnership(string scenario)
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Missing ID Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Missing ID Test Day",
                Description = "Testing invalid identifiers",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"MISS-ID-{Guid.NewGuid():N}",
                Name = "Missing ID Test Exercise",
                Description = "Exercise for missing ID validation",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Create valid assignment
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60,
                Notes = "Original assignment"
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);

        // Step 6: Create another workout plan
        var otherPlanResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Other Plan {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, otherPlanResponse.StatusCode);

        var otherPlan = await otherPlanResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(otherPlan);

        // Step 7: Create another day in the original plan
        var otherDayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 2,
                Name = "Second Workout Day",
                Description = "Ownership validation",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, otherDayResponse.StatusCode);

        var otherDay = await otherDayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(otherDay);

        // Step 8: Create a day in the other plan
        var otherPlanDayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{otherPlan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Other Plan Workout Day",
                Description = "Cross-plan ownership validation",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, otherPlanDayResponse.StatusCode);

        var otherPlanDay = await otherPlanDayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(otherPlanDay);

        // Step 9: Select identifiers based on ownership scenario
        var requestPlanId = scenario switch
        {
            "WrongPlan" => otherPlan.Id,
            "WrongDay" => plan.Id,
            "WrongPlanAndDay" => otherPlan.Id,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        var requestDayId = scenario switch
        {
            "WrongPlan" => day.Id,
            "WrongDay" => otherDay.Id,
            "WrongPlanAndDay" => otherPlanDay.Id,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        var requestAssignmentId = assignment.Id;

        // Step 10: Attempt to update assignment using incorrect ownership
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{requestPlanId}/days/{requestDayId}/exercises/{requestAssignmentId}",
            new UpdateWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 2,
                Sets = 5,
                Reps = 15,
                RestSeconds = 90,
                Notes = "This update should not be saved"
            });

        // Step 11: Verify update is rejected
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);

        // Step 12: Retrieve original workout plan
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        // Original workout plan contains two days
        Assert.Equal(2, savedPlan.Days.Count);

        // Find the original workout day
        var savedDay = Assert.Single(
            savedPlan.Days,
            d => d.Id == day.Id);

        // Find the original exercise assignment
        var savedAssignment = Assert.Single(savedDay.Exercises);

        // Step 13: Verify original assignment remains unchanged
        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(exercise.Id, savedAssignment.ExerciseId);
        Assert.Equal(1, savedAssignment.ExerciseOrder);
        Assert.Equal(3, savedAssignment.Sets);
        Assert.Equal(12, savedAssignment.Reps);
        Assert.Equal(60, savedAssignment.RestSeconds);
        Assert.Equal("Original assignment", savedAssignment.Notes);

        // Step 10: Verify original assignment remains unchanged
        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(exercise.Id, savedAssignment.ExerciseId);
        Assert.Equal(1, savedAssignment.ExerciseOrder);
        Assert.Equal(3, savedAssignment.Sets);
        Assert.Equal(12, savedAssignment.Reps);
        Assert.Equal(60, savedAssignment.RestSeconds);
        Assert.Equal("Original assignment", savedAssignment.Notes);
    }

    [Theory]
    [InlineData("WrongPlan")]
    [InlineData("WrongDay")]
    [InlineData("WrongPlanAndDay")]
    public async Task Admin_CannotDeleteWorkoutPlanExercise_WithIncorrectOwnership(string scenario)
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Missing ID Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Missing ID Test Day",
                Description = "Testing invalid identifiers",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"MISS-ID-{Guid.NewGuid():N}",
                Name = "Missing ID Test Exercise",
                Description = "Exercise for missing ID validation",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Create valid assignment
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60,
                Notes = "Original assignment"
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);

        // Step 6: Create another workout plan
        var otherPlanResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Other Plan {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, otherPlanResponse.StatusCode);

        var otherPlan = await otherPlanResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(otherPlan);

        // Step 7: Create another day in the original plan
        var otherDayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 2,
                Name = "Second Workout Day",
                Description = "Ownership validation",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, otherDayResponse.StatusCode);

        var otherDay = await otherDayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(otherDay);

        // Step 8: Create a day in the other plan
        var otherPlanDayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{otherPlan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Other Plan Workout Day",
                Description = "Cross-plan ownership validation",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, otherPlanDayResponse.StatusCode);

        var otherPlanDay = await otherPlanDayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(otherPlanDay);

        // Step 9: Select identifiers based on ownership scenario
        var requestPlanId = scenario switch
        {
            "WrongPlan" => otherPlan.Id,
            "WrongDay" => plan.Id,
            "WrongPlanAndDay" => otherPlan.Id,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        var requestDayId = scenario switch
        {
            "WrongPlan" => day.Id,
            "WrongDay" => otherDay.Id,
            "WrongPlanAndDay" => otherPlanDay.Id,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        var requestAssignmentId = assignment.Id;

        // Attempt deletion using incorrect ownership
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{requestPlanId}/days/{requestDayId}/exercises/{requestAssignmentId}");

        // Verify deletion is rejected
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        // Step 12: Retrieve original workout plan
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        // Original workout plan contains two days
        Assert.Equal(2, savedPlan.Days.Count);

        // Find the original workout day
        var savedDay = Assert.Single(
            savedPlan.Days,
            d => d.Id == day.Id);

        // Find the original exercise assignment
        var savedAssignment = Assert.Single(savedDay.Exercises);

        // Step 13: Verify original assignment remains unchanged
        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(exercise.Id, savedAssignment.ExerciseId);
        Assert.Equal(1, savedAssignment.ExerciseOrder);
        Assert.Equal(3, savedAssignment.Sets);
        Assert.Equal(12, savedAssignment.Reps);
        Assert.Equal(60, savedAssignment.RestSeconds);
        Assert.Equal("Original assignment", savedAssignment.Notes);

        // Step 10: Verify original assignment remains unchanged
        Assert.Equal(assignment.Id, savedAssignment.Id);
        Assert.Equal(exercise.Id, savedAssignment.ExerciseId);
        Assert.Equal(1, savedAssignment.ExerciseOrder);
        Assert.Equal(3, savedAssignment.Sets);
        Assert.Equal(12, savedAssignment.Reps);
        Assert.Equal(60, savedAssignment.RestSeconds);
        Assert.Equal("Original assignment", savedAssignment.Notes);
    }


    [Fact]
    public async Task UnauthenticatedUser_CannotUpdateWorkoutPlanExercise()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Create an update request without authentication
        var updateRequest = new UpdateWorkoutPlanExerciseRequest
        {
            ExerciseId = Guid.NewGuid(),
            ExerciseOrder = 1,
            Sets = 3,
            Reps = 12,
            RestSeconds = 60,
            Notes = "Unauthorized update attempt"
        };

        // Step 2: Attempt to update an exercise assignment
        // No JWT token is provided
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{Guid.NewGuid()}/days/{Guid.NewGuid()}/exercises/{Guid.NewGuid()}",
            updateRequest);

        // Step 3: Verify authentication is required
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            updateResponse.StatusCode);
    }
    [Fact]
    public async Task Member_CannotUpdateWorkoutPlanExercise()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Member
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(
            HttpStatusCode.OK,
            loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        // Step 2: Set Member JWT token
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 3: Prepare exercise assignment update request
        var updateRequest = new UpdateWorkoutPlanExerciseRequest
        {
            ExerciseId = Guid.NewGuid(),
            ExerciseOrder = 1,
            Sets = 3,
            Reps = 12,
            RestSeconds = 60,
            Notes = "Member should not update workout assignments"
        };

        // Step 4: Attempt update using Member credentials
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{Guid.NewGuid()}/days/{Guid.NewGuid()}/exercises/{Guid.NewGuid()}",
            updateRequest);

        // Step 5: Verify Member access is forbidden
        Assert.Equal(
            HttpStatusCode.Forbidden,
            updateResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_CanDeleteWorkoutPlanExerciseAssignment()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Delete Assignment Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Exercise Deletion Test Day",
                Description = "Testing exercise assignment deletion",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create master exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"DEL-EX-{Guid.NewGuid():N}",
                Name = "Deletion Test Exercise",
                Description = "Exercise should remain after assignment deletion",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Assign exercise to workout day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60,
                Notes = "Assignment to be deleted"
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        var assignment = await assignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(assignment);

        // Step 6: Delete exercise assignment
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/{assignment.Id}");

        // Step 7: Verify successful deletion
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Step 8: Retrieve workout plan
        var savedPlan = await client
            .GetFromJsonAsync<WorkoutPlanDetailResponse>(
                $"/api/admin/workout-plans/{plan.Id}");

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(savedPlan.Days);

        // Verify assignment has been removed
        Assert.Empty(savedDay.Exercises);

        // Step 9: Verify master exercise still exists
        var masterExerciseResponse = await client.GetAsync(
            $"/api/exercises/{exercise.Id}");

        Assert.Equal(HttpStatusCode.OK, masterExerciseResponse.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedUser_CannotDeleteWorkoutPlanExercise()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Attempt deletion without authentication
        // No JWT token is provided
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{Guid.NewGuid()}/days/{Guid.NewGuid()}/exercises/{Guid.NewGuid()}");

        // Step 2: Verify authentication is required
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            deleteResponse.StatusCode);
    }
    [Fact]
    public async Task Member_CannotDeleteWorkoutPlanExercise()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Member
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        // Step 2: Set Member JWT token
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 3: Attempt to delete an exercise assignment
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{Guid.NewGuid()}/days/{Guid.NewGuid()}/exercises/{Guid.NewGuid()}");

        // Step 4: Verify Member access is forbidden
        Assert.Equal(
            HttpStatusCode.Forbidden,
            deleteResponse.StatusCode);
    }
    [Fact]
    public async Task Admin_CanDeleteWorkoutPlanDay()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Delete Day Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create master exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"EX-{Guid.NewGuid():N}",
                Name = "Dumbbell Bench Press",
                Description = "Chest exercise",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Step 5: Assign exercise to workout day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60,
                Notes = "Exercise assigned before day deletion"
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        // Step 6: Delete workout day
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Step 7: Verify workout day was deleted
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);
        Assert.Empty(savedPlan.Days);

        // Step 8: Verify master exercise still exists
        var masterExerciseResponse = await client.GetAsync(
            $"/api/exercises/{exercise.Id}");

        Assert.Equal(HttpStatusCode.OK, masterExerciseResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotDeleteWorkoutPlanDay_WhenPlanIsInReview()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"In Review Day Deletion {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Create a master exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"EX-{Guid.NewGuid():N}",
                Name = "Dumbbell Bench Press",
                Description = "Chest exercise",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Assign exercise to the workout day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60,
                Notes = "Exercise assigned before review submission"
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        // Step 4: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // Step 5: Attempt to delete workout day
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}");

        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);

        // Step 6: Verify workout day still exists
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);
        Assert.Equal("In Review", savedPlan.ReviewStatus);
        Assert.Contains(savedPlan.Days, d => d.Id == day.Id);
    }

    [Fact]
    public async Task Admin_CannotDeleteWorkoutPlanDay_WhenPlanIsApproved()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"In Review Day Deletion {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Create a master exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"EX-{Guid.NewGuid():N}",
                Name = "Dumbbell Bench Press",
                Description = "Chest exercise",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Assign exercise to the workout day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60,
                Notes = "Exercise assigned before review submission"
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        // Step 4: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // Step 5: Approve workout plan
        var approveResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // Step 5: Attempt to delete workout day
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}");

        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);

        // Step 6: Verify workout day still exists
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);
        Assert.Equal("Approved", savedPlan.ReviewStatus);
        Assert.Contains(savedPlan.Days, d => d.Id == day.Id);
    }

    [Fact]
    public async Task Admin_CanDeleteWorkoutPlanDay_WhenPlanIsRejected()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"In Review Day Deletion {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Create a master exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"EX-{Guid.NewGuid():N}",
                Name = "Dumbbell Bench Press",
                Description = "Chest exercise",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Assign exercise to the workout day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60,
                Notes = "Exercise assigned before review submission"
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        // Step 4: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // Step 5: Reject workout plan
        var rejectResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/reject",
            new
            {
                Reason = "Workout plan requires changes before approval."
            });

        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

        // Step 6: Delete workout day from Rejected plan
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Step 6: Verify workout day still exists
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        // Step 7: Verify workout day was deleted
        Assert.NotNull(savedPlan);
        Assert.Equal("Rejected", savedPlan.ReviewStatus);
        Assert.True(savedPlan.IsActive);
        Assert.DoesNotContain(savedPlan.Days, d => d.Id == day.Id);
    }


    [Fact]
    public async Task Admin_CannotDeleteWorkoutPlanDay_WhenPlanIsInactive()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"In Review Day Deletion {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Create a master exercise
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new SaveExerciseRequest
            {
                Code = $"EX-{Guid.NewGuid():N}",
                Name = "Dumbbell Bench Press",
                Description = "Chest exercise",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        var exercise = await exerciseResponse.Content
            .ReadFromJsonAsync<ExerciseResponse>();

        Assert.NotNull(exercise);

        // Assign exercise to the workout day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exercise.Id,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60,
                Notes = "Exercise assigned before review submission"
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        // Step 4: Deactivate workout plan
        var deactivateResponse = await client.PatchAsync(
            $"/api/admin/workout-plans/{plan.Id}/deactivate",
            null);

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        // Step 5: Attempt to delete workout day
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}");

        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);

        // Step 6: Verify workout day still exists
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);
        Assert.Equal("Draft", savedPlan.ReviewStatus);
        Assert.False(savedPlan.IsActive);
        Assert.Contains(savedPlan.Days, d => d.Id == day.Id);
    }

    [Theory]
    [InlineData("Plan")]
    [InlineData("Day")]
    [InlineData("PlanAndDay")]
    public async Task Admin_CannotDeleteWorkoutPlanDay_WithMissingIds(
    string scenario)
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create a valid workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Missing ID Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create a valid workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Select nonexistent identifiers
        var requestedPlanId = scenario switch
        {
            "Plan" => Guid.NewGuid(),
            "Day" => plan.Id,
            "PlanAndDay" => Guid.NewGuid(),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        var requestedDayId = scenario switch
        {
            "Plan" => day.Id,
            "Day" => Guid.NewGuid(),
            "PlanAndDay" => Guid.NewGuid(),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        // Step 5: Attempt deletion
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{requestedPlanId}/days/{requestedDayId}");

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        // Step 6: Verify original workout day still exists
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);
        Assert.Contains(savedPlan.Days, d => d.Id == day.Id);
    }

    [Fact]
    public async Task Admin_CannotDeleteWorkoutPlanDay_WithIncorrectOwnership()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Create first workout plan
        var plan1Response = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Ownership Plan 1 {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, plan1Response.StatusCode);

        var plan1 = await plan1Response.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan1);

        // Step 3: Create a workout day under Plan 1
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan1.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Workout belonging to Plan 1",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create second workout plan
        var plan2Response = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Ownership Plan 2 {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, plan2Response.StatusCode);

        var plan2 = await plan2Response.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan2);

        // Step 5: Attempt to delete Plan 1's day through Plan 2
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{plan2.Id}/days/{day.Id}");

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        // Step 6: Verify the day still exists under Plan 1
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan1.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);
        Assert.Contains(savedPlan.Days, d => d.Id == day.Id);

        // Step 7: Verify Plan 2 has no workout days
        var plan2DetailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan2.Id}");

        Assert.Equal(HttpStatusCode.OK, plan2DetailsResponse.StatusCode);

        var savedPlan2 = await plan2DetailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan2);
        Assert.Empty(savedPlan2.Days);
    }
    [Fact]
    public async Task UnauthenticatedUser_CannotDeleteWorkoutPlanDay()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Attempt deletion without authentication
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{Guid.NewGuid()}/days/{Guid.NewGuid()}");

        // Step 2: Verify unauthorized response
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            deleteResponse.StatusCode);
    }
    [Fact]
    public async Task Member_CannotDeleteWorkoutPlanDay()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Member
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        // Step 2: Set Member JWT token
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 3: Attempt to delete workout day
        var deleteResponse = await client.DeleteAsync(
            $"/api/admin/workout-plans/{Guid.NewGuid()}/days/{Guid.NewGuid()}");

        // Step 4: Verify Member access is forbidden
        Assert.Equal(
            HttpStatusCode.Forbidden,
            deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_CanReorderWorkoutPlanExercises()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create three master exercises
        var exerciseNames = new[]
        {
        "Bench Press",
        "Incline Dumbbell Press",
        "Cable Chest Fly"
    };

        var exerciseIds = new List<Guid>();

        foreach (var exerciseName in exerciseNames)
        {
            var exerciseResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                new SaveExerciseRequest
                {
                    Code = $"EX-{Guid.NewGuid():N}",
                    Name = exerciseName,
                    Description = $"{exerciseName} description",
                    MuscleGroup = "Chest",
                    Equipment = "Dumbbell",
                    DifficultyLevel = "Beginner",
                    TrainingLocation = "Gym",
                    Instructions = "Perform controlled repetitions."
                });

            Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

            var exercise = await exerciseResponse.Content
                .ReadFromJsonAsync<ExerciseResponse>();

            Assert.NotNull(exercise);

            exerciseIds.Add(exercise.Id);
        }

        // Step 5: Assign exercises to workout day
        var assignments = new List<WorkoutPlanExerciseResponse>();

        for (var i = 0; i < exerciseIds.Count; i++)
        {
            var assignmentResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
                new AddWorkoutPlanExerciseRequest
                {
                    ExerciseId = exerciseIds[i],
                    ExerciseOrder = i + 1,
                    Sets = 3,
                    Reps = 12,
                    RestSeconds = 60,
                    Notes = $"Original assignment {i + 1}"
                });

            Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

            var assignment = await assignmentResponse.Content
                .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

            Assert.NotNull(assignment);

            assignments.Add(assignment);
        }

        // Step 6: Reorder exercises
        // Original: 1, 2, 3
        // New:      3, 1, 2

        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/reorder",
            new ReorderWorkoutPlanExercisesRequest
            {
                Exercises = new List<ReorderWorkoutPlanExerciseItem>
                {
                new()
                {
                    AssignmentId = assignments[0].Id,
                    ExerciseOrder = 3
                },
                new()
                {
                    AssignmentId = assignments[1].Id,
                    ExerciseOrder = 1
                },
                new()
                {
                    AssignmentId = assignments[2].Id,
                    ExerciseOrder = 2
                }
                }
            });

        Assert.Equal(HttpStatusCode.OK, reorderResponse.StatusCode);

        // Step 7: Verify response ordering
        var reordered = await reorderResponse.Content
            .ReadFromJsonAsync<List<WorkoutPlanExerciseResponse>>();

        Assert.NotNull(reordered);
        Assert.Equal(3, reordered.Count);

        Assert.Equal(assignments[1].Id, reordered[0].Id);
        Assert.Equal(1, reordered[0].ExerciseOrder);

        Assert.Equal(assignments[2].Id, reordered[1].Id);
        Assert.Equal(2, reordered[1].ExerciseOrder);

        Assert.Equal(assignments[0].Id, reordered[2].Id);
        Assert.Equal(3, reordered[2].ExerciseOrder);

        // Step 8: Verify assignment details remain unchanged
        foreach (var original in assignments)
        {
            var updated = Assert.Single(reordered, x => x.Id == original.Id);

            Assert.Equal(original.ExerciseId, updated.ExerciseId);
            Assert.Equal(original.Sets, updated.Sets);
            Assert.Equal(original.Reps, updated.Reps);
            Assert.Equal(original.RestSeconds, updated.RestSeconds);
            Assert.Equal(original.Notes, updated.Notes);
        }

        // Step 9: Verify persisted order through GET endpoint
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(
            savedPlan.Days,
            d => d.Id == day.Id);

        Assert.Equal(3, savedDay.Exercises.Count);

        Assert.Equal(assignments[1].Id, savedDay.Exercises[0].Id);
        Assert.Equal(assignments[2].Id, savedDay.Exercises[1].Id);
        Assert.Equal(assignments[0].Id, savedDay.Exercises[2].Id);
    }

    [Fact]
    public async Task Admin_CanReorderWorkoutPlanExercises_Rejected()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create three master exercises
        var exerciseNames = new[]
        {
        "Bench Press",
        "Incline Dumbbell Press",
        "Cable Chest Fly"
    };

        var exerciseIds = new List<Guid>();

        foreach (var exerciseName in exerciseNames)
        {
            var exerciseResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                new SaveExerciseRequest
                {
                    Code = $"EX-{Guid.NewGuid():N}",
                    Name = exerciseName,
                    Description = $"{exerciseName} description",
                    MuscleGroup = "Chest",
                    Equipment = "Dumbbell",
                    DifficultyLevel = "Beginner",
                    TrainingLocation = "Gym",
                    Instructions = "Perform controlled repetitions."
                });

            Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

            var exercise = await exerciseResponse.Content
                .ReadFromJsonAsync<ExerciseResponse>();

            Assert.NotNull(exercise);

            exerciseIds.Add(exercise.Id);
        }

        // Step 5: Assign exercises to workout day
        var assignments = new List<WorkoutPlanExerciseResponse>();

        for (var i = 0; i < exerciseIds.Count; i++)
        {
            var assignmentResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
                new AddWorkoutPlanExerciseRequest
                {
                    ExerciseId = exerciseIds[i],
                    ExerciseOrder = i + 1,
                    Sets = 3,
                    Reps = 12,
                    RestSeconds = 60,
                    Notes = $"Original assignment {i + 1}"
                });

            Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

            var assignment = await assignmentResponse.Content
                .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

            Assert.NotNull(assignment);

            assignments.Add(assignment);
        }

        // Step 5A: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // Step 5B: Reject the workout plan
        var rejectResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/reject",
            new RejectWorkoutPlanRequest
            {
                Reason = "Exercise sequence requires modification."
            });

        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        
        // Step 5C: Verify workout plan is Rejected
        var rejectedResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, rejectedResponse.StatusCode);

        var rejectedPlan = await rejectedResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(rejectedPlan);
        Assert.Equal("Rejected", rejectedPlan.ReviewStatus);
        Assert.True(rejectedPlan.IsActive);

        // Step 6: Reorder exercises in a Rejected workout plan
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/reorder",
            new ReorderWorkoutPlanExercisesRequest
            {
                Exercises = new List<ReorderWorkoutPlanExerciseItem>
                {
            new()
            {
                AssignmentId = assignments[0].Id,
                ExerciseOrder = 3
            },
            new()
            {
                AssignmentId = assignments[1].Id,
                ExerciseOrder = 1
            },
            new()
            {
                AssignmentId = assignments[2].Id,
                ExerciseOrder = 2
            }
                }
            });

        Assert.Equal(HttpStatusCode.OK, reorderResponse.StatusCode);

        // Step 7: Verify response ordering
        var reordered = await reorderResponse.Content
            .ReadFromJsonAsync<List<WorkoutPlanExerciseResponse>>();

        Assert.NotNull(reordered);
        Assert.Equal(3, reordered.Count);

        Assert.Equal(assignments[1].Id, reordered[0].Id);
        Assert.Equal(1, reordered[0].ExerciseOrder);

        Assert.Equal(assignments[2].Id, reordered[1].Id);
        Assert.Equal(2, reordered[1].ExerciseOrder);

        Assert.Equal(assignments[0].Id, reordered[2].Id);
        Assert.Equal(3, reordered[2].ExerciseOrder);

        // Step 8: Verify assignment details remain unchanged
        foreach (var original in assignments)
        {
            var updated = Assert.Single(reordered, x => x.Id == original.Id);

            Assert.Equal(original.ExerciseId, updated.ExerciseId);
            Assert.Equal(original.Sets, updated.Sets);
            Assert.Equal(original.Reps, updated.Reps);
            Assert.Equal(original.RestSeconds, updated.RestSeconds);
            Assert.Equal(original.Notes, updated.Notes);
        }

        // Step 9: Verify persisted order through GET endpoint
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(
            savedPlan.Days,
            d => d.Id == day.Id);

        Assert.Equal(3, savedDay.Exercises.Count);

        Assert.Equal(assignments[1].Id, savedDay.Exercises[0].Id);
        Assert.Equal(assignments[2].Id, savedDay.Exercises[1].Id);
        Assert.Equal(assignments[0].Id, savedDay.Exercises[2].Id);
        
        // Step 10: Verify workout plan remains Rejected
        Assert.Equal("Rejected", savedPlan.ReviewStatus);
        Assert.True(savedPlan.IsActive);
    }


    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanExercises_InReview()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create three master exercises
        var exerciseNames = new[]
        {
        "Bench Press",
        "Incline Dumbbell Press",
        "Cable Chest Fly"
    };

        var exerciseIds = new List<Guid>();

        foreach (var exerciseName in exerciseNames)
        {
            var exerciseResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                new SaveExerciseRequest
                {
                    Code = $"EX-{Guid.NewGuid():N}",
                    Name = exerciseName,
                    Description = $"{exerciseName} description",
                    MuscleGroup = "Chest",
                    Equipment = "Dumbbell",
                    DifficultyLevel = "Beginner",
                    TrainingLocation = "Gym",
                    Instructions = "Perform controlled repetitions."
                });

            Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

            var exercise = await exerciseResponse.Content
                .ReadFromJsonAsync<ExerciseResponse>();

            Assert.NotNull(exercise);

            exerciseIds.Add(exercise.Id);
        }

        // Step 5: Assign exercises to workout day
        var assignments = new List<WorkoutPlanExerciseResponse>();

        for (var i = 0; i < exerciseIds.Count; i++)
        {
            var assignmentResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
                new AddWorkoutPlanExerciseRequest
                {
                    ExerciseId = exerciseIds[i],
                    ExerciseOrder = i + 1,
                    Sets = 3,
                    Reps = 12,
                    RestSeconds = 60,
                    Notes = $"Original assignment {i + 1}"
                });

            Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

            var assignment = await assignmentResponse.Content
                .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

            Assert.NotNull(assignment);

            assignments.Add(assignment);
        }
        // Step 5A: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // Step 6: Attempt to reorder exercises while plan is InReview
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/reorder",
            new ReorderWorkoutPlanExercisesRequest
            {
                Exercises = new List<ReorderWorkoutPlanExerciseItem>
                {
            new()
            {
                AssignmentId = assignments[0].Id,
                ExerciseOrder = 3
            },
            new()
            {
                AssignmentId = assignments[1].Id,
                ExerciseOrder = 1
            },
            new()
            {
                AssignmentId = assignments[2].Id,
                ExerciseOrder = 2
            }
                }
            });

        // InReview plans must not allow exercise reordering
        Assert.Equal(HttpStatusCode.Conflict, reorderResponse.StatusCode);

        // Step 7: Verify exercise positions remain unchanged
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(
            savedPlan.Days,
            d => d.Id == day.Id);

        Assert.Equal(3, savedDay.Exercises.Count);

        // Verify original exercise positions
        Assert.Equal(assignments[0].Id, savedDay.Exercises[0].Id);
        Assert.Equal(1, savedDay.Exercises[0].ExerciseOrder);

        Assert.Equal(assignments[1].Id, savedDay.Exercises[1].Id);
        Assert.Equal(2, savedDay.Exercises[1].ExerciseOrder);

        Assert.Equal(assignments[2].Id, savedDay.Exercises[2].Id);
        Assert.Equal(3, savedDay.Exercises[2].ExerciseOrder);
    }

    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanExercises_Inactive()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create three master exercises
        var exerciseNames = new[]
        {
        "Bench Press",
        "Incline Dumbbell Press",
        "Cable Chest Fly"
    };

        var exerciseIds = new List<Guid>();

        foreach (var exerciseName in exerciseNames)
        {
            var exerciseResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                new SaveExerciseRequest
                {
                    Code = $"EX-{Guid.NewGuid():N}",
                    Name = exerciseName,
                    Description = $"{exerciseName} description",
                    MuscleGroup = "Chest",
                    Equipment = "Dumbbell",
                    DifficultyLevel = "Beginner",
                    TrainingLocation = "Gym",
                    Instructions = "Perform controlled repetitions."
                });

            Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

            var exercise = await exerciseResponse.Content
                .ReadFromJsonAsync<ExerciseResponse>();

            Assert.NotNull(exercise);

            exerciseIds.Add(exercise.Id);
        }

        // Step 5: Assign exercises to workout day
        var assignments = new List<WorkoutPlanExerciseResponse>();

        for (var i = 0; i < exerciseIds.Count; i++)
        {
            var assignmentResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
                new AddWorkoutPlanExerciseRequest
                {
                    ExerciseId = exerciseIds[i],
                    ExerciseOrder = i + 1,
                    Sets = 3,
                    Reps = 12,
                    RestSeconds = 60,
                    Notes = $"Original assignment {i + 1}"
                });

            Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

            var assignment = await assignmentResponse.Content
                .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

            Assert.NotNull(assignment);

            assignments.Add(assignment);
        }
        // Step 5A: Deactivate the workout plan
        var deactivateResponse = await client.PatchAsync(
            $"/api/admin/workout-plans/{plan.Id}/deactivate",
            null);

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        // Step 5B: Verify the workout plan is inactive
        var inactiveResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, inactiveResponse.StatusCode);

        var inactivePlan = await inactiveResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(inactivePlan);
        Assert.False(inactivePlan.IsActive);
        Assert.Equal("Draft", inactivePlan.ReviewStatus);

        // Step 6: Attempt to reorder exercises while plan is inactive
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/reorder",
            new ReorderWorkoutPlanExercisesRequest
            {
                Exercises = new List<ReorderWorkoutPlanExerciseItem>
                {
            new()
            {
                AssignmentId = assignments[0].Id,
                ExerciseOrder = 3
            },
            new()
            {
                AssignmentId = assignments[1].Id,
                ExerciseOrder = 1
            },
            new()
            {
                AssignmentId = assignments[2].Id,
                ExerciseOrder = 2
            }
                }
            });

        // Inactive workout plans must not allow exercise reordering
        Assert.Equal(HttpStatusCode.Conflict, reorderResponse.StatusCode);

        // Step 7: Verify exercise positions remain unchanged
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(
            savedPlan.Days,
            d => d.Id == day.Id);

        Assert.Equal(3, savedDay.Exercises.Count);

        // Verify original exercise positions
        Assert.Equal(assignments[0].Id, savedDay.Exercises[0].Id);
        Assert.Equal(1, savedDay.Exercises[0].ExerciseOrder);

        Assert.Equal(assignments[1].Id, savedDay.Exercises[1].Id);
        Assert.Equal(2, savedDay.Exercises[1].ExerciseOrder);

        Assert.Equal(assignments[2].Id, savedDay.Exercises[2].Id);
        Assert.Equal(3, savedDay.Exercises[2].ExerciseOrder);
    }


    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanExercises_Approved()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create three master exercises
        var exerciseNames = new[]
        {
        "Bench Press",
        "Incline Dumbbell Press",
        "Cable Chest Fly"
    };

        var exerciseIds = new List<Guid>();

        foreach (var exerciseName in exerciseNames)
        {
            var exerciseResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                new SaveExerciseRequest
                {
                    Code = $"EX-{Guid.NewGuid():N}",
                    Name = exerciseName,
                    Description = $"{exerciseName} description",
                    MuscleGroup = "Chest",
                    Equipment = "Dumbbell",
                    DifficultyLevel = "Beginner",
                    TrainingLocation = "Gym",
                    Instructions = "Perform controlled repetitions."
                });

            Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

            var exercise = await exerciseResponse.Content
                .ReadFromJsonAsync<ExerciseResponse>();

            Assert.NotNull(exercise);

            exerciseIds.Add(exercise.Id);
        }

        // Step 5: Assign exercises to workout day
        var assignments = new List<WorkoutPlanExerciseResponse>();

        for (var i = 0; i < exerciseIds.Count; i++)
        {
            var assignmentResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
                new AddWorkoutPlanExerciseRequest
                {
                    ExerciseId = exerciseIds[i],
                    ExerciseOrder = i + 1,
                    Sets = 3,
                    Reps = 12,
                    RestSeconds = 60,
                    Notes = $"Original assignment {i + 1}"
                });

            Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

            var assignment = await assignmentResponse.Content
                .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

            Assert.NotNull(assignment);

            assignments.Add(assignment);
        }
        // Step 5A: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // Step 6: Attempt to reorder exercises while plan is InReview
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/reorder",
            new ReorderWorkoutPlanExercisesRequest
            {
                Exercises = new List<ReorderWorkoutPlanExerciseItem>
                {
            new()
            {
                AssignmentId = assignments[0].Id,
                ExerciseOrder = 3
            },
            new()
            {
                AssignmentId = assignments[1].Id,
                ExerciseOrder = 1
            },
            new()
            {
                AssignmentId = assignments[2].Id,
                ExerciseOrder = 2
            }
                }
            });

        // Step 5B: Approve the workout plan
        var approveResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // Step 5C: Verify the workout plan is approved
        var approvedResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, approvedResponse.StatusCode);

        var approvedPlan = await approvedResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(approvedPlan);
        Assert.Equal("Approved", approvedPlan.ReviewStatus);
        Assert.True(approvedPlan.IsActive);



        // Step 7: Verify exercise positions remain unchanged
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(
            savedPlan.Days,
            d => d.Id == day.Id);

        Assert.Equal(3, savedDay.Exercises.Count);

        // Verify original exercise positions
        Assert.Equal(assignments[0].Id, savedDay.Exercises[0].Id);
        Assert.Equal(1, savedDay.Exercises[0].ExerciseOrder);

        Assert.Equal(assignments[1].Id, savedDay.Exercises[1].Id);
        Assert.Equal(2, savedDay.Exercises[1].ExerciseOrder);

        Assert.Equal(assignments[2].Id, savedDay.Exercises[2].Id);
        Assert.Equal(3, savedDay.Exercises[2].ExerciseOrder);
    }


    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanExercises_WithDuplicatePositions()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create three master exercises
        var exerciseNames = new[]
        {
        "Bench Press",
        "Incline Dumbbell Press",
        "Cable Chest Fly"
    };

        var exerciseIds = new List<Guid>();

        foreach (var exerciseName in exerciseNames)
        {
            var exerciseResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                new SaveExerciseRequest
                {
                    Code = $"EX-{Guid.NewGuid():N}",
                    Name = exerciseName,
                    Description = $"{exerciseName} description",
                    MuscleGroup = "Chest",
                    Equipment = "Dumbbell",
                    DifficultyLevel = "Beginner",
                    TrainingLocation = "Gym",
                    Instructions = "Perform controlled repetitions."
                });

            Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

            var exercise = await exerciseResponse.Content
                .ReadFromJsonAsync<ExerciseResponse>();

            Assert.NotNull(exercise);

            exerciseIds.Add(exercise.Id);
        }

        // Step 5: Assign exercises to workout day
        var assignments = new List<WorkoutPlanExerciseResponse>();

        for (var i = 0; i < exerciseIds.Count; i++)
        {
            var assignmentResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
                new AddWorkoutPlanExerciseRequest
                {
                    ExerciseId = exerciseIds[i],
                    ExerciseOrder = i + 1,
                    Sets = 3,
                    Reps = 12,
                    RestSeconds = 60,
                    Notes = $"Original assignment {i + 1}"
                });

            Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

            var assignment = await assignmentResponse.Content
                .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

            Assert.NotNull(assignment);

            assignments.Add(assignment);
        }

        // Step 6: Attempt to reorder exercises with duplicate positions
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/reorder",
            new ReorderWorkoutPlanExercisesRequest
            {
                Exercises = new List<ReorderWorkoutPlanExerciseItem>
                {
            new()
            {
                AssignmentId = assignments[0].Id,
                ExerciseOrder = 1
            },
            new()
            {
                AssignmentId = assignments[1].Id,
                ExerciseOrder = 1
            },
            new()
            {
                AssignmentId = assignments[2].Id,
                ExerciseOrder = 2
            }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, reorderResponse.StatusCode);

        // Step 7: Verify original exercise positions remain unchanged
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(
            savedPlan.Days,
            d => d.Id == day.Id);

        Assert.Equal(3, savedDay.Exercises.Count);

        // Verify original assignment IDs and positions
        Assert.Equal(assignments[0].Id, savedDay.Exercises[0].Id);
        Assert.Equal(1, savedDay.Exercises[0].ExerciseOrder);

        Assert.Equal(assignments[1].Id, savedDay.Exercises[1].Id);
        Assert.Equal(2, savedDay.Exercises[1].ExerciseOrder);

        Assert.Equal(assignments[2].Id, savedDay.Exercises[2].Id);
        Assert.Equal(3, savedDay.Exercises[2].ExerciseOrder);
    }

    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanExercises_WithDuplicateAssignmentIds()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create three master exercises
        var exerciseNames = new[]
        {
        "Bench Press",
        "Incline Dumbbell Press",
        "Cable Chest Fly"
    };

        var exerciseIds = new List<Guid>();

        foreach (var exerciseName in exerciseNames)
        {
            var exerciseResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                new SaveExerciseRequest
                {
                    Code = $"EX-{Guid.NewGuid():N}",
                    Name = exerciseName,
                    Description = $"{exerciseName} description",
                    MuscleGroup = "Chest",
                    Equipment = "Dumbbell",
                    DifficultyLevel = "Beginner",
                    TrainingLocation = "Gym",
                    Instructions = "Perform controlled repetitions."
                });

            Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

            var exercise = await exerciseResponse.Content
                .ReadFromJsonAsync<ExerciseResponse>();

            Assert.NotNull(exercise);

            exerciseIds.Add(exercise.Id);
        }

        // Step 5: Assign exercises to workout day
        var assignments = new List<WorkoutPlanExerciseResponse>();

        for (var i = 0; i < exerciseIds.Count; i++)
        {
            var assignmentResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
                new AddWorkoutPlanExerciseRequest
                {
                    ExerciseId = exerciseIds[i],
                    ExerciseOrder = i + 1,
                    Sets = 3,
                    Reps = 12,
                    RestSeconds = 60,
                    Notes = $"Original assignment {i + 1}"
                });

            Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

            var assignment = await assignmentResponse.Content
                .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

            Assert.NotNull(assignment);

            assignments.Add(assignment);
        }

        // Step 6: Attempt to reorder exercises with duplicate assignment IDs
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/reorder",
            new ReorderWorkoutPlanExercisesRequest
            {
                Exercises = new List<ReorderWorkoutPlanExerciseItem>
                {
            new()
            {
                AssignmentId = assignments[0].Id,
                ExerciseOrder = 1
            },
            new()
            {
                AssignmentId = assignments[0].Id, // Duplicate assignment ID
                ExerciseOrder = 2
            },
            new()
            {
                AssignmentId = assignments[2].Id,
                ExerciseOrder = 3
            }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, reorderResponse.StatusCode);

        // Step 7: Verify original exercise positions remain unchanged
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(
            savedPlan.Days,
            d => d.Id == day.Id);

        Assert.Equal(3, savedDay.Exercises.Count);

        // Verify original assignment IDs and positions
        Assert.Equal(assignments[0].Id, savedDay.Exercises[0].Id);
        Assert.Equal(1, savedDay.Exercises[0].ExerciseOrder);

        Assert.Equal(assignments[1].Id, savedDay.Exercises[1].Id);
        Assert.Equal(2, savedDay.Exercises[1].ExerciseOrder);

        Assert.Equal(assignments[2].Id, savedDay.Exercises[2].Id);
        Assert.Equal(3, savedDay.Exercises[2].ExerciseOrder);
    }


    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanExercises_WithMissingAssignments()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create three master exercises
        var exerciseNames = new[]
        {
        "Bench Press",
        "Incline Dumbbell Press",
        "Cable Chest Fly"
    };

        var exerciseIds = new List<Guid>();

        foreach (var exerciseName in exerciseNames)
        {
            var exerciseResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                new SaveExerciseRequest
                {
                    Code = $"EX-{Guid.NewGuid():N}",
                    Name = exerciseName,
                    Description = $"{exerciseName} description",
                    MuscleGroup = "Chest",
                    Equipment = "Dumbbell",
                    DifficultyLevel = "Beginner",
                    TrainingLocation = "Gym",
                    Instructions = "Perform controlled repetitions."
                });

            Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

            var exercise = await exerciseResponse.Content
                .ReadFromJsonAsync<ExerciseResponse>();

            Assert.NotNull(exercise);

            exerciseIds.Add(exercise.Id);
        }

        // Step 5: Assign exercises to workout day
        var assignments = new List<WorkoutPlanExerciseResponse>();

        for (var i = 0; i < exerciseIds.Count; i++)
        {
            var assignmentResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
                new AddWorkoutPlanExerciseRequest
                {
                    ExerciseId = exerciseIds[i],
                    ExerciseOrder = i + 1,
                    Sets = 3,
                    Reps = 12,
                    RestSeconds = 60,
                    Notes = $"Original assignment {i + 1}"
                });

            Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

            var assignment = await assignmentResponse.Content
                .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

            Assert.NotNull(assignment);

            assignments.Add(assignment);
        }

        // Step 6: Attempt to reorder with a missing exercise assignment
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/reorder",
            new ReorderWorkoutPlanExercisesRequest
            {
                Exercises = new List<ReorderWorkoutPlanExerciseItem>
                {
            new()
            {
                AssignmentId = assignments[0].Id,
                ExerciseOrder = 2
            },
            new()
            {
                AssignmentId = assignments[1].Id,
                ExerciseOrder = 1
            }

            // Assignment 3 intentionally omitted
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, reorderResponse.StatusCode);

        // Step 7: Verify original exercise positions remain unchanged
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(
            savedPlan.Days,
            d => d.Id == day.Id);

        Assert.Equal(3, savedDay.Exercises.Count);

        // Verify original assignment IDs and positions
        Assert.Equal(assignments[0].Id, savedDay.Exercises[0].Id);
        Assert.Equal(1, savedDay.Exercises[0].ExerciseOrder);

        Assert.Equal(assignments[1].Id, savedDay.Exercises[1].Id);
        Assert.Equal(2, savedDay.Exercises[1].ExerciseOrder);

        Assert.Equal(assignments[2].Id, savedDay.Exercises[2].Id);
        Assert.Equal(3, savedDay.Exercises[2].ExerciseOrder);
    }

    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanExercises_WithAssignmentFromAnotherDay()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create three master exercises
        var exerciseNames = new[]
        {
        "Bench Press",
        "Incline Dumbbell Press",
        "Cable Chest Fly"
    };

        var exerciseIds = new List<Guid>();

        foreach (var exerciseName in exerciseNames)
        {
            var exerciseResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                new SaveExerciseRequest
                {
                    Code = $"EX-{Guid.NewGuid():N}",
                    Name = exerciseName,
                    Description = $"{exerciseName} description",
                    MuscleGroup = "Chest",
                    Equipment = "Dumbbell",
                    DifficultyLevel = "Beginner",
                    TrainingLocation = "Gym",
                    Instructions = "Perform controlled repetitions."
                });

            Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

            var exercise = await exerciseResponse.Content
                .ReadFromJsonAsync<ExerciseResponse>();

            Assert.NotNull(exercise);

            exerciseIds.Add(exercise.Id);
        }

        // Step 5: Assign exercises to workout day
        var assignments = new List<WorkoutPlanExerciseResponse>();

        for (var i = 0; i < exerciseIds.Count; i++)
        {
            var assignmentResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
                new AddWorkoutPlanExerciseRequest
                {
                    ExerciseId = exerciseIds[i],
                    ExerciseOrder = i + 1,
                    Sets = 3,
                    Reps = 12,
                    RestSeconds = 60,
                    Notes = $"Original assignment {i + 1}"
                });

            Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

            var assignment = await assignmentResponse.Content
                .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

            Assert.NotNull(assignment);

            assignments.Add(assignment);
        }

        // Step 5A: Create another workout day within the same plan
        var secondDayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 2,
                Name = "Second Workout Day",
                Description = "Different workout day",
                IsRestDay = false,
                EstimatedDurationMinutes = 45
            });

        Assert.Equal(HttpStatusCode.Created, secondDayResponse.StatusCode);

        var secondDay = await secondDayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(secondDay);

        // Step 5B: Assign an exercise to the second workout day
        var foreignAssignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{secondDay.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseIds[0],
                ExerciseOrder = 1,
                Sets = 4,
                Reps = 10,
                RestSeconds = 90,
                Notes = "Assignment belonging to another day"
            });

        Assert.Equal(HttpStatusCode.Created, foreignAssignmentResponse.StatusCode);

        var foreignAssignment = await foreignAssignmentResponse.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

        Assert.NotNull(foreignAssignment);

        // Step 6: Attempt to reorder Day 1 using an assignment from Day 2
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/reorder",
            new ReorderWorkoutPlanExercisesRequest
            {
                Exercises = new List<ReorderWorkoutPlanExerciseItem>
                {
            new()
            {
                AssignmentId = assignments[0].Id,
                ExerciseOrder = 2
            },
            new()
            {
                AssignmentId = foreignAssignment.Id,
                ExerciseOrder = 1
            },
            new()
            {
                AssignmentId = assignments[2].Id,
                ExerciseOrder = 3
            }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, reorderResponse.StatusCode);

        // Step 7: Verify neither workout day was modified
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        // Verify Day 1
        var savedDay = Assert.Single(
            savedPlan.Days,
            d => d.Id == day.Id);

        Assert.Equal(3, savedDay.Exercises.Count);

        Assert.Equal(assignments[0].Id, savedDay.Exercises[0].Id);
        Assert.Equal(1, savedDay.Exercises[0].ExerciseOrder);

        Assert.Equal(assignments[1].Id, savedDay.Exercises[1].Id);
        Assert.Equal(2, savedDay.Exercises[1].ExerciseOrder);

        Assert.Equal(assignments[2].Id, savedDay.Exercises[2].Id);
        Assert.Equal(3, savedDay.Exercises[2].ExerciseOrder);

        // Verify Day 2
        var savedSecondDay = Assert.Single(
            savedPlan.Days,
            d => d.Id == secondDay.Id);

        var savedForeignAssignment = Assert.Single(savedSecondDay.Exercises);

        Assert.Equal(foreignAssignment.Id, savedForeignAssignment.Id);
        Assert.Equal(1, savedForeignAssignment.ExerciseOrder);
    }


    [Theory]
    [InlineData(0, 2, 3)]
    [InlineData(-1, 2, 3)]
    [InlineData(1, 3, 5)]
    [InlineData(2, 3, 4)]
    public async Task Admin_CannotReorderWorkoutPlanExercises_WithInvalidPositions(
     int firstOrder,
     int secondOrder,
     int thirdOrder)
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create workout day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Chest Day",
                Description = "Chest workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create three master exercises
        var exerciseNames = new[]
        {
        "Bench Press",
        "Incline Dumbbell Press",
        "Cable Chest Fly"
    };

        var exerciseIds = new List<Guid>();

        foreach (var exerciseName in exerciseNames)
        {
            var exerciseResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                new SaveExerciseRequest
                {
                    Code = $"EX-{Guid.NewGuid():N}",
                    Name = exerciseName,
                    Description = $"{exerciseName} description",
                    MuscleGroup = "Chest",
                    Equipment = "Dumbbell",
                    DifficultyLevel = "Beginner",
                    TrainingLocation = "Gym",
                    Instructions = "Perform controlled repetitions."
                });

            Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

            var exercise = await exerciseResponse.Content
                .ReadFromJsonAsync<ExerciseResponse>();

            Assert.NotNull(exercise);

            exerciseIds.Add(exercise.Id);
        }

        // Step 5: Assign exercises to workout day
        var assignments = new List<WorkoutPlanExerciseResponse>();

        for (var i = 0; i < exerciseIds.Count; i++)
        {
            var assignmentResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
                new AddWorkoutPlanExerciseRequest
                {
                    ExerciseId = exerciseIds[i],
                    ExerciseOrder = i + 1,
                    Sets = 3,
                    Reps = 12,
                    RestSeconds = 60,
                    Notes = $"Original assignment {i + 1}"
                });

            Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

            var assignment = await assignmentResponse.Content
                .ReadFromJsonAsync<WorkoutPlanExerciseResponse>();

            Assert.NotNull(assignment);

            assignments.Add(assignment);
        }

        // Step 6: Attempt to reorder exercises using invalid positions
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises/reorder",
            new ReorderWorkoutPlanExercisesRequest
            {
                Exercises = new List<ReorderWorkoutPlanExerciseItem>
                {
            new()
            {
                AssignmentId = assignments[0].Id,
                ExerciseOrder = firstOrder
            },
            new()
            {
                AssignmentId = assignments[1].Id,
                ExerciseOrder = secondOrder
            },
            new()
            {
                AssignmentId = assignments[2].Id,
                ExerciseOrder = thirdOrder
            }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, reorderResponse.StatusCode);

        // Step 7: Verify original exercise positions remain unchanged
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        var savedDay = Assert.Single(
            savedPlan.Days,
            d => d.Id == day.Id);

        Assert.Equal(3, savedDay.Exercises.Count);

        // Verify original assignment IDs and positions
        Assert.Equal(assignments[0].Id, savedDay.Exercises[0].Id);
        Assert.Equal(1, savedDay.Exercises[0].ExerciseOrder);

        Assert.Equal(assignments[1].Id, savedDay.Exercises[1].Id);
        Assert.Equal(2, savedDay.Exercises[1].ExerciseOrder);

        Assert.Equal(assignments[2].Id, savedDay.Exercises[2].Id);
        Assert.Equal(3, savedDay.Exercises[2].ExerciseOrder);
    }

    [Fact]
    public async Task UnauthenticatedUser_CannotReorderWorkoutPlanExercises()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Prepare a valid reorder request
        var planId = Guid.NewGuid();
        var dayId = Guid.NewGuid();

        var request = new ReorderWorkoutPlanExercisesRequest
        {
            Exercises = new List<ReorderWorkoutPlanExerciseItem>
        {
            new()
            {
                AssignmentId = Guid.NewGuid(),
                ExerciseOrder = 1
            },
            new()
            {
                AssignmentId = Guid.NewGuid(),
                ExerciseOrder = 2
            }
        }
        };

        // Step 2: Attempt to reorder without authentication
        var response = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{planId}/days/{dayId}/exercises/reorder",
            request);

        // Step 3: Verify authentication is required
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Member_CannotReorderWorkoutPlanExercises()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Member
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        // Step 2: Prepare exercise reorder request
        var planId = Guid.NewGuid();
        var dayId = Guid.NewGuid();

        var request = new ReorderWorkoutPlanExercisesRequest
        {
            Exercises = new List<ReorderWorkoutPlanExerciseItem>
        {
            new()
            {
                AssignmentId = Guid.NewGuid(),
                ExerciseOrder = 1
            },
            new()
            {
                AssignmentId = Guid.NewGuid(),
                ExerciseOrder = 2
            }
        }
        };

        // Step 3: Attempt exercise reordering as Member
        var response = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{planId}/days/{dayId}/exercises/reorder",
            request);

        // Step 4: Verify Member access is forbidden
        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanReorderWorkoutPlanDays()
{
    using var factory = new FitnessPlatformWebApplicationFactory();

    await TestIdentitySeeder.SeedAsync(factory.Services);

    using var client = factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    // Step 1: Authenticate as Administrator
    var loginResponse = await client.PostAsJsonAsync(
        "/api/auth/login",
        new
        {
            Email = TestIdentitySeeder.AdminEmail,
            Password = TestIdentitySeeder.TestPassword
        });

    Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

    var login = await loginResponse.Content
        .ReadFromJsonAsync<LoginTestResponse>();

    Assert.NotNull(login);

    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", login.AccessToken);

    // Step 2: Create workout plan
    var planResponse = await client.PostAsJsonAsync(
        "/api/admin/workout-plans",
        new CreateWorkoutPlanRequest
        {
            Name = $"Day Reorder Test {Guid.NewGuid():N}",
            GoalCode = "WEIGHT_LOSS",
            ExperienceLevel = "Beginner",
            TrainingLocation = "Gym",
            DaysPerWeek = 3,
            DurationWeeks = 12
        });

    Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

    var plan = await planResponse.Content
        .ReadFromJsonAsync<WorkoutPlanResponse>();

    Assert.NotNull(plan);

    // Step 3: Create three workout days
    var dayNames = new[] { "Chest", "Back", "Legs" };
    var days = new List<WorkoutPlanDayResponse>();

    for (var i = 0; i < dayNames.Length; i++)
    {
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = i + 1,
                Name = dayNames[i],
                Description = $"{dayNames[i]} workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);
        days.Add(day);
    }

    // Step 4: Reorder workout days
    var reorderResponse = await client.PutAsJsonAsync(
        $"/api/admin/workout-plans/{plan.Id}/days/reorder",
        new ReorderWorkoutPlanDaysRequest
        {
            Days = new List<ReorderWorkoutPlanDayItem>
            {
                new()
                {
                    DayId = days[0].Id,
                    DayNumber = 2
                },
                new()
                {
                    DayId = days[1].Id,
                    DayNumber = 3
                },
                new()
                {
                    DayId = days[2].Id,
                    DayNumber = 1
                }
            }
        });

    Assert.Equal(HttpStatusCode.OK, reorderResponse.StatusCode);

    // Step 5: Verify API response
    var reordered = await reorderResponse.Content
        .ReadFromJsonAsync<List<WorkoutPlanDayResponse>>();

    Assert.NotNull(reordered);
    Assert.Equal(3, reordered.Count);

    Assert.Equal(days[2].Id, reordered[0].Id);
    Assert.Equal(1, reordered[0].DayNumber);

    Assert.Equal(days[0].Id, reordered[1].Id);
    Assert.Equal(2, reordered[1].DayNumber);

    Assert.Equal(days[1].Id, reordered[2].Id);
    Assert.Equal(3, reordered[2].DayNumber);

    // Step 6: Verify day details are preserved
    foreach (var original in days)
    {
        var updated = Assert.Single(
            reordered,
            x => x.Id == original.Id);

        Assert.Equal(original.WorkoutPlanId, updated.WorkoutPlanId);
        Assert.Equal(original.Name, updated.Name);
        Assert.Equal(original.Description, updated.Description);
        Assert.Equal(original.IsRestDay, updated.IsRestDay);
        Assert.Equal(
            original.EstimatedDurationMinutes,
            updated.EstimatedDurationMinutes);
    }

    // Step 7: Retrieve workout plan to verify persistence
    var detailsResponse = await client.GetAsync(
        $"/api/admin/workout-plans/{plan.Id}");

    Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

    var savedPlan = await detailsResponse.Content
        .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

    Assert.NotNull(savedPlan);

    var savedDays = savedPlan.Days
        .OrderBy(x => x.DayNumber)
        .ToList();

    Assert.Equal(3, savedDays.Count);

    Assert.Equal(days[2].Id, savedDays[0].Id);
    Assert.Equal(1, savedDays[0].DayNumber);

    Assert.Equal(days[0].Id, savedDays[1].Id);
    Assert.Equal(2, savedDays[1].DayNumber);

    Assert.Equal(days[1].Id, savedDays[2].Id);
    Assert.Equal(3, savedDays[2].DayNumber);
}

    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanDays_WithDuplicateDayNumbers()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Day Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create three workout days
        var dayNames = new[] { "Chest", "Back", "Legs" };
        var days = new List<WorkoutPlanDayResponse>();

        for (var i = 0; i < dayNames.Length; i++)
        {
            var dayResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days",
                new CreateWorkoutPlanDayRequest
                {
                    DayNumber = i + 1,
                    Name = dayNames[i],
                    Description = $"{dayNames[i]} workout",
                    IsRestDay = false,
                    EstimatedDurationMinutes = 60
                });

            Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

            var day = await dayResponse.Content
                .ReadFromJsonAsync<WorkoutPlanDayResponse>();

            Assert.NotNull(day);
            days.Add(day);
        }

        // Step 4: Attempt reordering with duplicate day numbers
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/reorder",
            new ReorderWorkoutPlanDaysRequest
            {
                Days = new List<ReorderWorkoutPlanDayItem>
                {
            new()
            {
                DayId = days[0].Id,
                DayNumber = 1
            },
            new()
            {
                DayId = days[1].Id,
                DayNumber = 1
            },
            new()
            {
                DayId = days[2].Id,
                DayNumber = 2
            }
                }
            });

        // Step 5: Verify duplicate day numbers are rejected
        Assert.Equal(HttpStatusCode.BadRequest, reorderResponse.StatusCode);

        // Step 6: Retrieve workout plan
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        // Step 7: Verify original day numbers remain unchanged
        var savedDays = savedPlan.Days
            .OrderBy(x => x.DayNumber)
            .ToList();

        Assert.Equal(3, savedDays.Count);

        Assert.Equal(days[0].Id, savedDays[0].Id);
        Assert.Equal(1, savedDays[0].DayNumber);

        Assert.Equal(days[1].Id, savedDays[1].Id);
        Assert.Equal(2, savedDays[1].DayNumber);

        Assert.Equal(days[2].Id, savedDays[2].Id);
        Assert.Equal(3, savedDays[2].DayNumber);
    }

    [Theory]
    [InlineData(0, 2, 3)]
    [InlineData(-1, 2, 3)]
    [InlineData(1, 3, 5)]
    [InlineData(2, 3, 4)]
    public async Task Admin_CannotReorderWorkoutPlanDays_WithInvalidDayNumbers(
    int firstDayNumber,
    int secondDayNumber,
    int thirdDayNumber)
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Day Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create three workout days
        var dayNames = new[] { "Chest", "Back", "Legs" };
        var days = new List<WorkoutPlanDayResponse>();

        for (var i = 0; i < dayNames.Length; i++)
        {
            var dayResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days",
                new CreateWorkoutPlanDayRequest
                {
                    DayNumber = i + 1,
                    Name = dayNames[i],
                    Description = $"{dayNames[i]} workout",
                    IsRestDay = false,
                    EstimatedDurationMinutes = 60
                });

            Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

            var day = await dayResponse.Content
                .ReadFromJsonAsync<WorkoutPlanDayResponse>();

            Assert.NotNull(day);
            days.Add(day);
        }

        // Step 4: Attempt reordering with invalid day numbers
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/reorder",
            new ReorderWorkoutPlanDaysRequest
            {
                Days = new List<ReorderWorkoutPlanDayItem>
                {
            new()
            {
                DayId = days[0].Id,
                DayNumber = firstDayNumber
            },
            new()
            {
                DayId = days[1].Id,
                DayNumber = secondDayNumber
            },
            new()
            {
                DayId = days[2].Id,
                DayNumber = thirdDayNumber
            }
                }
            });

        // Step 5: Verify invalid day numbers are rejected
        Assert.Equal(HttpStatusCode.BadRequest, reorderResponse.StatusCode);

        // Step 6: Retrieve workout plan
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        // Step 7: Verify original day numbers remain unchanged
        var savedDays = savedPlan.Days
            .OrderBy(x => x.DayNumber)
            .ToList();

        Assert.Equal(3, savedDays.Count);

        Assert.Equal(days[0].Id, savedDays[0].Id);
        Assert.Equal(1, savedDays[0].DayNumber);

        Assert.Equal(days[1].Id, savedDays[1].Id);
        Assert.Equal(2, savedDays[1].DayNumber);

        Assert.Equal(days[2].Id, savedDays[2].Id);
        Assert.Equal(3, savedDays[2].DayNumber);
    }

    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanDays_WithMissingDays()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Day Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create three workout days
        var dayNames = new[] { "Chest", "Back", "Legs" };
        var days = new List<WorkoutPlanDayResponse>();

        for (var i = 0; i < dayNames.Length; i++)
        {
            var dayResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days",
                new CreateWorkoutPlanDayRequest
                {
                    DayNumber = i + 1,
                    Name = dayNames[i],
                    Description = $"{dayNames[i]} workout",
                    IsRestDay = false,
                    EstimatedDurationMinutes = 60
                });

            Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

            var day = await dayResponse.Content
                .ReadFromJsonAsync<WorkoutPlanDayResponse>();

            Assert.NotNull(day);
            days.Add(day);
        }

        // Step 4: Attempt reordering without including every workout day
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/reorder",
            new ReorderWorkoutPlanDaysRequest
            {
                Days = new List<ReorderWorkoutPlanDayItem>
                {
            new()
            {
                DayId = days[0].Id,
                DayNumber = 2
            },
            new()
            {
                DayId = days[1].Id,
                DayNumber = 1
            }
                }
            });

        // Step 5: Verify incomplete reordering is rejected
        Assert.Equal(HttpStatusCode.BadRequest, reorderResponse.StatusCode);

        // Step 6: Retrieve workout plan
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        // Step 7: Verify original day numbers remain unchanged
        var savedDays = savedPlan.Days
            .OrderBy(x => x.DayNumber)
            .ToList();

        Assert.Equal(3, savedDays.Count);

        Assert.Equal(days[0].Id, savedDays[0].Id);
        Assert.Equal(1, savedDays[0].DayNumber);

        Assert.Equal(days[1].Id, savedDays[1].Id);
        Assert.Equal(2, savedDays[1].DayNumber);

        Assert.Equal(days[2].Id, savedDays[2].Id);
        Assert.Equal(3, savedDays[2].DayNumber);
    }

    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanDays_WithDayFromAnotherPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Day Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create three workout days
        var dayNames = new[] { "Chest", "Back", "Legs" };
        var days = new List<WorkoutPlanDayResponse>();

        for (var i = 0; i < dayNames.Length; i++)
        {
            var dayResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days",
                new CreateWorkoutPlanDayRequest
                {
                    DayNumber = i + 1,
                    Name = dayNames[i],
                    Description = $"{dayNames[i]} workout",
                    IsRestDay = false,
                    EstimatedDurationMinutes = 60
                });

            Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

            var day = await dayResponse.Content
                .ReadFromJsonAsync<WorkoutPlanDayResponse>();

            Assert.NotNull(day);
            days.Add(day);
        }

        // Step 4: Create another workout plan
        var otherPlanResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Other Plan {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, otherPlanResponse.StatusCode);

        var otherPlan = await otherPlanResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(otherPlan);

        // Step 5: Create a workout day in Plan B
        var otherDayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{otherPlan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Shoulders",
                Description = "Shoulder workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, otherDayResponse.StatusCode);

        var otherDay = await otherDayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(otherDay);

        // Step 6: Attempt to reorder Plan A using a day from Plan B
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/reorder",
            new ReorderWorkoutPlanDaysRequest
            {
                Days = new List<ReorderWorkoutPlanDayItem>
                {
            new()
            {
                DayId = days[0].Id,
                DayNumber = 1
            },
            new()
            {
                DayId = days[1].Id,
                DayNumber = 2
            },
            new()
            {
                DayId = otherDay.Id,
                DayNumber = 3
            }
                }
            });

        // Step 7: Verify cross-plan reordering is rejected
        Assert.Equal(HttpStatusCode.BadRequest, reorderResponse.StatusCode);
        // Step 8: Verify Plan A remains unchanged
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        var savedDays = savedPlan.Days
            .OrderBy(x => x.DayNumber)
            .ToList();

        Assert.Equal(3, savedDays.Count);

        Assert.Equal(days[0].Id, savedDays[0].Id);
        Assert.Equal(1, savedDays[0].DayNumber);

        Assert.Equal(days[1].Id, savedDays[1].Id);
        Assert.Equal(2, savedDays[1].DayNumber);

        Assert.Equal(days[2].Id, savedDays[2].Id);
        Assert.Equal(3, savedDays[2].DayNumber);

        // Step 9: Verify Plan B remains unchanged
        var otherDetailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{otherPlan.Id}");

        Assert.Equal(HttpStatusCode.OK, otherDetailsResponse.StatusCode);

        var savedOtherPlan = await otherDetailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedOtherPlan);

        var savedOtherDay = Assert.Single(savedOtherPlan.Days);

        Assert.Equal(otherDay.Id, savedOtherDay.Id);
        Assert.Equal(1, savedOtherDay.DayNumber);
        Assert.Equal("Shoulders", savedOtherDay.Name);
    }

    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanDays_InReview()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Day Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create three workout days
        var dayNames = new[] { "Chest", "Back", "Legs" };
        var days = new List<WorkoutPlanDayResponse>();

        for (var i = 0; i < dayNames.Length; i++)
        {
            var dayResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days",
                new CreateWorkoutPlanDayRequest
                {
                    DayNumber = i + 1,
                    Name = dayNames[i],
                    Description = $"{dayNames[i]} workout",
                    IsRestDay = false,
                    EstimatedDurationMinutes = 60
                });

            Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

            var day = await dayResponse.Content
                .ReadFromJsonAsync<WorkoutPlanDayResponse>();

            Assert.NotNull(day);
            days.Add(day);
        }


        // Step 4: Assign one exercise to each non-rest workout day
        for (var i = 0; i < days.Count; i++)
        {
            // Create master exercise
            var exerciseResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                new SaveExerciseRequest
                {
                    Code = $"EX-{Guid.NewGuid():N}",
                    Name = $"Review Test Exercise {i + 1}",
                    Description = "Exercise for workout plan review validation",
                    MuscleGroup = "Chest",
                    Equipment = "Dumbbell",
                    DifficultyLevel = "Beginner",
                    TrainingLocation = "Gym",
                    Instructions = "Perform controlled repetitions."
                });

            Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

            var exercise = await exerciseResponse.Content
                .ReadFromJsonAsync<ExerciseResponse>();

            Assert.NotNull(exercise);

            // Assign exercise to workout day
            var assignmentResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days/{days[i].Id}/exercises",
                new AddWorkoutPlanExerciseRequest
                {
                    ExerciseId = exercise.Id,
                    ExerciseOrder = 1,
                    Sets = 3,
                    Reps = 12,
                    RestSeconds = 60,
                    Notes = "Exercise assigned for review validation"
                });

            Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);
        }

        // Step 4: Submit workout plan for review
        var submitResponse = await client.PostAsync(
    $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
    null);

        var submitError = await submitResponse.Content.ReadAsStringAsync();

        Assert.True(
            submitResponse.StatusCode == HttpStatusCode.OK,
            $"Submit for review failed. Status: {submitResponse.StatusCode}. Response: {submitError}");

        // Step 5: Attempt reordering while plan is InReview
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/reorder",
            new ReorderWorkoutPlanDaysRequest
            {
                Days = new List<ReorderWorkoutPlanDayItem>
                {
            new()
            {
                DayId = days[0].Id,
                DayNumber = 3
            },
            new()
            {
                DayId = days[1].Id,
                DayNumber = 1
            },
            new()
            {
                DayId = days[2].Id,
                DayNumber = 2
            }
                }
            });

        // Step 6: Verify reordering is blocked
        Assert.Equal(HttpStatusCode.Conflict, reorderResponse.StatusCode);



        // Step 7: Retrieve workout plan
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        // Verify workflow status
        Assert.Equal("In Review", savedPlan.ReviewStatus);

        // Verify workout day positions remain unchanged
        var savedDays = savedPlan.Days
            .OrderBy(x => x.DayNumber)
            .ToList();

        Assert.Equal(3, savedDays.Count);

        Assert.Equal(days[0].Id, savedDays[0].Id);
        Assert.Equal(1, savedDays[0].DayNumber);

        Assert.Equal(days[1].Id, savedDays[1].Id);
        Assert.Equal(2, savedDays[1].DayNumber);

        Assert.Equal(days[2].Id, savedDays[2].Id);
        Assert.Equal(3, savedDays[2].DayNumber);
    }

    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanDays_Approved()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Day Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create three workout days
        var dayNames = new[] { "Chest", "Back", "Legs" };
        var days = new List<WorkoutPlanDayResponse>();

        for (var i = 0; i < dayNames.Length; i++)
        {
            var dayResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days",
                new CreateWorkoutPlanDayRequest
                {
                    DayNumber = i + 1,
                    Name = dayNames[i],
                    Description = $"{dayNames[i]} workout",
                    IsRestDay = false,
                    EstimatedDurationMinutes = 60
                });

            Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

            var day = await dayResponse.Content
                .ReadFromJsonAsync<WorkoutPlanDayResponse>();

            Assert.NotNull(day);
            days.Add(day);
        }


        // Step 4: Assign one exercise to each non-rest workout day
        for (var i = 0; i < days.Count; i++)
        {
            // Create master exercise
            var exerciseResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                new SaveExerciseRequest
                {
                    Code = $"EX-{Guid.NewGuid():N}",
                    Name = $"Review Test Exercise {i + 1}",
                    Description = "Exercise for workout plan review validation",
                    MuscleGroup = "Chest",
                    Equipment = "Dumbbell",
                    DifficultyLevel = "Beginner",
                    TrainingLocation = "Gym",
                    Instructions = "Perform controlled repetitions."
                });

            Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

            var exercise = await exerciseResponse.Content
                .ReadFromJsonAsync<ExerciseResponse>();

            Assert.NotNull(exercise);

            // Assign exercise to workout day
            var assignmentResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days/{days[i].Id}/exercises",
                new AddWorkoutPlanExerciseRequest
                {
                    ExerciseId = exercise.Id,
                    ExerciseOrder = 1,
                    Sets = 3,
                    Reps = 12,
                    RestSeconds = 60,
                    Notes = "Exercise assigned for review validation"
                });

            Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);
        }

        // Step 4: Submit workout plan for review
        var submitResponse = await client.PostAsync(
    $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
    null);

        var submitError = await submitResponse.Content.ReadAsStringAsync();

        Assert.True(
            submitResponse.StatusCode == HttpStatusCode.OK,
            $"Submit for review failed. Status: {submitResponse.StatusCode}. Response: {submitError}");

        // Step 5: Approve the workout plan
        var approveResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/approve",
            null);

        var approveError = await approveResponse.Content.ReadAsStringAsync();

        Assert.True(
            approveResponse.StatusCode == HttpStatusCode.OK,
            $"Workout plan approval failed. Status: {approveResponse.StatusCode}. Response: {approveError}");

        // Step 5: Attempt reordering while plan is InReview
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/reorder",
            new ReorderWorkoutPlanDaysRequest
            {
                Days = new List<ReorderWorkoutPlanDayItem>
                {
            new()
            {
                DayId = days[0].Id,
                DayNumber = 3
            },
            new()
            {
                DayId = days[1].Id,
                DayNumber = 1
            },
            new()
            {
                DayId = days[2].Id,
                DayNumber = 2
            }
                }
            });

        // Step 6: Verify reordering is blocked
        Assert.Equal(HttpStatusCode.Conflict, reorderResponse.StatusCode);



        // Step 7: Retrieve workout plan
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        // Verify workflow status
        // Verify workflow status
        Assert.Equal("Approved", savedPlan.ReviewStatus);

        // Verify workout day positions remain unchanged
        var savedDays = savedPlan.Days
            .OrderBy(x => x.DayNumber)
            .ToList();

        Assert.Equal(3, savedDays.Count);

        Assert.Equal(days[0].Id, savedDays[0].Id);
        Assert.Equal(1, savedDays[0].DayNumber);

        Assert.Equal(days[1].Id, savedDays[1].Id);
        Assert.Equal(2, savedDays[1].DayNumber);

        Assert.Equal(days[2].Id, savedDays[2].Id);
        Assert.Equal(3, savedDays[2].DayNumber);
    }

    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanDays_Inactive()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Inactive Day Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create three workout days
        var dayNames = new[] { "Chest", "Back", "Legs" };
        var days = new List<WorkoutPlanDayResponse>();

        for (var i = 0; i < dayNames.Length; i++)
        {
            var dayResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days",
                new CreateWorkoutPlanDayRequest
                {
                    DayNumber = i + 1,
                    Name = dayNames[i],
                    Description = $"{dayNames[i]} workout",
                    IsRestDay = false,
                    EstimatedDurationMinutes = 60
                });

            Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

            var day = await dayResponse.Content
                .ReadFromJsonAsync<WorkoutPlanDayResponse>();

            Assert.NotNull(day);
            days.Add(day);
        }

        // Step 4: Deactivate workout plan
        var deactivateResponse = await client.PatchAsync(
            $"/api/admin/workout-plans/{plan.Id}/deactivate",
            null);

        var deactivateError = await deactivateResponse.Content.ReadAsStringAsync();

        Assert.True(
            deactivateResponse.StatusCode == HttpStatusCode.OK,
            $"Deactivation failed. Status: {deactivateResponse.StatusCode}. Response: {deactivateError}");

        // Step 5: Attempt valid reordering on inactive workout plan
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/reorder",
            new ReorderWorkoutPlanDaysRequest
            {
                Days = new List<ReorderWorkoutPlanDayItem>
                {
                new()
                {
                    DayId = days[0].Id,
                    DayNumber = 3
                },
                new()
                {
                    DayId = days[1].Id,
                    DayNumber = 1
                },
                new()
                {
                    DayId = days[2].Id,
                    DayNumber = 2
                }
                }
            });

        // Step 6: Verify inactive workout plan cannot be reordered
        Assert.Equal(HttpStatusCode.Conflict, reorderResponse.StatusCode);

        // Step 7: Retrieve workout plan
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        // Step 8: Verify workout plan remains inactive
        Assert.False(savedPlan.IsActive);

        // Step 9: Verify original workout day numbers remain unchanged
        var savedDays = savedPlan.Days
            .OrderBy(x => x.DayNumber)
            .ToList();

        Assert.Equal(3, savedDays.Count);

        Assert.Equal(days[0].Id, savedDays[0].Id);
        Assert.Equal(1, savedDays[0].DayNumber);

        Assert.Equal(days[1].Id, savedDays[1].Id);
        Assert.Equal(2, savedDays[1].DayNumber);

        Assert.Equal(days[2].Id, savedDays[2].Id);
        Assert.Equal(3, savedDays[2].DayNumber);
    }

    [Fact]
    public async Task Admin_CanReorderWorkoutPlanDays_Rejected()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Rejected Day Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create three workout days
        var dayNames = new[] { "Chest", "Back", "Legs" };
        var days = new List<WorkoutPlanDayResponse>();

        for (var i = 0; i < dayNames.Length; i++)
        {
            var dayResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days",
                new CreateWorkoutPlanDayRequest
                {
                    DayNumber = i + 1,
                    Name = dayNames[i],
                    Description = $"{dayNames[i]} workout",
                    IsRestDay = false,
                    EstimatedDurationMinutes = 60
                });

            Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

            var day = await dayResponse.Content
                .ReadFromJsonAsync<WorkoutPlanDayResponse>();

            Assert.NotNull(day);
            days.Add(day);
        }

        // Step 4: Assign one exercise to each workout day
        for (var i = 0; i < days.Count; i++)
        {
            var exerciseResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                new SaveExerciseRequest
                {
                    Code = $"EX-{Guid.NewGuid():N}",
                    Name = $"Rejected Review Exercise {i + 1}",
                    Description = "Exercise for rejected workout plan validation",
                    MuscleGroup = "Chest",
                    Equipment = "Dumbbell",
                    DifficultyLevel = "Beginner",
                    TrainingLocation = "Gym",
                    Instructions = "Perform controlled repetitions."
                });

            Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

            var exercise = await exerciseResponse.Content
                .ReadFromJsonAsync<ExerciseResponse>();

            Assert.NotNull(exercise);

            var assignmentResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days/{days[i].Id}/exercises",
                new AddWorkoutPlanExerciseRequest
                {
                    ExerciseId = exercise.Id,
                    ExerciseOrder = 1,
                    Sets = 3,
                    Reps = 12,
                    RestSeconds = 60,
                    Notes = "Exercise assigned for review validation"
                });

            Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);
        }

        // Step 5: Submit workout plan for review
        var submitResponse = await client.PostAsync(
            $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
            null);

        var submitError = await submitResponse.Content.ReadAsStringAsync();

        Assert.True(
            submitResponse.StatusCode == HttpStatusCode.OK,
            $"Submit for review failed. Status: {submitResponse.StatusCode}. Response: {submitError}");

        // Step 6: Reject workout plan
        var rejectResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/reject",
            new RejectWorkoutPlanRequest
            {
                Reason = "Workout schedule requires changes."
            });

        var rejectError = await rejectResponse.Content.ReadAsStringAsync();

        Assert.True(
            rejectResponse.StatusCode == HttpStatusCode.OK,
            $"Workout plan rejection failed. Status: {rejectResponse.StatusCode}. Response: {rejectError}");

        // Step 7: Reorder days in rejected workout plan
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/reorder",
            new ReorderWorkoutPlanDaysRequest
            {
                Days = new List<ReorderWorkoutPlanDayItem>
                {
                new()
                {
                    DayId = days[0].Id,
                    DayNumber = 3
                },
                new()
                {
                    DayId = days[1].Id,
                    DayNumber = 1
                },
                new()
                {
                    DayId = days[2].Id,
                    DayNumber = 2
                }
                }
            });

        // Step 8: Verify reordering succeeds
        Assert.Equal(HttpStatusCode.OK, reorderResponse.StatusCode);

        var reordered = await reorderResponse.Content
            .ReadFromJsonAsync<List<WorkoutPlanDayResponse>>();

        Assert.NotNull(reordered);
        Assert.Equal(3, reordered.Count);

        Assert.Equal(days[1].Id, reordered[0].Id);
        Assert.Equal(1, reordered[0].DayNumber);

        Assert.Equal(days[2].Id, reordered[1].Id);
        Assert.Equal(2, reordered[1].DayNumber);

        Assert.Equal(days[0].Id, reordered[2].Id);
        Assert.Equal(3, reordered[2].DayNumber);

        // Step 9: Retrieve workout plan
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        // Step 10: Verify workout plan remains Rejected and Active
        Assert.Equal("Rejected", savedPlan.ReviewStatus);
        Assert.True(savedPlan.IsActive);

        // Step 11: Verify reordered day numbers are persisted
        var savedDays = savedPlan.Days
            .OrderBy(x => x.DayNumber)
            .ToList();

        Assert.Equal(3, savedDays.Count);

        Assert.Equal(days[1].Id, savedDays[0].Id);
        Assert.Equal(1, savedDays[0].DayNumber);

        Assert.Equal(days[2].Id, savedDays[1].Id);
        Assert.Equal(2, savedDays[1].DayNumber);

        Assert.Equal(days[0].Id, savedDays[2].Id);
        Assert.Equal(3, savedDays[2].DayNumber);
    }
    [Fact]
    public async Task UnauthenticatedUser_CannotReorderWorkoutPlanDays()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Attempt reordering without authentication
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{Guid.NewGuid()}/days/reorder",
            new ReorderWorkoutPlanDaysRequest
            {
                Days = new List<ReorderWorkoutPlanDayItem>
                {
                new()
                {
                    DayId = Guid.NewGuid(),
                    DayNumber = 1
                }
                }
            });

        // Step 2: Verify unauthenticated access is denied
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            reorderResponse.StatusCode);
    }

    [Fact]
    public async Task Member_CannotReorderWorkoutPlanDays()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Member
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Attempt to reorder workout days as Member
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{Guid.NewGuid()}/days/reorder",
            new ReorderWorkoutPlanDaysRequest
            {
                Days = new List<ReorderWorkoutPlanDayItem>
                {
                new()
                {
                    DayId = Guid.NewGuid(),
                    DayNumber = 1
                }
                }
            });

        // Step 3: Verify Member access is forbidden
        Assert.Equal(
            HttpStatusCode.Forbidden,
            reorderResponse.StatusCode);
    }
    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanDays_WithDuplicateDayIds()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Duplicate Day ID Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create three workout days
        var dayNames = new[] { "Chest", "Back", "Legs" };
        var days = new List<WorkoutPlanDayResponse>();

        for (var i = 0; i < dayNames.Length; i++)
        {
            var dayResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days",
                new CreateWorkoutPlanDayRequest
                {
                    DayNumber = i + 1,
                    Name = dayNames[i],
                    Description = $"{dayNames[i]} workout",
                    IsRestDay = false,
                    EstimatedDurationMinutes = 60
                });

            Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

            var day = await dayResponse.Content
                .ReadFromJsonAsync<WorkoutPlanDayResponse>();

            Assert.NotNull(day);

            days.Add(day);
        }

        // Step 4: Attempt reordering with duplicate workout day IDs
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/reorder",
            new ReorderWorkoutPlanDaysRequest
            {
                Days = new List<ReorderWorkoutPlanDayItem>
                {
                new()
                {
                    DayId = days[0].Id,
                    DayNumber = 1
                },
                new()
                {
                    DayId = days[0].Id, // Duplicate Day ID
                    DayNumber = 2
                },
                new()
                {
                    DayId = days[2].Id,
                    DayNumber = 3
                }
                }
            });

        // Step 5: Verify duplicate workout day IDs are rejected
        Assert.Equal(HttpStatusCode.BadRequest, reorderResponse.StatusCode);

        // Step 6: Retrieve workout plan
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        // Step 7: Verify original workout day numbers remain unchanged
        var savedDays = savedPlan.Days
            .OrderBy(x => x.DayNumber)
            .ToList();

        Assert.Equal(3, savedDays.Count);

        Assert.Equal(days[0].Id, savedDays[0].Id);
        Assert.Equal(1, savedDays[0].DayNumber);

        Assert.Equal(days[1].Id, savedDays[1].Id);
        Assert.Equal(2, savedDays[1].DayNumber);

        Assert.Equal(days[2].Id, savedDays[2].Id);
        Assert.Equal(3, savedDays[2].DayNumber);
    }

    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanDays_WithEmptyDays()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Empty Days Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create three workout days
        var dayNames = new[] { "Chest", "Back", "Legs" };
        var days = new List<WorkoutPlanDayResponse>();

        for (var i = 0; i < dayNames.Length; i++)
        {
            var dayResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days",
                new CreateWorkoutPlanDayRequest
                {
                    DayNumber = i + 1,
                    Name = dayNames[i],
                    Description = $"{dayNames[i]} workout",
                    IsRestDay = false,
                    EstimatedDurationMinutes = 60
                });

            Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

            var day = await dayResponse.Content
                .ReadFromJsonAsync<WorkoutPlanDayResponse>();

            Assert.NotNull(day);

            days.Add(day);
        }

        // Step 4: Attempt reordering with an empty Days collection
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/reorder",
            new ReorderWorkoutPlanDaysRequest
            {
                Days = new List<ReorderWorkoutPlanDayItem>()
            });

        // Step 5: Verify empty workout day requests are rejected
        Assert.Equal(HttpStatusCode.BadRequest, reorderResponse.StatusCode);

        // Step 6: Retrieve workout plan
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        // Step 7: Verify original workout day numbers remain unchanged
        var savedDays = savedPlan.Days
            .OrderBy(x => x.DayNumber)
            .ToList();

        Assert.Equal(3, savedDays.Count);

        Assert.Equal(days[0].Id, savedDays[0].Id);
        Assert.Equal(1, savedDays[0].DayNumber);

        Assert.Equal(days[1].Id, savedDays[1].Id);
        Assert.Equal(2, savedDays[1].DayNumber);

        Assert.Equal(days[2].Id, savedDays[2].Id);
        Assert.Equal(3, savedDays[2].DayNumber);
    }

    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanDays_WithEmptyDayId()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Empty Day ID Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create three workout days
        var dayNames = new[] { "Chest", "Back", "Legs" };
        var days = new List<WorkoutPlanDayResponse>();

        for (var i = 0; i < dayNames.Length; i++)
        {
            var dayResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days",
                new CreateWorkoutPlanDayRequest
                {
                    DayNumber = i + 1,
                    Name = dayNames[i],
                    Description = $"{dayNames[i]} workout",
                    IsRestDay = false,
                    EstimatedDurationMinutes = 60
                });

            Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

            var day = await dayResponse.Content
                .ReadFromJsonAsync<WorkoutPlanDayResponse>();

            Assert.NotNull(day);

            days.Add(day);
        }

        // Step 4: Attempt reordering with an empty workout day ID
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/reorder",
            new ReorderWorkoutPlanDaysRequest
            {
                Days = new List<ReorderWorkoutPlanDayItem>
                {
                new()
                {
                    DayId = days[0].Id,
                    DayNumber = 1
                },
                new()
                {
                    DayId = Guid.Empty, // Invalid workout day ID
                    DayNumber = 2
                },
                new()
                {
                    DayId = days[2].Id,
                    DayNumber = 3
                }
                }
            });

        // Step 5: Verify empty workout day ID is rejected
        Assert.Equal(HttpStatusCode.BadRequest, reorderResponse.StatusCode);

        // Step 6: Retrieve workout plan
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        // Step 7: Verify original workout day numbers remain unchanged
        var savedDays = savedPlan.Days
            .OrderBy(x => x.DayNumber)
            .ToList();

        Assert.Equal(3, savedDays.Count);

        Assert.Equal(days[0].Id, savedDays[0].Id);
        Assert.Equal(1, savedDays[0].DayNumber);

        Assert.Equal(days[1].Id, savedDays[1].Id);
        Assert.Equal(2, savedDays[1].DayNumber);

        Assert.Equal(days[2].Id, savedDays[2].Id);
        Assert.Equal(3, savedDays[2].DayNumber);
    }
    [Fact]
    public async Task Admin_CannotReorderWorkoutPlanDays_WhenPlanDoesNotExist()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Generate a nonexistent workout plan ID
        var nonexistentPlanId = Guid.NewGuid();

        // Step 3: Attempt to reorder workout days
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{nonexistentPlanId}/days/reorder",
            new ReorderWorkoutPlanDaysRequest
            {
                Days = new List<ReorderWorkoutPlanDayItem>
                {
                new()
                {
                    DayId = Guid.NewGuid(),
                    DayNumber = 1
                }
                }
            });

        // Step 4: Verify API returns 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, reorderResponse.StatusCode);

        // Step 5: Verify the error message
        var errorResponse = await reorderResponse.Content.ReadAsStringAsync();

        Assert.Contains(
            "Workout plan not found",
            errorResponse,
            StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Admin_CanReorderWorkoutPlanDays_WithoutChangingExercisesOrRestDays()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Preserve Exercises Reorder Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        // Step 3: Create Chest, Rest, and Back days
        var dayNames = new[] { "Chest", "Rest", "Back" };
        var days = new List<WorkoutPlanDayResponse>();

        for (var i = 0; i < dayNames.Length; i++)
        {
            var isRestDay = i == 1;

            var dayResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days",
                new CreateWorkoutPlanDayRequest
                {
                    DayNumber = i + 1,
                    Name = dayNames[i],
                    Description = $"{dayNames[i]} day",
                    IsRestDay = isRestDay,
                    EstimatedDurationMinutes = isRestDay ? null : 60
                });

            Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

            var day = await dayResponse.Content
                .ReadFromJsonAsync<WorkoutPlanDayResponse>();

            Assert.NotNull(day);
            days.Add(day);
        }

        // Step 4: Create and assign exercises to non-rest days
        var assignedExerciseIds = new Dictionary<Guid, Guid>();

        foreach (var dayIndex in new[] { 0, 2 })
        {
            var exerciseName = dayIndex == 0
                ? "Bench Press"
                : "Lat Pulldown";

            var exerciseResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                new SaveExerciseRequest
                {
                    Code = $"EX-{Guid.NewGuid():N}",
                    Name = $"{exerciseName} {Guid.NewGuid():N}",
                    Description = "Exercise preservation test",
                    MuscleGroup = dayIndex == 0 ? "Chest" : "Back",
                    Equipment = "Machine",
                    DifficultyLevel = "Beginner",
                    TrainingLocation = "Gym",
                    Instructions = "Perform controlled repetitions."
                });

            Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

            var exercise = await exerciseResponse.Content
                .ReadFromJsonAsync<ExerciseResponse>();

            Assert.NotNull(exercise);

            var assignmentResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/days/{days[dayIndex].Id}/exercises",
                new AddWorkoutPlanExerciseRequest
                {
                    ExerciseId = exercise.Id,
                    ExerciseOrder = 1,
                    Sets = 3,
                    Reps = 12,
                    RestSeconds = 60,
                    Notes = "Preserve this assignment during reordering"
                });

            Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

            assignedExerciseIds.Add(days[dayIndex].Id, exercise.Id);
        }

        // Step 5: Retrieve the workout plan before reordering
        var beforeResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, beforeResponse.StatusCode);

        var beforePlan = await beforeResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(beforePlan);

        Assert.Equal(3, beforePlan.Days.Count);

        // Capture the full serialized day details before reordering.
        // Only DayNumber should change.
        var beforeDays = beforePlan.Days.ToDictionary(
            x => x.Id,
            x => System.Text.Json.JsonSerializer.Serialize(x));

        // Step 6: Reorder workout days
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/reorder",
            new ReorderWorkoutPlanDaysRequest
            {
                Days = new List<ReorderWorkoutPlanDayItem>
                {
                new()
                {
                    DayId = days[0].Id,
                    DayNumber = 2
                },
                new()
                {
                    DayId = days[1].Id,
                    DayNumber = 3
                },
                new()
                {
                    DayId = days[2].Id,
                    DayNumber = 1
                }
                }
            });

        Assert.Equal(HttpStatusCode.OK, reorderResponse.StatusCode);

        // Step 7: Verify response contains the correct new order
        var reordered = await reorderResponse.Content
            .ReadFromJsonAsync<List<WorkoutPlanDayResponse>>();

        Assert.NotNull(reordered);
        Assert.Equal(3, reordered.Count);

        Assert.Equal(days[2].Id, reordered[0].Id);
        Assert.Equal(1, reordered[0].DayNumber);

        Assert.Equal(days[0].Id, reordered[1].Id);
        Assert.Equal(2, reordered[1].DayNumber);

        Assert.Equal(days[1].Id, reordered[2].Id);
        Assert.Equal(3, reordered[2].DayNumber);

        // Step 8: Retrieve workout plan after reordering
        var afterResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, afterResponse.StatusCode);

        var afterPlan = await afterResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(afterPlan);

        var savedDays = afterPlan.Days
            .OrderBy(x => x.DayNumber)
            .ToList();

        Assert.Equal(3, savedDays.Count);

        // Verify persisted positions
        Assert.Equal(days[2].Id, savedDays[0].Id);
        Assert.Equal(1, savedDays[0].DayNumber);

        Assert.Equal(days[0].Id, savedDays[1].Id);
        Assert.Equal(2, savedDays[1].DayNumber);

        Assert.Equal(days[1].Id, savedDays[2].Id);
        Assert.Equal(3, savedDays[2].DayNumber);

        // Step 9: Verify rest-day settings and other properties
        foreach (var original in days)
        {
            var savedDay = Assert.Single(
                savedDays,
                x => x.Id == original.Id);

            Assert.Equal(original.WorkoutPlanId, savedDay.WorkoutPlanId);
            Assert.Equal(original.Name, savedDay.Name);
            Assert.Equal(original.Description, savedDay.Description);
            Assert.Equal(original.IsRestDay, savedDay.IsRestDay);
            Assert.Equal(
                original.EstimatedDurationMinutes,
                savedDay.EstimatedDurationMinutes);
        }

        // Verify the rest day remains a rest day
        var restDay = Assert.Single(
            savedDays,
            x => x.Id == days[1].Id);

        Assert.True(restDay.IsRestDay);
        Assert.Equal(3, restDay.DayNumber);

        // Step 10: Verify all non-position day data remains unchanged.
        // This also checks exercise assignments if the detail response
        // includes exercises nested within each day.
        foreach (var savedDay in savedDays)
        {
            var originalDay = beforePlan.Days.Single(
                x => x.Id == savedDay.Id);

            var originalJson = System.Text.Json.JsonSerializer.Serialize(
                originalDay);

            var savedJson = System.Text.Json.JsonSerializer.Serialize(
                savedDay);

            using var originalDocument =
                System.Text.Json.JsonDocument.Parse(originalJson);

            using var savedDocument =
                System.Text.Json.JsonDocument.Parse(savedJson);

            var originalProperties = originalDocument.RootElement
                .EnumerateObject()
                .Where(x => !x.Name.Equals(
                    "DayNumber",
                    StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    x => x.Name,
                    x => x.Value.GetRawText());

            var savedProperties = savedDocument.RootElement
                .EnumerateObject()
                .Where(x => !x.Name.Equals(
                    "DayNumber",
                    StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    x => x.Name,
                    x => x.Value.GetRawText());

            Assert.Equal(
                originalProperties.Count,
                savedProperties.Count);

            foreach (var property in originalProperties)
            {
                Assert.True(savedProperties.ContainsKey(property.Key));

                Assert.Equal(
                    property.Value,
                    savedProperties[property.Key]);
            }
        }

        // Step 11: Verify the assigned exercise records remain in the database
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var savedAssignments = await db.WorkoutPlanExercises
            .Where(x => x.WorkoutPlanDayId == days[0].Id ||
                        x.WorkoutPlanDayId == days[2].Id)
            .ToListAsync();

        Assert.Equal(2, savedAssignments.Count);

        foreach (var assignment in savedAssignments)
        {
            Assert.True(
                assignedExerciseIds.ContainsKey(assignment.WorkoutPlanDayId));

            Assert.Equal(
                assignedExerciseIds[assignment.WorkoutPlanDayId],
                assignment.ExerciseId);

            Assert.Equal(1, assignment.ExerciseOrder);
            Assert.Equal(3, assignment.Sets);
            Assert.Equal(12, assignment.Reps);
            Assert.Equal(60, assignment.RestSeconds);
            Assert.Equal(
                "Preserve this assignment during reordering",
                assignment.Notes);
        }

        // Verify rest day has no exercise assignments
        var restDayAssignments = await db.WorkoutPlanExercises
            .CountAsync(x => x.WorkoutPlanDayId == days[1].Id);

        Assert.Equal(0, restDayAssignments);
    }

    [Fact]
    public async Task Admin_CanReactivateInactiveWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Reactivation Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.True(plan.IsActive);

        var originalReviewStatus = plan.ReviewStatus;

        // Step 3: Deactivate workout plan
        var deactivateResponse = await client.PatchAsync(
            $"/api/admin/workout-plans/{plan.Id}/deactivate",
                   null);

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        // Step 4: Verify plan is inactive
        var inactiveResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, inactiveResponse.StatusCode);

        var inactivePlan = await inactiveResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(inactivePlan);
        Assert.False(inactivePlan.IsActive);

        // Step 5: Reactivate workout plan
        var activateResponse = await client.PatchAsync(
            $"/api/admin/workout-plans/{plan.Id}/activate",
                   null);

        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        // Step 6: Verify activation response
        var activationJson = await activateResponse.Content
            .ReadAsStringAsync();

        using var activationDocument =
            System.Text.Json.JsonDocument.Parse(activationJson);

        var activationResult = activationDocument.RootElement;

        Assert.Equal(
            plan.Id,
            activationResult.GetProperty("id").GetGuid());

        Assert.True(
            activationResult.GetProperty("isActive").GetBoolean());

        // Step 7: Retrieve workout plan after reactivation
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        // Step 8: Verify reactivation persisted
        Assert.Equal(plan.Id, savedPlan.Id);
        Assert.True(savedPlan.IsActive);

        // Step 9: Verify review status was preserved
        Assert.Equal(originalReviewStatus, savedPlan.ReviewStatus);
    }
    [Fact]
    public async Task Admin_CannotReactivateAlreadyActiveWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create an active workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Already Active Reactivation Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.True(plan.IsActive);

        var originalReviewStatus = plan.ReviewStatus;

        // Step 3: Attempt to activate an already active plan
        var activateResponse = await client.PatchAsync(
            $"/api/admin/workout-plans/{plan.Id}/activate",
                   null);

        // Step 4: Verify API returns 409 Conflict
        Assert.Equal(
            HttpStatusCode.Conflict,
            activateResponse.StatusCode);

        // Step 5: Verify error message
        var errorResponse = await activateResponse.Content
            .ReadAsStringAsync();

        Assert.Contains(
            "Workout plan is already active",
            errorResponse,
            StringComparison.OrdinalIgnoreCase);

        // Step 6: Retrieve workout plan
        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);

        // Step 7: Verify workout plan remains active
        Assert.True(savedPlan.IsActive);

        // Step 8: Verify review status remains unchanged
        Assert.Equal(
            originalReviewStatus,
            savedPlan.ReviewStatus);
    }

    [Fact]
    public async Task Admin_CannotReactivateNonexistentWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var nonexistentPlanId = Guid.NewGuid();

        var activateResponse = await client.PatchAsync(
            $"/api/admin/workout-plans/{nonexistentPlanId}/activate",
                   null);

        Assert.Equal(HttpStatusCode.NotFound, activateResponse.StatusCode);

        var errorResponse = await activateResponse.Content.ReadAsStringAsync();

        Assert.Contains(
            "Workout plan not found",
            errorResponse,
            StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task UnauthenticatedUser_CannotReactivateWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var activateResponse = await client.PatchAsync(
            $"/api/admin/workout-plans/{Guid.NewGuid()}/activate",
                   null);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            activateResponse.StatusCode);
    }
    [Fact]
    public async Task Member_CannotReactivateWorkoutPlan()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var activateResponse = await client.PatchAsync(
            $"/api/admin/workout-plans/{Guid.NewGuid()}/activate",
                   null);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            activateResponse.StatusCode);
    }

    private static async Task VerifyReactivationPreservesReviewStatusAsync(
    string expectedReviewStatus)
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Step 1: Authenticate as Administrator
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Step 2: Create workout plan
        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Reactivation Review Status {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("Draft", plan.ReviewStatus);

        // Step 3: Create training day
        var dayResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days",
            new CreateWorkoutPlanDayRequest
            {
                DayNumber = 1,
                Name = "Full Body Workout",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);

        var day = await dayResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDayResponse>();

        Assert.NotNull(day);

        // Step 4: Create exercise in Exercise Master
        var exerciseResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                Code = $"REACTIVATE-{Guid.NewGuid():N}",
                Name = "Reactivation Test Squat",
                Description = "Exercise for workout plan reactivation testing",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled squats.",
                VideoUrl = "",
                GifUrl = ""
            });

        Assert.Equal(HttpStatusCode.Created, exerciseResponse.StatusCode);

        using var exerciseJson = JsonDocument.Parse(
            await exerciseResponse.Content.ReadAsStringAsync());

        var exerciseId = exerciseJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Step 5: Assign exercise to training day
        var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/admin/workout-plans/{plan.Id}/days/{day.Id}/exercises",
            new AddWorkoutPlanExerciseRequest
            {
                ExerciseId = exerciseId,
                ExerciseOrder = 1,
                Sets = 3,
                Reps = 12,
                RestSeconds = 60
            });

        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);

        // Step 6: Submit workout plan for review
        var submitResponse = await client.PostAsync(
                   $"/api/admin/workout-plans/{plan.Id}/submit-for-review",
                   null);

        var submitResponseBody =
            await submitResponse.Content.ReadAsStringAsync();

        Assert.True(
            submitResponse.StatusCode == HttpStatusCode.OK,
            $"Submission failed. Status: {submitResponse.StatusCode}. Response: {submitResponseBody}");

        // Step 7: Approve or reject depending on test scenario
        if (expectedReviewStatus == "Approved")
        {
            var approveResponse = await client.PostAsync(
                $"/api/admin/workout-plans/{plan.Id}/approve",
                       null);

            Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        }
        else if (expectedReviewStatus == "Rejected")
        {
            var rejectResponse = await client.PostAsJsonAsync(
                $"/api/admin/workout-plans/{plan.Id}/reject",
                new RejectWorkoutPlanRequest
                {
                    Reason = "Review status preservation test"
                });

            Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        }

        // Step 8: Verify review status before deactivation
        var beforeResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, beforeResponse.StatusCode);

        var beforePlan = await beforeResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(beforePlan);
        Assert.True(beforePlan.IsActive);
        Assert.Equal(expectedReviewStatus, beforePlan.ReviewStatus);

        // Step 9: Deactivate workout plan
        var deactivateResponse = await client.PatchAsync(
            $"/api/admin/workout-plans/{plan.Id}/deactivate",
                   null);

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        // Step 10: Verify deactivation persisted
        var inactiveResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, inactiveResponse.StatusCode);

        var inactivePlan = await inactiveResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(inactivePlan);
        Assert.False(inactivePlan.IsActive);
        Assert.Equal(expectedReviewStatus, inactivePlan.ReviewStatus);

        // Step 11: Reactivate workout plan
        var activateResponse = await client.PatchAsync(
            $"/api/admin/workout-plans/{plan.Id}/activate",
                   null);

        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        // Step 12: Verify persisted state after reactivation
        var afterResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, afterResponse.StatusCode);

        var afterPlan = await afterResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(afterPlan);

        Assert.True(afterPlan.IsActive);
        Assert.Equal(expectedReviewStatus, afterPlan.ReviewStatus);
        Assert.Equal(beforePlan.RejectionReason, afterPlan.RejectionReason);

        // Step 13: Verify workout day and exercise were preserved
        var savedDay = Assert.Single(afterPlan.Days);

        Assert.False(savedDay.IsRestDay);
        Assert.Single(savedDay.Exercises);
    }
    [Fact]
    public async Task Admin_CanReactivateInReviewWorkoutPlan_WithoutChangingReviewStatus()
    {
        await VerifyReactivationPreservesReviewStatusAsync("In Review");
    }

    [Fact]
    public async Task Admin_CanReactivateApprovedWorkoutPlan_WithoutChangingReviewStatus()
    {
        await VerifyReactivationPreservesReviewStatusAsync("Approved");
    }

    [Fact]
    public async Task Admin_CanReactivateRejectedWorkoutPlan_WithoutChangingReviewStatus()
    {
        await VerifyReactivationPreservesReviewStatusAsync("Rejected");
    }
    [Fact]
    public async Task Admin_CannotReactivateWorkoutPlanTwice()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginTestResponse>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var planResponse = await client.PostAsJsonAsync(
            "/api/admin/workout-plans",
            new CreateWorkoutPlanRequest
            {
                Name = $"Repeated Reactivation Test {Guid.NewGuid():N}",
                GoalCode = "WEIGHT_LOSS",
                ExperienceLevel = "Beginner",
                TrainingLocation = "Gym",
                DaysPerWeek = 3,
                DurationWeeks = 12
            });

        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);

        var plan = await planResponse.Content
            .ReadFromJsonAsync<WorkoutPlanResponse>();

        Assert.NotNull(plan);

        var deactivateResponse = await client.PatchAsync(
            $"/api/admin/workout-plans/{plan.Id}/deactivate",
                   null);

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var firstActivation = await client.PatchAsync(
            $"/api/admin/workout-plans/{plan.Id}/activate",
                   null);

        Assert.Equal(HttpStatusCode.OK, firstActivation.StatusCode);

        var secondActivation = await client.PatchAsync(
            $"/api/admin/workout-plans/{plan.Id}/activate",
                   null);

        Assert.Equal(HttpStatusCode.Conflict, secondActivation.StatusCode);

        var detailsResponse = await client.GetAsync(
            $"/api/admin/workout-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        var savedPlan = await detailsResponse.Content
            .ReadFromJsonAsync<WorkoutPlanDetailResponse>();

        Assert.NotNull(savedPlan);
        Assert.True(savedPlan.IsActive);
    }
}
