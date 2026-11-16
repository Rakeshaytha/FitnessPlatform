using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FitnessPlatform.IntegrationTests;

public class AuthenticationSecurityTests
{
    [Fact]
    public async Task Health_WithoutToken_Returns200()
    {
        using var factory = new FitnessPlatformWebApplicationFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/profiles/59c5f810-a2dc-45f2-8877-4eab31cafb77")]
    [InlineData("/api/profiles/59c5f810-a2dc-45f2-8877-4eab31cafb77/onboarding")]
    [InlineData("/api/profiles/59c5f810-a2dc-45f2-8877-4eab31cafb77/workout-preferences")]
    [InlineData("/api/profiles/59c5f810-a2dc-45f2-8877-4eab31cafb77/dietary-preferences")]
    [InlineData("/api/profiles/59c5f810-a2dc-45f2-8877-4eab31cafb77/fitness-goals")]
    public async Task ProtectedEndpoints_WithoutToken_Return401(string url)
    {
        using var factory = new FitnessPlatformWebApplicationFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var response = await client.GetAsync(url);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }
}