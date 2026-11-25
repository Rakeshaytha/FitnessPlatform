using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FitnessPlatform.Contracts.Profiles;
using FitnessPlatform.Domain.Entities;
using FitnessPlatform.Infrastructure.Identity;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessPlatform.IntegrationTests;

public class ProfileIntegrityIntegrationTests
{
    private static HttpClient CreateClient(
        FitnessPlatformWebApplicationFactory factory)
    {
        return factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
    }

    private static async Task<(Guid UserId, string Email)>
        CreateMemberAsync(
            FitnessPlatformWebApplicationFactory factory,
            Guid? userId = null)
    {
        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var scope = factory.Services.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var email =
            $"profile-{Guid.NewGuid():N}@fitnessplatform.local";

        var user = new ApplicationUser
        {
            Id = userId ?? Guid.NewGuid(),
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

        return (user.Id, email);
    }

    private static async Task AuthenticateAsync(
        HttpClient client,
        string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = email,
                Password = TestIdentitySeeder.TestPassword
            });

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Authentication failed: {response.StatusCode}. {body}");

        using var document = JsonDocument.Parse(body);

        var accessToken = document.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);
    }

    private static CreateUserProfileRequest CreateRequest(
        string firstName = "Profile",
        decimal weightKg = 80)
    {
        return new CreateUserProfileRequest
        {
            FirstName = firstName,
            LastName = "IntegrationTest",
            DateOfBirth = new DateOnly(1990, 1, 1),
            HeightCm = 175,
            WeightKg = weightKg,
            CountryCode = "AE"
        };
    }

    private static async Task<UserProfileResponse>
        AssertCreatedAsync(
            HttpResponseMessage response,
            Guid expectedUserId)
    {
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 but received {response.StatusCode}. {body}");

        var profile = await response.Content
            .ReadFromJsonAsync<UserProfileResponse>();

        Assert.NotNull(profile);
        Assert.NotEqual(Guid.Empty, profile.Id);
        Assert.Equal(expectedUserId, profile.UserId);

        return profile;
    }

    private static async Task AssertDuplicateConflictAsync(
        HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Conflict,
            $"Expected 409 but received {response.StatusCode}. {body}");

        using var document = JsonDocument.Parse(body);

        Assert.Equal(
            "A profile already exists for this user.",
            document.RootElement
                .GetProperty("error")
                .GetString());
    }

    private static async Task<List<UserProfile>>
        GetProfilesAsync(
            FitnessPlatformWebApplicationFactory factory,
            Guid userId)
    {
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        return await db.UserProfiles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync();
    }

    private static bool IsDuplicateProfileIndexViolation(
        Exception exception)
    {
        Exception? current = exception;

        while (current is not null)
        {
            if (current is SqlException sqlException)
            {
                foreach (SqlError error in sqlException.Errors)
                {
                    if ((error.Number == 2601 ||
                         error.Number == 2627) &&
                        error.Message.Contains(
                            "UX_UserProfiles_UserId",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            current = current.InnerException;
        }

        return false;
    }

    [Fact]
    public async Task CreateProfile_FirstProfile_ReturnsCreated()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        var member = await CreateMemberAsync(factory);

        using var client = CreateClient(factory);

        await AuthenticateAsync(client, member.Email);

        var request = CreateRequest();

        using var response = await client.PostAsJsonAsync(
            "/api/profiles",
            request);

        var created = await AssertCreatedAsync(
            response,
            member.UserId);

        Assert.Equal(request.FirstName, created.FirstName);
        Assert.Equal(request.LastName, created.LastName);
        Assert.Equal(request.HeightCm, created.HeightCm);
        Assert.Equal(request.WeightKg, created.WeightKg);
        Assert.Equal(request.CountryCode, created.CountryCode);
        Assert.Equal(request.DateOfBirth, created.DateOfBirth);

        var profiles = await GetProfilesAsync(
            factory,
            member.UserId);

        var persisted = Assert.Single(profiles);

        Assert.Equal(created.Id, persisted.Id);
        Assert.Equal(member.UserId, persisted.UserId);
        Assert.Equal(request.FirstName, persisted.FirstName);
        Assert.Equal(request.WeightKg, persisted.WeightKg);
    }
    [Fact]
    public async Task CreateProfile_SequentialDuplicate_ReturnsConflict()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        var member = await CreateMemberAsync(factory);

        using var client = CreateClient(factory);

        await AuthenticateAsync(client, member.Email);

        var originalRequest = CreateRequest(
            "Original",
            80);

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/profiles",
            originalRequest);

        var original = await AssertCreatedAsync(
            firstResponse,
            member.UserId);

        var duplicateRequest = CreateRequest(
            "Duplicate",
            95);

        using var duplicateResponse = await client.PostAsJsonAsync(
            "/api/profiles",
            duplicateRequest);

        await AssertDuplicateConflictAsync(duplicateResponse);

        var profiles = await GetProfilesAsync(
            factory,
            member.UserId);

        var persisted = Assert.Single(profiles);

        Assert.Equal(original.Id, persisted.Id);
        Assert.Equal("Original", persisted.FirstName);
        Assert.Equal(80m, persisted.WeightKg);
    }

    [Fact]
    public async Task CreateProfile_ConcurrentDuplicate_ReturnsOneCreatedAndOneConflict()
    {
        var userId = Guid.NewGuid();

        var interceptor =
            new ProfileCreationConcurrencyInterceptor(userId);

        using var factory =
            new FitnessPlatformWebApplicationFactory(interceptor);

        var member = await CreateMemberAsync(
            factory,
            userId);

        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        await AuthenticateAsync(firstClient, member.Email);
        await AuthenticateAsync(secondClient, member.Email);

        var firstRequest = CreateRequest(
            "ConcurrentFirst",
            80);

        var secondRequest = CreateRequest(
            "ConcurrentSecond",
            90);

        var firstTask = firstClient.PostAsJsonAsync(
            "/api/profiles",
            firstRequest);

        var secondTask = secondClient.PostAsJsonAsync(
            "/api/profiles",
            secondRequest);

        await Task.WhenAll(firstTask, secondTask);

        using var firstResponse = await firstTask;
        using var secondResponse = await secondTask;

        Assert.Equal(2, interceptor.ArrivalCount);

        var responses = new[]
        {
        firstResponse,
        secondResponse
    };

        var createdResponse = Assert.Single(
            responses.Where(x =>
                x.StatusCode == HttpStatusCode.Created));

        var conflictResponse = Assert.Single(
            responses.Where(x =>
                x.StatusCode == HttpStatusCode.Conflict));

        var created = await AssertCreatedAsync(
            createdResponse,
            member.UserId);

        await AssertDuplicateConflictAsync(conflictResponse);

        var winningRequest =
            ReferenceEquals(createdResponse, firstResponse)
                ? firstRequest
                : secondRequest;

        var profiles = await GetProfilesAsync(
            factory,
            member.UserId);

        var persisted = Assert.Single(profiles);

        Assert.Equal(created.Id, persisted.Id);

        Assert.Equal(
            winningRequest.FirstName,
            persisted.FirstName);

        Assert.Equal(
            winningRequest.WeightKg,
            persisted.WeightKg);

        Assert.Equal(
            winningRequest.FirstName,
            created.FirstName);

        Assert.Equal(
            winningRequest.WeightKg,
            created.WeightKg);
    }

    [Fact]
    public async Task CreateProfile_DifferentUsersConcurrently_BothCreated()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        var firstMember = await CreateMemberAsync(factory);
        var secondMember = await CreateMemberAsync(factory);

        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        await AuthenticateAsync(
            firstClient,
            firstMember.Email);

        await AuthenticateAsync(
            secondClient,
            secondMember.Email);

        var firstTask = firstClient.PostAsJsonAsync(
            "/api/profiles",
            CreateRequest("FirstMember", 80));

        var secondTask = secondClient.PostAsJsonAsync(
            "/api/profiles",
            CreateRequest("SecondMember", 90));

        await Task.WhenAll(firstTask, secondTask);

        using var firstResponse = await firstTask;
        using var secondResponse = await secondTask;

        var firstProfile = await AssertCreatedAsync(
            firstResponse,
            firstMember.UserId);

        var secondProfile = await AssertCreatedAsync(
            secondResponse,
            secondMember.UserId);

        Assert.NotEqual(firstProfile.Id, secondProfile.Id);

        var firstPersisted = Assert.Single(
            await GetProfilesAsync(
                factory,
                firstMember.UserId));

        var secondPersisted = Assert.Single(
            await GetProfilesAsync(
                factory,
                secondMember.UserId));

        Assert.Equal(firstProfile.Id, firstPersisted.Id);
        Assert.Equal(secondProfile.Id, secondPersisted.Id);

        Assert.Equal("FirstMember", firstPersisted.FirstName);
        Assert.Equal("SecondMember", secondPersisted.FirstName);
    }

    [Fact]
    public async Task CreateProfile_DirectDuplicateInsert_DatabaseRejectsDuplicate()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        var member = await CreateMemberAsync(factory);

        using var client = CreateClient(factory);

        await AuthenticateAsync(client, member.Email);

        using var response = await client.PostAsJsonAsync(
            "/api/profiles",
            CreateRequest("Original", 80));

        var original = await AssertCreatedAsync(
            response,
            member.UserId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<FitnessPlatformDbContext>();

            var duplicate = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = member.UserId,
                FirstName = "DatabaseDuplicate",
                LastName = "IntegrationTest",
                DateOfBirth = new DateOnly(1990, 1, 1),
                HeightCm = 175,
                WeightKg = 95,
                CountryCode = "AE",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            db.UserProfiles.Add(duplicate);

            var exception =
                await Assert.ThrowsAsync<DbUpdateException>(
                    () => db.SaveChangesAsync());

            Assert.True(
                IsDuplicateProfileIndexViolation(exception),
                exception.ToString());
        }

        var profiles = await GetProfilesAsync(
            factory,
            member.UserId);

        var persisted = Assert.Single(profiles);

        Assert.Equal(original.Id, persisted.Id);
        Assert.Equal("Original", persisted.FirstName);
        Assert.Equal(80m, persisted.WeightKg);
    }

    [Fact]
    public async Task CreateProfile_WithoutToken_ReturnsUnauthorized()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync(
            "/api/profiles",
            CreateRequest());

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ExistingProfile_PreservesIdentityAndUpdatesData()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        var member = await CreateMemberAsync(factory);

        using var client = CreateClient(factory);

        await AuthenticateAsync(client, member.Email);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/profiles",
            CreateRequest("Original", 80));

        var original = await AssertCreatedAsync(
            createResponse,
            member.UserId);

        var originalPersisted = Assert.Single(
            await GetProfilesAsync(
                factory,
                member.UserId));

        var updateRequest = new UpdateUserProfileRequest
        {
            FirstName = "Updated",
            LastName = "UpdatedMember",
            DateOfBirth = new DateOnly(1991, 2, 2),
            HeightCm = 180,
            WeightKg = 85,
            CountryCode = "IN"
        };

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/profiles/{original.Id}",
            updateRequest);

        var responseBody =
            await updateResponse.Content.ReadAsStringAsync();

        Assert.True(
            updateResponse.StatusCode == HttpStatusCode.OK,
            $"Expected 200 but received {updateResponse.StatusCode}. {responseBody}");

        var updated = await updateResponse.Content
            .ReadFromJsonAsync<UserProfileResponse>();

        Assert.NotNull(updated);

        // Verify identity remains unchanged.
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal(original.UserId, updated.UserId);
        Assert.Equal(original.CreatedAtUtc, updated.CreatedAtUtc);

        // Verify updated response values.
        Assert.Equal(updateRequest.FirstName, updated.FirstName);
        Assert.Equal(updateRequest.LastName, updated.LastName);
        Assert.Equal(updateRequest.DateOfBirth, updated.DateOfBirth);
        Assert.Equal(updateRequest.HeightCm, updated.HeightCm);
        Assert.Equal(updateRequest.WeightKg, updated.WeightKg);
        Assert.Equal(updateRequest.CountryCode, updated.CountryCode);

        // Verify exactly one profile remains in the database.
        var persisted = Assert.Single(
            await GetProfilesAsync(
                factory,
                member.UserId));

        Assert.Equal(original.Id, persisted.Id);
        Assert.Equal(member.UserId, persisted.UserId);

        // Verify updated database values.
        Assert.Equal(updateRequest.FirstName, persisted.FirstName);
        Assert.Equal(updateRequest.LastName, persisted.LastName);
        Assert.Equal(updateRequest.DateOfBirth, persisted.DateOfBirth);
        Assert.Equal(updateRequest.HeightCm, persisted.HeightCm);
        Assert.Equal(updateRequest.WeightKg, persisted.WeightKg);
        Assert.Equal(updateRequest.CountryCode, persisted.CountryCode);

        // Verify timestamp integrity.
        Assert.Equal(
            originalPersisted.CreatedAtUtc,
            persisted.CreatedAtUtc);

        Assert.True(
            persisted.UpdatedAtUtc >=
            originalPersisted.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetOnboarding_NewProfile_ReturnsIncompleteOnboarding()
    {
        using var factory =
            new FitnessPlatformWebApplicationFactory();

        var member = await CreateMemberAsync(factory);

        using var client = CreateClient(factory);

        await AuthenticateAsync(client, member.Email);

        var request = CreateRequest(
            "OnboardingMember",
            82);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/profiles",
            request);

        var created = await AssertCreatedAsync(
            createResponse,
            member.UserId);

        using var onboardingResponse = await client.GetAsync(
            $"/api/profiles/{created.Id}/onboarding");

        var responseBody =
            await onboardingResponse.Content.ReadAsStringAsync();

        Assert.True(
            onboardingResponse.StatusCode == HttpStatusCode.OK,
            $"Expected 200 but received {onboardingResponse.StatusCode}. {responseBody}");

        using var document = JsonDocument.Parse(responseBody);

        var root = document.RootElement;

        // Verify profile identity.
        Assert.Equal(
            created.Id,
            root.GetProperty("profileId").GetGuid());

        Assert.Equal(
            member.UserId,
            root.GetProperty("userId").GetGuid());

        // Verify personal information.
        Assert.Equal(
            request.FirstName,
            root.GetProperty("firstName").GetString());

        Assert.Equal(
            request.LastName,
            root.GetProperty("lastName").GetString());

        Assert.Equal(
            request.HeightCm,
            root.GetProperty("heightCm").GetDecimal());

        Assert.Equal(
            request.WeightKg,
            root.GetProperty("weightKg").GetDecimal());

        Assert.Equal(
            request.CountryCode,
            root.GetProperty("countryCode").GetString());

        Assert.Equal(
            "1990-01-01",
            root.GetProperty("dateOfBirth").GetString());

        // Newly created profiles have no fitness goals.
        var fitnessGoals = root.GetProperty("fitnessGoals");

        Assert.Equal(
            JsonValueKind.Array,
            fitnessGoals.ValueKind);

        Assert.Equal(
            0,
            fitnessGoals.GetArrayLength());

        // Workout preferences are not configured.
        Assert.Equal(
            JsonValueKind.Null,
            root.GetProperty("workoutPreferences").ValueKind);

        // Dietary preferences are not configured.
        Assert.Equal(
            JsonValueKind.Null,
            root.GetProperty("dietaryPreferences").ValueKind);

        // Onboarding must remain incomplete.
        Assert.False(
            root.GetProperty("isOnboardingComplete").GetBoolean());

        // Retrieving onboarding must not create another profile.
        var persisted = Assert.Single(
            await GetProfilesAsync(
                factory,
                member.UserId));

        Assert.Equal(created.Id, persisted.Id);
        Assert.Equal(member.UserId, persisted.UserId);
    }


}
