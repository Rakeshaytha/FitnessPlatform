using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;
using FitnessPlatform.Contracts.MemberWorkoutPlanAssignments;
using FitnessPlatform.Domain.Entities;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using FitnessPlatform.Infrastructure.Identity;
using FitnessPlatform.Contracts.WorkoutPlans;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using FitnessPlatform.Contracts.Exercises;

namespace FitnessPlatform.IntegrationTests;

public sealed class MemberWorkoutPlanAssignmentIntegrationTests
{
    // ============================================================
    // Authentication helper
    // ============================================================

    private static async Task AuthenticateAsync(
        HttpClient client,
        string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var login = await response.Content
            .ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(login);

        Assert.False(
            string.IsNullOrWhiteSpace(login.AccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);
    }

    private sealed class LoginResponse
    {
        public string AccessToken { get; set; } =
            string.Empty;
    }

    private static async Task<(
    Guid PlanId,
    Guid DayId,
    Guid AssignmentId,
    Guid OriginalExerciseId)>
    PrepareEditableExerciseAssignmentAsync(
        FitnessPlatformWebApplicationFactory factory,
        HttpClient adminClient)
    {
        var sourcePlan =
            await CreateCompleteTestWorkoutPlanAsync(factory);

        var newVersionId =
            await CreateTestWorkoutPlanVersionAsync(
                factory,
                adminClient,
                sourcePlan.PlanId);

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var trainingDay = await db.WorkoutPlanDays
            .AsNoTracking()
            .SingleAsync(x =>
                x.WorkoutPlanId == newVersionId &&
                !x.IsRestDay);

        var assignment = await db.WorkoutPlanExercises
            .AsNoTracking()
            .SingleAsync(x =>
                x.WorkoutPlanDayId == trainingDay.Id &&
                x.ExerciseOrder == 1);

        return (
            newVersionId,
            trainingDay.Id,
            assignment.Id,
            assignment.ExerciseId);
    }

    private static async Task<Guid> CreateReplacementExerciseAsync(
    FitnessPlatformWebApplicationFactory factory,
    string reviewStatus = "Approved")
    {
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            Code = $"EX-{Guid.NewGuid():N}",
            Name = "Replacement Test Exercise",
            Description = "Exercise replacement integration test.",
            MuscleGroup = "Chest",
            Equipment = "Dumbbell",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform controlled repetitions.",
            ReviewStatus = reviewStatus,
            IsActive = reviewStatus != "Retired",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.Exercises.Add(exercise);

        await db.SaveChangesAsync();

        return exercise.Id;
    }

    private static async Task<WorkoutPlanExercise>
    GetPersistedWorkoutExerciseAsync(
        FitnessPlatformWebApplicationFactory factory,
        Guid assignmentId)
    {
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        return await db.WorkoutPlanExercises
            .AsNoTracking()
            .SingleAsync(x => x.Id == assignmentId);
    }

    private static UpdateWorkoutPlanExerciseRequest
    CreateExerciseReplacementRequest(
        Guid exerciseId)
    {
        return new UpdateWorkoutPlanExerciseRequest
        {
            ExerciseId = exerciseId,
            ExerciseOrder = 1,
            Sets = 4,
            Reps = 15,
            RestSeconds = 90,
            DurationSeconds = null,
            Notes = "Updated integration test exercise."
        };
    }
    private static async Task AssertExerciseUnavailableAsync(
    HttpResponseMessage response)
    {
        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "EXERCISE_NOT_AVAILABLE",
            document.RootElement
                .GetProperty("code")
                .GetString());
    }

    private static void AssertWorkoutExerciseUnchanged(
    WorkoutPlanExercise original,
    WorkoutPlanExercise current)
    {
        Assert.Equal(original.Id, current.Id);

        Assert.Equal(
            original.WorkoutPlanDayId,
            current.WorkoutPlanDayId);

        Assert.Equal(
            original.ExerciseId,
            current.ExerciseId);

        Assert.Equal(
            original.ExerciseOrder,
            current.ExerciseOrder);

        Assert.Equal(original.Sets, current.Sets);

        Assert.Equal(original.Reps, current.Reps);

        Assert.Equal(
            original.RestSeconds,
            current.RestSeconds);

        Assert.Equal(
            original.DurationSeconds,
            current.DurationSeconds);

        Assert.Equal(original.Notes, current.Notes);
    }


    private static async Task<Guid> GetExerciseMasterIdAsync(
    FitnessPlatformWebApplicationFactory factory,
    Guid workoutPlanExerciseId)
    {
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        return await db.WorkoutPlanExercises
            .AsNoTracking()
            .Where(x => x.Id == workoutPlanExerciseId)
            .Select(x => x.ExerciseId)
            .SingleAsync();
    }
    private static async Task RetireTestExerciseAsync(
    HttpClient adminClient,
    Guid exerciseId)
    {
        var response = await adminClient.PutAsync(
            $"/api/exercises/{exerciseId}/retire",
            null);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Exercise retirement failed: {response.StatusCode}. {responseBody}");
    }

    private static async Task PrepareConcurrentAdminsAsync(
    FitnessPlatformWebApplicationFactory factory,
    HttpClient firstAdminClient,
    HttpClient secondAdminClient)
    {
        await TestIdentitySeeder.SeedAsync(factory.Services);

        await AuthenticateAsync(
            firstAdminClient,
            TestIdentitySeeder.AdminEmail);

        await AuthenticateAsync(
            secondAdminClient,
            TestIdentitySeeder.AdminEmail);
    }

    private static void AssertValidConcurrentAssignmentResponses(
        HttpResponseMessage[] responses)
    {
        Assert.Equal(2, responses.Length);

        Assert.All(
            responses,
            response => Assert.Contains(
                response.StatusCode,
                new[]
                {
                HttpStatusCode.Created,
                HttpStatusCode.Conflict
                }));

        Assert.Contains(
            responses,
            response => response.StatusCode == HttpStatusCode.Created);
    }

    private static async Task<HttpResponseMessage[]>
    ExecuteConcurrentRequestsAsync(
        Func<Task<HttpResponseMessage>> firstRequest,
        Func<Task<HttpResponseMessage>> secondRequest)
    {
        var startSignal = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<HttpResponseMessage> ExecuteAsync(
            Func<Task<HttpResponseMessage>> request)
        {
            await startSignal.Task;

            return await request();
        }

        var firstTask = ExecuteAsync(firstRequest);
        var secondTask = ExecuteAsync(secondRequest);

        startSignal.SetResult(true);

        return await Task.WhenAll(firstTask, secondTask);
    }

    private static async Task<List<MemberWorkoutPlanAssignment>>
        GetPersistedAssignmentsAsync(
            FitnessPlatformWebApplicationFactory factory,
            Guid profileId)
    {
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        return await db.MemberWorkoutPlanAssignments
            .AsNoTracking()
            .Where(x => x.UserProfileId == profileId)
            .OrderBy(x => x.AssignedAtUtc)
            .ToListAsync();
    }

    private static void AssertValidAssignmentConcurrencyResponses(
        IEnumerable<HttpResponseMessage> responses)
    {
        Assert.All(
            responses,
            response => Assert.Contains(
                response.StatusCode,
                new[]
                {
                HttpStatusCode.Created,
                HttpStatusCode.Conflict
                }));
    }


    private static async Task<MemberActiveWorkoutPlanResponse>
    GetMemberActiveAssignmentAsync(HttpClient memberClient)
    {
        var response = await memberClient.GetAsync(
            "/api/member/workout-plans/active");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var assignment = await response.Content
            .ReadFromJsonAsync<MemberActiveWorkoutPlanResponse>();

        Assert.NotNull(assignment);

        return assignment;
    }

    private static async Task<MemberWorkoutPlanDetailResponse>
        GetMemberWorkoutDetailsAsync(HttpClient memberClient)
    {
        var response = await memberClient.GetAsync(
            "/api/member/workout-plans/active/details");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var details = await response.Content
            .ReadFromJsonAsync<MemberWorkoutPlanDetailResponse>();

        Assert.NotNull(details);

        return details;
    }

    private static async Task AssertAssignedPlanConflictAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        var error = document.RootElement
            .GetProperty("error")
            .GetString();

        Assert.Equal(
            "This workout plan has been assigned to members and cannot be modified. Create a new workout plan version to make changes.",
            error);
    }

    [Theory]
    [InlineData("UpdatePlan")]
    [InlineData("AddDay")]
    [InlineData("UpdateDay")]
    [InlineData("DeleteDay")]
    [InlineData("AddExercise")]
    [InlineData("UpdateExercise")]
    [InlineData("DeleteExercise")]
    [InlineData("ReorderDays")]
    [InlineData("ReorderExercises")]
    public async Task ModifyAssignedWorkoutPlan_ContentMutation_ReturnsAssignmentConflict(
    string operation)
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory,
            adminClient,
            memberClient);

        var originalDetails =
            await GetMemberWorkoutDetailsAsync(memberClient);

        Guid exerciseMasterId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<FitnessPlatformDbContext>();

            exerciseMasterId = await db.WorkoutPlanExercises
                .Where(x => x.Id == data.FirstExerciseId)
                .Select(x => x.ExerciseId)
                .SingleAsync();
        }

        var baseUrl =
            $"/api/admin/workout-plans/{data.PlanId}";

        var dayUrl =
            $"{baseUrl}/days/{data.TrainingDayId}";

        HttpResponseMessage response;

        switch (operation)
        {
            case "UpdatePlan":

                response = await adminClient.PutAsJsonAsync(
                    baseUrl,
                    new UpdateWorkoutPlanRequest
                    {
                        Name = "Modified Assigned Workout Plan",
                        Description = "Attempted modification.",
                        GoalCode = "WEIGHT_LOSS",
                        ExperienceLevel = "Beginner",
                        TrainingLocation = "Gym",
                        DaysPerWeek = 5,
                        DurationWeeks = 12
                    });

                break;

            case "AddDay":

                response = await adminClient.PostAsJsonAsync(
                    $"{baseUrl}/days",
                    new CreateWorkoutPlanDayRequest
                    {
                        DayNumber = 3,
                        Name = "Additional Workout Day",
                        Description = "Attempted new workout day.",
                        IsRestDay = false,
                        EstimatedDurationMinutes = 45
                    });

                break;

            case "UpdateDay":

                response = await adminClient.PutAsJsonAsync(
                    dayUrl,
                    new UpdateWorkoutPlanDayRequest
                    {
                        DayNumber = 1,
                        Name = "Modified Workout Day",
                        Description = "Attempted day modification.",
                        IsRestDay = false,
                        EstimatedDurationMinutes = 75
                    });

                break;

            case "DeleteDay":

                response = await adminClient.DeleteAsync(dayUrl);

                break;

            case "AddExercise":

                Guid newExerciseId;

                using (var scope = factory.Services.CreateScope())
                {
                    var db = scope.ServiceProvider
                        .GetRequiredService<FitnessPlatformDbContext>();

                    var exercise = new Exercise
                    {
                        Id = Guid.NewGuid(),
                        Code = $"EX-{Guid.NewGuid():N}",
                        Name = "Additional Test Exercise",
                        Description = "Assignment protection test.",
                        MuscleGroup = "Chest",
                        Equipment = "Dumbbell",
                        DifficultyLevel = "Beginner",
                        TrainingLocation = "Gym",
                        Instructions = "Controlled repetitions.",
                        IsActive = true,
                        ReviewStatus = "Approved"
                    };

                    db.Exercises.Add(exercise);

                    await db.SaveChangesAsync();

                    newExerciseId = exercise.Id;
                }

                response = await adminClient.PostAsJsonAsync(
                    $"{dayUrl}/exercises",
                    new AddWorkoutPlanExerciseRequest
                    {
                        ExerciseId = newExerciseId,
                        ExerciseOrder = 3,
                        Sets = 3,
                        Reps = 12,
                        RestSeconds = 60,
                        Notes = "Attempted additional exercise."
                    });

                break;

            case "UpdateExercise":

                response = await adminClient.PutAsJsonAsync(
                    $"{dayUrl}/exercises/{data.FirstExerciseId}",
                    new UpdateWorkoutPlanExerciseRequest
                    {
                        ExerciseId = exerciseMasterId,
                        ExerciseOrder = 1,
                        Sets = 5,
                        Reps = 15,
                        RestSeconds = 120,
                        Notes = "Attempted exercise modification."
                    });

                break;

            case "DeleteExercise":

                response = await adminClient.DeleteAsync(
                    $"{dayUrl}/exercises/{data.FirstExerciseId}");

                break;

            case "ReorderDays":

                response = await adminClient.PutAsJsonAsync(
                    $"{baseUrl}/days/reorder",
                    new ReorderWorkoutPlanDaysRequest
                    {
                        Days = new List<ReorderWorkoutPlanDayItem>
                        {
                        new()
                        {
                            DayId = data.TrainingDayId,
                            DayNumber = 2
                        },
                        new()
                        {
                            DayId = data.RestDayId,
                            DayNumber = 1
                        }
                        }
                    });

                break;

            case "ReorderExercises":

                response = await adminClient.PutAsJsonAsync(
                    $"{dayUrl}/exercises/reorder",
                    new ReorderWorkoutPlanExercisesRequest
                    {
                        Exercises =
                            new List<ReorderWorkoutPlanExerciseItem>
                            {
                            new()
                            {
                                AssignmentId = data.FirstExerciseId,
                                ExerciseOrder = 2
                            },
                            new()
                            {
                                AssignmentId = data.SecondExerciseId,
                                ExerciseOrder = 1
                            }
                            }
                    });

                break;

            default:

                throw new InvalidOperationException(
                    $"Unsupported operation: {operation}");
        }

        using (response)
        {
            await AssertAssignedPlanConflictAsync(response);
        }

        // Verify that the rejected operation did not modify
        // the workout content visible to the assigned member.

        var currentDetails =
            await GetMemberWorkoutDetailsAsync(memberClient);

        Assert.Equal(
            JsonSerializer.Serialize(originalDetails),
            JsonSerializer.Serialize(currentDetails));
    }


    // ============================================================
    // Version Preservation and Content Protection Helpers
    // ============================================================

    private static string GetAdminWorkoutPlanEndpoint(Guid planId)
    {
        return $"/api/admin/workout-plans/{planId}";
    }

    private static async Task<WorkoutPlan> GetTestWorkoutPlanAsync(
        FitnessPlatformWebApplicationFactory factory,
        Guid planId)
    {
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        return await db.WorkoutPlans
            .AsNoTracking()
            .SingleAsync(x => x.Id == planId);
    }

    private static async Task<Guid> CreateTestWorkoutPlanVersionAsync(
        FitnessPlatformWebApplicationFactory factory,
        HttpClient adminClient,
        Guid sourcePlanId)
    {
        var sourcePlan = await GetTestWorkoutPlanAsync(
            factory,
            sourcePlanId);

        var response = await adminClient.PostAsync(
            $"{GetAdminWorkoutPlanEndpoint(sourcePlanId)}/versions",
            null);

        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Version creation failed: {response.StatusCode}. {responseBody}");

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var newVersion = await db.WorkoutPlans
            .AsNoTracking()
            .SingleAsync(x =>
                x.VersionGroupId == sourcePlan.VersionGroupId &&
                x.VersionNumber == sourcePlan.VersionNumber + 1);

        Assert.NotEqual(sourcePlanId, newVersion.Id);
        Assert.Equal(sourcePlanId, newVersion.PreviousVersionId);
        Assert.Equal("Draft", newVersion.ReviewStatus);
        Assert.True(newVersion.IsLatestVersion);

        return newVersion.Id;
    }

    private static async Task ApproveTestWorkoutPlanAsync(
        HttpClient adminClient,
        Guid planId)
    {
        var endpoint = GetAdminWorkoutPlanEndpoint(planId);

        var submitResponse = await adminClient.PostAsync(
            $"{endpoint}/submit-for-review",
            null);

        Assert.True(
            submitResponse.StatusCode == HttpStatusCode.OK,
            await submitResponse.Content.ReadAsStringAsync());

        var approveResponse = await adminClient.PostAsync(
            $"{endpoint}/approve",
            null);

        Assert.True(
            approveResponse.StatusCode == HttpStatusCode.OK,
            await approveResponse.Content.ReadAsStringAsync());
    }

    private static async Task<MemberActiveWorkoutPlanResponse>
        GetMemberActivePlanAsync(HttpClient memberClient)
    {
        var response = await memberClient.GetAsync(
            "/api/member/workout-plans/active");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var assignment = await response.Content
            .ReadFromJsonAsync<MemberActiveWorkoutPlanResponse>();

        Assert.NotNull(assignment);

        return assignment;
    }

    private static async Task<MemberWorkoutPlanDetailResponse>
        GetMemberPlanDetailsAsync(HttpClient memberClient)
    {
        var response = await memberClient.GetAsync(
            "/api/member/workout-plans/active/details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var details = await response.Content
            .ReadFromJsonAsync<MemberWorkoutPlanDetailResponse>();

        Assert.NotNull(details);

        return details;
    }

    private static async Task AssertAssignedContentConflictAsync(
        HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Conflict,
            $"Expected HTTP 409 but received {response.StatusCode}. {body}");

        using var document = JsonDocument.Parse(body);

        var error = document.RootElement
            .GetProperty("error")
            .GetString();

        Assert.Equal(
            "This workout plan has been assigned to members and cannot be modified. Create a new workout plan version to make changes.",
            error);
    }


    private static async Task<MemberWorkoutPlanAssignmentHistoryResponse>
    GetAdminHistoryAsync(
        HttpClient adminClient,
        Guid profileId)
    {
        var response = await adminClient.GetAsync(
            $"/api/admin/members/{profileId}/workout-plans/history");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var history = await response.Content
            .ReadFromJsonAsync<MemberWorkoutPlanAssignmentHistoryResponse>();

        Assert.NotNull(history);

        Assert.Equal(profileId, history.UserProfileId);

        return history;
    }

    private static async Task<MemberWorkoutPlanHistoryResponse>
        GetMemberHistoryAsync(HttpClient memberClient)
    {
        var response = await memberClient.GetAsync(
            "/api/member/workout-plans/history");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var history = await response.Content
            .ReadFromJsonAsync<MemberWorkoutPlanHistoryResponse>();

        Assert.NotNull(history);

        return history;
    }

    private static async Task PrepareAdminAsync(
        FitnessPlatformWebApplicationFactory factory,
        HttpClient adminClient)
    {
        await TestIdentitySeeder.SeedAsync(factory.Services);

        await AuthenticateAsync(
            adminClient,
            TestIdentitySeeder.AdminEmail);
    }

    private static async Task<(Guid UserId, Guid ProfileId, string Email)>
    CreateTestMemberAsync(
        FitnessPlatformWebApplicationFactory factory,
        bool createProfile = true)
    {
        using var scope = factory.Services.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var email =
            $"assignmentmember-{Guid.NewGuid():N}@fitnessplatform.local";

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(
            user,
            TestIdentitySeeder.TestPassword);

        Assert.True(
            createResult.Succeeded,
            string.Join(", ",
                createResult.Errors.Select(x => x.Description)));

        var roleResult = await userManager.AddToRoleAsync(
            user,
            "Member");

        Assert.True(
            roleResult.Succeeded,
            string.Join(", ",
                roleResult.Errors.Select(x => x.Description)));

        if (!createProfile)
        {
            return (user.Id, Guid.Empty, email);
        }

        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FirstName = "Assignment",
            LastName = "Member",
            HeightCm = 175,
            WeightKg = 80,
            CountryCode = "AE",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.UserProfiles.Add(profile);

        await db.SaveChangesAsync();

        return (user.Id, profile.Id, email);
    }

    private static async Task<(
    Guid PlanId,
    Guid TrainingDayId,
    Guid RestDayId,
    Guid FirstExerciseId,
    Guid SecondExerciseId)>
    CreateCompleteTestWorkoutPlanAsync(
        FitnessPlatformWebApplicationFactory factory)
    {
        var planId = await CreateTestWorkoutPlanAsync(factory);

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var firstExercise = new Exercise
        {
            Id = Guid.NewGuid(),
            Code = $"EX-{Guid.NewGuid():N}",
            Name = "Integration Test Bench Press",
            Description = "Test chest exercise.",
            MuscleGroup = "Chest",
            Equipment = "Barbell",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform controlled repetitions.",
            ReviewStatus = "Approved",
            IsActive = true
        };

        var secondExercise = new Exercise
        {
            Id = Guid.NewGuid(),
            Code = $"EX-{Guid.NewGuid():N}",
            Name = "Integration Test Cable Row",
            Description = "Test back exercise.",
            MuscleGroup = "Back",
            Equipment = "Cable",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Maintain controlled movement.",
            ReviewStatus = "Approved",
            IsActive = true
        };

        db.Exercises.AddRange(
            firstExercise,
            secondExercise);

        var trainingDay = new WorkoutPlanDay
        {
            Id = Guid.NewGuid(),
            WorkoutPlanId = planId,
            DayNumber = 1,
            Name = "Upper Body",
            Description = "Upper body strength training.",
            IsRestDay = false,
            EstimatedDurationMinutes = 60
        };

        var restDay = new WorkoutPlanDay
        {
            Id = Guid.NewGuid(),
            WorkoutPlanId = planId,
            DayNumber = 2,
            Name = "Rest Day",
            Description = "Recovery day.",
            IsRestDay = true,
            EstimatedDurationMinutes = null
        };

        db.WorkoutPlanDays.AddRange(
            trainingDay,
            restDay);

        var firstPlanExercise = new WorkoutPlanExercise
        {
            Id = Guid.NewGuid(),
            WorkoutPlanDayId = trainingDay.Id,
            ExerciseId = firstExercise.Id,
            ExerciseOrder = 1,
            Sets = 3,
            Reps = 12,
            RestSeconds = 60,
            Notes = "Maintain correct form."
        };

        var secondPlanExercise = new WorkoutPlanExercise
        {
            Id = Guid.NewGuid(),
            WorkoutPlanDayId = trainingDay.Id,
            ExerciseId = secondExercise.Id,
            ExerciseOrder = 2,
            Sets = 4,
            Reps = 10,
            RestSeconds = 90,
            Notes = "Controlled repetitions."
        };

        db.WorkoutPlanExercises.AddRange(
            firstPlanExercise,
            secondPlanExercise);

        await db.SaveChangesAsync();

        return (
            planId,
            trainingDay.Id,
            restDay.Id,
            firstPlanExercise.Id,
            secondPlanExercise.Id);
    }

    private static async Task<MemberWorkoutPlanAssignmentResponse>
    AssignTestWorkoutPlanAsync(
        HttpClient adminClient,
        Guid profileId,
        Guid planId)
    {
        var request = CreateAssignmentRequest(planId);

        var response = await adminClient.PostAsJsonAsync(
            GetAssignmentEndpoint(profileId),
            request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var assignment = await response.Content
            .ReadFromJsonAsync<MemberWorkoutPlanAssignmentResponse>();

        Assert.NotNull(assignment);

        Assert.Equal(profileId, assignment.UserProfileId);
        Assert.Equal(planId, assignment.WorkoutPlanId);
        Assert.Equal("Active", assignment.Status);

        return assignment;
    }

    private static async Task<(
    Guid ProfileId,
    Guid PlanId,
    Guid TrainingDayId,
    Guid RestDayId,
    Guid FirstExerciseId,
    Guid SecondExerciseId)>
    PrepareAssignedMemberAsync(
        FitnessPlatformWebApplicationFactory factory,
        HttpClient adminClient,
        HttpClient memberClient)
    {
        await TestIdentitySeeder.SeedAsync(factory.Services);

        await AuthenticateAsync(
            adminClient,
            TestIdentitySeeder.AdminEmail);

        var member = await CreateTestMemberAsync(factory);

        var plan = await CreateCompleteTestWorkoutPlanAsync(factory);

        await AssignTestWorkoutPlanAsync(
            adminClient,
            member.ProfileId,
            plan.PlanId);

        await AuthenticateAsync(
            memberClient,
            member.Email);

        return (
            member.ProfileId,
            plan.PlanId,
            plan.TrainingDayId,
            plan.RestDayId,
            plan.FirstExerciseId,
            plan.SecondExerciseId);
    }
    // ============================================================
    // Test Data Helpers
    // ============================================================

    private static async Task<Guid> CreateTestProfileAsync(
        FitnessPlatformWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            FirstName = "Assignment",
            LastName = "Test",
            HeightCm = 175,
            WeightKg = 80,
            CountryCode = "AE",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.UserProfiles.Add(profile);

        await db.SaveChangesAsync();

        return profile.Id;
    }

    private static async Task<Guid> CreateTestWorkoutPlanAsync(
        FitnessPlatformWebApplicationFactory factory,
        string reviewStatus = "Approved",
        bool isActive = true,
        bool isLatestVersion = true)
    {
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var plan = new WorkoutPlan
        {
            Id = Guid.NewGuid(),
            Name = $"Assignment Test {Guid.NewGuid():N}",
            Description = "Workout plan assignment integration test.",
            GoalCode = "WEIGHT_LOSS",
            ExperienceLevel = "Beginner",
            TrainingLocation = "Gym",
            DaysPerWeek = 5,
            DurationWeeks = 12,
            ReviewStatus = reviewStatus,
            IsActive = isActive,
            VersionNumber = 1,
            VersionGroupId = Guid.NewGuid(),
            IsLatestVersion = isLatestVersion,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.WorkoutPlans.Add(plan);

        await db.SaveChangesAsync();

        return plan.Id;
    }

    private static AssignWorkoutPlanRequest CreateAssignmentRequest(
        Guid workoutPlanId)
    {
        return new AssignWorkoutPlanRequest
        {
            WorkoutPlanId = workoutPlanId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Notes = "Integration test assignment."
        };
    }

    private static string GetAssignmentEndpoint(Guid profileId)
    {
        return $"/api/admin/members/{profileId}/workout-plans/assign";
    }

    // ============================================================
    // Batch A - Authentication and Authorization
    // ============================================================

    [Fact]
    public async Task AssignWorkoutPlan_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var client = factory.CreateClient();

        var profileId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            $"/api/admin/members/{profileId}/workout-plans/assign",
            new
            {
                WorkoutPlanId = Guid.NewGuid(),
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow)
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task GetAdminActiveAssignment_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var client = factory.CreateClient();

        var profileId = Guid.NewGuid();

        var response = await client.GetAsync(
            $"/api/admin/members/{profileId}/workout-plans/active");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task GetAdminAssignmentHistory_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var client = factory.CreateClient();

        var profileId = Guid.NewGuid();

        var response = await client.GetAsync(
            $"/api/admin/members/{profileId}/workout-plans/history");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Theory]
    [InlineData("/api/member/workout-plans/active")]
    [InlineData("/api/member/workout-plans/active/details")]
    [InlineData("/api/member/workout-plans/active/days")]
    [InlineData("/api/member/workout-plans/active/days/1")]
    [InlineData("/api/member/workout-plans/active/days/1/exercises")]
    [InlineData("/api/member/workout-plans/history")]
    public async Task MemberWorkoutPlanEndpoints_WithoutAuthentication_ReturnUnauthorized(
        string endpoint)
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var client = factory.CreateClient();

        var response = await client.GetAsync(endpoint);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task AssignWorkoutPlan_AsMember_ReturnsForbidden()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(
            factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(
            client,
            TestIdentitySeeder.Member1Email);

        var profileId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            $"/api/admin/members/{profileId}/workout-plans/assign",
            new
            {
                WorkoutPlanId = Guid.NewGuid(),
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow)
            });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("history")]
    public async Task AdminAssignmentEndpoints_AsMember_ReturnForbidden(
        string operation)
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(
            factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(
            client,
            TestIdentitySeeder.Member1Email);

        var profileId = Guid.NewGuid();

        var response = await client.GetAsync(
            $"/api/admin/members/{profileId}/workout-plans/{operation}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Theory]
    [InlineData("/api/member/workout-plans/active")]
    [InlineData("/api/member/workout-plans/active/details")]
    [InlineData("/api/member/workout-plans/active/days")]
    [InlineData("/api/member/workout-plans/active/days/1")]
    [InlineData("/api/member/workout-plans/active/days/1/exercises")]
    [InlineData("/api/member/workout-plans/history")]
    public async Task MemberWorkoutPlanEndpoints_AsAdmin_ReturnForbidden(
        string endpoint)
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(
            factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(
            client,
            TestIdentitySeeder.AdminEmail);

        var response = await client.GetAsync(endpoint);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task GetMemberWorkoutExercise_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var client = factory.CreateClient();

        var exerciseId = Guid.NewGuid();

        var response = await client.GetAsync(
            $"/api/member/workout-plans/active/days/1/exercises/{exerciseId}");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task GetMemberWorkoutExercise_AsAdmin_ReturnsForbidden()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(
            factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(
            client,
            TestIdentitySeeder.AdminEmail);

        var exerciseId = Guid.NewGuid();

        var response = await client.GetAsync(
            $"/api/member/workout-plans/active/days/1/exercises/{exerciseId}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }
    // ============================================================
    // Batch B - Assignment Validation and Eligibility
    // ============================================================

    [Fact]
    public async Task AssignWorkoutPlan_EmptyProfileId_ReturnsBadRequest()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(client, TestIdentitySeeder.AdminEmail);

        var request = CreateAssignmentRequest(Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            GetAssignmentEndpoint(Guid.Empty),
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AssignWorkoutPlan_EmptyWorkoutPlanId_ReturnsBadRequest()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(client, TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var request = CreateAssignmentRequest(Guid.Empty);

        var response = await client.PostAsJsonAsync(
            GetAssignmentEndpoint(profileId),
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AssignWorkoutPlan_ProfileNotFound_ReturnsNotFound()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(client, TestIdentitySeeder.AdminEmail);

        var planId = await CreateTestWorkoutPlanAsync(factory);

        var request = CreateAssignmentRequest(planId);

        var response = await client.PostAsJsonAsync(
            GetAssignmentEndpoint(Guid.NewGuid()),
            request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignWorkoutPlan_WorkoutPlanNotFound_ReturnsNotFound()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(client, TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var request = CreateAssignmentRequest(Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            GetAssignmentEndpoint(profileId),
            request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignWorkoutPlan_InvalidStartDate_ReturnsBadRequest()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(client, TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var planId = await CreateTestWorkoutPlanAsync(factory);

        var request = CreateAssignmentRequest(planId);

        request.StartDate = DateOnly.MinValue;

        var response = await client.PostAsJsonAsync(
            GetAssignmentEndpoint(profileId),
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AssignWorkoutPlan_EndDateBeforeStartDate_ReturnsBadRequest()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(client, TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var planId = await CreateTestWorkoutPlanAsync(factory);

        var request = CreateAssignmentRequest(planId);

        request.EndDate = request.StartDate.AddDays(-1);

        var response = await client.PostAsJsonAsync(
            GetAssignmentEndpoint(profileId),
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AssignWorkoutPlan_NotesExceedMaximumLength_ReturnsBadRequest()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(client, TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var planId = await CreateTestWorkoutPlanAsync(factory);

        var request = CreateAssignmentRequest(planId);

        request.Notes = new string('A', 1001);

        var response = await client.PostAsJsonAsync(
            GetAssignmentEndpoint(profileId),
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("In Review")]
    [InlineData("Rejected")]
    public async Task AssignWorkoutPlan_NotApproved_ReturnsConflict(
        string reviewStatus)
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(client, TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var planId = await CreateTestWorkoutPlanAsync(
            factory,
            reviewStatus: reviewStatus);

        var request = CreateAssignmentRequest(planId);

        var response = await client.PostAsJsonAsync(
            GetAssignmentEndpoint(profileId),
            request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AssignWorkoutPlan_InactivePlan_ReturnsConflict()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(client, TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var planId = await CreateTestWorkoutPlanAsync(
            factory,
            isActive: false);

        var request = CreateAssignmentRequest(planId);

        var response = await client.PostAsJsonAsync(
            GetAssignmentEndpoint(profileId),
            request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AssignWorkoutPlan_HistoricalVersion_ReturnsConflict()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(client, TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var planId = await CreateTestWorkoutPlanAsync(
            factory,
            isLatestVersion: false);

        var request = CreateAssignmentRequest(planId);

        var response = await client.PostAsJsonAsync(
            GetAssignmentEndpoint(profileId),
            request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AssignWorkoutPlan_EligiblePlan_ReturnsCreated()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(client, TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var planId = await CreateTestWorkoutPlanAsync(factory);

        var request = CreateAssignmentRequest(planId);

        request.EndDate = request.StartDate.AddDays(84);

        var response = await client.PostAsJsonAsync(
            GetAssignmentEndpoint(profileId),
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var assignment = await response.Content
            .ReadFromJsonAsync<MemberWorkoutPlanAssignmentResponse>();

        Assert.NotNull(assignment);

        Assert.NotEqual(Guid.Empty, assignment.Id);

        Assert.Equal(profileId, assignment.UserProfileId);

        Assert.Equal(planId, assignment.WorkoutPlanId);

        Assert.Equal("Active", assignment.Status);

        Assert.Equal(1, assignment.VersionNumber);

        Assert.True(assignment.IsLatestVersion);

        Assert.Equal(request.StartDate, assignment.StartDate);

        Assert.Equal(request.EndDate, assignment.EndDate);

        Assert.Equal(request.Notes, assignment.Notes);

        Assert.NotEqual(Guid.Empty, assignment.AssignedByUserId);

        Assert.Null(assignment.DeactivatedAtUtc);
    }

    [Fact]
    public async Task AssignWorkoutPlan_SamePlanTwice_ReturnsConflict()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(
            client,
            TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var planId = await CreateTestWorkoutPlanAsync(factory);

        await AssignTestWorkoutPlanAsync(
            client,
            profileId,
            planId);

        var response = await client.PostAsJsonAsync(
            GetAssignmentEndpoint(profileId),
            CreateAssignmentRequest(planId));

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);
    }
    [Fact]
    public async Task AssignWorkoutPlan_DifferentPlan_ReturnsCreated()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(
            client,
            TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var firstPlanId = await CreateTestWorkoutPlanAsync(factory);

        var secondPlanId = await CreateTestWorkoutPlanAsync(factory);

        var firstAssignment = await AssignTestWorkoutPlanAsync(
            client,
            profileId,
            firstPlanId);

        var secondAssignment = await AssignTestWorkoutPlanAsync(
            client,
            profileId,
            secondPlanId);

        Assert.NotEqual(
            firstAssignment.Id,
            secondAssignment.Id);

        Assert.Equal(
            secondPlanId,
            secondAssignment.WorkoutPlanId);

        Assert.Equal("Active", secondAssignment.Status);
    }
    [Fact]
    public async Task ReassignWorkoutPlan_PreviousAssignment_BecomesInactive()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(
            client,
            TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var firstPlanId = await CreateTestWorkoutPlanAsync(factory);

        var secondPlanId = await CreateTestWorkoutPlanAsync(factory);

        var firstAssignment = await AssignTestWorkoutPlanAsync(
            client,
            profileId,
            firstPlanId);

        await AssignTestWorkoutPlanAsync(
            client,
            profileId,
            secondPlanId);

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var previousAssignment = await db.MemberWorkoutPlanAssignments
            .AsNoTracking()
            .SingleAsync(x => x.Id == firstAssignment.Id);

        Assert.Equal(
            "Inactive",
            previousAssignment.Status);

        Assert.NotNull(
            previousAssignment.DeactivatedAtUtc);

        Assert.Equal(
            firstPlanId,
            previousAssignment.WorkoutPlanId);
    }
    [Fact]
    public async Task ReassignWorkoutPlan_MultipleAssignments_OnlyOneRemainsActive()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(
            client,
            TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var firstPlanId = await CreateTestWorkoutPlanAsync(factory);
        var secondPlanId = await CreateTestWorkoutPlanAsync(factory);
        var thirdPlanId = await CreateTestWorkoutPlanAsync(factory);

        await AssignTestWorkoutPlanAsync(client, profileId, firstPlanId);
        await AssignTestWorkoutPlanAsync(client, profileId, secondPlanId);
        await AssignTestWorkoutPlanAsync(client, profileId, thirdPlanId);

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var assignments = await db.MemberWorkoutPlanAssignments
            .AsNoTracking()
            .Where(x => x.UserProfileId == profileId)
            .ToListAsync();

        Assert.Equal(3, assignments.Count);

        var activeAssignment = Assert.Single(
            assignments,
            x => x.Status == "Active");

        Assert.Equal(
            thirdPlanId,
            activeAssignment.WorkoutPlanId);

        Assert.Equal(
            2,
            assignments.Count(x => x.Status == "Inactive"));
    }
    [Fact]
    public async Task ReassignWorkoutPlan_InvalidPlan_PreservesExistingAssignment()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(
            client,
            TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var approvedPlanId = await CreateTestWorkoutPlanAsync(factory);

        var draftPlanId = await CreateTestWorkoutPlanAsync(
            factory,
            reviewStatus: "Draft");

        var originalAssignment = await AssignTestWorkoutPlanAsync(
            client,
            profileId,
            approvedPlanId);

        var response = await client.PostAsJsonAsync(
            GetAssignmentEndpoint(profileId),
            CreateAssignmentRequest(draftPlanId));

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var assignments = await db.MemberWorkoutPlanAssignments
            .AsNoTracking()
            .Where(x => x.UserProfileId == profileId)
            .ToListAsync();

        var assignment = Assert.Single(assignments);

        Assert.Equal(originalAssignment.Id, assignment.Id);
        Assert.Equal(approvedPlanId, assignment.WorkoutPlanId);
        Assert.Equal("Active", assignment.Status);
        Assert.Null(assignment.DeactivatedAtUtc);
    }
    [Fact]
    public async Task GetAdminActiveAssignment_ExistingAssignment_ReturnsOk()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(
            client,
            TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var planId = await CreateTestWorkoutPlanAsync(factory);

        var expected = await AssignTestWorkoutPlanAsync(
            client,
            profileId,
            planId);

        var response = await client.GetAsync(
            $"/api/admin/members/{profileId}/workout-plans/active");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var assignment = await response.Content
            .ReadFromJsonAsync<MemberWorkoutPlanAssignmentResponse>();

        Assert.NotNull(assignment);
        Assert.Equal(expected.Id, assignment.Id);
        Assert.Equal(planId, assignment.WorkoutPlanId);
        Assert.Equal("Active", assignment.Status);
    }
    [Fact]
    public async Task GetAdminActiveAssignment_NoAssignment_ReturnsNotFound()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient();

        await AuthenticateAsync(
            client,
            TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var response = await client.GetAsync(
            $"/api/admin/members/{profileId}/workout-plans/active");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
    [Fact]
    public async Task GetMemberActiveWorkoutPlan_ExistingAssignment_ReturnsOk()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory,
            adminClient,
            memberClient);

        var response = await memberClient.GetAsync(
            "/api/member/workout-plans/active");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var assignment = await response.Content
            .ReadFromJsonAsync<MemberActiveWorkoutPlanResponse>();

        Assert.NotNull(assignment);

        Assert.Equal(data.ProfileId, assignment.UserProfileId);
        Assert.Equal(data.PlanId, assignment.WorkoutPlanId);
        Assert.Equal("Active", assignment.Status);
        Assert.Equal(1, assignment.VersionNumber);
    }
    [Fact]
    public async Task GetMemberWorkoutPlanDetails_ReturnsDaysAndExercises()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory,
            adminClient,
            memberClient);

        var response = await memberClient.GetAsync(
            "/api/member/workout-plans/active/details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var details = await response.Content
            .ReadFromJsonAsync<MemberWorkoutPlanDetailResponse>();

        Assert.NotNull(details);

        Assert.Equal(
            data.PlanId,
            details.Assignment.WorkoutPlanId);

        Assert.Equal(2, details.Days.Count);

        var trainingDay = Assert.Single(
            details.Days,
            x => x.DayNumber == 1);

        Assert.Equal(data.TrainingDayId, trainingDay.Id);
        Assert.False(trainingDay.IsRestDay);
        Assert.Equal(2, trainingDay.Exercises.Count);

        var restDay = Assert.Single(
            details.Days,
            x => x.DayNumber == 2);

        Assert.Equal(data.RestDayId, restDay.Id);
        Assert.True(restDay.IsRestDay);
        Assert.Empty(restDay.Exercises);
    }
    [Fact]
    public async Task GetMemberWorkoutDays_ReturnsOrderedDays()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory,
            adminClient,
            memberClient);

        var response = await memberClient.GetAsync(
            "/api/member/workout-plans/active/days");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var days = await response.Content
            .ReadFromJsonAsync<List<WorkoutPlanDayResponse>>();

        Assert.NotNull(days);
        Assert.Equal(2, days.Count);

        Assert.Equal(1, days[0].DayNumber);
        Assert.Equal(2, days[1].DayNumber);

        Assert.Equal(data.TrainingDayId, days[0].Id);
        Assert.Equal(data.RestDayId, days[1].Id);

        Assert.False(days[0].IsRestDay);
        Assert.True(days[1].IsRestDay);
    }
    [Fact]
    public async Task GetMemberWorkoutDay_ExistingDay_ReturnsOk()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory,
            adminClient,
            memberClient);

        var response = await memberClient.GetAsync(
            "/api/member/workout-plans/active/days/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var day = await response.Content
            .ReadFromJsonAsync<WorkoutPlanDayDetailResponse>();

        Assert.NotNull(day);

        Assert.Equal(data.TrainingDayId, day.Id);
        Assert.Equal("Upper Body", day.Name);
        Assert.Equal(1, day.DayNumber);
        Assert.Equal(2, day.Exercises.Count);
    }
    [Fact]
    public async Task GetMemberWorkoutDayExercises_ReturnsOrderedExercises()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory,
            adminClient,
            memberClient);

        var response = await memberClient.GetAsync(
            "/api/member/workout-plans/active/days/1/exercises");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var exercises = await response.Content
            .ReadFromJsonAsync<List<WorkoutPlanExerciseDetailResponse>>();

        Assert.NotNull(exercises);
        Assert.Equal(2, exercises.Count);

        Assert.Equal(data.FirstExerciseId, exercises[0].Id);
        Assert.Equal(data.SecondExerciseId, exercises[1].Id);

        Assert.Equal(1, exercises[0].ExerciseOrder);
        Assert.Equal(2, exercises[1].ExerciseOrder);

        Assert.Equal(3, exercises[0].Sets);
        Assert.Equal(12, exercises[0].Reps);
        Assert.Equal(60, exercises[0].RestSeconds);

        Assert.Equal(4, exercises[1].Sets);
        Assert.Equal(10, exercises[1].Reps);
        Assert.Equal(90, exercises[1].RestSeconds);
    }
    [Fact]
    public async Task GetMemberWorkoutExercise_ExistingExercise_ReturnsOk()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory,
            adminClient,
            memberClient);

        var response = await memberClient.GetAsync(
            $"/api/member/workout-plans/active/days/1/exercises/{data.FirstExerciseId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var exercise = await response.Content
            .ReadFromJsonAsync<WorkoutPlanExerciseDetailResponse>();

        Assert.NotNull(exercise);

        Assert.Equal(data.FirstExerciseId, exercise.Id);

        Assert.Equal(
            "Integration Test Bench Press",
            exercise.ExerciseName);

        Assert.False(
            string.IsNullOrWhiteSpace(exercise.ExerciseCode));

        Assert.Equal(3, exercise.Sets);
        Assert.Equal(12, exercise.Reps);
        Assert.Equal(60, exercise.RestSeconds);
        Assert.Equal("Maintain correct form.", exercise.Notes);
    }
    [Fact]
    public async Task GetMemberWorkoutDayExercises_RestDay_ReturnsEmptyCollection()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        await PrepareAssignedMemberAsync(
            factory,
            adminClient,
            memberClient);

        var response = await memberClient.GetAsync(
            "/api/member/workout-plans/active/days/2/exercises");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var exercises = await response.Content
            .ReadFromJsonAsync<List<WorkoutPlanExerciseDetailResponse>>();

        Assert.NotNull(exercises);
        Assert.Empty(exercises);
    }
    [Fact]
    public async Task GetMemberWorkoutDay_NonexistentDay_ReturnsNotFound()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        await PrepareAssignedMemberAsync(
            factory,
            adminClient,
            memberClient);

        var response = await memberClient.GetAsync(
            "/api/member/workout-plans/active/days/99");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
    [Fact]
    public async Task GetMemberWorkoutExercise_UnrelatedExercise_ReturnsNotFound()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        await PrepareAssignedMemberAsync(
            factory,
            adminClient,
            memberClient);

        var unrelatedPlan =
            await CreateCompleteTestWorkoutPlanAsync(factory);

        var response = await memberClient.GetAsync(
            $"/api/member/workout-plans/active/days/1/exercises/{unrelatedPlan.FirstExerciseId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
    [Fact]
    public async Task GetMemberActiveWorkoutPlan_NoAssignment_ReturnsNotFound()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var memberClient = factory.CreateClient();

        var member = await CreateTestMemberAsync(factory);

        await AuthenticateAsync(
            memberClient,
            member.Email);

        var response = await memberClient.GetAsync(
            "/api/member/workout-plans/active");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
    [Fact]
    public async Task GetMemberActiveWorkoutPlan_NoProfile_ReturnsNotFound()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var memberClient = factory.CreateClient();

        var member = await CreateTestMemberAsync(
            factory,
            createProfile: false);

        await AuthenticateAsync(
            memberClient,
            member.Email);

        var response = await memberClient.GetAsync(
            "/api/member/workout-plans/active");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
    // ============================================================
    // Batch E - Administrator Assignment History
    // ============================================================

    [Fact]
    public async Task GetAdminAssignmentHistory_NoAssignments_ReturnsEmptyHistory()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var profileId = await CreateTestProfileAsync(factory);

        var history = await GetAdminHistoryAsync(
            adminClient,
            profileId);

        Assert.Equal(0, history.TotalAssignments);
        Assert.Empty(history.Assignments);
        Assert.Null(history.ActiveAssignment);
    }

    [Fact]
    public async Task GetAdminAssignmentHistory_NonexistentProfile_ReturnsNotFound()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var response = await adminClient.GetAsync(
            $"/api/admin/members/{Guid.NewGuid()}/workout-plans/history");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task GetAdminAssignmentHistory_SingleAssignment_ReturnsHistory()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var profileId = await CreateTestProfileAsync(factory);

        var planId = await CreateTestWorkoutPlanAsync(factory);

        var expected = await AssignTestWorkoutPlanAsync(
            adminClient,
            profileId,
            planId);

        var history = await GetAdminHistoryAsync(
            adminClient,
            profileId);

        Assert.Equal(1, history.TotalAssignments);

        var assignment = Assert.Single(history.Assignments);

        Assert.Equal(expected.Id, assignment.Id);
        Assert.Equal(planId, assignment.WorkoutPlanId);
        Assert.Equal("Active", assignment.Status);

        Assert.NotNull(history.ActiveAssignment);

        Assert.Equal(
            expected.Id,
            history.ActiveAssignment.Id);
    }

    [Fact]
    public async Task GetAdminAssignmentHistory_MultipleAssignments_ReturnsAllRecords()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var profileId = await CreateTestProfileAsync(factory);

        var firstPlanId = await CreateTestWorkoutPlanAsync(factory);
        var secondPlanId = await CreateTestWorkoutPlanAsync(factory);
        var thirdPlanId = await CreateTestWorkoutPlanAsync(factory);

        var first = await AssignTestWorkoutPlanAsync(
            adminClient, profileId, firstPlanId);

        var second = await AssignTestWorkoutPlanAsync(
            adminClient, profileId, secondPlanId);

        var third = await AssignTestWorkoutPlanAsync(
            adminClient, profileId, thirdPlanId);

        var history = await GetAdminHistoryAsync(
            adminClient,
            profileId);

        Assert.Equal(3, history.TotalAssignments);
        Assert.Equal(3, history.Assignments.Count);

        Assert.Contains(
            history.Assignments,
            x => x.Id == first.Id && x.Status == "Inactive");

        Assert.Contains(
            history.Assignments,
            x => x.Id == second.Id && x.Status == "Inactive");

        Assert.Contains(
            history.Assignments,
            x => x.Id == third.Id && x.Status == "Active");
    }

    [Fact]
    public async Task GetAdminAssignmentHistory_ReturnsNewestAssignmentsFirst()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var profileId = await CreateTestProfileAsync(factory);

        var firstPlanId = await CreateTestWorkoutPlanAsync(factory);
        var secondPlanId = await CreateTestWorkoutPlanAsync(factory);
        var thirdPlanId = await CreateTestWorkoutPlanAsync(factory);

        var first = await AssignTestWorkoutPlanAsync(
            adminClient, profileId, firstPlanId);

        var second = await AssignTestWorkoutPlanAsync(
            adminClient, profileId, secondPlanId);

        var third = await AssignTestWorkoutPlanAsync(
            adminClient, profileId, thirdPlanId);

        // Establish deterministic timestamps for ordering verification.

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<FitnessPlatformDbContext>();

            var assignments = await db.MemberWorkoutPlanAssignments
                .Where(x => x.UserProfileId == profileId)
                .ToListAsync();

            var timestamp = DateTime.UtcNow.AddDays(-3);

            assignments.Single(x => x.Id == first.Id)
                .AssignedAtUtc = timestamp;

            assignments.Single(x => x.Id == second.Id)
                .AssignedAtUtc = timestamp.AddDays(1);

            assignments.Single(x => x.Id == third.Id)
                .AssignedAtUtc = timestamp.AddDays(2);

            await db.SaveChangesAsync();
        }

        var history = await GetAdminHistoryAsync(
            adminClient,
            profileId);

        Assert.Equal(3, history.TotalAssignments);

        Assert.Equal(third.Id, history.Assignments[0].Id);
        Assert.Equal(second.Id, history.Assignments[1].Id);
        Assert.Equal(first.Id, history.Assignments[2].Id);
    }

    [Fact]
    public async Task GetAdminAssignmentHistory_IdentifiesCorrectActiveAssignment()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var profileId = await CreateTestProfileAsync(factory);

        var firstPlanId = await CreateTestWorkoutPlanAsync(factory);
        var secondPlanId = await CreateTestWorkoutPlanAsync(factory);

        var first = await AssignTestWorkoutPlanAsync(
            adminClient, profileId, firstPlanId);

        var second = await AssignTestWorkoutPlanAsync(
            adminClient, profileId, secondPlanId);

        var history = await GetAdminHistoryAsync(
            adminClient,
            profileId);

        Assert.NotNull(history.ActiveAssignment);

        Assert.Equal(second.Id, history.ActiveAssignment.Id);
        Assert.Equal(secondPlanId, history.ActiveAssignment.WorkoutPlanId);
        Assert.Equal("Active", history.ActiveAssignment.Status);

        var active = Assert.Single(
            history.Assignments,
            x => x.Status == "Active");

        Assert.Equal(second.Id, active.Id);

        var inactive = Assert.Single(
            history.Assignments,
            x => x.Status == "Inactive");

        Assert.Equal(first.Id, inactive.Id);
        Assert.NotNull(inactive.DeactivatedAtUtc);
    }
    // ============================================================
    // Batch E - Member Assignment History
    // ============================================================

    [Fact]
    public async Task GetMemberAssignmentHistory_NoAssignments_ReturnsEmptyHistory()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var memberClient = factory.CreateClient();

        var member = await CreateTestMemberAsync(factory);

        await AuthenticateAsync(
            memberClient,
            member.Email);

        var history = await GetMemberHistoryAsync(memberClient);

        Assert.Equal(member.ProfileId, history.UserProfileId);
        Assert.Equal(0, history.TotalAssignments);
        Assert.Empty(history.Assignments);
        Assert.Null(history.ActiveAssignment);
    }

    [Fact]
    public async Task GetMemberAssignmentHistory_ReturnsOwnAssignmentRecords()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var member = await CreateTestMemberAsync(factory);

        var firstPlanId = await CreateTestWorkoutPlanAsync(factory);
        var secondPlanId = await CreateTestWorkoutPlanAsync(factory);

        var first = await AssignTestWorkoutPlanAsync(
            adminClient,
            member.ProfileId,
            firstPlanId);

        var second = await AssignTestWorkoutPlanAsync(
            adminClient,
            member.ProfileId,
            secondPlanId);

        await AuthenticateAsync(memberClient, member.Email);

        var history = await GetMemberHistoryAsync(memberClient);

        Assert.Equal(member.ProfileId, history.UserProfileId);
        Assert.Equal(2, history.TotalAssignments);
        Assert.Equal(2, history.Assignments.Count);

        Assert.NotNull(history.ActiveAssignment);

        Assert.Equal(second.Id, history.ActiveAssignment.Id);

        Assert.Contains(
            history.Assignments,
            x => x.Id == first.Id && x.Status == "Inactive");

        Assert.Contains(
            history.Assignments,
            x => x.Id == second.Id && x.Status == "Active");

        Assert.All(
            history.Assignments,
            x => Assert.Equal(member.ProfileId, x.UserProfileId));
    }

    [Fact]
    public async Task GetMemberAssignmentHistory_ExcludesOtherMembersAssignments()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var member1Client = factory.CreateClient();
        using var member2Client = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var member1 = await CreateTestMemberAsync(factory);
        var member2 = await CreateTestMemberAsync(factory);

        var firstPlanId = await CreateTestWorkoutPlanAsync(factory);
        var secondPlanId = await CreateTestWorkoutPlanAsync(factory);

        var firstAssignment = await AssignTestWorkoutPlanAsync(
            adminClient,
            member1.ProfileId,
            firstPlanId);

        var secondAssignment = await AssignTestWorkoutPlanAsync(
            adminClient,
            member2.ProfileId,
            secondPlanId);

        await AuthenticateAsync(member1Client, member1.Email);
        await AuthenticateAsync(member2Client, member2.Email);

        var firstHistory = await GetMemberHistoryAsync(member1Client);
        var secondHistory = await GetMemberHistoryAsync(member2Client);

        Assert.Equal(member1.ProfileId, firstHistory.UserProfileId);
        Assert.Equal(member2.ProfileId, secondHistory.UserProfileId);

        Assert.Equal(1, firstHistory.TotalAssignments);
        Assert.Equal(1, secondHistory.TotalAssignments);

        var firstRecord = Assert.Single(firstHistory.Assignments);
        var secondRecord = Assert.Single(secondHistory.Assignments);

        Assert.Equal(firstAssignment.Id, firstRecord.Id);
        Assert.Equal(secondAssignment.Id, secondRecord.Id);

        Assert.DoesNotContain(
            firstHistory.Assignments,
            x => x.Id == secondAssignment.Id);

        Assert.DoesNotContain(
            secondHistory.Assignments,
            x => x.Id == firstAssignment.Id);
    }

    [Fact]
    public async Task GetMemberAssignmentHistory_ExcludesAdministratorOnlyInformation()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var member = await CreateTestMemberAsync(factory);

        var planId = await CreateTestWorkoutPlanAsync(factory);

        var request = CreateAssignmentRequest(planId);

        request.Notes = "Confidential administrator assignment notes.";

        var assignResponse = await adminClient.PostAsJsonAsync(
            GetAssignmentEndpoint(member.ProfileId),
            request);

        Assert.Equal(
            HttpStatusCode.Created,
            assignResponse.StatusCode);

        var adminHistory = await GetAdminHistoryAsync(
            adminClient,
            member.ProfileId);

        var adminRecord = Assert.Single(adminHistory.Assignments);

        Assert.Equal(request.Notes, adminRecord.Notes);

        Assert.NotEqual(
            Guid.Empty,
            adminRecord.AssignedByUserId);

        await AuthenticateAsync(memberClient, member.Email);

        var response = await memberClient.GetAsync(
            "/api/member/workout-plans/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;

        var records = root.GetProperty("assignments");

        Assert.Equal(1, records.GetArrayLength());

        var record = records[0];

        var activeRecord = root.GetProperty("activeAssignment");

        var restrictedProperties = new[]
        {
        "notes",
        "assignedByUserId",
        "deactivatedAtUtc",
        "createdAtUtc",
        "updatedAtUtc"
    };

        foreach (var propertyName in restrictedProperties)
        {
            Assert.False(
                record.TryGetProperty(propertyName, out _));

            Assert.False(
                activeRecord.TryGetProperty(propertyName, out _));
        }

        Assert.DoesNotContain(
            request.Notes!,
            json);
    }

    [Fact]
    public async Task GetMemberAssignmentHistory_NoProfile_ReturnsNotFound()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var memberClient = factory.CreateClient();

        var member = await CreateTestMemberAsync(
            factory,
            createProfile: false);

        await AuthenticateAsync(memberClient, member.Email);

        var response = await memberClient.GetAsync(
            "/api/member/workout-plans/history");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
    [Fact]
    public async Task CreateWorkoutPlanVersion_AssignedPlan_ReturnsNewVersion()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory, adminClient, memberClient);

        var newPlanId = await CreateTestWorkoutPlanVersionAsync(
            factory, adminClient, data.PlanId);

        var originalPlan = await GetTestWorkoutPlanAsync(
            factory, data.PlanId);

        var newPlan = await GetTestWorkoutPlanAsync(
            factory, newPlanId);

        Assert.Equal(1, originalPlan.VersionNumber);
        Assert.Equal(2, newPlan.VersionNumber);

        Assert.False(originalPlan.IsLatestVersion);
        Assert.True(newPlan.IsLatestVersion);

        Assert.Equal(
            originalPlan.VersionGroupId,
            newPlan.VersionGroupId);

        Assert.Equal(
            originalPlan.Id,
            newPlan.PreviousVersionId);

        Assert.Equal("Approved", originalPlan.ReviewStatus);
        Assert.Equal("Draft", newPlan.ReviewStatus);
    }
    [Fact]
    public async Task CreateWorkoutPlanVersion_ExistingAssignment_RemainsOnOriginalVersion()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory, adminClient, memberClient);

        var before = await GetMemberActivePlanAsync(memberClient);

        var newPlanId = await CreateTestWorkoutPlanVersionAsync(
            factory, adminClient, data.PlanId);

        await ApproveTestWorkoutPlanAsync(
            adminClient, newPlanId);

        var after = await GetMemberActivePlanAsync(memberClient);

        Assert.Equal(before.Id, after.Id);
        Assert.Equal(data.PlanId, after.WorkoutPlanId);
        Assert.NotEqual(newPlanId, after.WorkoutPlanId);
        Assert.Equal(1, after.VersionNumber);
        Assert.False(after.IsLatestVersion);
        Assert.Equal("Active", after.Status);

        var history = await GetAdminHistoryAsync(
            adminClient, data.ProfileId);

        Assert.Equal(1, history.TotalAssignments);
    }
    [Fact]
    public async Task GetMemberWorkoutPlanDetails_HistoricalVersion_ReturnsOriginalContent()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory, adminClient, memberClient);

        await CreateTestWorkoutPlanVersionAsync(
            factory, adminClient, data.PlanId);

        var details = await GetMemberPlanDetailsAsync(memberClient);

        Assert.Equal(
            data.PlanId,
            details.Assignment.WorkoutPlanId);

        Assert.Equal(1, details.Assignment.VersionNumber);
        Assert.False(details.Assignment.IsLatestVersion);

        Assert.Equal(2, details.Days.Count);

        var trainingDay = Assert.Single(
            details.Days,
            x => x.DayNumber == 1);

        Assert.Equal(data.TrainingDayId, trainingDay.Id);
        Assert.Equal(2, trainingDay.Exercises.Count);

        Assert.Contains(
            trainingDay.Exercises,
            x => x.Id == data.FirstExerciseId);

        Assert.Contains(
            trainingDay.Exercises,
            x => x.Id == data.SecondExerciseId);

        var restDay = Assert.Single(
            details.Days,
            x => x.DayNumber == 2);

        Assert.True(restDay.IsRestDay);
        Assert.Empty(restDay.Exercises);
    }
    [Fact]
    public async Task AssignWorkoutPlan_HistoricalAssignedVersion_ReturnsConflict()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory, adminClient, memberClient);

        await CreateTestWorkoutPlanVersionAsync(
            factory, adminClient, data.PlanId);

        var newMember = await CreateTestMemberAsync(factory);

        var response = await adminClient.PostAsJsonAsync(
            GetAssignmentEndpoint(newMember.ProfileId),
            CreateAssignmentRequest(data.PlanId));

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        var history = await GetAdminHistoryAsync(
            adminClient, newMember.ProfileId);

        Assert.Equal(0, history.TotalAssignments);
        Assert.Null(history.ActiveAssignment);
    }
    [Fact]
    public async Task AssignWorkoutPlan_ApprovedLatestVersion_ReturnsCreated()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory, adminClient, memberClient);

        var newPlanId = await CreateTestWorkoutPlanVersionAsync(
            factory, adminClient, data.PlanId);

        await ApproveTestWorkoutPlanAsync(
            adminClient, newPlanId);

        var newMember = await CreateTestMemberAsync(factory);

        var assignment = await AssignTestWorkoutPlanAsync(
            adminClient,
            newMember.ProfileId,
            newPlanId);

        Assert.Equal(newPlanId, assignment.WorkoutPlanId);
        Assert.Equal(2, assignment.VersionNumber);
        Assert.True(assignment.IsLatestVersion);

        using var newMemberClient = factory.CreateClient();

        await AuthenticateAsync(
            newMemberClient, newMember.Email);

        var newMemberPlan = await GetMemberActivePlanAsync(
            newMemberClient);

        var originalMemberPlan = await GetMemberActivePlanAsync(
            memberClient);

        Assert.Equal(2, newMemberPlan.VersionNumber);
        Assert.Equal(1, originalMemberPlan.VersionNumber);

        Assert.Equal(newPlanId, newMemberPlan.WorkoutPlanId);
        Assert.Equal(data.PlanId, originalMemberPlan.WorkoutPlanId);
    }
    [Fact]
    public async Task ReassignWorkoutPlan_LatestVersion_UpdatesActiveAssignment()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory, adminClient, memberClient);

        var originalAssignment = await GetMemberActivePlanAsync(
            memberClient);

        var newPlanId = await CreateTestWorkoutPlanVersionAsync(
            factory, adminClient, data.PlanId);

        await ApproveTestWorkoutPlanAsync(
            adminClient, newPlanId);

        var newAssignment = await AssignTestWorkoutPlanAsync(
            adminClient,
            data.ProfileId,
            newPlanId);

        var currentAssignment = await GetMemberActivePlanAsync(
            memberClient);

        Assert.NotEqual(
            originalAssignment.Id,
            currentAssignment.Id);

        Assert.Equal(newAssignment.Id, currentAssignment.Id);
        Assert.Equal(newPlanId, currentAssignment.WorkoutPlanId);
        Assert.Equal(2, currentAssignment.VersionNumber);
        Assert.True(currentAssignment.IsLatestVersion);
        Assert.Equal("Active", currentAssignment.Status);
    }
    [Fact]
    public async Task ReassignWorkoutPlan_LatestVersion_PreservesHistoricalAssignment()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory, adminClient, memberClient);

        var originalAssignment = await GetMemberActivePlanAsync(
            memberClient);

        var newPlanId = await CreateTestWorkoutPlanVersionAsync(
            factory, adminClient, data.PlanId);

        await ApproveTestWorkoutPlanAsync(
            adminClient, newPlanId);

        var newAssignment = await AssignTestWorkoutPlanAsync(
            adminClient,
            data.ProfileId,
            newPlanId);

        var adminHistory = await GetAdminHistoryAsync(
            adminClient, data.ProfileId);

        var memberHistory = await GetMemberHistoryAsync(
            memberClient);

        Assert.Equal(2, adminHistory.TotalAssignments);
        Assert.Equal(2, memberHistory.TotalAssignments);

        var historicalRecord = Assert.Single(
            adminHistory.Assignments,
            x => x.Id == originalAssignment.Id);

        Assert.Equal(data.PlanId, historicalRecord.WorkoutPlanId);
        Assert.Equal(1, historicalRecord.VersionNumber);
        Assert.Equal("Inactive", historicalRecord.Status);
        Assert.NotNull(historicalRecord.DeactivatedAtUtc);
        Assert.False(historicalRecord.IsLatestVersion);

        var memberHistoricalRecord = Assert.Single(
            memberHistory.Assignments,
            x => x.Id == originalAssignment.Id);

        Assert.Equal(1, memberHistoricalRecord.VersionNumber);
        Assert.Equal("Inactive", memberHistoricalRecord.Status);

        Assert.NotNull(memberHistory.ActiveAssignment);

        Assert.Equal(
            newAssignment.Id,
            memberHistory.ActiveAssignment.Id);

        Assert.Equal(
            2,
            memberHistory.ActiveAssignment.VersionNumber);
    }
    [Fact]
    public async Task ReassignWorkoutPlan_LatestVersion_PreservesPreviousVersionHistory()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory,
            adminClient,
            memberClient);

        var newVersionId = await CreateTestWorkoutPlanVersionAsync(
            factory,
            adminClient,
            data.PlanId);

        await ApproveTestWorkoutPlanAsync(
            adminClient,
            newVersionId);

        await AssignTestWorkoutPlanAsync(
            adminClient,
            data.ProfileId,
            newVersionId);

        var history = await GetMemberHistoryAsync(memberClient);

        Assert.Equal(2, history.TotalAssignments);

        var originalVersion = Assert.Single(
            history.Assignments,
            x => x.WorkoutPlanId == data.PlanId);

        var latestVersion = Assert.Single(
            history.Assignments,
            x => x.WorkoutPlanId == newVersionId);

        Assert.Equal(1, originalVersion.VersionNumber);
        Assert.Equal("Inactive", originalVersion.Status);
        Assert.False(originalVersion.IsLatestVersion);

        Assert.Equal(2, latestVersion.VersionNumber);
        Assert.Equal("Active", latestVersion.Status);
        Assert.True(latestVersion.IsLatestVersion);

        Assert.Equal(
            originalVersion.VersionGroupId,
            latestVersion.VersionGroupId);

        Assert.NotNull(history.ActiveAssignment);

        Assert.Equal(
            latestVersion.Id,
            history.ActiveAssignment.Id);
    }
    [Fact]
    public async Task UpdateWorkoutPlan_NewVersion_DoesNotModifyOriginalVersion()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory,
            adminClient,
            memberClient);

        var originalDetails =
            await GetMemberWorkoutDetailsAsync(memberClient);

        var newVersionId = await CreateTestWorkoutPlanVersionAsync(
            factory,
            adminClient,
            data.PlanId);

        Guid newDayId;
        Guid newPlanExerciseId;
        Guid exerciseMasterId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<FitnessPlatformDbContext>();

            newDayId = await db.WorkoutPlanDays
                .Where(x =>
                    x.WorkoutPlanId == newVersionId &&
                    x.DayNumber == 1)
                .Select(x => x.Id)
                .SingleAsync();

            var exercise = await db.WorkoutPlanExercises
                .AsNoTracking()
                .SingleAsync(x =>
                    x.WorkoutPlanDayId == newDayId &&
                    x.ExerciseOrder == 1);

            newPlanExerciseId = exercise.Id;
            exerciseMasterId = exercise.ExerciseId;
        }

        Assert.NotEqual(
            data.FirstExerciseId,
            newPlanExerciseId);

        var request = new UpdateWorkoutPlanExerciseRequest
        {
            ExerciseId = exerciseMasterId,
            ExerciseOrder = 1,
            Sets = 5,
            Reps = 8,
            RestSeconds = 120,
            Notes = "Updated version two instructions."
        };

        var response = await adminClient.PutAsJsonAsync(
            $"/api/admin/workout-plans/{newVersionId}/days/{newDayId}/exercises/{newPlanExerciseId}",
            request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var currentDetails =
            await GetMemberWorkoutDetailsAsync(memberClient);

        var originalExercise = originalDetails.Days
            .SelectMany(x => x.Exercises)
            .Single(x => x.Id == data.FirstExerciseId);

        var currentExercise = currentDetails.Days
            .SelectMany(x => x.Exercises)
            .Single(x => x.Id == data.FirstExerciseId);

        Assert.Equal(
            originalExercise.Sets,
            currentExercise.Sets);

        Assert.Equal(
            originalExercise.Reps,
            currentExercise.Reps);

        Assert.Equal(
            originalExercise.RestSeconds,
            currentExercise.RestSeconds);

        Assert.Equal(
            originalExercise.Notes,
            currentExercise.Notes);

        using var verificationScope = factory.Services.CreateScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var updatedExercise = await verificationDb.WorkoutPlanExercises
            .AsNoTracking()
            .SingleAsync(x => x.Id == newPlanExerciseId);

        Assert.Equal(5, updatedExercise.Sets);
        Assert.Equal(8, updatedExercise.Reps);
        Assert.Equal(120, updatedExercise.RestSeconds);
    }

    [Fact]
    public async Task DeactivateWorkoutPlan_AssignedPlan_MemberRetainsAccess()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory,
            adminClient,
            memberClient);

        var originalAssignment =
            await GetMemberActiveAssignmentAsync(memberClient);

        var response = await adminClient.PatchAsync(
            $"/api/admin/workout-plans/{data.PlanId}/deactivate",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<FitnessPlatformDbContext>();

            var plan = await db.WorkoutPlans
                .AsNoTracking()
                .SingleAsync(x => x.Id == data.PlanId);

            Assert.False(plan.IsActive);
        }

        var activeAssignment =
            await GetMemberActiveAssignmentAsync(memberClient);

        Assert.Equal(
            originalAssignment.Id,
            activeAssignment.Id);

        Assert.Equal(data.PlanId, activeAssignment.WorkoutPlanId);
        Assert.Equal("Active", activeAssignment.Status);

        var details =
            await GetMemberWorkoutDetailsAsync(memberClient);

        Assert.Equal(2, details.Days.Count);

        var newMember = await CreateTestMemberAsync(factory);

        var newAssignmentResponse = await adminClient.PostAsJsonAsync(
            GetAssignmentEndpoint(newMember.ProfileId),
            CreateAssignmentRequest(data.PlanId));

        Assert.Equal(
            HttpStatusCode.Conflict,
            newAssignmentResponse.StatusCode);
    }
    [Fact]
    public async Task ConcurrentAssignment_SamePlan_OnlyOneAssignmentCreated()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var firstAdminClient = factory.CreateClient();
        using var secondAdminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, firstAdminClient);

        await AuthenticateAsync(
            secondAdminClient,
            TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);
        var planId = await CreateTestWorkoutPlanAsync(factory);

        var endpoint = GetAssignmentEndpoint(profileId);

        var responses = await ExecuteConcurrentRequestsAsync(
            () => firstAdminClient.PostAsJsonAsync(
                endpoint,
                CreateAssignmentRequest(planId)),
            () => secondAdminClient.PostAsJsonAsync(
                endpoint,
                CreateAssignmentRequest(planId)));

        AssertValidAssignmentConcurrencyResponses(responses);

        Assert.Single(
            responses,
            x => x.StatusCode == HttpStatusCode.Created);

        Assert.Single(
            responses,
            x => x.StatusCode == HttpStatusCode.Conflict);

        var assignments = await GetPersistedAssignmentsAsync(
            factory,
            profileId);

        var assignment = Assert.Single(assignments);

        Assert.Equal(planId, assignment.WorkoutPlanId);
        Assert.Equal("Active", assignment.Status);
        Assert.Null(assignment.DeactivatedAtUtc);
    }
    [Fact]
    public async Task ConcurrentAssignment_DifferentPlans_OnlyOneActiveAssignment()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var firstAdminClient = factory.CreateClient();
        using var secondAdminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, firstAdminClient);

        await AuthenticateAsync(
            secondAdminClient,
            TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var firstPlanId = await CreateTestWorkoutPlanAsync(factory);
        var secondPlanId = await CreateTestWorkoutPlanAsync(factory);

        var endpoint = GetAssignmentEndpoint(profileId);

        var responses = await ExecuteConcurrentRequestsAsync(
            () => firstAdminClient.PostAsJsonAsync(
                endpoint,
                CreateAssignmentRequest(firstPlanId)),
            () => secondAdminClient.PostAsJsonAsync(
                endpoint,
                CreateAssignmentRequest(secondPlanId)));

        AssertValidAssignmentConcurrencyResponses(responses);

        var successfulRequests = responses.Count(
            x => x.StatusCode == HttpStatusCode.Created);

        Assert.InRange(successfulRequests, 1, 2);

        var assignments = await GetPersistedAssignmentsAsync(
            factory,
            profileId);

        Assert.Equal(successfulRequests, assignments.Count);

        var activeAssignment = Assert.Single(
            assignments,
            x => x.Status == "Active");

        Assert.Contains(
            activeAssignment.WorkoutPlanId,
            new[] { firstPlanId, secondPlanId });

        Assert.All(
            assignments.Where(x => x.Status != "Active"),
            x =>
            {
                Assert.Equal("Inactive", x.Status);
                Assert.NotNull(x.DeactivatedAtUtc);
            });
    }
    [Fact]
    public async Task ConcurrentReassignment_PreservesHistoryAndSingleActiveAssignment()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var firstAdminClient = factory.CreateClient();
        using var secondAdminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, firstAdminClient);

        await AuthenticateAsync(
            secondAdminClient,
            TestIdentitySeeder.AdminEmail);

        var profileId = await CreateTestProfileAsync(factory);

        var originalPlanId = await CreateTestWorkoutPlanAsync(factory);
        var secondPlanId = await CreateTestWorkoutPlanAsync(factory);
        var thirdPlanId = await CreateTestWorkoutPlanAsync(factory);

        var originalAssignment = await AssignTestWorkoutPlanAsync(
            firstAdminClient,
            profileId,
            originalPlanId);

        var endpoint = GetAssignmentEndpoint(profileId);

        var responses = await ExecuteConcurrentRequestsAsync(
            () => firstAdminClient.PostAsJsonAsync(
                endpoint,
                CreateAssignmentRequest(secondPlanId)),
            () => secondAdminClient.PostAsJsonAsync(
                endpoint,
                CreateAssignmentRequest(thirdPlanId)));

        AssertValidAssignmentConcurrencyResponses(responses);

        var successfulRequests = responses.Count(
            x => x.StatusCode == HttpStatusCode.Created);

        Assert.InRange(successfulRequests, 1, 2);

        var assignments = await GetPersistedAssignmentsAsync(
            factory,
            profileId);

        Assert.Equal(
            1 + successfulRequests,
            assignments.Count);

        var originalRecord = Assert.Single(
            assignments,
            x => x.Id == originalAssignment.Id);

        Assert.Equal("Inactive", originalRecord.Status);
        Assert.Equal(originalPlanId, originalRecord.WorkoutPlanId);
        Assert.NotNull(originalRecord.DeactivatedAtUtc);

        var activeAssignment = Assert.Single(
            assignments,
            x => x.Status == "Active");

        Assert.Contains(
            activeAssignment.WorkoutPlanId,
            new[] { secondPlanId, thirdPlanId });

        Assert.All(
            assignments.Where(x => x.Status == "Inactive"),
            x => Assert.NotNull(x.DeactivatedAtUtc));
    }
    [Fact]
    public async Task ConcurrentAssignment_DifferentMembers_MaintainsIndependentAssignments()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var firstAdminClient = factory.CreateClient();
        using var secondAdminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, firstAdminClient);

        await AuthenticateAsync(
            secondAdminClient,
            TestIdentitySeeder.AdminEmail);

        var firstProfileId = await CreateTestProfileAsync(factory);
        var secondProfileId = await CreateTestProfileAsync(factory);

        var planId = await CreateTestWorkoutPlanAsync(factory);

        var responses = await ExecuteConcurrentRequestsAsync(
            () => firstAdminClient.PostAsJsonAsync(
                GetAssignmentEndpoint(firstProfileId),
                CreateAssignmentRequest(planId)),
            () => secondAdminClient.PostAsJsonAsync(
                GetAssignmentEndpoint(secondProfileId),
                CreateAssignmentRequest(planId)));

        AssertValidAssignmentConcurrencyResponses(responses);

        Assert.Contains(
            responses,
            x => x.StatusCode == HttpStatusCode.Created);

        // A transient SQL concurrency conflict is retryable.

        if (responses[0].StatusCode == HttpStatusCode.Conflict)
        {
            await AssignTestWorkoutPlanAsync(
                firstAdminClient,
                firstProfileId,
                planId);
        }

        if (responses[1].StatusCode == HttpStatusCode.Conflict)
        {
            await AssignTestWorkoutPlanAsync(
                secondAdminClient,
                secondProfileId,
                planId);
        }

        var firstAssignments = await GetPersistedAssignmentsAsync(
            factory,
            firstProfileId);

        var secondAssignments = await GetPersistedAssignmentsAsync(
            factory,
            secondProfileId);

        var firstAssignment = Assert.Single(firstAssignments);
        var secondAssignment = Assert.Single(secondAssignments);

        Assert.NotEqual(firstAssignment.Id, secondAssignment.Id);

        Assert.Equal(firstProfileId, firstAssignment.UserProfileId);
        Assert.Equal(secondProfileId, secondAssignment.UserProfileId);

        Assert.Equal(planId, firstAssignment.WorkoutPlanId);
        Assert.Equal(planId, secondAssignment.WorkoutPlanId);

        Assert.Equal("Active", firstAssignment.Status);
        Assert.Equal("Active", secondAssignment.Status);
    }
    [Fact]
    public async Task AssignWorkoutPlan_ConcurrentSamePlan_OnlyOneAssignmentCreated()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var firstAdminClient = factory.CreateClient();
        using var secondAdminClient = factory.CreateClient();

        await PrepareConcurrentAdminsAsync(
            factory,
            firstAdminClient,
            secondAdminClient);

        var profileId = await CreateTestProfileAsync(factory);

        var planId = await CreateTestWorkoutPlanAsync(factory);

        var responses = await ExecuteConcurrentRequestsAsync(
            () => firstAdminClient.PostAsJsonAsync(
                GetAssignmentEndpoint(profileId),
                CreateAssignmentRequest(planId)),
            () => secondAdminClient.PostAsJsonAsync(
                GetAssignmentEndpoint(profileId),
                CreateAssignmentRequest(planId)));

        Assert.Single(
            responses,
            x => x.StatusCode == HttpStatusCode.Created);

        Assert.Single(
            responses,
            x => x.StatusCode == HttpStatusCode.Conflict);

        var assignments = await GetPersistedAssignmentsAsync(
            factory,
            profileId);

        var assignment = Assert.Single(assignments);

        Assert.Equal(planId, assignment.WorkoutPlanId);
        Assert.Equal("Active", assignment.Status);
        Assert.Null(assignment.DeactivatedAtUtc);
    }
    [Fact]
    public async Task AssignWorkoutPlan_ConcurrentDifferentPlans_OnlyOneRemainsActive()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var firstAdminClient = factory.CreateClient();
        using var secondAdminClient = factory.CreateClient();

        await PrepareConcurrentAdminsAsync(
            factory,
            firstAdminClient,
            secondAdminClient);

        var profileId = await CreateTestProfileAsync(factory);

        var firstPlanId = await CreateTestWorkoutPlanAsync(factory);
        var secondPlanId = await CreateTestWorkoutPlanAsync(factory);

        var responses = await ExecuteConcurrentRequestsAsync(
            () => firstAdminClient.PostAsJsonAsync(
                GetAssignmentEndpoint(profileId),
                CreateAssignmentRequest(firstPlanId)),
            () => secondAdminClient.PostAsJsonAsync(
                GetAssignmentEndpoint(profileId),
                CreateAssignmentRequest(secondPlanId)));

        AssertValidConcurrentAssignmentResponses(responses);

        var assignments = await GetPersistedAssignmentsAsync(
            factory,
            profileId);

        var activeAssignment = Assert.Single(
            assignments,
            x => x.Status == "Active");

        Assert.Contains(
            activeAssignment.WorkoutPlanId,
            new[] { firstPlanId, secondPlanId });

        var successfulRequests = responses.Count(
            x => x.StatusCode == HttpStatusCode.Created);

        Assert.Equal(successfulRequests, assignments.Count);

        Assert.All(
            assignments.Where(x => x.Status != "Active"),
            x =>
            {
                Assert.Equal("Inactive", x.Status);
                Assert.NotNull(x.DeactivatedAtUtc);
            });
    }
    [Fact]
    public async Task ReassignWorkoutPlan_ConcurrentRequests_PreservesAssignmentHistory()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var firstAdminClient = factory.CreateClient();
        using var secondAdminClient = factory.CreateClient();

        await PrepareConcurrentAdminsAsync(
            factory,
            firstAdminClient,
            secondAdminClient);

        var profileId = await CreateTestProfileAsync(factory);

        var originalPlanId = await CreateTestWorkoutPlanAsync(factory);
        var secondPlanId = await CreateTestWorkoutPlanAsync(factory);
        var thirdPlanId = await CreateTestWorkoutPlanAsync(factory);

        var originalAssignment = await AssignTestWorkoutPlanAsync(
            firstAdminClient,
            profileId,
            originalPlanId);

        var responses = await ExecuteConcurrentRequestsAsync(
            () => firstAdminClient.PostAsJsonAsync(
                GetAssignmentEndpoint(profileId),
                CreateAssignmentRequest(secondPlanId)),
            () => secondAdminClient.PostAsJsonAsync(
                GetAssignmentEndpoint(profileId),
                CreateAssignmentRequest(thirdPlanId)));

        AssertValidConcurrentAssignmentResponses(responses);

        var assignments = await GetPersistedAssignmentsAsync(
            factory,
            profileId);

        var original = Assert.Single(
            assignments,
            x => x.Id == originalAssignment.Id);

        Assert.Equal(originalPlanId, original.WorkoutPlanId);
        Assert.Equal("Inactive", original.Status);
        Assert.NotNull(original.DeactivatedAtUtc);

        var active = Assert.Single(
            assignments,
            x => x.Status == "Active");

        Assert.Contains(
            active.WorkoutPlanId,
            new[] { secondPlanId, thirdPlanId });

        var successfulRequests = responses.Count(
            x => x.StatusCode == HttpStatusCode.Created);

        Assert.Equal(
            successfulRequests + 1,
            assignments.Count);

        Assert.All(
            assignments.Where(x => x.Status == "Inactive"),
            x => Assert.NotNull(x.DeactivatedAtUtc));
    }

    [Fact]
    public async Task AssignWorkoutPlan_ConcurrentDifferentMembers_PreservesMemberIsolation()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var firstAdminClient = factory.CreateClient();
        using var secondAdminClient = factory.CreateClient();

        await PrepareConcurrentAdminsAsync(
            factory,
            firstAdminClient,
            secondAdminClient);

        var firstProfileId = await CreateTestProfileAsync(factory);
        var secondProfileId = await CreateTestProfileAsync(factory);

        var firstPlanId = await CreateTestWorkoutPlanAsync(factory);
        var secondPlanId = await CreateTestWorkoutPlanAsync(factory);

        var responses = await ExecuteConcurrentRequestsAsync(
            () => firstAdminClient.PostAsJsonAsync(
                GetAssignmentEndpoint(firstProfileId),
                CreateAssignmentRequest(firstPlanId)),
            () => secondAdminClient.PostAsJsonAsync(
                GetAssignmentEndpoint(secondProfileId),
                CreateAssignmentRequest(secondPlanId)));

        AssertValidConcurrentAssignmentResponses(responses);

        // Retry an unrelated-member operation if SQL Server
        // reported a transient concurrency conflict.

        if (responses[0].StatusCode == HttpStatusCode.Conflict)
        {
            await AssignTestWorkoutPlanAsync(
                firstAdminClient,
                firstProfileId,
                firstPlanId);
        }

        if (responses[1].StatusCode == HttpStatusCode.Conflict)
        {
            await AssignTestWorkoutPlanAsync(
                secondAdminClient,
                secondProfileId,
                secondPlanId);
        }

        var firstAssignments = await GetPersistedAssignmentsAsync(
            factory,
            firstProfileId);

        var secondAssignments = await GetPersistedAssignmentsAsync(
            factory,
            secondProfileId);

        var first = Assert.Single(firstAssignments);
        var second = Assert.Single(secondAssignments);

        Assert.Equal(firstProfileId, first.UserProfileId);
        Assert.Equal(secondProfileId, second.UserProfileId);

        Assert.Equal(firstPlanId, first.WorkoutPlanId);
        Assert.Equal(secondPlanId, second.WorkoutPlanId);

        Assert.Equal("Active", first.Status);
        Assert.Equal("Active", second.Status);

        Assert.NotEqual(first.Id, second.Id);
    }
    [Fact]
    public async Task CreateWorkoutPlanVersion_ConcurrentRequests_OnlyOneVersionCreated()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var firstAdminClient = factory.CreateClient();
        using var secondAdminClient = factory.CreateClient();

        await PrepareConcurrentAdminsAsync(
            factory,
            firstAdminClient,
            secondAdminClient);

        var plan = await CreateCompleteTestWorkoutPlanAsync(factory);

        var endpoint =
            $"/api/admin/workout-plans/{plan.PlanId}/versions";

        var responses = await ExecuteConcurrentRequestsAsync(
            () => firstAdminClient.PostAsync(endpoint, null),
            () => secondAdminClient.PostAsync(endpoint, null));

        Assert.Single(
            responses,
            x => x.StatusCode == HttpStatusCode.Created);

        Assert.Single(
            responses,
            x => x.StatusCode == HttpStatusCode.Conflict);

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var sourcePlan = await db.WorkoutPlans
            .AsNoTracking()
            .SingleAsync(x => x.Id == plan.PlanId);

        var versions = await db.WorkoutPlans
            .AsNoTracking()
            .Where(x => x.VersionGroupId == sourcePlan.VersionGroupId)
            .OrderBy(x => x.VersionNumber)
            .ToListAsync();

        Assert.Equal(2, versions.Count);

        Assert.Equal(1, versions[0].VersionNumber);
        Assert.False(versions[0].IsLatestVersion);

        Assert.Equal(2, versions[1].VersionNumber);
        Assert.True(versions[1].IsLatestVersion);

        Assert.Equal(plan.PlanId, versions[1].PreviousVersionId);

        Assert.Single(
            versions,
            x => x.IsLatestVersion);

        var clonedDays = await db.WorkoutPlanDays
            .AsNoTracking()
            .Where(x => x.WorkoutPlanId == versions[1].Id)
            .ToListAsync();

        Assert.Equal(2, clonedDays.Count);

        var clonedTrainingDay = Assert.Single(
            clonedDays,
            x => !x.IsRestDay);

        var clonedExercises = await db.WorkoutPlanExercises
            .AsNoTracking()
            .Where(x => x.WorkoutPlanDayId == clonedTrainingDay.Id)
            .ToListAsync();

        Assert.Equal(2, clonedExercises.Count);
    }

    [Fact]
    public async Task AssignWorkoutPlan_ConcurrentDeactivation_PreservesConsistentState()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var firstAdminClient = factory.CreateClient();
        using var secondAdminClient = factory.CreateClient();

        await PrepareConcurrentAdminsAsync(
            factory,
            firstAdminClient,
            secondAdminClient);

        var profileId = await CreateTestProfileAsync(factory);

        var planId = await CreateTestWorkoutPlanAsync(factory);

        var responses = await ExecuteConcurrentRequestsAsync(
            () => firstAdminClient.PostAsJsonAsync(
                GetAssignmentEndpoint(profileId),
                CreateAssignmentRequest(planId)),
            () => secondAdminClient.PatchAsync(
                $"/api/admin/workout-plans/{planId}/deactivate",
                null));

        Assert.Contains(
            responses[0].StatusCode,
            new[]
            {
            HttpStatusCode.Created,
            HttpStatusCode.Conflict
            });

        Assert.Contains(
            responses[1].StatusCode,
            new[]
            {
            HttpStatusCode.OK,
            HttpStatusCode.Conflict
            });

        Assert.True(
            responses[0].StatusCode == HttpStatusCode.Created ||
            responses[1].StatusCode == HttpStatusCode.OK);

        var assignments = await GetPersistedAssignmentsAsync(
            factory,
            profileId);

        if (responses[0].StatusCode == HttpStatusCode.Created)
        {
            var assignment = Assert.Single(assignments);

            Assert.Equal(planId, assignment.WorkoutPlanId);
            Assert.Equal("Active", assignment.Status);
        }
        else
        {
            Assert.Empty(assignments);
        }

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var plan = await db.WorkoutPlans
            .AsNoTracking()
            .SingleAsync(x => x.Id == planId);

        if (responses[1].StatusCode == HttpStatusCode.OK)
        {
            Assert.False(plan.IsActive);

            var newProfileId = await CreateTestProfileAsync(factory);

            var newAssignmentResponse =
                await firstAdminClient.PostAsJsonAsync(
                    GetAssignmentEndpoint(newProfileId),
                    CreateAssignmentRequest(planId));

            Assert.Equal(
                HttpStatusCode.Conflict,
                newAssignmentResponse.StatusCode);
        }
        else
        {
            Assert.True(plan.IsActive);
        }
    }

    [Fact]
    public async Task AssignWorkoutPlan_ConcurrentVersionCreation_PreservesVersionConsistency()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var firstAdminClient = factory.CreateClient();
        using var secondAdminClient = factory.CreateClient();

        await PrepareConcurrentAdminsAsync(
            factory,
            firstAdminClient,
            secondAdminClient);

        var profileId = await CreateTestProfileAsync(factory);

        var plan = await CreateCompleteTestWorkoutPlanAsync(factory);

        var responses = await ExecuteConcurrentRequestsAsync(
            () => firstAdminClient.PostAsJsonAsync(
                GetAssignmentEndpoint(profileId),
                CreateAssignmentRequest(plan.PlanId)),
            () => secondAdminClient.PostAsync(
                $"/api/admin/workout-plans/{plan.PlanId}/versions",
                null));

        AssertValidConcurrentAssignmentResponses(responses);

        var assignments = await GetPersistedAssignmentsAsync(
            factory,
            profileId);

        if (responses[0].StatusCode == HttpStatusCode.Created)
        {
            var assignment = Assert.Single(assignments);

            Assert.Equal(plan.PlanId, assignment.WorkoutPlanId);
            Assert.Equal("Active", assignment.Status);
        }
        else
        {
            Assert.Empty(assignments);
        }

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var sourcePlan = await db.WorkoutPlans
            .AsNoTracking()
            .SingleAsync(x => x.Id == plan.PlanId);

        var versions = await db.WorkoutPlans
            .AsNoTracking()
            .Where(x => x.VersionGroupId == sourcePlan.VersionGroupId)
            .ToListAsync();

        Assert.Single(
            versions,
            x => x.IsLatestVersion);

        if (responses[1].StatusCode == HttpStatusCode.Created)
        {
            Assert.Equal(2, versions.Count);
            Assert.False(sourcePlan.IsLatestVersion);

            var newVersion = Assert.Single(
                versions,
                x => x.VersionNumber == 2);

            Assert.True(newVersion.IsLatestVersion);
            Assert.Equal("Draft", newVersion.ReviewStatus);
            Assert.Equal(plan.PlanId, newVersion.PreviousVersionId);

            var newProfileId = await CreateTestProfileAsync(factory);

            var historicalAssignmentResponse =
                await firstAdminClient.PostAsJsonAsync(
                    GetAssignmentEndpoint(newProfileId),
                    CreateAssignmentRequest(plan.PlanId));

            Assert.Equal(
                HttpStatusCode.Conflict,
                historicalAssignmentResponse.StatusCode);
        }
        else
        {
            Assert.Single(versions);
            Assert.True(sourcePlan.IsLatestVersion);
        }
    }

    [Fact]
    public async Task MemberWorkoutPlanAssignment_DatabaseRejectsDuplicateActiveAssignment()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var profileId = await CreateTestProfileAsync(factory);

        var firstPlanId = await CreateTestWorkoutPlanAsync(factory);
        var secondPlanId = await CreateTestWorkoutPlanAsync(factory);

        var originalAssignment = await AssignTestWorkoutPlanAsync(
            adminClient,
            profileId,
            firstPlanId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<FitnessPlatformDbContext>();

            var connection = db.Database.GetDbConnection();

            Assert.Equal(
                "FitnessPlatformIntegrationTestsDb",
                connection.Database);

            Assert.Contains(
                "(localdb)",
                connection.DataSource.ToLowerInvariant());

            var duplicateAssignment =
                new MemberWorkoutPlanAssignment
                {
                    Id = Guid.NewGuid(),
                    UserProfileId = profileId,
                    WorkoutPlanId = secondPlanId,
                    Status = "Active",
                    StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    AssignedAtUtc = DateTime.UtcNow,
                    AssignedByUserId = originalAssignment.AssignedByUserId,
                    Notes = "Duplicate active assignment test.",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

            db.MemberWorkoutPlanAssignments.Add(
                duplicateAssignment);

            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                async () => await db.SaveChangesAsync());

            var sqlException = Assert.IsType<
                Microsoft.Data.SqlClient.SqlException>(
                    exception.InnerException);

            Assert.Contains(
                sqlException.Number,
                new[] { 2601, 2627 });

            Assert.Contains(
                "UX_MemberWorkoutPlanAssignments_ActiveUserProfile",
                sqlException.Message);
        }

        var assignments = await GetPersistedAssignmentsAsync(
            factory,
            profileId);

        var assignment = Assert.Single(assignments);

        Assert.Equal(originalAssignment.Id, assignment.Id);
        Assert.Equal(firstPlanId, assignment.WorkoutPlanId);
        Assert.Equal("Active", assignment.Status);
    }

    [Fact]
    public async Task Rollback_ReassignmentFailureAfterDeactivation_RestoresOriginalAssignment()
    {
        var interceptor = new RollbackFailureInterceptor();

        using var factory =
            new FitnessPlatformWebApplicationFactory(interceptor);

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var profileId = await CreateTestProfileAsync(factory);

        var originalPlanId =
            await CreateTestWorkoutPlanAsync(factory);

        var replacementPlanId =
            await CreateTestWorkoutPlanAsync(factory);

        var originalAssignment =
            await AssignTestWorkoutPlanAsync(
                adminClient,
                profileId,
                originalPlanId);

        interceptor.Arm(db =>
            db.ChangeTracker
                .Entries<MemberWorkoutPlanAssignment>()
                .Any(x =>
                    x.Entity.Id == originalAssignment.Id &&
                    x.Entity.Status == "Inactive"));

        var response = await adminClient.PostAsJsonAsync(
            GetAssignmentEndpoint(profileId),
            CreateAssignmentRequest(replacementPlanId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        Assert.Equal(1, interceptor.TriggerCount);

        var assignments =
            await GetPersistedAssignmentsAsync(factory, profileId);

        var assignment = Assert.Single(assignments);

        Assert.Equal(originalAssignment.Id, assignment.Id);

        Assert.Equal(originalPlanId, assignment.WorkoutPlanId);

        Assert.Equal("Active", assignment.Status);

        Assert.Null(assignment.DeactivatedAtUtc);
    }

    [Fact]
    public async Task Rollback_ReassignmentFailureAfterInsertion_RemovesReplacementAssignment()
    {
        var interceptor = new RollbackFailureInterceptor();

        using var factory =
            new FitnessPlatformWebApplicationFactory(interceptor);

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var profileId = await CreateTestProfileAsync(factory);

        var originalPlanId =
            await CreateTestWorkoutPlanAsync(factory);

        var replacementPlanId =
            await CreateTestWorkoutPlanAsync(factory);

        var originalAssignment =
            await AssignTestWorkoutPlanAsync(
                adminClient,
                profileId,
                originalPlanId);

        interceptor.Arm(db =>
            db.ChangeTracker
                .Entries<MemberWorkoutPlanAssignment>()
                .Any(x =>
                    x.Entity.UserProfileId == profileId &&
                    x.Entity.WorkoutPlanId == replacementPlanId &&
                    x.Entity.Status == "Active"));

        var response = await adminClient.PostAsJsonAsync(
            GetAssignmentEndpoint(profileId),
            CreateAssignmentRequest(replacementPlanId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        Assert.Equal(1, interceptor.TriggerCount);

        var assignments =
            await GetPersistedAssignmentsAsync(factory, profileId);

        var assignment = Assert.Single(assignments);

        Assert.Equal(originalAssignment.Id, assignment.Id);

        Assert.Equal(originalPlanId, assignment.WorkoutPlanId);

        Assert.Equal("Active", assignment.Status);

        Assert.Null(assignment.DeactivatedAtUtc);

        Assert.DoesNotContain(
            assignments,
            x => x.WorkoutPlanId == replacementPlanId);
    }
    [Fact]
    public async Task Rollback_VersionCreationFailure_PreservesOriginalLatestVersion()
    {
        var interceptor = new RollbackFailureInterceptor();

        using var factory =
            new FitnessPlatformWebApplicationFactory(interceptor);

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var plan = await CreateCompleteTestWorkoutPlanAsync(factory);

        interceptor.Arm(db =>
            db.ChangeTracker
                .Entries<WorkoutPlan>()
                .Any(x =>
                    x.Entity.PreviousVersionId == plan.PlanId &&
                    x.Entity.IsLatestVersion));

        var response = await adminClient.PostAsync(
            $"/api/admin/workout-plans/{plan.PlanId}/versions",
            null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        Assert.Equal(1, interceptor.TriggerCount);

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var originalPlan = await db.WorkoutPlans
            .AsNoTracking()
            .SingleAsync(x => x.Id == plan.PlanId);

        Assert.True(originalPlan.IsLatestVersion);

        Assert.Equal(1, originalPlan.VersionNumber);

        Assert.Equal("Approved", originalPlan.ReviewStatus);

        var versions = await db.WorkoutPlans
            .AsNoTracking()
            .Where(x =>
                x.VersionGroupId == originalPlan.VersionGroupId)
            .ToListAsync();

        var remainingVersion = Assert.Single(versions);

        Assert.Equal(plan.PlanId, remainingVersion.Id);

        Assert.DoesNotContain(
            versions,
            x => x.PreviousVersionId == plan.PlanId);
    }
    [Fact]
    public async Task Rollback_VersionCloneFailure_RemovesClonedDaysAndExercises()
    {
        var interceptor = new RollbackFailureInterceptor();

        using var factory =
            new FitnessPlatformWebApplicationFactory(interceptor);

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var plan = await CreateCompleteTestWorkoutPlanAsync(factory);

        Guid failedVersionId = Guid.Empty;

        var clonedDayIds = new List<Guid>();

        var clonedExerciseIds = new List<Guid>();

        interceptor.Arm(db =>
        {
            var newVersion = db.ChangeTracker
                .Entries<WorkoutPlan>()
                .Select(x => x.Entity)
                .FirstOrDefault(x =>
                    x.PreviousVersionId == plan.PlanId);

            if (newVersion is null)
            {
                return false;
            }

            var newDays = db.ChangeTracker
                .Entries<WorkoutPlanDay>()
                .Select(x => x.Entity)
                .Where(x => x.WorkoutPlanId == newVersion.Id)
                .ToList();

            var newDayIds = newDays
                .Select(x => x.Id)
                .ToList();

            var newExercises = db.ChangeTracker
                .Entries<WorkoutPlanExercise>()
                .Select(x => x.Entity)
                .Where(x =>
                    newDayIds.Contains(x.WorkoutPlanDayId))
                .ToList();

            if (newDays.Count != 2 ||
                newExercises.Count != 2)
            {
                return false;
            }

            failedVersionId = newVersion.Id;

            clonedDayIds = newDayIds;

            clonedExerciseIds = newExercises
                .Select(x => x.Id)
                .ToList();

            return true;
        });

        var response = await adminClient.PostAsync(
            $"/api/admin/workout-plans/{plan.PlanId}/versions",
            null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        Assert.Equal(1, interceptor.TriggerCount);

        Assert.NotEqual(Guid.Empty, failedVersionId);

        Assert.Equal(2, clonedDayIds.Count);

        Assert.Equal(2, clonedExerciseIds.Count);

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        Assert.False(await db.WorkoutPlans
            .AnyAsync(x => x.Id == failedVersionId));

        Assert.False(await db.WorkoutPlanDays
            .AnyAsync(x => x.WorkoutPlanId == failedVersionId));

        Assert.False(await db.WorkoutPlanDays
            .AnyAsync(x => clonedDayIds.Contains(x.Id)));

        Assert.False(await db.WorkoutPlanExercises
            .AnyAsync(x => clonedDayIds.Contains(
                x.WorkoutPlanDayId)));

        Assert.False(await db.WorkoutPlanExercises
            .AnyAsync(x => clonedExerciseIds.Contains(x.Id)));

        var originalPlan = await db.WorkoutPlans
            .AsNoTracking()
            .SingleAsync(x => x.Id == plan.PlanId);

        Assert.True(originalPlan.IsLatestVersion);

        var originalDays = await db.WorkoutPlanDays
            .AsNoTracking()
            .Where(x => x.WorkoutPlanId == plan.PlanId)
            .ToListAsync();

        Assert.Equal(2, originalDays.Count);

        Assert.Contains(
            originalDays,
            x => x.Id == plan.TrainingDayId);

        Assert.Contains(
            originalDays,
            x => x.Id == plan.RestDayId);

        var originalDayIds = originalDays
            .Select(x => x.Id)
            .ToList();

        var originalExercises = await db.WorkoutPlanExercises
            .AsNoTracking()
            .Where(x =>
                originalDayIds.Contains(x.WorkoutPlanDayId))
            .ToListAsync();

        Assert.Equal(2, originalExercises.Count);

        Assert.Contains(
            originalExercises,
            x => x.Id == plan.FirstExerciseId);

        Assert.Contains(
            originalExercises,
            x => x.Id == plan.SecondExerciseId);
    }

    [Fact]
    public async Task RetireExercise_AssignedWorkoutPlan_MemberRetainsExerciseAccess()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();

        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory,
            adminClient,
            memberClient);

        var exerciseMasterId =
            await GetExerciseMasterIdAsync(
                factory,
                data.FirstExerciseId);

        var originalDetails =
            await GetMemberWorkoutDetailsAsync(memberClient);

        await RetireTestExerciseAsync(
            adminClient,
            exerciseMasterId);

        var currentDetails =
            await GetMemberWorkoutDetailsAsync(memberClient);

        Assert.Equal(
            JsonSerializer.Serialize(originalDetails),
            JsonSerializer.Serialize(currentDetails));

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercise = await db.Exercises
            .AsNoTracking()
            .SingleAsync(x => x.Id == exerciseMasterId);

        Assert.Equal("Retired", exercise.ReviewStatus);

        Assert.False(exercise.IsActive);

        Assert.True(await db.WorkoutPlanExercises
            .AnyAsync(x =>
                x.Id == data.FirstExerciseId &&
                x.ExerciseId == exerciseMasterId));
    }

    [Fact]
    public async Task RetireExercise_HistoricalWorkoutVersion_PreservesMemberContent()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();

        using var memberClient = factory.CreateClient();

        var data = await PrepareAssignedMemberAsync(
            factory,
            adminClient,
            memberClient);

        var exerciseMasterId =
            await GetExerciseMasterIdAsync(
                factory,
                data.FirstExerciseId);

        var newVersionId =
            await CreateTestWorkoutPlanVersionAsync(
                factory,
                adminClient,
                data.PlanId);

        Assert.NotEqual(data.PlanId, newVersionId);

        var historicalDetails =
            await GetMemberWorkoutDetailsAsync(memberClient);

        Assert.Equal(
            data.PlanId,
            historicalDetails.Assignment.WorkoutPlanId);

        Assert.False(
            historicalDetails.Assignment.IsLatestVersion);

        await RetireTestExerciseAsync(
            adminClient,
            exerciseMasterId);

        var currentDetails =
            await GetMemberWorkoutDetailsAsync(memberClient);

        Assert.Equal(
            JsonSerializer.Serialize(historicalDetails),
            JsonSerializer.Serialize(currentDetails));

        Assert.Equal(
            data.PlanId,
            currentDetails.Assignment.WorkoutPlanId);

        Assert.False(
            currentDetails.Assignment.IsLatestVersion);

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        Assert.True(await db.WorkoutPlanExercises
            .AnyAsync(x =>
                x.Id == data.FirstExerciseId &&
                x.ExerciseId == exerciseMasterId));
    }

    [Fact]
    public async Task AddWorkoutPlanExercise_RetiredExercise_ReturnsConflict()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var sourcePlan =
            await CreateCompleteTestWorkoutPlanAsync(factory);

        var exerciseMasterId =
            await GetExerciseMasterIdAsync(
                factory,
                sourcePlan.FirstExerciseId);

        await RetireTestExerciseAsync(
            adminClient,
            exerciseMasterId);

        var draftPlanId =
            await CreateTestWorkoutPlanAsync(
                factory,
                reviewStatus: "Draft");

        var dayId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<FitnessPlatformDbContext>();

            db.WorkoutPlanDays.Add(new WorkoutPlanDay
            {
                Id = dayId,
                WorkoutPlanId = draftPlanId,
                DayNumber = 1,
                Name = "Training Day",
                Description = "Exercise availability test.",
                IsRestDay = false,
                EstimatedDurationMinutes = 60
            });

            await db.SaveChangesAsync();
        }

        var request = new AddWorkoutPlanExerciseRequest
        {
            ExerciseId = exerciseMasterId,
            ExerciseOrder = 1,
            Sets = 3,
            Reps = 12,
            RestSeconds = 60,
            Notes = "Retired exercise test."
        };

        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/workout-plans/{draftPlanId}/days/{dayId}/exercises",
            request);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "EXERCISE_NOT_AVAILABLE",
            document.RootElement
                .GetProperty("code")
                .GetString());

        using var verificationScope =
            factory.Services.CreateScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        Assert.False(await verificationDb.WorkoutPlanExercises
            .AnyAsync(x => x.WorkoutPlanDayId == dayId));
    }
    [Fact]
    public async Task UpdateWorkoutPlanExercise_RetiredReplacement_ReturnsConflict()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var data =
            await PrepareEditableExerciseAssignmentAsync(
                factory,
                adminClient);

        var replacementId =
            await CreateReplacementExerciseAsync(factory);

        await RetireTestExerciseAsync(
            adminClient,
            replacementId);

        var original =
            await GetPersistedWorkoutExerciseAsync(
                factory,
                data.AssignmentId);

        var request =
            CreateExerciseReplacementRequest(replacementId);

        var response = await adminClient.PutAsJsonAsync(
            $"/api/admin/workout-plans/{data.PlanId}/days/{data.DayId}/exercises/{data.AssignmentId}",
            request);

        await AssertExerciseUnavailableAsync(response);

        var current =
            await GetPersistedWorkoutExerciseAsync(
                factory,
                data.AssignmentId);

        AssertWorkoutExerciseUnchanged(
            original,
            current);
    }

    [Fact]
    public async Task UpdateWorkoutPlanExercise_DraftReplacement_ReturnsConflict()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var data =
            await PrepareEditableExerciseAssignmentAsync(
                factory,
                adminClient);

        var replacementId =
            await CreateReplacementExerciseAsync(
                factory,
                reviewStatus: "Draft");

        var original =
            await GetPersistedWorkoutExerciseAsync(
                factory,
                data.AssignmentId);

        var request =
            CreateExerciseReplacementRequest(replacementId);

        var response = await adminClient.PutAsJsonAsync(
            $"/api/admin/workout-plans/{data.PlanId}/days/{data.DayId}/exercises/{data.AssignmentId}",
            request);

        await AssertExerciseUnavailableAsync(response);

        var current =
            await GetPersistedWorkoutExerciseAsync(
                factory,
                data.AssignmentId);

        AssertWorkoutExerciseUnchanged(
            original,
            current);
    }

    [Fact]
    public async Task UpdateWorkoutPlanExercise_ApprovedReplacement_ReturnsOk()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var data =
            await PrepareEditableExerciseAssignmentAsync(
                factory,
                adminClient);

        var replacementId =
            await CreateReplacementExerciseAsync(factory);

        Assert.NotEqual(
            data.OriginalExerciseId,
            replacementId);

        var request =
            CreateExerciseReplacementRequest(replacementId);

        var response = await adminClient.PutAsJsonAsync(
            $"/api/admin/workout-plans/{data.PlanId}/days/{data.DayId}/exercises/{data.AssignmentId}",
            request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var current =
            await GetPersistedWorkoutExerciseAsync(
                factory,
                data.AssignmentId);

        Assert.Equal(
            data.AssignmentId,
            current.Id);

        Assert.Equal(
            data.DayId,
            current.WorkoutPlanDayId);

        Assert.Equal(
            replacementId,
            current.ExerciseId);

        Assert.Equal(1, current.ExerciseOrder);

        Assert.Equal(4, current.Sets);

        Assert.Equal(15, current.Reps);

        Assert.Equal(90, current.RestSeconds);

        Assert.Equal(
            request.Notes,
            current.Notes);
    }

    [Fact]
    public async Task UpdateWorkoutPlanExercise_ExistingRetiredExercise_AllowsParameterUpdate()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var data =
            await PrepareEditableExerciseAssignmentAsync(
                factory,
                adminClient);

        await RetireTestExerciseAsync(
            adminClient,
            data.OriginalExerciseId);

        var request =
            CreateExerciseReplacementRequest(
                data.OriginalExerciseId);

        var response = await adminClient.PutAsJsonAsync(
            $"/api/admin/workout-plans/{data.PlanId}/days/{data.DayId}/exercises/{data.AssignmentId}",
            request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var current =
            await GetPersistedWorkoutExerciseAsync(
                factory,
                data.AssignmentId);

        Assert.Equal(
            data.AssignmentId,
            current.Id);

        Assert.Equal(
            data.OriginalExerciseId,
            current.ExerciseId);

        Assert.Equal(4, current.Sets);

        Assert.Equal(15, current.Reps);

        Assert.Equal(90, current.RestSeconds);

        Assert.Equal(
            request.Notes,
            current.Notes);

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercise = await db.Exercises
            .AsNoTracking()
            .SingleAsync(x =>
                x.Id == data.OriginalExerciseId);

        Assert.Equal(
            "Retired",
            exercise.ReviewStatus);

        Assert.False(exercise.IsActive);
    }

    [Fact]
    public async Task ExerciseLibrary_RetiredExercise_IsExcluded()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();

        using var memberClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var member = await CreateTestMemberAsync(factory);

        await AuthenticateAsync(
            memberClient,
            member.Email);

        var exerciseId =
            await CreateReplacementExerciseAsync(factory);

        string exerciseCode;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<FitnessPlatformDbContext>();

            exerciseCode = await db.Exercises
                .AsNoTracking()
                .Where(x => x.Id == exerciseId)
                .Select(x => x.Code)
                .SingleAsync();
        }

        var searchUrl =
            $"/api/exercise-library?search={Uri.EscapeDataString(exerciseCode)}";

        var detailUrl =
            $"/api/exercise-library/{exerciseId}";

        // Verify exercise is available before retirement.
        var beforeResponse =
            await memberClient.GetAsync(searchUrl);

        Assert.Equal(
            HttpStatusCode.OK,
            beforeResponse.StatusCode);

        var beforeExercises =
            await beforeResponse.Content
                .ReadFromJsonAsync<List<ExerciseResponse>>();

        Assert.NotNull(beforeExercises);

        Assert.Contains(
            beforeExercises,
            x => x.Id == exerciseId);

        var beforeDetailResponse =
            await memberClient.GetAsync(detailUrl);

        Assert.Equal(
            HttpStatusCode.OK,
            beforeDetailResponse.StatusCode);

        // Retire the exercise.
        await RetireTestExerciseAsync(
            adminClient,
            exerciseId);

        // Verify exercise is excluded from the library.
        var afterResponse =
            await memberClient.GetAsync(searchUrl);

        Assert.Equal(
            HttpStatusCode.OK,
            afterResponse.StatusCode);

        var afterExercises =
            await afterResponse.Content
                .ReadFromJsonAsync<List<ExerciseResponse>>();

        Assert.NotNull(afterExercises);

        Assert.DoesNotContain(
            afterExercises,
            x => x.Id == exerciseId);

        // Direct exercise library retrieval must also fail.
        var afterDetailResponse =
            await memberClient.GetAsync(detailUrl);

        Assert.Equal(
            HttpStatusCode.NotFound,
            afterDetailResponse.StatusCode);

        // Retirement must not physically delete the exercise.
        using var verificationScope =
            factory.Services.CreateScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var persistedExercise = await verificationDb.Exercises
            .AsNoTracking()
            .SingleAsync(x => x.Id == exerciseId);

        Assert.Equal(
            "Retired",
            persistedExercise.ReviewStatus);

        Assert.False(persistedExercise.IsActive);
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Retired")]
    public async Task UpdateExercise_ProtectedExercise_ReturnsConflict(
    string reviewStatus)
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var adminClient = factory.CreateClient();

        await PrepareAdminAsync(factory, adminClient);

        var exerciseId =
            await CreateReplacementExerciseAsync(factory);

        if (reviewStatus == "Retired")
        {
            await RetireTestExerciseAsync(
                adminClient,
                exerciseId);
        }

        Exercise original;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<FitnessPlatformDbContext>();

            original = await db.Exercises
                .AsNoTracking()
                .SingleAsync(x => x.Id == exerciseId);
        }

        Assert.Equal(
            reviewStatus,
            original.ReviewStatus);

        var request = new SaveExerciseRequest
        {
            Code = original.Code,
            Name = "Unauthorized Exercise Modification",
            Description = "Modified exercise description.",
            MuscleGroup = original.MuscleGroup,
            Equipment = original.Equipment,
            DifficultyLevel = original.DifficultyLevel,
            TrainingLocation = original.TrainingLocation,
            Instructions = "Modified exercise instructions.",
            VideoUrl = original.VideoUrl,
            GifUrl = original.GifUrl
        };

        var response = await adminClient.PutAsJsonAsync(
            $"/api/exercises/{exerciseId}",
            request);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "Only Draft exercises can be updated.",
            document.RootElement
                .GetProperty("error")
                .GetString());

        using var verificationScope =
            factory.Services.CreateScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var current = await verificationDb.Exercises
            .AsNoTracking()
            .SingleAsync(x => x.Id == exerciseId);

        Assert.Equal(original.Id, current.Id);

        Assert.Equal(original.Code, current.Code);

        Assert.Equal(original.Name, current.Name);

        Assert.Equal(
            original.Description,
            current.Description);

        Assert.Equal(
            original.MuscleGroup,
            current.MuscleGroup);

        Assert.Equal(
            original.Equipment,
            current.Equipment);

        Assert.Equal(
            original.DifficultyLevel,
            current.DifficultyLevel);

        Assert.Equal(
            original.TrainingLocation,
            current.TrainingLocation);

        Assert.Equal(
            original.Instructions,
            current.Instructions);

        Assert.Equal(
            original.VideoUrl,
            current.VideoUrl);

        Assert.Equal(
            original.GifUrl,
            current.GifUrl);

        Assert.Equal(
            original.ReviewStatus,
            current.ReviewStatus);

        Assert.Equal(
            original.IsActive,
            current.IsActive);

        Assert.Equal(
            original.CreatedAtUtc,
            current.CreatedAtUtc);

        Assert.Equal(
            original.UpdatedAtUtc,
            current.UpdatedAtUtc);
    }

    private static async Task ApproveExerciseForWorkoutPlanAsync(
    HttpClient client,
    Guid exerciseId)
    {
        // Step 1: Submit the Draft exercise for review.
        using var submitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseId}/submit-review",
            null);

        var submitResponseBody =
            await submitResponse.Content.ReadAsStringAsync();

        Assert.True(
            submitResponse.StatusCode == HttpStatusCode.OK,
            $"Exercise submission failed. " +
            $"ExerciseId: {exerciseId}. " +
            $"Status: {submitResponse.StatusCode}. " +
            $"Response: {submitResponseBody}");

        // Step 2: Approve the exercise.
        using var approveResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/approve",
            null);

        var approveResponseBody =
            await approveResponse.Content.ReadAsStringAsync();

        Assert.True(
            approveResponse.StatusCode == HttpStatusCode.OK,
            $"Exercise approval failed. " +
            $"ExerciseId: {exerciseId}. " +
            $"Status: {approveResponse.StatusCode}. " +
            $"Response: {approveResponseBody}");
    }

}