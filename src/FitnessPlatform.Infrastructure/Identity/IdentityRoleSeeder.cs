using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace FitnessPlatform.Infrastructure.Identity;

public static class IdentityRoleSeeder
{
    public static async Task SeedRolesAsync(
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        string[] roles =
        {
            "Admin",
            "Trainer",
            "Member"
        };

        foreach (var roleName in roles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(
                new IdentityRole<Guid>(roleName));

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    result.Errors.Select(x => x.Description));

                throw new InvalidOperationException(
                    $"Failed to create role '{roleName}': {errors}");
            }
        }
    }
}
