using FitnessPlatform.Infrastructure.Identity;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessPlatform.IntegrationTests;

public static class TestIdentitySeeder
{
    public const string AdminEmail = "testadmin@fitnessplatform.local";
    public const string Member1Email = "testmember1@fitnessplatform.local";
    public const string Member2Email = "testmember2@fitnessplatform.local";

    public const string TestPassword = "Test@12345678";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<FitnessPlatformDbContext>();

        var connection = db.Database.GetDbConnection();

        if (connection.Database != "FitnessPlatformIntegrationTestsDb" ||
            !connection.DataSource.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Unsafe database connection. Test seeding aborted.");
        }

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { "Admin", "Trainer", "Member" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(
                    new IdentityRole<Guid>(role));

                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        $"Unable to create role: {role}");
            }
        }

        await CreateUserAsync(userManager, AdminEmail, "Admin");
        await CreateUserAsync(userManager, Member1Email, "Member");
        await CreateUserAsync(userManager, Member2Email, "Member");
    }

    private static async Task CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user == null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(
                user,
                TestPassword);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(", ",
                        result.Errors.Select(e => e.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var result = await userManager.AddToRoleAsync(user, role);

            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Unable to assign role: {role}");
        }
    }
}