using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;

using FitnessPlatform.Contracts.WorkoutSessions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionAuthorizationIntegrationTests
{
    private const string RoutePrefix =
        WorkoutSessionTestData.RoutePrefix;

    // ============================================================
    // COMMON REQUEST HELPER
    // ============================================================

    private static async Task<HttpResponseMessage> SendRequestAsync(
        HttpClient client,
        string endpoint)
    {
        return endpoint switch
        {
            "start" => await client.PostAsJsonAsync(
                $"{RoutePrefix}/start",
                new StartWorkoutSessionRequest
                {
                    MemberWorkoutPlanAssignmentId =
                        Guid.NewGuid(),

                    WorkoutPlanDayId =
                        Guid.NewGuid(),

                    TimeZoneId = "Asia/Dubai"
                }),

            "active" => await client.GetAsync(
                $"{RoutePrefix}/active"),

            "details" => await client.GetAsync(
                $"{RoutePrefix}/{Guid.NewGuid()}"),

            _ => throw new ArgumentOutOfRangeException(
                nameof(endpoint),
                endpoint,
                "Unsupported workout session endpoint.")
        };
    }

    // ============================================================
    // 1-3. UNAUTHENTICATED REQUESTS
    // ============================================================

    [Theory]
    [InlineData("start")]
    [InlineData("active")]
    [InlineData("details")]
    public async Task UnauthenticatedRequest_ReturnsUnauthorized(
        string endpoint)
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        using var response =
            await SendRequestAsync(client, endpoint);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    // ============================================================
    // 4-6. INVALID JWT TOKEN
    // ============================================================

    [Theory]
    [InlineData("start")]
    [InlineData("active")]
    [InlineData("details")]
    public async Task InvalidToken_ReturnsUnauthorized(
        string endpoint)
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                "invalid-workout-session-token");

        using var response =
            await SendRequestAsync(client, endpoint);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    // ============================================================
    // 7-9. EXPIRED JWT TOKEN
    // ============================================================

    [Theory]
    [InlineData("start")]
    [InlineData("active")]
    [InlineData("details")]
    public async Task ExpiredToken_ReturnsUnauthorized(
        string endpoint)
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var member =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory);

        var expiredToken =
            CreateExpiredToken(
                factory,
                member.UserId,
                member.Email);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                expiredToken);

        using var response =
            await SendRequestAsync(client, endpoint);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        Assert.Equal(
            0,
            await WorkoutSessionTestData.CountSessionsAsync(
                factory,
                member.ProfileId));
    }

    // ============================================================
    // 10-12. ADMIN ROLE
    // ============================================================

    [Theory]
    [InlineData("start")]
    [InlineData("active")]
    [InlineData("details")]
    public async Task AdminRole_ReturnsForbidden(
        string endpoint)
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var admin =
            await WorkoutSessionTestData.CreateAdminAsync(
                factory);

        await WorkoutSessionTestData.AuthenticateAsync(
            client,
            admin.Email);

        using var response =
            await SendRequestAsync(client, endpoint);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    // ============================================================
    // 13-15. TRAINER ROLE
    // ============================================================

    [Theory]
    [InlineData("start")]
    [InlineData("active")]
    [InlineData("details")]
    public async Task TrainerRole_ReturnsForbidden(
        string endpoint)
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var trainer =
            await WorkoutSessionTestData.CreateTrainerAsync(
                factory);

        await WorkoutSessionTestData.AuthenticateAsync(
            client,
            trainer.Email);

        using var response =
            await SendRequestAsync(client, endpoint);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    // ============================================================
    // 16-18. MEMBER WITHOUT PROFILE
    // ============================================================

    [Theory]
    [InlineData("start")]
    [InlineData("active")]
    [InlineData("details")]
    public async Task MemberWithoutProfile_ReturnsNotFound(
        string endpoint)
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var member =
            await WorkoutSessionTestData.CreateMemberAsync(
                factory,
                createProfile: false);

        Assert.Equal(
            Guid.Empty,
            member.ProfileId);

        await WorkoutSessionTestData.AuthenticateAsync(
            client,
            member.Email);

        using var response =
            await SendRequestAsync(client, endpoint);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        var content =
            await response.Content.ReadAsStringAsync();

        Assert.Contains(
            "Member profile not found.",
            content);
    }

    // ============================================================
    // 19. AUTHORIZED MEMBER CAN START WORKOUT
    // ============================================================

    [Fact]
    public async Task AuthorizedMember_CanStartWorkoutSession()
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
                $"{RoutePrefix}/start",
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

        Assert.Equal(
            fixture.Member.ProfileId,
            result.Session.UserProfileId);

        Assert.Equal(
            fixture.AssignmentId,
            result.Session.MemberWorkoutPlanAssignmentId);

        Assert.Equal(
            "InProgress",
            result.Session.Status);

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountSessionsAsync(
                factory,
                fixture.Member.ProfileId));
    }

    // ============================================================
    // 20. AUTHORIZED MEMBER CAN RETRIEVE ACTIVE WORKOUT
    // ============================================================

    [Fact]
    public async Task AuthorizedMember_CanRetrieveActiveWorkoutSession()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client);

        var sessionId =
            await StartSessionAsync(
                client,
                fixture);

        using var response =
            await client.GetAsync(
                $"{RoutePrefix}/active");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var result =
            await response.Content
                .ReadFromJsonAsync<WorkoutSessionDetailResponse>();

        Assert.NotNull(result);

        Assert.Equal(
            sessionId,
            result.Session.Id);

        Assert.Equal(
            fixture.Member.ProfileId,
            result.Session.UserProfileId);

        Assert.Equal(
            "InProgress",
            result.Session.Status);

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountUnfinishedSessionsAsync(
                factory,
                fixture.Member.ProfileId));
    }

    // ============================================================
    // 21. AUTHORIZED MEMBER CAN RETRIEVE WORKOUT DETAILS
    // ============================================================

    [Fact]
    public async Task AuthorizedMember_CanRetrieveWorkoutSessionDetails()
    {
        using var factory =
            WorkoutSessionTestData.CreateFactory();

        using var client =
            WorkoutSessionTestData.CreateClient(factory);

        var fixture =
            await WorkoutSessionTestData.PrepareAssignedMemberAsync(
                factory,
                client);

        var sessionId =
            await StartSessionAsync(
                client,
                fixture);

        using var response =
            await client.GetAsync(
                $"{RoutePrefix}/{sessionId}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var result =
            await response.Content
                .ReadFromJsonAsync<WorkoutSessionDetailResponse>();

        Assert.NotNull(result);

        Assert.Equal(
            sessionId,
            result.Session.Id);

        Assert.Equal(
            fixture.Member.ProfileId,
            result.Session.UserProfileId);

        Assert.Equal(
            fixture.Plan.PlanId,
            result.Session.WorkoutPlanId);

        Assert.Equal(
            fixture.Plan.TrainingDayId,
            result.Session.WorkoutPlanDayId);

        Assert.Equal(
            2,
            result.Exercises.Count);

        Assert.Equal(
            1,
            await WorkoutSessionTestData.CountSessionsAsync(
                factory,
                fixture.Member.ProfileId));
    }

    // ============================================================
    // HELPER: START WORKOUT SESSION
    // ============================================================

    private static async Task<Guid> StartSessionAsync(
        HttpClient client,
        WorkoutSessionTestData.AssignedMember fixture)
    {
        var request =
            WorkoutSessionTestData.CreateStartRequest(
                fixture);

        using var response =
            await client.PostAsJsonAsync(
                $"{RoutePrefix}/start",
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

        return result.Session.Id;
    }

    // ============================================================
    // HELPER: CREATE EXPIRED JWT TOKEN
    // ============================================================

    private static string CreateExpiredToken(
        FitnessPlatformWebApplicationFactory factory,
        Guid userId,
        string email)
    {
        var configuration =
            factory.Services
                .GetRequiredService<IConfiguration>();

        var signingKey =
            configuration["Jwt:Key"];

        var issuer =
            configuration["Jwt:Issuer"];

        var audience =
            configuration["Jwt:Audience"];

        Assert.False(
            string.IsNullOrWhiteSpace(signingKey));

        Assert.False(
            string.IsNullOrWhiteSpace(issuer));

        Assert.False(
            string.IsNullOrWhiteSpace(audience));

        var securityKey =
            new SymmetricSecurityKey(
                Convert.FromBase64String(signingKey!));

        var credentials =
            new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256);

        var utcNow = DateTime.UtcNow;

        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                userId.ToString()),

            new Claim(
                JwtRegisteredClaimNames.Email,
                email),

            new Claim(
                "role",
                "Member"),

            new Claim(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: utcNow.AddHours(-2),
            expires: utcNow.AddHours(-1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}