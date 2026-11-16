using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitnessPlatform.Contracts.WorkoutPreferences;
using FitnessPlatform.Domain.Entities;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FitnessPlatform.Api.Endpoints;

public static class WorkoutPreferenceEndpoints
{
    public static IEndpointRouteBuilder MapWorkoutPreferenceEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profiles/{profileId:guid}/workout-preferences")
            .WithTags("Workout Preferences")
            .RequireAuthorization();

        // GET: Retrieve workout preferences
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

            var preference = await db.WorkoutPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserProfileId == profileId,
                    cancellationToken);

            if (preference is null)
            {
                return Results.NotFound(new
                {
                    Error = "Workout preferences not found."
                });
            }

            return Results.Ok(MapResponse(preference));
        })
        .WithName("GetWorkoutPreferences");

        // PUT: Create or update workout preferences
        group.MapPut("/", async (
            Guid profileId,
            SaveWorkoutPreferenceRequest request,
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

            if (string.IsNullOrWhiteSpace(request.TrainingLocation) ||
                string.IsNullOrWhiteSpace(request.ExperienceLevel))
            {
                return Results.BadRequest(new
                {
                    Error = "Training location and experience level are required."
                });
            }

            if (request.DaysPerWeek < 1 || request.DaysPerWeek > 7)
            {
                return Results.BadRequest(new
                {
                    Error = "DaysPerWeek must be between 1 and 7."
                });
            }

            if (request.SessionDurationMinutes < 15 ||
                request.SessionDurationMinutes > 180)
            {
                return Results.BadRequest(new
                {
                    Error = "SessionDurationMinutes must be between 15 and 180."
                });
            }

            var preference = await db.WorkoutPreferences
                .FirstOrDefaultAsync(
                    x => x.UserProfileId == profileId,
                    cancellationToken);

            var isNew = preference is null;

            if (preference is null)
            {
                preference = new WorkoutPreference
                {
                    Id = Guid.NewGuid(),
                    UserProfileId = profileId
                };

                db.WorkoutPreferences.Add(preference);
            }

            preference.TrainingLocation = request.TrainingLocation.Trim();
            preference.ExperienceLevel = request.ExperienceLevel.Trim();
            preference.DaysPerWeek = request.DaysPerWeek;
            preference.SessionDurationMinutes =
                request.SessionDurationMinutes;

            await db.SaveChangesAsync(cancellationToken);

            var response = MapResponse(preference);

            return isNew
                ? Results.Created(
                    $"/api/profiles/{profileId}/workout-preferences",
                    response)
                : Results.Ok(response);
        })
        .WithName("SaveWorkoutPreferences");

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

    private static WorkoutPreferenceResponse MapResponse(
        WorkoutPreference preference)
    {
        return new WorkoutPreferenceResponse
        {
            Id = preference.Id,
            UserProfileId = preference.UserProfileId,
            TrainingLocation = preference.TrainingLocation,
            ExperienceLevel = preference.ExperienceLevel,
            DaysPerWeek = preference.DaysPerWeek,
            SessionDurationMinutes =
                preference.SessionDurationMinutes
        };
    }
}