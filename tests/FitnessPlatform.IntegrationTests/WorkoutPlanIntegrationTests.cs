
using FitnessPlatform.Contracts.Exercises;
using FitnessPlatform.Contracts.WorkoutPlans;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

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

}
