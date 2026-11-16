using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace FitnessPlatform.Infrastructure.Identity;

public static class AdminAccountSeeder
{
    public static async Task SeedAdminAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Administrator credentials are missing.");
        }

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
                password);

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    result.Errors.Select(x => x.Description));

                throw new InvalidOperationException(
                    $"Administrator creation failed: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, "Admin"))
        {
            var result = await userManager.AddToRoleAsync(
                user,
                "Admin");

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    result.Errors.Select(x => x.Description));

                throw new InvalidOperationException(
                    $"Administrator role assignment failed: {errors}");
            }
        }
    }
}
