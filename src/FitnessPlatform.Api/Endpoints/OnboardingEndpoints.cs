using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitnessPlatform.Contracts.Onboarding;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FitnessPlatform.Api.Endpoints;

public static class OnboardingEndpoints
{
    public static IEndpointRouteBuilder MapOnboardingEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profiles")
            .WithTags("Onboarding")
            .RequireAuthorization();

        group.MapGet("/{profileId:guid}/onboarding", async (
            Guid profileId,
            HttpContext httpContext,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            var profile = await db.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == profileId,
                    cancellationToken);

            if (profile is null)
            {
                return Results.NotFound(new
                {
                    Error = "User profile not found."
                });
            }

            // Verify that the authenticated user owns this profile.
            // Administrators are permitted to access all profiles.
            if (!CanAccessProfile(httpContext, profile.UserId))
            {
                return Results.Forbid();
            }

            var fitnessGoals = await db.UserProfileFitnessGoals
                .AsNoTracking()
                .Where(x => x.UserProfileId == profileId)
                .OrderBy(x => x.FitnessGoal.Name)
                .Select(x => new OnboardingFitnessGoalResponse
                {
                    Id = x.FitnessGoal.Id,
                    Code = x.FitnessGoal.Code,
                    Name = x.FitnessGoal.Name
                })
                .ToListAsync(cancellationToken);

            var workoutPreferences = await db.WorkoutPreferences
                .AsNoTracking()
                .Where(x => x.UserProfileId == profileId)
                .Select(x => new OnboardingWorkoutPreferenceResponse
                {
                    TrainingLocation = x.TrainingLocation,
                    ExperienceLevel = x.ExperienceLevel,
                    DaysPerWeek = x.DaysPerWeek,
                    SessionDurationMinutes = x.SessionDurationMinutes
                })
                .FirstOrDefaultAsync(cancellationToken);

            var dietaryPreferences = await db.DietaryPreferences
                .AsNoTracking()
                .Where(x => x.UserProfileId == profileId)
                .Select(x => new OnboardingDietaryPreferenceResponse
                {
                    DietType = x.DietType,
                    PreferredCuisine = x.PreferredCuisine,
                    IncludesEggs = x.IncludesEggs,
                    IncludesMeat = x.IncludesMeat
                })
                .FirstOrDefaultAsync(cancellationToken);

            var isComplete =
                fitnessGoals.Count > 0 &&
                workoutPreferences is not null &&
                dietaryPreferences is not null;

            var response = new UserOnboardingResponse
            {
                ProfileId = profile.Id,
                UserId = profile.UserId,
                FirstName = profile.FirstName,
                LastName = profile.LastName,
                DateOfBirth = profile.DateOfBirth,
                HeightCm = profile.HeightCm,
                WeightKg = profile.WeightKg,
                CountryCode = profile.CountryCode,
                FitnessGoals = fitnessGoals,
                WorkoutPreferences = workoutPreferences,
                DietaryPreferences = dietaryPreferences,
                IsOnboardingComplete = isComplete
            };

            return Results.Ok(response);
        })
        .WithName("GetUserOnboarding");

        return app;
    }

    private static bool CanAccessProfile(
        HttpContext httpContext,
        Guid profileUserId)
    {
        // Admin users can access all profiles.
        if (httpContext.User.IsInRole("Admin"))
        {
            return true;
        }

        // Read the authenticated user's ID from the JWT.
        var userIdClaim = httpContext.User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdClaim, out var authenticatedUserId))
        {
            return false;
        }

        // Members can access only their own profile.
        return authenticatedUserId == profileUserId;
    }
}