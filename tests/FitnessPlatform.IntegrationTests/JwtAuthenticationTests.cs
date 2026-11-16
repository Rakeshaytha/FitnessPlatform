using Microsoft.AspNetCore.Mvc.Testing;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace FitnessPlatform.IntegrationTests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

public class JwtAuthenticationTests
{
    [Fact]
    public async Task Admin_WithValidCredentials_ReturnsJwt()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.True(
            document.RootElement.TryGetProperty(
                "accessToken",
                out var tokenElement));

        var token = tokenElement.GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));

        Assert.Equal(3, token.Split('.').Length);
    }

    [Fact]
    public async Task Admin_WithInvalidPassword_Returns401()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = "IncorrectPassword@123"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Member_WithValidCredentials_ReturnsJwt()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.Member1Email,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.True(
            document.RootElement.TryGetProperty(
                "accessToken",
                out var tokenElement));

        var token = tokenElement.GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));

        Assert.Equal(3, token.Split('.').Length);
    }

    [Fact]
    public async Task Admin_Jwt_ContainsCorrectClaims()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var accessToken = document.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        var handler = new JwtSecurityTokenHandler();

        var jwt = handler.ReadJwtToken(accessToken);

        Assert.Equal(
            "FitnessPlatform.IntegrationTests",
            jwt.Issuer);

        Assert.Contains(
            "FitnessPlatform.IntegrationTests",
            jwt.Audiences);

        Assert.Contains(
            jwt.Claims,
            claim => claim.Type == "email" &&
                     claim.Value == TestIdentitySeeder.AdminEmail);

        Assert.Contains(
            jwt.Claims,
            claim => claim.Type == "role" &&
                     claim.Value == "Admin");

        Assert.True(jwt.ValidTo > DateTime.UtcNow);

        Assert.True(
            jwt.ValidTo <= DateTime.UtcNow.AddMinutes(61));
    }
    [Fact]
    public async Task Admin_Jwt_HasValidCryptographicSignature()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();

        await TestIdentitySeeder.SeedAsync(factory.Services);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestIdentitySeeder.AdminEmail,
                Password = TestIdentitySeeder.TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var accessToken = document.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        var configuration = factory.Services
            .GetRequiredService<IConfiguration>();

        var signingKey = configuration["Jwt:Key"];

        Assert.False(string.IsNullOrWhiteSpace(signingKey));

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
            Convert.FromBase64String(signingKey)),

            ValidateIssuer = true,
            ValidIssuer = "FitnessPlatform.IntegrationTests",

            ValidateAudience = true,
            ValidAudience = "FitnessPlatform.IntegrationTests",

            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero
        };

        var handler = new JwtSecurityTokenHandler();

        var principal = handler.ValidateToken(
            accessToken,
            validationParameters,
            out var validatedToken);

        Assert.NotNull(principal);
        Assert.NotNull(validatedToken);
    }
}