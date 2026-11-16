using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitnessPlatform.Contracts.DietaryPreferences;
using FitnessPlatform.Domain.Entities;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FitnessPlatform.Api.Endpoints;

public static class DietaryPreferenceEndpoints
{
    public static IEndpointRouteBuilder MapDietaryPreferenceEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(
            "/api/profiles/{profileId:guid}/dietary-preferences")
            .WithTags("Dietary Preferences")
            .RequireAuthorization();

        // GET: Retrieve dietary preferences
        group.MapGet("/", async (
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

            if (!CanAccessProfile(httpContext, profile.UserId))
            {
                return Results.Forbid();
            }

            var preference = await db.DietaryPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserProfileId == profileId,
                    cancellationToken);

            if (preference is null)
            {
                return Results.NotFound(new
                {
                    Error = "Dietary preferences not found."
                });
            }

            return Results.Ok(MapResponse(preference));
        })
        .WithName("GetDietaryPreferences");

        // PUT: Create or update dietary preferences
        group.MapPut("/", async (
            Guid profileId,
            SaveDietaryPreferenceRequest request,
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

            if (!CanAccessProfile(httpContext, profile.UserId))
            {
                return Results.Forbid();
            }

            // Validate diet type
            if (string.IsNullOrWhiteSpace(request.DietType) ||
                request.DietType.Length > 50)
            {
                return Results.BadRequest(new
                {
                    Error = "DietType must contain between 1 and 50 characters."
                });
            }

            // Validate preferred cuisine
            if (string.IsNullOrWhiteSpace(request.PreferredCuisine) ||
                request.PreferredCuisine.Length > 100)
            {
                return Results.BadRequest(new
                {
                    Error = "PreferredCuisine must contain between 1 and 100 characters."
                });
            }

            // Check whether preferences already exist
            var preference = await db.DietaryPreferences
                .FirstOrDefaultAsync(
                    x => x.UserProfileId == profileId,
                    cancellationToken);

            var isNew = preference is null;

            if (preference is null)
            {
                preference = new DietaryPreference
                {
                    Id = Guid.NewGuid(),
                    UserProfileId = profileId
                };

                db.DietaryPreferences.Add(preference);
            }

            // Update preferences
            preference.DietType = request.DietType.Trim();
            preference.PreferredCuisine =
                request.PreferredCuisine.Trim();

            preference.IncludesEggs = request.IncludesEggs;
            preference.IncludesMeat = request.IncludesMeat;

            await db.SaveChangesAsync(cancellationToken);

            var response = MapResponse(preference);

            return isNew
                ? Results.Created(
                    $"/api/profiles/{profileId}/dietary-preferences",
                    response)
                : Results.Ok(response);
        })
        .WithName("SaveDietaryPreferences");

        return app;
    }

    private static bool CanAccessProfile(
        HttpContext httpContext,
        Guid profileUserId)
    {
        if (httpContext.User.IsInRole("Admin"))
        {
            return true;
        }

        var userIdClaim = httpContext.User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdClaim, out var authenticatedUserId))
        {
            return false;
        }

        return authenticatedUserId == profileUserId;
    }

    private static DietaryPreferenceResponse MapResponse(
        DietaryPreference preference)
    {
        return new DietaryPreferenceResponse
        {
            Id = preference.Id,
            UserProfileId = preference.UserProfileId,
            DietType = preference.DietType,
            PreferredCuisine = preference.PreferredCuisine,
            IncludesEggs = preference.IncludesEggs,
            IncludesMeat = preference.IncludesMeat
        };
    }
}