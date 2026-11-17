using FitnessPlatform.Application.Exercises;
using FitnessPlatform.Contracts.Exercises;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FitnessPlatform.IntegrationTests;

public class ExerciseWorkflowIntegrationTests
{
    [Fact]
    public async Task Admin_CanCreateExercise_AndSubmitForReview()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create an exercise with a unique code
        var exerciseCode = $"TEST-{Guid.NewGuid():N}";

        var request = new SaveExerciseRequest
        {
            Code = exerciseCode,
            Name = "Integration Test Squat",
            Description = "Exercise created by an integration test.",
            MuscleGroup = "Legs",
            Equipment = "Bodyweight",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform a controlled squat."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        Assert.NotEqual(Guid.Empty, exerciseId);

        // STEP 3: Submit the exercise for review
        var submitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseId}/submit-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // STEP 4: Verify exercise status in database
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercise = await db.Exercises
            .AsNoTracking()
            .SingleAsync(x => x.Id == exerciseId);

        Assert.Equal("In Review", exercise.ReviewStatus);
        Assert.True(exercise.IsActive);

        // STEP 5: Verify review audit history
        var audit = await db.ExerciseReviewAudits
            .AsNoTracking()
            .SingleAsync(x => x.ExerciseId == exerciseId);

        Assert.Equal("Draft", audit.PreviousStatus);
        Assert.Equal("In Review", audit.NewStatus);
    }
    [Fact]
    public async Task Admin_CanCompleteExerciseReviewLifecycle()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create a unique exercise
        var request = new SaveExerciseRequest
        {
            Code = $"LIFECYCLE-{Guid.NewGuid():N}",
            Name = "Exercise Lifecycle Integration Test",
            Description = "Tests the complete exercise review lifecycle.",
            MuscleGroup = "Legs",
            Equipment = "Bodyweight",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform controlled repetitions."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        Assert.NotEqual(Guid.Empty, exerciseId);

        // STEP 3: Submit Draft exercise for review
        var submitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseId}/submit-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // STEP 4: Approve exercise
        var approveResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/approve",
            null);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // STEP 5: Retire exercise
        var retireResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/retire",
            null);

        Assert.Equal(HttpStatusCode.OK, retireResponse.StatusCode);

        // STEP 6: Verify final database state
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercise = await db.Exercises
            .AsNoTracking()
            .SingleAsync(x => x.Id == exerciseId);

        Assert.Equal("Retired", exercise.ReviewStatus);
        Assert.False(exercise.IsActive);

        // STEP 7: Verify all three audit records
        var audits = await db.ExerciseReviewAudits
            .AsNoTracking()
            .Where(x => x.ExerciseId == exerciseId)
            .OrderBy(x => x.PerformedAtUtc)
            .ToListAsync();

        Assert.Equal(3, audits.Count);

        Assert.Contains(audits, x =>
            x.PreviousStatus == "Draft" &&
            x.NewStatus == "In Review");

        Assert.Contains(audits, x =>
            x.PreviousStatus == "In Review" &&
            x.NewStatus == "Approved");

        Assert.Contains(audits, x =>
            x.PreviousStatus == "Approved" &&
            x.NewStatus == "Retired");
    }
    [Fact]
    public async Task Admin_CannotApproveDraftExercise()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create a new Draft exercise
        var request = new SaveExerciseRequest
        {
            Code = $"INVALID-APPROVE-{Guid.NewGuid():N}",
            Name = "Invalid Approval Integration Test",
            Description = "Tests approval rejection for Draft exercises.",
            MuscleGroup = "Legs",
            Equipment = "Bodyweight",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform controlled repetitions."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 3: Attempt approval without submitting for review
        var approveResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/approve",
            null);

        // STEP 4: Verify API rejects invalid transition
        Assert.Equal(
            HttpStatusCode.Conflict,
            approveResponse.StatusCode);

        // STEP 5: Verify exercise remains in Draft status
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercise = await db.Exercises
            .AsNoTracking()
            .SingleAsync(x => x.Id == exerciseId);

        Assert.Equal("Draft", exercise.ReviewStatus);
        Assert.True(exercise.IsActive);

        // STEP 6: Verify no audit record was created
        var auditCount = await db.ExerciseReviewAudits
            .CountAsync(x => x.ExerciseId == exerciseId);

        Assert.Equal(0, auditCount);
    }

    [Fact]
    public async Task Admin_CannotRetireDraftExercise()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create a Draft exercise
        var request = new SaveExerciseRequest
        {
            Code = $"INVALID-RETIRE-{Guid.NewGuid():N}",
            Name = "Invalid Retirement Integration Test",
            Description = "Tests retirement rejection for Draft exercises.",
            MuscleGroup = "Legs",
            Equipment = "Bodyweight",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform controlled repetitions."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        Assert.NotEqual(Guid.Empty, exerciseId);

        // STEP 3: Attempt to retire a Draft exercise
        var retireResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/retire",
            null);

        // STEP 4: Verify HTTP 409 Conflict
        Assert.Equal(
            HttpStatusCode.Conflict,
            retireResponse.StatusCode);

        // STEP 5: Verify exercise remains unchanged
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercise = await db.Exercises
            .AsNoTracking()
            .SingleAsync(x => x.Id == exerciseId);

        Assert.Equal("Draft", exercise.ReviewStatus);
        Assert.True(exercise.IsActive);

        // STEP 6: Verify no audit record was created
        var auditCount = await db.ExerciseReviewAudits
            .CountAsync(x => x.ExerciseId == exerciseId);

        Assert.Equal(0, auditCount);
    }

    [Fact]
    public async Task Admin_CannotSubmitExerciseForReviewTwice()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create a Draft exercise
        var request = new SaveExerciseRequest
        {
            Code = $"DUP-REVIEW-{Guid.NewGuid():N}",
            Name = "Duplicate Review Submission Test",
            Description = "Tests rejection of repeated review submission.",
            MuscleGroup = "Legs",
            Equipment = "Bodyweight",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform controlled repetitions."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        Assert.NotEqual(Guid.Empty, exerciseId);

        // STEP 3: Submit exercise for review
        var firstSubmitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseId}/submit-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            firstSubmitResponse.StatusCode);

        // STEP 4: Attempt duplicate submission
        var secondSubmitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseId}/submit-review",
            null);

        Assert.Equal(
            HttpStatusCode.Conflict,
            secondSubmitResponse.StatusCode);

        // STEP 5: Verify exercise remains In Review
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercise = await db.Exercises
            .AsNoTracking()
            .SingleAsync(x => x.Id == exerciseId);

        Assert.Equal("In Review", exercise.ReviewStatus);
        Assert.True(exercise.IsActive);

        // STEP 6: Verify only one audit record exists
        var audits = await db.ExerciseReviewAudits
            .AsNoTracking()
            .Where(x => x.ExerciseId == exerciseId)
            .ToListAsync();

        Assert.Single(audits);

        Assert.Equal("Draft", audits[0].PreviousStatus);
        Assert.Equal("In Review", audits[0].NewStatus);
    }

    [Fact]
    public async Task Admin_CannotApproveInactiveExercise()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create a Draft exercise
        var request = new SaveExerciseRequest
        {
            Code = $"INACTIVE-APPROVE-{Guid.NewGuid():N}",
            Name = "Inactive Exercise Approval Test",
            Description = "Tests rejection of inactive exercise approval.",
            MuscleGroup = "Legs",
            Equipment = "Bodyweight",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform controlled repetitions."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        Assert.NotEqual(Guid.Empty, exerciseId);

        // STEP 3: Submit exercise for review
        var submitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseId}/submit-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // STEP 4: Mark exercise inactive in the isolated test database
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<FitnessPlatformDbContext>();

            var exercise = await db.Exercises
                .SingleAsync(x => x.Id == exerciseId);

            exercise.IsActive = false;

            await db.SaveChangesAsync();
        }

        // STEP 5: Attempt to approve inactive exercise
        var approveResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/approve",
            null);

        Assert.Equal(
            HttpStatusCode.Conflict,
            approveResponse.StatusCode);

        // STEP 6: Verify exercise remains unchanged
        using var verificationScope = factory.Services.CreateScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var updatedExercise = await verificationDb.Exercises
            .AsNoTracking()
            .SingleAsync(x => x.Id == exerciseId);

        Assert.Equal("In Review", updatedExercise.ReviewStatus);
        Assert.False(updatedExercise.IsActive);

        // STEP 7: Verify no approval audit record was created
        var audits = await verificationDb.ExerciseReviewAudits
            .AsNoTracking()
            .Where(x => x.ExerciseId == exerciseId)
            .ToListAsync();

        Assert.Single(audits);

        Assert.Equal("Draft", audits[0].PreviousStatus);
        Assert.Equal("In Review", audits[0].NewStatus);
    }

    [Fact]
    public async Task Admin_CanBulkSubmitDraftExercisesForReview()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create two Draft exercises
        var exerciseIds = new List<Guid>();

        for (var i = 1; i <= 2; i++)
        {
            var request = new SaveExerciseRequest
            {
                Code = $"BULK-{Guid.NewGuid():N}",
                Name = $"Bulk Submission Test Exercise {i}",
                Description = "Tests bulk exercise review submission.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            };

            var createResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                request);

            Assert.Equal(
                HttpStatusCode.Created,
                createResponse.StatusCode);

            using var createDocument = JsonDocument.Parse(
                await createResponse.Content.ReadAsStringAsync());

            var exerciseId = createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

            Assert.NotEqual(Guid.Empty, exerciseId);

            exerciseIds.Add(exerciseId);
        }

        Assert.Equal(2, exerciseIds.Count);

        // STEP 3: Submit both exercises for review
        var bulkRequest = new BulkExerciseReviewRequest
        {
            ExerciseIds = exerciseIds
        };

        var bulkResponse = await client.PutAsJsonAsync(
            "/api/exercises/bulk/submit-review",
            bulkRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            bulkResponse.StatusCode);

        // STEP 4: Verify both exercises changed status
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercises = await db.Exercises
            .AsNoTracking()
            .Where(x => exerciseIds.Contains(x.Id))
            .ToListAsync();

        Assert.Equal(2, exercises.Count);

        Assert.All(exercises, exercise =>
        {
            Assert.Equal("In Review", exercise.ReviewStatus);
            Assert.True(exercise.IsActive);
        });

        // STEP 5: Verify audit records for both exercises
        var audits = await db.ExerciseReviewAudits
            .AsNoTracking()
            .Where(x => exerciseIds.Contains(x.ExerciseId))
            .ToListAsync();

        Assert.Equal(2, audits.Count);

        Assert.All(audits, audit =>
        {
            Assert.Equal("Draft", audit.PreviousStatus);
            Assert.Equal("In Review", audit.NewStatus);
            Assert.Equal("Submit", audit.Action);
            Assert.Equal("Bulk", audit.ActionSource);
        });

        // Verify each exercise has exactly one audit record
        Assert.All(exerciseIds, exerciseId =>
        {
            Assert.Single(audits, x => x.ExerciseId == exerciseId);
        });
    }

    [Fact]
    public async Task Admin_CanBulkApproveExercises()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create two Draft exercises
        var exerciseIds = new List<Guid>();

        for (var i = 1; i <= 2; i++)
        {
            var request = new SaveExerciseRequest
            {
                Code = $"BULK-APP-{Guid.NewGuid():N}",
                Name = $"Bulk Approval Test Exercise {i}",
                Description = "Tests bulk exercise approval.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            };

            var createResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                request);

            Assert.Equal(
                HttpStatusCode.Created,
                createResponse.StatusCode);

            using var createDocument = JsonDocument.Parse(
                await createResponse.Content.ReadAsStringAsync());

            var exerciseId = createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

            Assert.NotEqual(Guid.Empty, exerciseId);

            exerciseIds.Add(exerciseId);
        }

        Assert.Equal(2, exerciseIds.Count);

        // STEP 3: Bulk submit exercises for review
        var bulkRequest = new BulkExerciseReviewRequest
        {
            ExerciseIds = exerciseIds
        };

        var submitResponse = await client.PutAsJsonAsync(
            "/api/exercises/bulk/submit-review",
            bulkRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // STEP 4: Bulk approve exercises
        var approveResponse = await client.PutAsJsonAsync(
            "/api/exercises/bulk/approve",
            bulkRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            approveResponse.StatusCode);

        // STEP 5: Verify both exercises are Approved
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercises = await db.Exercises
            .AsNoTracking()
            .Where(x => exerciseIds.Contains(x.Id))
            .ToListAsync();

        Assert.Equal(2, exercises.Count);

        Assert.All(exercises, exercise =>
        {
            Assert.Equal("Approved", exercise.ReviewStatus);
            Assert.True(exercise.IsActive);
        });

        // STEP 6: Verify audit history
        var audits = await db.ExerciseReviewAudits
            .AsNoTracking()
            .Where(x => exerciseIds.Contains(x.ExerciseId))
            .ToListAsync();

        // Each exercise should have two audit records:
        // Draft -> In Review
        // In Review -> Approved
        Assert.Equal(4, audits.Count);

        foreach (var exerciseId in exerciseIds)
        {
            var exerciseAudits = audits
                .Where(x => x.ExerciseId == exerciseId)
                .ToList();

            Assert.Equal(2, exerciseAudits.Count);

            Assert.Contains(exerciseAudits, audit =>
                audit.PreviousStatus == "Draft" &&
                audit.NewStatus == "In Review" &&
                audit.Action == "Submit" &&
                audit.ActionSource == "Bulk");

            Assert.Contains(exerciseAudits, audit =>
                audit.PreviousStatus == "In Review" &&
                audit.NewStatus == "Approved" &&
                audit.Action == "Approve" &&
                audit.ActionSource == "Bulk");
        }
    }

    [Fact]
    public async Task Admin_CannotBulkApprove_WhenOneExerciseIsDraft()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create two Draft exercises
        var exerciseIds = new List<Guid>();

        for (var i = 1; i <= 2; i++)
        {
            var request = new SaveExerciseRequest
            {
                Code = $"BULK-REJECT-{Guid.NewGuid():N}",
                Name = $"Bulk Approval Rejection Test {i}",
                Description = "Tests rejection of mixed-status bulk approval.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            };

            var createResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                request);

            Assert.Equal(
                HttpStatusCode.Created,
                createResponse.StatusCode);

            using var createDocument = JsonDocument.Parse(
                await createResponse.Content.ReadAsStringAsync());

            var exerciseId = createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

            Assert.NotEqual(Guid.Empty, exerciseId);

            exerciseIds.Add(exerciseId);
        }

        // STEP 3: Submit only Exercise 1 for review
        var submitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseIds[0]}/submit-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // Exercise 1 = In Review
        // Exercise 2 = Draft

        // STEP 4: Attempt bulk approval of both exercises
        var bulkRequest = new BulkExerciseReviewRequest
        {
            ExerciseIds = exerciseIds
        };

        var approveResponse = await client.PutAsJsonAsync(
            "/api/exercises/bulk/approve",
            bulkRequest);

        Assert.Equal(
            HttpStatusCode.Conflict,
            approveResponse.StatusCode);

        // STEP 5: Verify neither exercise was approved
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercises = await db.Exercises
            .AsNoTracking()
            .Where(x => exerciseIds.Contains(x.Id))
            .ToListAsync();

        Assert.Equal(2, exercises.Count);

        var firstExercise = exercises.Single(
            x => x.Id == exerciseIds[0]);

        var secondExercise = exercises.Single(
            x => x.Id == exerciseIds[1]);

        Assert.Equal("In Review", firstExercise.ReviewStatus);
        Assert.Equal("Draft", secondExercise.ReviewStatus);

        Assert.True(firstExercise.IsActive);
        Assert.True(secondExercise.IsActive);

        // STEP 6: Verify no approval audit records exist
        var audits = await db.ExerciseReviewAudits
            .AsNoTracking()
            .Where(x => exerciseIds.Contains(x.ExerciseId))
            .ToListAsync();

        Assert.Single(audits);

        Assert.Equal(exerciseIds[0], audits[0].ExerciseId);
        Assert.Equal("Draft", audits[0].PreviousStatus);
        Assert.Equal("In Review", audits[0].NewStatus);
        Assert.Equal("Submit", audits[0].Action);
    }

    [Fact]
    public async Task Admin_CannotBulkSubmit_WhenOneExerciseIsAlreadyInReview()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create two Draft exercises
        var exerciseIds = new List<Guid>();

        for (var i = 1; i <= 2; i++)
        {
            var request = new SaveExerciseRequest
            {
                Code = $"BULK-SUB-REJECT-{Guid.NewGuid():N}",
                Name = $"Bulk Submission Rejection Test {i}",
                Description = "Tests rejection of mixed-status bulk submission.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            };

            var createResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                request);

            Assert.Equal(
                HttpStatusCode.Created,
                createResponse.StatusCode);

            using var createDocument = JsonDocument.Parse(
                await createResponse.Content.ReadAsStringAsync());

            var exerciseId = createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

            Assert.NotEqual(Guid.Empty, exerciseId);

            exerciseIds.Add(exerciseId);
        }

        Assert.Equal(2, exerciseIds.Count);

        // STEP 3: Submit only Exercise 2 for review
        var submitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseIds[1]}/submit-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // Exercise 1 = Draft
        // Exercise 2 = In Review

        // STEP 4: Attempt bulk submission of both exercises
        var bulkRequest = new BulkExerciseReviewRequest
        {
            ExerciseIds = exerciseIds
        };

        var bulkResponse = await client.PutAsJsonAsync(
            "/api/exercises/bulk/submit-review",
            bulkRequest);

        Assert.Equal(
            HttpStatusCode.Conflict,
            bulkResponse.StatusCode);

        // STEP 5: Verify neither exercise was modified
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercises = await db.Exercises
            .AsNoTracking()
            .Where(x => exerciseIds.Contains(x.Id))
            .ToListAsync();

        Assert.Equal(2, exercises.Count);

        var firstExercise = exercises.Single(
            x => x.Id == exerciseIds[0]);

        var secondExercise = exercises.Single(
            x => x.Id == exerciseIds[1]);

        Assert.Equal("Draft", firstExercise.ReviewStatus);
        Assert.Equal("In Review", secondExercise.ReviewStatus);

        Assert.True(firstExercise.IsActive);
        Assert.True(secondExercise.IsActive);

        // STEP 6: Verify no additional audit records were created
        var audits = await db.ExerciseReviewAudits
            .AsNoTracking()
            .Where(x => exerciseIds.Contains(x.ExerciseId))
            .ToListAsync();

        // Only Exercise 2's original submission should be recorded
        Assert.Single(audits);

        Assert.Equal(exerciseIds[1], audits[0].ExerciseId);
        Assert.Equal("Draft", audits[0].PreviousStatus);
        Assert.Equal("In Review", audits[0].NewStatus);
        Assert.Equal("Submit", audits[0].Action);
    }

    [Fact]
    public async Task Admin_CannotBulkApprove_WhenOneExerciseIsInactive()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create two Draft exercises
        var exerciseIds = new List<Guid>();

        for (var i = 1; i <= 2; i++)
        {
            var request = new SaveExerciseRequest
            {
                Code = $"BULK-INACTIVE-{Guid.NewGuid():N}",
                Name = $"Bulk Inactive Approval Test {i}",
                Description = "Tests bulk approval with inactive exercise.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            };

            var createResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                request);

            Assert.Equal(
                HttpStatusCode.Created,
                createResponse.StatusCode);

            using var createDocument = JsonDocument.Parse(
                await createResponse.Content.ReadAsStringAsync());

            var exerciseId = createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

            Assert.NotEqual(Guid.Empty, exerciseId);

            exerciseIds.Add(exerciseId);
        }

        Assert.Equal(2, exerciseIds.Count);

        // STEP 3: Submit both exercises for review
        var bulkRequest = new BulkExerciseReviewRequest
        {
            ExerciseIds = exerciseIds
        };

        var submitResponse = await client.PutAsJsonAsync(
            "/api/exercises/bulk/submit-review",
            bulkRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // STEP 4: Make Exercise 2 inactive
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<FitnessPlatformDbContext>();

            var inactiveExercise = await db.Exercises
                .SingleAsync(x => x.Id == exerciseIds[1]);

            inactiveExercise.IsActive = false;

            await db.SaveChangesAsync();
        }

        // STEP 5: Attempt bulk approval
        var approveResponse = await client.PutAsJsonAsync(
            "/api/exercises/bulk/approve",
            bulkRequest);

        Assert.Equal(
            HttpStatusCode.Conflict,
            approveResponse.StatusCode);

        // STEP 6: Verify database status
        using var verificationScope = factory.Services.CreateScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercises = await verificationDb.Exercises
            .AsNoTracking()
            .Where(x => exerciseIds.Contains(x.Id))
            .ToListAsync();

        Assert.Equal(2, exercises.Count);

        var firstExercise = exercises.Single(
            x => x.Id == exerciseIds[0]);

        var secondExercise = exercises.Single(
            x => x.Id == exerciseIds[1]);

        Assert.Equal("In Review", firstExercise.ReviewStatus);
        Assert.Equal("In Review", secondExercise.ReviewStatus);

        Assert.True(firstExercise.IsActive);
        Assert.False(secondExercise.IsActive);

        // STEP 7: Verify audit history
        var audits = await verificationDb.ExerciseReviewAudits
            .AsNoTracking()
            .Where(x => exerciseIds.Contains(x.ExerciseId))
            .ToListAsync();

        // Only the original two submission audits should exist
        Assert.Equal(2, audits.Count);

        Assert.All(audits, audit =>
        {
            Assert.Equal("Draft", audit.PreviousStatus);
            Assert.Equal("In Review", audit.NewStatus);
            Assert.Equal("Submit", audit.Action);
            Assert.Equal("Bulk", audit.ActionSource);
        });

        Assert.All(exerciseIds, exerciseId =>
        {
            Assert.Single(audits, x => x.ExerciseId == exerciseId);
        });
    }
    [Fact]
    public async Task Admin_CannotBulkSubmit_WhenOneExerciseIsInactive()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create two Draft exercises
        var exerciseIds = new List<Guid>();

        for (var i = 1; i <= 2; i++)
        {
            var request = new SaveExerciseRequest
            {
                Code = $"BULK-INACTIVE-SUB-{Guid.NewGuid():N}",
                Name = $"Bulk Inactive Submission Test {i}",
                Description = "Tests bulk submission with inactive exercise.",
                MuscleGroup = "Legs",
                Equipment = "Bodyweight",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform controlled repetitions."
            };

            var createResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                request);

            Assert.Equal(
                HttpStatusCode.Created,
                createResponse.StatusCode);

            using var createDocument = JsonDocument.Parse(
                await createResponse.Content.ReadAsStringAsync());

            var exerciseId = createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

            Assert.NotEqual(Guid.Empty, exerciseId);

            exerciseIds.Add(exerciseId);
        }

        Assert.Equal(2, exerciseIds.Count);

        // STEP 3: Mark Exercise 2 as inactive
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<FitnessPlatformDbContext>();

            var inactiveExercise = await db.Exercises
                .SingleAsync(x => x.Id == exerciseIds[1]);

            inactiveExercise.IsActive = false;

            await db.SaveChangesAsync();
        }

        // STEP 4: Attempt bulk review submission
        var bulkRequest = new BulkExerciseReviewRequest
        {
            ExerciseIds = exerciseIds
        };

        var submitResponse = await client.PutAsJsonAsync(
            "/api/exercises/bulk/submit-review",
            bulkRequest);

        Assert.Equal(
            HttpStatusCode.Conflict,
            submitResponse.StatusCode);

        // STEP 5: Verify exercise statuses remain unchanged
        using var verificationScope = factory.Services.CreateScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercises = await verificationDb.Exercises
            .AsNoTracking()
            .Where(x => exerciseIds.Contains(x.Id))
            .ToListAsync();

        Assert.Equal(2, exercises.Count);

        var firstExercise = exercises.Single(
            x => x.Id == exerciseIds[0]);

        var secondExercise = exercises.Single(
            x => x.Id == exerciseIds[1]);

        Assert.Equal("Draft", firstExercise.ReviewStatus);
        Assert.Equal("Draft", secondExercise.ReviewStatus);

        Assert.True(firstExercise.IsActive);
        Assert.False(secondExercise.IsActive);

        // STEP 6: Verify no audit records were created
        var audits = await verificationDb.ExerciseReviewAudits
            .AsNoTracking()
            .Where(x => exerciseIds.Contains(x.ExerciseId))
            .ToListAsync();

        Assert.Empty(audits);
    }

    [Fact]
    public async Task Admin_CannotBulkApprove_WhenExerciseIdDoesNotExist()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create one exercise
        var request = new SaveExerciseRequest
        {
            Code = $"BULK-MISSING-{Guid.NewGuid():N}",
            Name = "Bulk Missing Exercise Test",
            Description = "Tests bulk approval with missing exercise ID.",
            MuscleGroup = "Legs",
            Equipment = "Bodyweight",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform controlled repetitions."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        Assert.NotEqual(Guid.Empty, exerciseId);

        // STEP 3: Submit existing exercise for review
        var submitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseId}/submit-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // STEP 4: Generate a non-existing exercise ID
        var missingExerciseId = Guid.NewGuid();

        // STEP 5: Attempt bulk approval
        var bulkRequest = new BulkExerciseReviewRequest
        {
            ExerciseIds = new List<Guid>
        {
            exerciseId,
            missingExerciseId
        }
        };

        var approveResponse = await client.PutAsJsonAsync(
            "/api/exercises/bulk/approve",
            bulkRequest);

            Assert.Equal(
             HttpStatusCode.BadRequest,
             approveResponse.StatusCode);

        // STEP 6: Verify existing exercise was not approved
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercise = await db.Exercises
            .AsNoTracking()
            .SingleAsync(x => x.Id == exerciseId);

        Assert.Equal("In Review", exercise.ReviewStatus);
        Assert.True(exercise.IsActive);

        // STEP 7: Verify no approval audit was created
        var audits = await db.ExerciseReviewAudits
            .AsNoTracking()
            .Where(x => x.ExerciseId == exerciseId)
            .ToListAsync();

        // Only the original submission audit should exist
        Assert.Single(audits);

        Assert.Equal("Draft", audits[0].PreviousStatus);
        Assert.Equal("In Review", audits[0].NewStatus);
        Assert.Equal("Submit", audits[0].Action);
    }

    [Fact]
    public async Task Admin_CannotBulkSubmit_WhenExerciseIdDoesNotExist()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create one Draft exercise
        var request = new SaveExerciseRequest
        {
            Code = $"BULK-MISSING-SUB-{Guid.NewGuid():N}",
            Name = "Bulk Missing Submission Test",
            Description = "Tests bulk submission with missing exercise ID.",
            MuscleGroup = "Legs",
            Equipment = "Bodyweight",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform controlled repetitions."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        Assert.NotEqual(Guid.Empty, exerciseId);

        // STEP 3: Generate a nonexistent exercise ID
        var missingExerciseId = Guid.NewGuid();

        Assert.NotEqual(exerciseId, missingExerciseId);

        // STEP 4: Attempt bulk submission
        var bulkRequest = new BulkExerciseReviewRequest
        {
            ExerciseIds = new List<Guid>
        {
            exerciseId,
            missingExerciseId
        }
        };

        var submitResponse = await client.PutAsJsonAsync(
            "/api/exercises/bulk/submit-review",
            bulkRequest);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            submitResponse.StatusCode);

        // STEP 5: Verify existing exercise remains Draft
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var exercise = await db.Exercises
            .AsNoTracking()
            .SingleAsync(x => x.Id == exerciseId);

        Assert.Equal("Draft", exercise.ReviewStatus);
        Assert.True(exercise.IsActive);

        // STEP 6: Verify no audit records were created
        var audits = await db.ExerciseReviewAudits
            .AsNoTracking()
            .Where(x => x.ExerciseId == exerciseId)
            .ToListAsync();

        Assert.Empty(audits);
    }

    [Fact]
    public async Task Admin_CanRetrieveCompleteExerciseAuditHistory()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create a Draft exercise
        var request = new SaveExerciseRequest
        {
            Code = $"AUDIT-HISTORY-{Guid.NewGuid():N}",
            Name = "Exercise Audit History Test",
            Description = "Tests complete exercise review audit history.",
            MuscleGroup = "Chest",
            Equipment = "Barbell",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform controlled repetitions."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        Assert.NotEqual(Guid.Empty, exerciseId);

        // STEP 3: Submit exercise for review
        var submitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseId}/submit-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // STEP 4: Approve exercise
        var approveResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/approve",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            approveResponse.StatusCode);

        // STEP 5: Retire exercise
        var retireResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/retire",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            retireResponse.StatusCode);

        // STEP 6: Retrieve audit history through API
        var historyResponse = await client.GetAsync(
            $"/api/exercises/{exerciseId}/audit-history");

        Assert.Equal(
            HttpStatusCode.OK,
            historyResponse.StatusCode);

        using var historyDocument = JsonDocument.Parse(
    await historyResponse.Content.ReadAsStringAsync());

        var response = historyDocument.RootElement;

        // Verify response structure
        Assert.Equal(JsonValueKind.Object, response.ValueKind);

        // Verify exercise ID
        Assert.Equal(
            exerciseId,
            response.GetProperty("exerciseId").GetGuid());

        // Verify total audit records
        Assert.Equal(
            3,
            response.GetProperty("totalRecords").GetInt32());

        // Extract audit history array
        var history = response.GetProperty("history");

        Assert.Equal(JsonValueKind.Array, history.ValueKind);
        Assert.Equal(3, history.GetArrayLength());

        // STEP 7: Verify all workflow transitions
        var auditEntries = history.EnumerateArray()
            .Select(x => new
            {
                PreviousStatus = x.GetProperty("previousStatus").GetString(),
                NewStatus = x.GetProperty("newStatus").GetString(),
                Action = x.GetProperty("action").GetString(),
                ActionSource = x.GetProperty("actionSource").GetString()
            })
            .ToList();

        Assert.Contains(auditEntries, x =>
            x.PreviousStatus == "Draft" &&
            x.NewStatus == "In Review" &&
            x.Action == "Submit");

        Assert.Contains(auditEntries, x =>
            x.PreviousStatus == "In Review" &&
            x.NewStatus == "Approved" &&
            x.Action == "Approve");

        Assert.Contains(auditEntries, x =>
            x.PreviousStatus == "Approved" &&
            x.NewStatus == "Retired" &&
            x.Action == "Retire");

        // STEP 8: Verify audit records in database
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var audits = await db.ExerciseReviewAudits
            .AsNoTracking()
            .Where(x => x.ExerciseId == exerciseId)
            .ToListAsync();

        Assert.Equal(3, audits.Count);

        // STEP 9: Verify final exercise status
        var exercise = await db.Exercises
            .AsNoTracking()
            .SingleAsync(x => x.Id == exerciseId);

        Assert.Equal("Retired", exercise.ReviewStatus);
        Assert.False(exercise.IsActive);
    }

    [Fact]
    public async Task UnauthenticatedUser_CannotApproveExercise()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Generate an exercise ID
        var exerciseId = Guid.NewGuid();

        // STEP 2: Attempt approval without authentication
        // No JWT token is provided
        var response = await client.PutAsync(
            $"/api/exercises/{exerciseId}/approve",
            null);

        // STEP 3: Verify authentication is required
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Member_CannotApproveExercise()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Member
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

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        // STEP 2: Assign Member JWT token
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 3: Attempt exercise approval
        var exerciseId = Guid.NewGuid();

        var approveResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/approve",
            null);

        // STEP 4: Verify Member is forbidden
        Assert.Equal(
            HttpStatusCode.Forbidden,
            approveResponse.StatusCode);
    }

    [Fact]
    public async Task Member_CannotAccessExerciseReviewWorkflowEndpoints()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Member
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

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        // STEP 2: Assign Member JWT token
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 3: Generate an exercise ID
        var exerciseId = Guid.NewGuid();

        // STEP 4: Attempt individual review submission
        var submitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseId}/submit-review",
            null);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            submitResponse.StatusCode);

        // STEP 5: Attempt exercise retirement
        var retireResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/retire",
            null);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            retireResponse.StatusCode);

        // STEP 6: Prepare bulk request
        var bulkRequest = new BulkExerciseReviewRequest
        {
            ExerciseIds = new List<Guid>
        {
            exerciseId,
            Guid.NewGuid()
        }
        };

        // STEP 7: Attempt bulk review submission
        var bulkSubmitResponse = await client.PutAsJsonAsync(
            "/api/exercises/bulk/submit-review",
            bulkRequest);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            bulkSubmitResponse.StatusCode);

        // STEP 8: Attempt bulk approval
        var bulkApproveResponse = await client.PutAsJsonAsync(
            "/api/exercises/bulk/approve",
            bulkRequest);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            bulkApproveResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_CanUpdateDraftExercise()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create Draft exercise
        var exerciseCode = $"UPDATE-{Guid.NewGuid():N}";

        var createRequest = new SaveExerciseRequest
        {
            Code = exerciseCode,
            Name = "Original Chest Press",
            Description = "Original exercise description",
            MuscleGroup = "Chest",
            Equipment = "Machine",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform the original exercise."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            createRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 3: Update Draft exercise
        var updateRequest = new SaveExerciseRequest
        {
            Code = exerciseCode,
            Name = "Updated Chest Press",
            Description = "Updated exercise description",
            MuscleGroup = "Chest",
            Equipment = "Dumbbell",
            DifficultyLevel = "Intermediate",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 12 repetitions."
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/exercises/{exerciseId}",
            updateRequest);

        // STEP 4: Verify response
        Assert.Equal(
            HttpStatusCode.OK,
            updateResponse.StatusCode);

        using var updateDocument = JsonDocument.Parse(
            await updateResponse.Content.ReadAsStringAsync());

        var updatedExercise = updateDocument.RootElement;

        Assert.Equal(
            exerciseId,
            updatedExercise.GetProperty("id").GetGuid());

        Assert.Equal(
            "Updated Chest Press",
            updatedExercise.GetProperty("name").GetString());

        Assert.Equal(
            "Dumbbell",
            updatedExercise.GetProperty("equipment").GetString());

        Assert.Equal(
            "Intermediate",
            updatedExercise.GetProperty("difficultyLevel").GetString());

        Assert.Equal(
            "Draft",
            updatedExercise.GetProperty("reviewStatus").GetString());

        // STEP 5: Retrieve exercise to verify persistence
        var getResponse = await client.GetAsync(
            $"/api/exercises/{exerciseId}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        using var getDocument = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "Updated Chest Press",
            getDocument.RootElement.GetProperty("name").GetString());

        Assert.Equal(
            "Dumbbell",
            getDocument.RootElement.GetProperty("equipment").GetString());
    }

    [Fact]
    public async Task Admin_CannotUpdateExerciseInReview()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create a Draft exercise
        var exerciseCode = $"REVIEW-UPDATE-{Guid.NewGuid():N}";

        var createRequest = new SaveExerciseRequest
        {
            Code = exerciseCode,
            Name = "Original Chest Press",
            Description = "Original exercise description",
            MuscleGroup = "Chest",
            Equipment = "Machine",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform the original exercise."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            createRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 3: Submit exercise for review
        var submitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseId}/submit-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // STEP 4: Attempt to update an In Review exercise
        var updateRequest = new SaveExerciseRequest
        {
            Code = exerciseCode,
            Name = "Modified Chest Press",
            Description = "Modified exercise description",
            MuscleGroup = "Chest",
            Equipment = "Dumbbell",
            DifficultyLevel = "Intermediate",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 12 repetitions."
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/exercises/{exerciseId}",
            updateRequest);

        // STEP 5: Verify update is rejected
        Assert.Equal(
            HttpStatusCode.Conflict,
            updateResponse.StatusCode);

        // STEP 6: Retrieve exercise to verify no changes
        var getResponse = await client.GetAsync(
            $"/api/exercises/{exerciseId}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        using var getDocument = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync());

        var exercise = getDocument.RootElement;

        Assert.Equal(
            "Original Chest Press",
            exercise.GetProperty("name").GetString());

        Assert.Equal(
            "Machine",
            exercise.GetProperty("equipment").GetString());

        Assert.Equal(
            "In Review",
            exercise.GetProperty("reviewStatus").GetString());
    }

    [Fact]
    public async Task Admin_CannotUpdateApprovedExercise()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create Draft exercise
        var exerciseCode = $"APPROVED-UPDATE-{Guid.NewGuid():N}";

        var createRequest = new SaveExerciseRequest
        {
            Code = exerciseCode,
            Name = "Original Shoulder Press",
            Description = "Original shoulder exercise",
            MuscleGroup = "Shoulders",
            Equipment = "Machine",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform the original exercise."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            createRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 3: Submit exercise for review
        var submitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseId}/submit-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // STEP 4: Approve exercise
        var approveResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/approve",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            approveResponse.StatusCode);

        // STEP 5: Attempt to update Approved exercise
        var updateRequest = new SaveExerciseRequest
        {
            Code = exerciseCode,
            Name = "Modified Shoulder Press",
            Description = "Modified shoulder exercise",
            MuscleGroup = "Shoulders",
            Equipment = "Dumbbell",
            DifficultyLevel = "Intermediate",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 12 repetitions."
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/exercises/{exerciseId}",
            updateRequest);

        // STEP 6: Verify update is rejected
        Assert.Equal(
            HttpStatusCode.Conflict,
            updateResponse.StatusCode);

        // STEP 7: Retrieve exercise
        var getResponse = await client.GetAsync(
            $"/api/exercises/{exerciseId}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        using var getDocument = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync());

        var exercise = getDocument.RootElement;

        // STEP 8: Verify original information remains unchanged
        Assert.Equal(
            "Original Shoulder Press",
            exercise.GetProperty("name").GetString());

        Assert.Equal(
            "Machine",
            exercise.GetProperty("equipment").GetString());

        Assert.Equal(
            "Approved",
            exercise.GetProperty("reviewStatus").GetString());

        Assert.True(
            exercise.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Admin_CannotUpdateRetiredExercise()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Create Draft exercise
        var exerciseCode = $"RETIRED-UPDATE-{Guid.NewGuid():N}";

        var createRequest = new SaveExerciseRequest
        {
            Code = exerciseCode,
            Name = "Original Leg Press",
            Description = "Original leg exercise",
            MuscleGroup = "Legs",
            Equipment = "Machine",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform the original leg press."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            createRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 3: Submit exercise for review
        var submitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseId}/submit-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // STEP 4: Approve exercise
        var approveResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/approve",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            approveResponse.StatusCode);

        // STEP 5: Retire exercise
        var retireResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/retire",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            retireResponse.StatusCode);

        // STEP 6: Attempt to update Retired exercise
        var updateRequest = new SaveExerciseRequest
        {
            Code = exerciseCode,
            Name = "Modified Leg Press",
            Description = "Modified leg exercise",
            MuscleGroup = "Legs",
            Equipment = "Dumbbell",
            DifficultyLevel = "Intermediate",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 12 repetitions."
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/exercises/{exerciseId}",
            updateRequest);

        // STEP 7: Verify update is rejected
        Assert.Equal(
            HttpStatusCode.Conflict,
            updateResponse.StatusCode);

        // STEP 8: Retrieve exercise
        var getResponse = await client.GetAsync(
            $"/api/exercises/{exerciseId}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        using var getDocument = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync());

        var exercise = getDocument.RootElement;

        // STEP 9: Verify original details remain unchanged
        Assert.Equal(
            "Original Leg Press",
            exercise.GetProperty("name").GetString());

        Assert.Equal(
            "Machine",
            exercise.GetProperty("equipment").GetString());

        Assert.Equal(
            "Retired",
            exercise.GetProperty("reviewStatus").GetString());

        // Retired exercises must be inactive
        Assert.False(
            exercise.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Admin_CannotUpdateExerciseWithDuplicateCode()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Admin
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Generate unique exercise codes
        var firstCode = $"DUP-A-{Guid.NewGuid():N}";
        var secondCode = $"DUP-B-{Guid.NewGuid():N}";

        // STEP 3: Create first Draft exercise
        var firstRequest = new SaveExerciseRequest
        {
            Code = firstCode,
            Name = "First Chest Press",
            Description = "First exercise description",
            MuscleGroup = "Chest",
            Equipment = "Machine",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform the first exercise."
        };

        var firstResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            firstRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        using var firstDocument = JsonDocument.Parse(
            await firstResponse.Content.ReadAsStringAsync());

        var firstExerciseId = firstDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 4: Create second Draft exercise
        var secondRequest = new SaveExerciseRequest
        {
            Code = secondCode,
            Name = "Second Chest Press",
            Description = "Second exercise description",
            MuscleGroup = "Chest",
            Equipment = "Dumbbell",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform the second exercise."
        };

        var secondResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            secondRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            secondResponse.StatusCode);

        using var secondDocument = JsonDocument.Parse(
            await secondResponse.Content.ReadAsStringAsync());

        var secondExerciseId = secondDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 5: Attempt to assign the first exercise's code
        // to the second exercise
        var updateRequest = new SaveExerciseRequest
        {
            Code = firstCode,
            Name = "Modified Second Chest Press",
            Description = "Modified exercise description",
            MuscleGroup = "Chest",
            Equipment = "Dumbbell",
            DifficultyLevel = "Intermediate",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 12 repetitions."
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/exercises/{secondExerciseId}",
            updateRequest);

        // STEP 6: Verify duplicate code is rejected
        Assert.Equal(
            HttpStatusCode.Conflict,
            updateResponse.StatusCode);

        // STEP 7: Retrieve both exercises
        var getFirstResponse = await client.GetAsync(
            $"/api/exercises/{firstExerciseId}");

        var getSecondResponse = await client.GetAsync(
            $"/api/exercises/{secondExerciseId}");

        Assert.Equal(
            HttpStatusCode.OK,
            getFirstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            getSecondResponse.StatusCode);

        using var getFirstDocument = JsonDocument.Parse(
            await getFirstResponse.Content.ReadAsStringAsync());

        using var getSecondDocument = JsonDocument.Parse(
            await getSecondResponse.Content.ReadAsStringAsync());

        // STEP 8: Verify both exercise codes remain unchanged
        Assert.Equal(
            ExerciseValidator.NormalizeCode(firstCode),
            getFirstDocument.RootElement.GetProperty("code").GetString());

        Assert.Equal(
            ExerciseValidator.NormalizeCode(secondCode),
            getSecondDocument.RootElement.GetProperty("code").GetString());

        // STEP 9: Verify second exercise name was not modified
        Assert.Equal(
            "Second Chest Press",
            getSecondDocument.RootElement.GetProperty("name").GetString());

        Assert.Equal(
            "Draft",
            getSecondDocument.RootElement.GetProperty("reviewStatus").GetString());
    }

    [Fact]
    public async Task Admin_CannotUpdateNonexistentExercise()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Admin
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

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // STEP 2: Generate a nonexistent exercise ID
        var exerciseId = Guid.NewGuid();

        // STEP 3: Prepare a valid update request
        var updateRequest = new SaveExerciseRequest
        {
            Code = $"MISSING-{Guid.NewGuid():N}",
            Name = "Nonexistent Chest Press",
            Description = "Exercise does not exist",
            MuscleGroup = "Chest",
            Equipment = "Machine",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 12 repetitions."
        };

        // STEP 4: Attempt to update nonexistent exercise
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/exercises/{exerciseId}",
            updateRequest);

        // STEP 5: Verify HTTP 404 Not Found
        Assert.Equal(
            HttpStatusCode.NotFound,
            updateResponse.StatusCode);

        // STEP 6: Verify error message
        using var responseDocument = JsonDocument.Parse(
            await updateResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "Exercise not found.",
            responseDocument.RootElement
                .GetProperty("error")
                .GetString());

        // STEP 7: Verify exercise was not created
        var getResponse = await client.GetAsync(
            $"/api/exercises/{exerciseId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            getResponse.StatusCode);
    }

    [Fact]
    public async Task Member_CannotUpdateExercise()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var adminLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(
            HttpStatusCode.OK,
            adminLoginResponse.StatusCode);

        using var adminLoginDocument = JsonDocument.Parse(
            await adminLoginResponse.Content.ReadAsStringAsync());

        var adminAccessToken = adminLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(adminAccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminAccessToken);

        // STEP 2: Create a Draft exercise as Admin
        var exerciseCode = $"MEMBER-UPDATE-{Guid.NewGuid():N}";

        var createRequest = new SaveExerciseRequest
        {
            Code = exerciseCode,
            Name = "Original Bicep Curl",
            Description = "Original bicep exercise",
            MuscleGroup = "Biceps",
            Equipment = "Dumbbell",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 10 repetitions."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            createRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 3: Login as Member
        var memberLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(
            HttpStatusCode.OK,
            memberLoginResponse.StatusCode);

        using var memberLoginDocument = JsonDocument.Parse(
            await memberLoginResponse.Content.ReadAsStringAsync());

        var memberAccessToken = memberLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(memberAccessToken));

        // STEP 4: Switch authentication to Member
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                memberAccessToken);

        // STEP 5: Attempt to update exercise as Member
        var updateRequest = new SaveExerciseRequest
        {
            Code = exerciseCode,
            Name = "Modified Bicep Curl",
            Description = "Modified exercise description",
            MuscleGroup = "Biceps",
            Equipment = "Cable",
            DifficultyLevel = "Intermediate",
            TrainingLocation = "Gym",
            Instructions = "Perform 4 sets of 12 repetitions."
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/exercises/{exerciseId}",
            updateRequest);

        // STEP 6: Verify Member is forbidden
        Assert.Equal(
            HttpStatusCode.Forbidden,
            updateResponse.StatusCode);

        // STEP 7: Switch back to Admin
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminAccessToken);

        // STEP 8: Retrieve exercise as Admin
        var getResponse = await client.GetAsync(
            $"/api/exercises/{exerciseId}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        using var getDocument = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync());

        var exercise = getDocument.RootElement;

        // STEP 9: Verify exercise remains unchanged
        Assert.Equal(
            "Original Bicep Curl",
            exercise.GetProperty("name").GetString());

        Assert.Equal(
            "Dumbbell",
            exercise.GetProperty("equipment").GetString());

        Assert.Equal(
            "Draft",
            exercise.GetProperty("reviewStatus").GetString());

        Assert.True(
            exercise.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task UnauthenticatedUser_CannotUpdateExercise()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Prepare a valid exercise update request
        var exerciseId = Guid.NewGuid();

        var updateRequest = new SaveExerciseRequest
        {
            Code = $"UNAUTH-UPDATE-{Guid.NewGuid():N}",
            Name = "Unauthorized Chest Press",
            Description = "Unauthorized exercise update test",
            MuscleGroup = "Chest",
            Equipment = "Machine",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 12 repetitions."
        };

        // STEP 2: Attempt update without authentication
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/exercises/{exerciseId}",
            updateRequest);

        // STEP 3: Verify HTTP 401 Unauthorized
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            updateResponse.StatusCode);
    }

    [Fact]
    public async Task Member_CanRetrieveApprovedExercisesFromLibrary()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var adminLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(
            HttpStatusCode.OK,
            adminLoginResponse.StatusCode);

        using var adminLoginDocument = JsonDocument.Parse(
            await adminLoginResponse.Content.ReadAsStringAsync());

        var adminAccessToken = adminLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(adminAccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminAccessToken);

        // STEP 2: Create Draft exercise
        var exerciseCode = $"LIBRARY-{Guid.NewGuid():N}";

        var createRequest = new SaveExerciseRequest
        {
            Code = exerciseCode,
            Name = "Library Test Chest Press",
            Description = "Chest exercise for member library testing",
            MuscleGroup = "Chest",
            Equipment = "Machine",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 12 repetitions."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            createRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 3: Submit exercise for review
        var submitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseId}/submit-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // STEP 4: Approve exercise
        var approveResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/approve",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            approveResponse.StatusCode);

        // STEP 5: Login as Member
        var memberLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(
            HttpStatusCode.OK,
            memberLoginResponse.StatusCode);

        using var memberLoginDocument = JsonDocument.Parse(
            await memberLoginResponse.Content.ReadAsStringAsync());

        var memberAccessToken = memberLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(memberAccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                memberAccessToken);

        // STEP 6: Retrieve Member Exercise Library
        var libraryResponse = await client.GetAsync(
            "/api/exercise-library");

        Assert.Equal(
            HttpStatusCode.OK,
            libraryResponse.StatusCode);

        // STEP 7: Read exercise library response
        using var libraryDocument = JsonDocument.Parse(
            await libraryResponse.Content.ReadAsStringAsync());

        var exercises = libraryDocument.RootElement;

        Assert.Equal(
            JsonValueKind.Array,
            exercises.ValueKind);

        // STEP 8: Find the newly approved exercise
        var matchingExercises = exercises
            .EnumerateArray()
            .Where(x =>
                x.GetProperty("id").GetGuid() == exerciseId)
            .ToList();

        var exercise = Assert.Single(matchingExercises);

        // STEP 9: Verify exercise information
        Assert.Equal(
            exerciseCode.ToUpperInvariant(),
            exercise.GetProperty("code").GetString());

        Assert.Equal(
            "Library Test Chest Press",
            exercise.GetProperty("name").GetString());

        Assert.Equal(
            "Chest",
            exercise.GetProperty("muscleGroup").GetString());

        Assert.Equal(
            "Machine",
            exercise.GetProperty("equipment").GetString());

        Assert.Equal(
            "Approved",
            exercise.GetProperty("reviewStatus").GetString());

        Assert.True(
            exercise.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Member_CannotSeeDraftOrInReviewExercisesInLibrary()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var adminLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, adminLoginResponse.StatusCode);

        using var adminLoginDocument = JsonDocument.Parse(
            await adminLoginResponse.Content.ReadAsStringAsync());

        var adminAccessToken = adminLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(adminAccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminAccessToken);

        // STEP 2: Create a Draft exercise
        var draftCode = $"LIB-DRAFT-{Guid.NewGuid():N}";

        var draftRequest = new SaveExerciseRequest
        {
            Code = draftCode,
            Name = "Library Draft Exercise",
            Description = "Exercise that should remain hidden",
            MuscleGroup = "Chest",
            Equipment = "Machine",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 12 repetitions."
        };

        var draftResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            draftRequest);

        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);

        using var draftDocument = JsonDocument.Parse(
            await draftResponse.Content.ReadAsStringAsync());

        var draftExerciseId = draftDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 3: Create another Draft exercise
        var reviewCode = $"LIB-REVIEW-{Guid.NewGuid():N}";

        var reviewRequest = new SaveExerciseRequest
        {
            Code = reviewCode,
            Name = "Library In Review Exercise",
            Description = "Exercise awaiting approval",
            MuscleGroup = "Back",
            Equipment = "Cable",
            DifficultyLevel = "Intermediate",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 10 repetitions."
        };

        var reviewResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            reviewRequest);

        Assert.Equal(HttpStatusCode.Created, reviewResponse.StatusCode);

        using var reviewDocument = JsonDocument.Parse(
            await reviewResponse.Content.ReadAsStringAsync());

        var reviewExerciseId = reviewDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 4: Submit second exercise for review
        var submitResponse = await client.PostAsync(
            $"/api/exercises/{reviewExerciseId}/submit-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // STEP 5: Login as Member
        var memberLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, memberLoginResponse.StatusCode);

        using var memberLoginDocument = JsonDocument.Parse(
            await memberLoginResponse.Content.ReadAsStringAsync());

        var memberAccessToken = memberLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(memberAccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", memberAccessToken);

        // STEP 6: Retrieve Member Exercise Library
        var libraryResponse = await client.GetAsync(
            "/api/exercise-library");

        Assert.Equal(HttpStatusCode.OK, libraryResponse.StatusCode);

        using var libraryDocument = JsonDocument.Parse(
            await libraryResponse.Content.ReadAsStringAsync());

        var exercises = libraryDocument.RootElement;

        Assert.Equal(JsonValueKind.Array, exercises.ValueKind);

        // STEP 7: Verify Draft exercise is hidden
        Assert.DoesNotContain(
            exercises.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == draftExerciseId);

        // STEP 8: Verify In Review exercise is hidden
        Assert.DoesNotContain(
            exercises.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == reviewExerciseId);

        // STEP 9: Verify every returned exercise is approved and active
        foreach (var exercise in exercises.EnumerateArray())
        {
            Assert.Equal(
                "Approved",
                exercise.GetProperty("reviewStatus").GetString());

            Assert.True(
                exercise.GetProperty("isActive").GetBoolean());
        }
    }

    [Fact]
    public async Task Member_CannotSeeRetiredExercisesInLibrary()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var adminLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, adminLoginResponse.StatusCode);

        using var adminLoginDocument = JsonDocument.Parse(
            await adminLoginResponse.Content.ReadAsStringAsync());

        var adminAccessToken = adminLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(adminAccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminAccessToken);

        // STEP 2: Create Draft exercise
        var exerciseCode = $"LIB-RETIRED-{Guid.NewGuid():N}";

        var createRequest = new SaveExerciseRequest
        {
            Code = exerciseCode,
            Name = "Library Retired Leg Press",
            Description = "Exercise used to verify retirement visibility",
            MuscleGroup = "Legs",
            Equipment = "Machine",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 12 repetitions."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            createRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 3: Submit exercise for review
        var submitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseId}/submit-review",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            submitResponse.StatusCode);

        // STEP 4: Approve exercise
        var approveResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/approve",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            approveResponse.StatusCode);

        // STEP 5: Login as Member
        var memberLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(
            HttpStatusCode.OK,
            memberLoginResponse.StatusCode);

        using var memberLoginDocument = JsonDocument.Parse(
            await memberLoginResponse.Content.ReadAsStringAsync());

        var memberAccessToken = memberLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(memberAccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", memberAccessToken);

        // STEP 6: Verify Approved exercise is visible
        var approvedLibraryResponse = await client.GetAsync(
            "/api/exercise-library");

        Assert.Equal(
            HttpStatusCode.OK,
            approvedLibraryResponse.StatusCode);

        using var approvedLibraryDocument = JsonDocument.Parse(
            await approvedLibraryResponse.Content.ReadAsStringAsync());

        Assert.Contains(
            approvedLibraryDocument.RootElement.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == exerciseId);

        // STEP 7: Switch back to Admin
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminAccessToken);

        // STEP 8: Retire the exercise
        var retireResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/retire",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            retireResponse.StatusCode);

        // STEP 9: Switch back to Member
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", memberAccessToken);

        // STEP 10: Retrieve Member Exercise Library again
        var retiredLibraryResponse = await client.GetAsync(
            "/api/exercise-library");

        Assert.Equal(
            HttpStatusCode.OK,
            retiredLibraryResponse.StatusCode);

        using var retiredLibraryDocument = JsonDocument.Parse(
            await retiredLibraryResponse.Content.ReadAsStringAsync());

        // STEP 11: Verify Retired exercise is hidden
        Assert.DoesNotContain(
            retiredLibraryDocument.RootElement.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == exerciseId);

        // STEP 12: Verify all visible exercises are Approved and Active
        foreach (var exercise in
            retiredLibraryDocument.RootElement.EnumerateArray())
        {
            Assert.Equal(
                "Approved",
                exercise.GetProperty("reviewStatus").GetString());

            Assert.True(
                exercise.GetProperty("isActive").GetBoolean());
        }

        // STEP 13: Verify retired exercise remains in Admin records
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminAccessToken);

        var getResponse = await client.GetAsync(
            $"/api/exercises/{exerciseId}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        using var getDocument = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "Retired",
            getDocument.RootElement.GetProperty("reviewStatus").GetString());

        Assert.False(
            getDocument.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task UnauthenticatedUser_CannotAccessExerciseLibrary()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Ensure no authentication token is supplied
        client.DefaultRequestHeaders.Authorization = null;

        // STEP 2: Attempt to retrieve Exercise Library
        var response = await client.GetAsync(
            "/api/exercise-library");

        // STEP 3: Verify HTTP 401 Unauthorized
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Member_CanSearchApprovedExercisesByNameAndCode()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var adminLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, adminLoginResponse.StatusCode);

        using var adminLoginDocument = JsonDocument.Parse(
            await adminLoginResponse.Content.ReadAsStringAsync());

        var adminToken = adminLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(adminToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // STEP 2: Create a unique approved exercise
        var uniqueText = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var exerciseCode = $"SEARCH-{uniqueText}";
        var exerciseName = $"Searchable Chest Press {uniqueText}";

        var createRequest = new SaveExerciseRequest
        {
            Code = exerciseCode,
            Name = exerciseName,
            Description = "Exercise for library search testing",
            MuscleGroup = "Chest",
            Equipment = "Machine",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 12 repetitions."
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            createRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var exerciseId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 3: Submit and approve exercise
        var submitResponse = await client.PostAsync(
            $"/api/exercises/{exerciseId}/submit-review",
            null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        var approveResponse = await client.PutAsync(
            $"/api/exercises/{exerciseId}/approve",
            null);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // STEP 4: Create another exercise that remains Draft
        var draftCode = $"DRAFT-{uniqueText}";

        var draftRequest = new SaveExerciseRequest
        {
            Code = draftCode,
            Name = $"Draft Chest Press {uniqueText}",
            Description = "Draft exercise must remain hidden",
            MuscleGroup = "Chest",
            Equipment = "Machine",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 10 repetitions."
        };

        var draftResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            draftRequest);

        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);

        using var draftDocument = JsonDocument.Parse(
            await draftResponse.Content.ReadAsStringAsync());

        var draftExerciseId = draftDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 5: Login as Member
        var memberLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, memberLoginResponse.StatusCode);

        using var memberLoginDocument = JsonDocument.Parse(
            await memberLoginResponse.Content.ReadAsStringAsync());

        var memberToken = memberLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(memberToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", memberToken);

        // STEP 6: Search by exercise name
        var nameSearch = Uri.EscapeDataString(exerciseName);

        var nameResponse = await client.GetAsync(
            $"/api/exercise-library?search={nameSearch}");

        Assert.Equal(HttpStatusCode.OK, nameResponse.StatusCode);

        using var nameDocument = JsonDocument.Parse(
            await nameResponse.Content.ReadAsStringAsync());

        var nameResults = nameDocument.RootElement;

        Assert.Equal(JsonValueKind.Array, nameResults.ValueKind);

        Assert.Contains(
            nameResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == exerciseId);

        // STEP 7: Search by exercise code
        var codeResponse = await client.GetAsync(
            $"/api/exercise-library?search={exerciseCode}");

        Assert.Equal(HttpStatusCode.OK, codeResponse.StatusCode);

        using var codeDocument = JsonDocument.Parse(
            await codeResponse.Content.ReadAsStringAsync());

        var codeResults = codeDocument.RootElement;

        Assert.Equal(JsonValueKind.Array, codeResults.ValueKind);

        Assert.Contains(
            codeResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == exerciseId);

        // STEP 8: Search using shared text
        // Draft exercise must not appear
        var sharedResponse = await client.GetAsync(
            $"/api/exercise-library?search={uniqueText}");

        Assert.Equal(HttpStatusCode.OK, sharedResponse.StatusCode);

        using var sharedDocument = JsonDocument.Parse(
            await sharedResponse.Content.ReadAsStringAsync());

        var sharedResults = sharedDocument.RootElement;

        Assert.Contains(
            sharedResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == exerciseId);

        Assert.DoesNotContain(
            sharedResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == draftExerciseId);

        // STEP 9: Verify all returned exercises are Approved and Active
        foreach (var exercise in sharedResults.EnumerateArray())
        {
            Assert.Equal(
                "Approved",
                exercise.GetProperty("reviewStatus").GetString());

            Assert.True(
                exercise.GetProperty("isActive").GetBoolean());
        }

        // STEP 10: Search for nonexistent exercise
        var missingSearch = Guid.NewGuid().ToString("N");

        var missingResponse = await client.GetAsync(
            $"/api/exercise-library?search={missingSearch}");

        Assert.Equal(HttpStatusCode.OK, missingResponse.StatusCode);

        using var missingDocument = JsonDocument.Parse(
            await missingResponse.Content.ReadAsStringAsync());

        Assert.Empty(missingDocument.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Member_CanFilterApprovedExercisesByMuscleGroup()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var adminLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, adminLoginResponse.StatusCode);

        using var adminLoginDocument = JsonDocument.Parse(
            await adminLoginResponse.Content.ReadAsStringAsync());

        var adminToken = adminLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(adminToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // STEP 2: Generate unique exercise identifiers
        var uniqueText = Guid.NewGuid().ToString("N").ToUpperInvariant();

        var chestCode = $"FILTER-CHEST-{uniqueText}";
        var backCode = $"FILTER-BACK-{uniqueText}";
        var draftCode = $"FILTER-DRAFT-{uniqueText}";

        // STEP 3: Create Chest exercise
        var chestRequest = new SaveExerciseRequest
        {
            Code = chestCode,
            Name = $"Chest Press {uniqueText}",
            Description = "Approved chest exercise",
            MuscleGroup = "Chest",
            Equipment = "Machine",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 12 repetitions."
        };

        var chestCreateResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            chestRequest);

        Assert.Equal(HttpStatusCode.Created, chestCreateResponse.StatusCode);

        using var chestDocument = JsonDocument.Parse(
            await chestCreateResponse.Content.ReadAsStringAsync());

        var chestExerciseId = chestDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 4: Approve Chest exercise
        var chestSubmitResponse = await client.PostAsync(
            $"/api/exercises/{chestExerciseId}/submit-review",
            null);

        Assert.Equal(HttpStatusCode.OK, chestSubmitResponse.StatusCode);

        var chestApproveResponse = await client.PutAsync(
            $"/api/exercises/{chestExerciseId}/approve",
            null);

        Assert.Equal(HttpStatusCode.OK, chestApproveResponse.StatusCode);

        // STEP 5: Create Back exercise
        var backRequest = new SaveExerciseRequest
        {
            Code = backCode,
            Name = $"Back Row {uniqueText}",
            Description = "Approved back exercise",
            MuscleGroup = "Back",
            Equipment = "Cable",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 12 repetitions."
        };

        var backCreateResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            backRequest);

        Assert.Equal(HttpStatusCode.Created, backCreateResponse.StatusCode);

        using var backDocument = JsonDocument.Parse(
            await backCreateResponse.Content.ReadAsStringAsync());

        var backExerciseId = backDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 6: Approve Back exercise
        var backSubmitResponse = await client.PostAsync(
            $"/api/exercises/{backExerciseId}/submit-review",
            null);

        Assert.Equal(HttpStatusCode.OK, backSubmitResponse.StatusCode);

        var backApproveResponse = await client.PutAsync(
            $"/api/exercises/{backExerciseId}/approve",
            null);

        Assert.Equal(HttpStatusCode.OK, backApproveResponse.StatusCode);

        // STEP 7: Create another Chest exercise that remains Draft
        var draftRequest = new SaveExerciseRequest
        {
            Code = draftCode,
            Name = $"Draft Chest Press {uniqueText}",
            Description = "Draft exercise must remain hidden",
            MuscleGroup = "Chest",
            Equipment = "Dumbbell",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 10 repetitions."
        };

        var draftCreateResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            draftRequest);

        Assert.Equal(HttpStatusCode.Created, draftCreateResponse.StatusCode);

        using var draftDocument = JsonDocument.Parse(
            await draftCreateResponse.Content.ReadAsStringAsync());

        var draftExerciseId = draftDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 8: Login as Member
        var memberLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, memberLoginResponse.StatusCode);

        using var memberLoginDocument = JsonDocument.Parse(
            await memberLoginResponse.Content.ReadAsStringAsync());

        var memberToken = memberLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(memberToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", memberToken);

        // STEP 9: Filter exercises by Chest
        var chestFilterResponse = await client.GetAsync(
            "/api/exercise-library?muscleGroup=Chest");

        Assert.Equal(HttpStatusCode.OK, chestFilterResponse.StatusCode);

        using var chestFilterDocument = JsonDocument.Parse(
            await chestFilterResponse.Content.ReadAsStringAsync());

        var chestResults = chestFilterDocument.RootElement;

        Assert.Equal(JsonValueKind.Array, chestResults.ValueKind);

        // STEP 10: Verify approved Chest exercise is included
        Assert.Contains(
            chestResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == chestExerciseId);

        // STEP 11: Verify Back exercise is excluded
        Assert.DoesNotContain(
            chestResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == backExerciseId);

        // STEP 12: Verify Draft Chest exercise is excluded
        Assert.DoesNotContain(
            chestResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == draftExerciseId);

        // STEP 13: Verify every returned exercise is Chest, Approved, Active
        foreach (var exercise in chestResults.EnumerateArray())
        {
            Assert.Equal(
                "Chest",
                exercise.GetProperty("muscleGroup").GetString());

            Assert.Equal(
                "Approved",
                exercise.GetProperty("reviewStatus").GetString());

            Assert.True(
                exercise.GetProperty("isActive").GetBoolean());
        }

        // STEP 14: Verify combined search and muscle group filter
        var combinedResponse = await client.GetAsync(
            $"/api/exercise-library?muscleGroup=Chest&search={uniqueText}");

        Assert.Equal(HttpStatusCode.OK, combinedResponse.StatusCode);

        using var combinedDocument = JsonDocument.Parse(
            await combinedResponse.Content.ReadAsStringAsync());

        var combinedResults = combinedDocument.RootElement;

        Assert.Contains(
            combinedResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == chestExerciseId);

        Assert.DoesNotContain(
            combinedResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == backExerciseId);

        Assert.DoesNotContain(
            combinedResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == draftExerciseId);

        // STEP 15: Verify Back filter returns Back exercise
        var backFilterResponse = await client.GetAsync(
            "/api/exercise-library?muscleGroup=Back");

        Assert.Equal(HttpStatusCode.OK, backFilterResponse.StatusCode);

        using var backFilterDocument = JsonDocument.Parse(
            await backFilterResponse.Content.ReadAsStringAsync());

        Assert.Contains(
            backFilterDocument.RootElement.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == backExerciseId);
    }

    [Fact]
    public async Task Member_CanFilterApprovedExercisesByEquipment()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Admin
        var adminLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, adminLoginResponse.StatusCode);

        using var adminLoginDocument = JsonDocument.Parse(
            await adminLoginResponse.Content.ReadAsStringAsync());

        var adminToken = adminLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(adminToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // STEP 2: Generate unique identifiers
        var uniqueText = Guid.NewGuid().ToString("N").ToUpperInvariant();

        var dumbbellCode = $"EQUIP-DB-{uniqueText}";
        var machineCode = $"EQUIP-MC-{uniqueText}";
        var draftCode = $"EQUIP-DR-{uniqueText}";

        // STEP 3: Create an approved Dumbbell exercise
        var dumbbellRequest = new SaveExerciseRequest
        {
            Code = dumbbellCode,
            Name = $"Dumbbell Chest Press {uniqueText}",
            Description = "Approved dumbbell exercise",
            MuscleGroup = "Chest",
            Equipment = "Dumbbell",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 12 repetitions."
        };

        var dumbbellCreateResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            dumbbellRequest);

        Assert.Equal(HttpStatusCode.Created, dumbbellCreateResponse.StatusCode);

        using var dumbbellDocument = JsonDocument.Parse(
            await dumbbellCreateResponse.Content.ReadAsStringAsync());

        var dumbbellId = dumbbellDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        var dumbbellSubmitResponse = await client.PostAsync(
            $"/api/exercises/{dumbbellId}/submit-review",
            null);

        Assert.Equal(HttpStatusCode.OK, dumbbellSubmitResponse.StatusCode);

        var dumbbellApproveResponse = await client.PutAsync(
            $"/api/exercises/{dumbbellId}/approve",
            null);

        Assert.Equal(HttpStatusCode.OK, dumbbellApproveResponse.StatusCode);

        // STEP 4: Create an approved Machine exercise
        var machineRequest = new SaveExerciseRequest
        {
            Code = machineCode,
            Name = $"Machine Chest Press {uniqueText}",
            Description = "Approved machine exercise",
            MuscleGroup = "Chest",
            Equipment = "Machine",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 10 repetitions."
        };

        var machineCreateResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            machineRequest);

        Assert.Equal(HttpStatusCode.Created, machineCreateResponse.StatusCode);

        using var machineDocument = JsonDocument.Parse(
            await machineCreateResponse.Content.ReadAsStringAsync());

        var machineId = machineDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        var machineSubmitResponse = await client.PostAsync(
            $"/api/exercises/{machineId}/submit-review",
            null);

        Assert.Equal(HttpStatusCode.OK, machineSubmitResponse.StatusCode);

        var machineApproveResponse = await client.PutAsync(
            $"/api/exercises/{machineId}/approve",
            null);

        Assert.Equal(HttpStatusCode.OK, machineApproveResponse.StatusCode);

        // STEP 5: Create a Draft Dumbbell exercise
        var draftRequest = new SaveExerciseRequest
        {
            Code = draftCode,
            Name = $"Draft Dumbbell Chest Press {uniqueText}",
            Description = "Draft exercise must remain hidden",
            MuscleGroup = "Chest",
            Equipment = "Dumbbell",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Gym",
            Instructions = "Perform 3 sets of 8 repetitions."
        };

        var draftCreateResponse = await client.PostAsJsonAsync(
            "/api/exercises",
            draftRequest);

        Assert.Equal(HttpStatusCode.Created, draftCreateResponse.StatusCode);

        using var draftDocument = JsonDocument.Parse(
            await draftCreateResponse.Content.ReadAsStringAsync());

        var draftId = draftDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // STEP 6: Authenticate as Member
        var memberLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, memberLoginResponse.StatusCode);

        using var memberLoginDocument = JsonDocument.Parse(
            await memberLoginResponse.Content.ReadAsStringAsync());

        var memberToken = memberLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(memberToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", memberToken);

        // STEP 7: Filter by Dumbbell
        var dumbbellResponse = await client.GetAsync(
            "/api/exercise-library?equipment=Dumbbell");

        Assert.Equal(HttpStatusCode.OK, dumbbellResponse.StatusCode);

        using var dumbbellResultsDocument = JsonDocument.Parse(
            await dumbbellResponse.Content.ReadAsStringAsync());

        var dumbbellResults = dumbbellResultsDocument.RootElement;

        Assert.Equal(JsonValueKind.Array, dumbbellResults.ValueKind);

        Assert.Contains(
            dumbbellResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == dumbbellId);

        Assert.DoesNotContain(
            dumbbellResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == machineId);

        Assert.DoesNotContain(
            dumbbellResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == draftId);

        // STEP 8: Verify all Dumbbell results are Approved and Active
        foreach (var exercise in dumbbellResults.EnumerateArray())
        {
            Assert.Equal(
                "Dumbbell",
                exercise.GetProperty("equipment").GetString());

            Assert.Equal(
                "Approved",
                exercise.GetProperty("reviewStatus").GetString());

            Assert.True(
                exercise.GetProperty("isActive").GetBoolean());
        }

        // STEP 9: Filter by Machine
        var machineResponse = await client.GetAsync(
            "/api/exercise-library?equipment=Machine");

        Assert.Equal(HttpStatusCode.OK, machineResponse.StatusCode);

        using var machineResultsDocument = JsonDocument.Parse(
            await machineResponse.Content.ReadAsStringAsync());

        var machineResults = machineResultsDocument.RootElement;

        Assert.Contains(
            machineResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == machineId);

        Assert.DoesNotContain(
            machineResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == dumbbellId);

        // STEP 10: Combine Muscle Group and Equipment
        var combinedResponse = await client.GetAsync(
            "/api/exercise-library?muscleGroup=Chest&equipment=Dumbbell");

        Assert.Equal(HttpStatusCode.OK, combinedResponse.StatusCode);

        using var combinedDocument = JsonDocument.Parse(
            await combinedResponse.Content.ReadAsStringAsync());

        var combinedResults = combinedDocument.RootElement;

        Assert.Contains(
            combinedResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == dumbbellId);

        Assert.DoesNotContain(
            combinedResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == machineId);

        Assert.DoesNotContain(
            combinedResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == draftId);

        // STEP 11: Combine Search, Muscle Group and Equipment
        var searchResponse = await client.GetAsync(
            $"/api/exercise-library?search={uniqueText}&muscleGroup=Chest&equipment=Dumbbell");

        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        using var searchDocument = JsonDocument.Parse(
            await searchResponse.Content.ReadAsStringAsync());

        var searchResults = searchDocument.RootElement;

        Assert.Contains(
            searchResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == dumbbellId);

        Assert.DoesNotContain(
            searchResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == machineId);

        Assert.DoesNotContain(
            searchResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == draftId);

        // STEP 12: Verify a nonexistent equipment value returns no results
        var missingEquipment = $"UNKNOWN-{Guid.NewGuid():N}";

        var missingResponse = await client.GetAsync(
            $"/api/exercise-library?equipment={missingEquipment}");

        Assert.Equal(HttpStatusCode.OK, missingResponse.StatusCode);

        using var missingDocument = JsonDocument.Parse(
            await missingResponse.Content.ReadAsStringAsync());

        Assert.Empty(missingDocument.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Member_CanFilterApprovedExercisesByDifficultyLevel()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var adminLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, adminLoginResponse.StatusCode);

        using var adminLoginDocument = JsonDocument.Parse(
            await adminLoginResponse.Content.ReadAsStringAsync());

        var adminToken = adminLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(adminToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // STEP 2: Generate unique identifiers
        var uniqueText = Guid.NewGuid().ToString("N").ToUpperInvariant();

        // STEP 3: Create exercises with different difficulty levels
        async Task<Guid> CreateExerciseAsync(
            string codePrefix,
            string namePrefix,
            string difficultyLevel,
            bool approve)
        {
            var request = new SaveExerciseRequest
            {
                Code = $"{codePrefix}-{uniqueText}",
                Name = $"{namePrefix} {uniqueText}",
                Description = $"Exercise for {difficultyLevel} filtering test",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = difficultyLevel,
                TrainingLocation = "Gym",
                Instructions = "Perform 3 sets of 12 repetitions."
            };

            var createResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                request);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            using var createDocument = JsonDocument.Parse(
                await createResponse.Content.ReadAsStringAsync());

            var exerciseId = createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

            if (approve)
            {
                var submitResponse = await client.PostAsync(
                    $"/api/exercises/{exerciseId}/submit-review",
                    null);

                Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

                var approveResponse = await client.PutAsync(
                    $"/api/exercises/{exerciseId}/approve",
                    null);

                Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
            }

            return exerciseId;
        }

        var beginnerId = await CreateExerciseAsync(
            "DIFF-BEGINNER",
            "Beginner Chest Press",
            "Beginner",
            true);

        var intermediateId = await CreateExerciseAsync(
            "DIFF-INTERMEDIATE",
            "Intermediate Chest Press",
            "Intermediate",
            true);

        var advancedId = await CreateExerciseAsync(
            "DIFF-ADVANCED",
            "Advanced Chest Press",
            "Advanced",
            true);

        var draftId = await CreateExerciseAsync(
            "DIFF-DRAFT",
            "Draft Beginner Chest Press",
            "Beginner",
            false);

        // STEP 4: Login as Member
        var memberLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, memberLoginResponse.StatusCode);

        using var memberLoginDocument = JsonDocument.Parse(
            await memberLoginResponse.Content.ReadAsStringAsync());

        var memberToken = memberLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(memberToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", memberToken);

        // STEP 5: Filter Beginner exercises
        var beginnerResponse = await client.GetAsync(
            "/api/exercise-library?difficultyLevel=Beginner");

        Assert.Equal(HttpStatusCode.OK, beginnerResponse.StatusCode);

        using var beginnerDocument = JsonDocument.Parse(
            await beginnerResponse.Content.ReadAsStringAsync());

        var beginnerResults = beginnerDocument.RootElement;

        Assert.Equal(JsonValueKind.Array, beginnerResults.ValueKind);

        Assert.Contains(
            beginnerResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == beginnerId);

        Assert.DoesNotContain(
            beginnerResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == intermediateId);

        Assert.DoesNotContain(
            beginnerResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == advancedId);

        Assert.DoesNotContain(
            beginnerResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == draftId);

        // STEP 6: Verify every Beginner result is Approved and Active
        foreach (var exercise in beginnerResults.EnumerateArray())
        {
            Assert.Equal(
                "Beginner",
                exercise.GetProperty("difficultyLevel").GetString());

            Assert.Equal(
                "Approved",
                exercise.GetProperty("reviewStatus").GetString());

            Assert.True(
                exercise.GetProperty("isActive").GetBoolean());
        }

        // STEP 7: Filter Intermediate exercises
        var intermediateResponse = await client.GetAsync(
            "/api/exercise-library?difficultyLevel=Intermediate");

        Assert.Equal(HttpStatusCode.OK, intermediateResponse.StatusCode);

        using var intermediateDocument = JsonDocument.Parse(
            await intermediateResponse.Content.ReadAsStringAsync());

        Assert.Contains(
            intermediateDocument.RootElement.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == intermediateId);

        Assert.DoesNotContain(
            intermediateDocument.RootElement.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == beginnerId);

        // STEP 8: Filter Advanced exercises
        var advancedResponse = await client.GetAsync(
            "/api/exercise-library?difficultyLevel=Advanced");

        Assert.Equal(HttpStatusCode.OK, advancedResponse.StatusCode);

        using var advancedDocument = JsonDocument.Parse(
            await advancedResponse.Content.ReadAsStringAsync());

        Assert.Contains(
            advancedDocument.RootElement.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == advancedId);

        Assert.DoesNotContain(
            advancedDocument.RootElement.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == beginnerId);

        // STEP 9: Combine all four filters
        var combinedResponse = await client.GetAsync(
            $"/api/exercise-library?search={uniqueText}&muscleGroup=Chest&equipment=Dumbbell&difficultyLevel=Beginner");

        Assert.Equal(HttpStatusCode.OK, combinedResponse.StatusCode);

        using var combinedDocument = JsonDocument.Parse(
            await combinedResponse.Content.ReadAsStringAsync());

        var combinedResults = combinedDocument.RootElement;

        Assert.Equal(JsonValueKind.Array, combinedResults.ValueKind);

        Assert.Single(combinedResults.EnumerateArray());

        Assert.Equal(
            beginnerId,
            combinedResults[0].GetProperty("id").GetGuid());

        // STEP 10: Verify a nonexistent difficulty level returns no results
        var missingDifficulty = $"UNKNOWN-{Guid.NewGuid():N}";

        var missingResponse = await client.GetAsync(
            $"/api/exercise-library?difficultyLevel={missingDifficulty}");

        Assert.Equal(HttpStatusCode.OK, missingResponse.StatusCode);

        using var missingDocument = JsonDocument.Parse(
            await missingResponse.Content.ReadAsStringAsync());

        Assert.Empty(missingDocument.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Member_CanFilterApprovedExercisesByTrainingLocation()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var adminLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, adminLoginResponse.StatusCode);

        using var adminLoginDocument = JsonDocument.Parse(
            await adminLoginResponse.Content.ReadAsStringAsync());

        var adminToken = adminLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(adminToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // STEP 2: Generate unique identifiers
        var uniqueText = Guid.NewGuid().ToString("N").ToUpperInvariant();

        // STEP 3: Helper to create exercises
        async Task<Guid> CreateExerciseAsync(
            string codePrefix,
            string namePrefix,
            string trainingLocation,
            bool approve)
        {
            var request = new SaveExerciseRequest
            {
                Code = $"{codePrefix}-{uniqueText}",
                Name = $"{namePrefix} {uniqueText}",
                Description = $"Exercise for {trainingLocation} filtering test",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = trainingLocation,
                Instructions = "Perform 3 sets of 12 repetitions."
            };

            var createResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                request);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            using var createDocument = JsonDocument.Parse(
                await createResponse.Content.ReadAsStringAsync());

            var exerciseId = createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

            if (approve)
            {
                var submitResponse = await client.PostAsync(
                    $"/api/exercises/{exerciseId}/submit-review",
                    null);

                Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

                var approveResponse = await client.PutAsync(
                    $"/api/exercises/{exerciseId}/approve",
                    null);

                Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
            }

            return exerciseId;
        }

        // STEP 4: Create approved Gym exercise
        var gymId = await CreateExerciseAsync(
            "LOCATION-GYM",
            "Gym Chest Press",
            "Gym",
            true);

        // STEP 5: Create approved Home exercise
        var homeId = await CreateExerciseAsync(
            "LOCATION-HOME",
            "Home Chest Press",
            "Home",
            true);

        // STEP 6: Create Draft Home exercise
        var draftId = await CreateExerciseAsync(
            "LOCATION-DRAFT",
            "Draft Home Chest Press",
            "Home",
            false);

        // STEP 7: Login as Member
        var memberLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, memberLoginResponse.StatusCode);

        using var memberLoginDocument = JsonDocument.Parse(
            await memberLoginResponse.Content.ReadAsStringAsync());

        var memberToken = memberLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(memberToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", memberToken);

        // STEP 8: Filter Gym exercises
        var gymResponse = await client.GetAsync(
            "/api/exercise-library?trainingLocation=Gym");

        Assert.Equal(HttpStatusCode.OK, gymResponse.StatusCode);

        using var gymDocument = JsonDocument.Parse(
            await gymResponse.Content.ReadAsStringAsync());

        var gymResults = gymDocument.RootElement;

        Assert.Equal(JsonValueKind.Array, gymResults.ValueKind);

        Assert.Contains(
            gymResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == gymId);

        Assert.DoesNotContain(
            gymResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == homeId);

        Assert.DoesNotContain(
            gymResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == draftId);

        // STEP 9: Verify Gym results are Approved and Active
        foreach (var exercise in gymResults.EnumerateArray())
        {
            Assert.Equal(
                "Gym",
                exercise.GetProperty("trainingLocation").GetString());

            Assert.Equal(
                "Approved",
                exercise.GetProperty("reviewStatus").GetString());

            Assert.True(
                exercise.GetProperty("isActive").GetBoolean());
        }

        // STEP 10: Filter Home exercises
        var homeResponse = await client.GetAsync(
            "/api/exercise-library?trainingLocation=Home");

        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);

        using var homeDocument = JsonDocument.Parse(
            await homeResponse.Content.ReadAsStringAsync());

        var homeResults = homeDocument.RootElement;

        Assert.Equal(JsonValueKind.Array, homeResults.ValueKind);

        Assert.Contains(
            homeResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == homeId);

        Assert.DoesNotContain(
            homeResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == gymId);

        Assert.DoesNotContain(
            homeResults.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == draftId);

        // STEP 11: Verify Home results are Approved and Active
        foreach (var exercise in homeResults.EnumerateArray())
        {
            Assert.Equal(
                "Home",
                exercise.GetProperty("trainingLocation").GetString());

            Assert.Equal(
                "Approved",
                exercise.GetProperty("reviewStatus").GetString());

            Assert.True(
                exercise.GetProperty("isActive").GetBoolean());
        }

        // STEP 12: Combine all five filters
        var combinedResponse = await client.GetAsync(
            $"/api/exercise-library?search={uniqueText}&muscleGroup=Chest&equipment=Dumbbell&difficultyLevel=Beginner&trainingLocation=Home");

        Assert.Equal(HttpStatusCode.OK, combinedResponse.StatusCode);

        using var combinedDocument = JsonDocument.Parse(
            await combinedResponse.Content.ReadAsStringAsync());

        var combinedResults = combinedDocument.RootElement;

        Assert.Equal(JsonValueKind.Array, combinedResults.ValueKind);

        Assert.Single(combinedResults.EnumerateArray());

        Assert.Equal(
            homeId,
            combinedResults[0].GetProperty("id").GetGuid());

        // STEP 13: Verify nonexistent training location returns no results
        var missingLocation = $"UNKNOWN-{Guid.NewGuid():N}";

        var missingResponse = await client.GetAsync(
            $"/api/exercise-library?trainingLocation={missingLocation}");

        Assert.Equal(HttpStatusCode.OK, missingResponse.StatusCode);

        using var missingDocument = JsonDocument.Parse(
            await missingResponse.Content.ReadAsStringAsync());

        Assert.Empty(missingDocument.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Member_CanPaginateApprovedExerciseLibrary()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Login as Admin
        var adminLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, adminLoginResponse.StatusCode);

        using var adminLoginDocument = JsonDocument.Parse(
            await adminLoginResponse.Content.ReadAsStringAsync());

        var adminToken = adminLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(adminToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // STEP 2: Create four uniquely searchable approved exercises
        // and one Draft exercise.
        var uniqueText = Guid.NewGuid().ToString("N").ToUpperInvariant();

        async Task<Guid> CreateExerciseAsync(int number, bool approve)
        {
            var request = new SaveExerciseRequest
            {
                Code = $"PAGE-{number}-{uniqueText}",
                Name = $"Pagination Test {number:D2} {uniqueText}",
                Description = "Exercise for pagination integration testing",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Home",
                Instructions = "Perform 3 sets of 12 repetitions."
            };

            var createResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                request);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            using var createDocument = JsonDocument.Parse(
                await createResponse.Content.ReadAsStringAsync());

            var exerciseId = createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

            if (approve)
            {
                var submitResponse = await client.PostAsync(
                    $"/api/exercises/{exerciseId}/submit-review",
                    null);

                Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

                var approveResponse = await client.PutAsync(
                    $"/api/exercises/{exerciseId}/approve",
                    null);

                Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
            }

            return exerciseId;
        }

        var firstId = await CreateExerciseAsync(1, true);
        var secondId = await CreateExerciseAsync(2, true);
        var thirdId = await CreateExerciseAsync(3, true);
        var fourthId = await CreateExerciseAsync(4, true);
        var draftId = await CreateExerciseAsync(5, false);

        // STEP 3: Login as Member
        var memberLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, memberLoginResponse.StatusCode);

        using var memberLoginDocument = JsonDocument.Parse(
            await memberLoginResponse.Content.ReadAsStringAsync());

        var memberToken = memberLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(memberToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", memberToken);

        // STEP 4: Build a query using all five filters.
        // The unique search term isolates this test's exercises.
        var baseUrl =
            $"/api/exercise-library?search={uniqueText}" +
            "&muscleGroup=Chest" +
            "&equipment=Dumbbell" +
            "&difficultyLevel=Beginner" +
            "&trainingLocation=Home";

        // STEP 5: Retrieve page 1
        var page1Response = await client.GetAsync(
            $"{baseUrl}&page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, page1Response.StatusCode);

        using var page1Document = JsonDocument.Parse(
            await page1Response.Content.ReadAsStringAsync());

        var page1Results = page1Document.RootElement;

        Assert.Equal(JsonValueKind.Array, page1Results.ValueKind);
        Assert.Equal(2, page1Results.GetArrayLength());

        Assert.Equal(
            firstId,
            page1Results[0].GetProperty("id").GetGuid());

        Assert.Equal(
            secondId,
            page1Results[1].GetProperty("id").GetGuid());

        // STEP 6: Retrieve page 2
        var page2Response = await client.GetAsync(
            $"{baseUrl}&page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, page2Response.StatusCode);

        using var page2Document = JsonDocument.Parse(
            await page2Response.Content.ReadAsStringAsync());

        var page2Results = page2Document.RootElement;

        Assert.Equal(JsonValueKind.Array, page2Results.ValueKind);
        Assert.Equal(2, page2Results.GetArrayLength());

        Assert.Equal(
            thirdId,
            page2Results[0].GetProperty("id").GetGuid());

        Assert.Equal(
            fourthId,
            page2Results[1].GetProperty("id").GetGuid());

        // STEP 7: Verify pages do not overlap
        var page1Ids = page1Results
            .EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .ToHashSet();

        var page2Ids = page2Results
            .EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .ToHashSet();

        Assert.Empty(page1Ids.Intersect(page2Ids));

        // STEP 8: Verify page 3 is empty
        var page3Response = await client.GetAsync(
            $"{baseUrl}&page=3&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, page3Response.StatusCode);

        using var page3Document = JsonDocument.Parse(
            await page3Response.Content.ReadAsStringAsync());

        Assert.Empty(page3Document.RootElement.EnumerateArray());

        // STEP 9: Verify Draft exercise is excluded
        Assert.DoesNotContain(draftId, page1Ids);
        Assert.DoesNotContain(draftId, page2Ids);

        // STEP 10: Verify all returned exercises are Approved and Active
        foreach (var exercise in page1Results.EnumerateArray()
            .Concat(page2Results.EnumerateArray()))
        {
            Assert.Equal(
                "Approved",
                exercise.GetProperty("reviewStatus").GetString());

            Assert.True(
                exercise.GetProperty("isActive").GetBoolean());

            Assert.Equal(
                "Chest",
                exercise.GetProperty("muscleGroup").GetString());

            Assert.Equal(
                "Dumbbell",
                exercise.GetProperty("equipment").GetString());

            Assert.Equal(
                "Beginner",
                exercise.GetProperty("difficultyLevel").GetString());

            Assert.Equal(
                "Home",
                exercise.GetProperty("trainingLocation").GetString());
        }

        // STEP 11: Verify invalid page number
        var invalidPageResponse = await client.GetAsync(
            $"{baseUrl}&page=0&pageSize=2");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            invalidPageResponse.StatusCode);

        // STEP 12: Verify invalid page size
        var invalidSizeResponse = await client.GetAsync(
            $"{baseUrl}&page=1&pageSize=0");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            invalidSizeResponse.StatusCode);

        // STEP 13: Verify page size above maximum
        var oversizedResponse = await client.GetAsync(
            $"{baseUrl}&page=1&pageSize=101");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            oversizedResponse.StatusCode);

        // STEP 14: Verify page offset overflow protection
        var overflowResponse = await client.GetAsync(
            $"{baseUrl}&page=2147483647&pageSize=100");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            overflowResponse.StatusCode);

        // STEP 15: Verify pageSize alone defaults to page 1
        var defaultPageResponse = await client.GetAsync(
            $"{baseUrl}&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, defaultPageResponse.StatusCode);

        using var defaultPageDocument = JsonDocument.Parse(
            await defaultPageResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            firstId,
            defaultPageDocument.RootElement[0]
                .GetProperty("id")
                .GetGuid());

        // STEP 16: Verify no pagination parameters preserve old behavior
        var unpaginatedResponse = await client.GetAsync(baseUrl);

        Assert.Equal(HttpStatusCode.OK, unpaginatedResponse.StatusCode);

        using var unpaginatedDocument = JsonDocument.Parse(
            await unpaginatedResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            4,
            unpaginatedDocument.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Member_CanViewOnlyApprovedActiveExerciseDetails()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // STEP 1: Authenticate as Admin
        var adminLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, adminLoginResponse.StatusCode);

        using var adminLoginDocument = JsonDocument.Parse(
            await adminLoginResponse.Content.ReadAsStringAsync());

        var adminToken = adminLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(adminToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var uniqueText = Guid.NewGuid().ToString("N").ToUpperInvariant();

        // STEP 2: Helper to create exercises
        async Task<Guid> CreateExerciseAsync(
            string prefix,
            string name,
            string finalStatus)
        {
            var request = new SaveExerciseRequest
            {
                Code = $"{prefix}-{uniqueText}",
                Name = $"{name} {uniqueText}",
                Description = "Exercise detail integration test",
                MuscleGroup = "Chest",
                Equipment = "Dumbbell",
                DifficultyLevel = "Beginner",
                TrainingLocation = "Gym",
                Instructions = "Perform 3 sets of 12 repetitions."
            };

            var createResponse = await client.PostAsJsonAsync(
                "/api/exercises",
                request);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            using var createDocument = JsonDocument.Parse(
                await createResponse.Content.ReadAsStringAsync());

            var exerciseId = createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

            if (finalStatus != "Draft")
            {
                var submitResponse = await client.PostAsync(
                    $"/api/exercises/{exerciseId}/submit-review",
                    null);

                Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
            }

            if (finalStatus == "Approved" || finalStatus == "Retired")
            {
                var approveResponse = await client.PutAsync(
                    $"/api/exercises/{exerciseId}/approve",
                    null);

                Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
            }

            if (finalStatus == "Retired")
            {
                var retireResponse = await client.PutAsync(
                    $"/api/exercises/{exerciseId}/retire",
                    null);

                Assert.Equal(HttpStatusCode.OK, retireResponse.StatusCode);
            }

            return exerciseId;
        }

        // STEP 3: Create exercises in different workflow states
        var approvedId = await CreateExerciseAsync(
            "DETAIL-APPROVED", "Approved Chest Press", "Approved");

        var draftId = await CreateExerciseAsync(
            "DETAIL-DRAFT", "Draft Chest Press", "Draft");

        var inReviewId = await CreateExerciseAsync(
            "DETAIL-REVIEW", "Review Chest Press", "In Review");

        var retiredId = await CreateExerciseAsync(
            "DETAIL-RETIRED", "Retired Chest Press", "Retired");

        // STEP 4: Verify unauthenticated access is rejected
        client.DefaultRequestHeaders.Authorization = null;

        var unauthorizedResponse = await client.GetAsync(
            $"/api/exercise-library/{approvedId}");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            unauthorizedResponse.StatusCode);

        // STEP 5: Authenticate as Member
        var memberLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, memberLoginResponse.StatusCode);

        using var memberLoginDocument = JsonDocument.Parse(
            await memberLoginResponse.Content.ReadAsStringAsync());

        var memberToken = memberLoginDocument.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(memberToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", memberToken);

        // STEP 6: Approved exercise must be accessible
        var approvedResponse = await client.GetAsync(
            $"/api/exercise-library/{approvedId}");

        Assert.Equal(HttpStatusCode.OK, approvedResponse.StatusCode);

        using var approvedDocument = JsonDocument.Parse(
            await approvedResponse.Content.ReadAsStringAsync());

        var exercise = approvedDocument.RootElement;

        Assert.Equal(
            approvedId,
            exercise.GetProperty("id").GetGuid());

        Assert.Equal(
            "Approved",
            exercise.GetProperty("reviewStatus").GetString());

        Assert.True(
            exercise.GetProperty("isActive").GetBoolean());

        Assert.Equal(
            "Chest",
            exercise.GetProperty("muscleGroup").GetString());

        Assert.Equal(
            "Dumbbell",
            exercise.GetProperty("equipment").GetString());

        Assert.Equal(
            "Perform 3 sets of 12 repetitions.",
            exercise.GetProperty("instructions").GetString());

        // STEP 7: Draft exercise must be hidden
        var draftResponse = await client.GetAsync(
            $"/api/exercise-library/{draftId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            draftResponse.StatusCode);

        // STEP 8: In Review exercise must be hidden
        var inReviewResponse = await client.GetAsync(
            $"/api/exercise-library/{inReviewId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            inReviewResponse.StatusCode);

        // STEP 9: Retired exercise must be hidden
        var retiredResponse = await client.GetAsync(
            $"/api/exercise-library/{retiredId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            retiredResponse.StatusCode);

        // STEP 10: Nonexistent exercise must return 404
        var missingResponse = await client.GetAsync(
            $"/api/exercise-library/{Guid.NewGuid()}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            missingResponse.StatusCode);
    }
}