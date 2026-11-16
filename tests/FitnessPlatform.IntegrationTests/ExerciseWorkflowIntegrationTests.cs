using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FitnessPlatform.Contracts.Exercises;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
}