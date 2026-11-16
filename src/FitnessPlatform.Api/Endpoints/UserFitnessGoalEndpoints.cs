using System.Security.Claims;
using FitnessPlatform.Contracts.FitnessGoals;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using FitnessPlatform.Domain.Entities;

namespace FitnessPlatform.Api.Endpoints;

public static class UserFitnessGoalEndpoints
{
    public static IEndpointRouteBuilder MapUserFitnessGoalEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profiles")
            .WithTags("User Fitness Goals")
            .RequireAuthorization();

        // PUT: Assign fitness goals to a user profile
        group.MapPut("/{profileId:guid}/fitness-goals", async (
            Guid profileId,
            AssignFitnessGoalsRequest request,
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

            if (request is null || request.FitnessGoalIds is null)
            {
                return Results.BadRequest(new
                {
                    Error = "FitnessGoalIds is required."
                });
            }

            var selectedIds = request.FitnessGoalIds
                .Distinct()
                .ToList();

            if (selectedIds.Count == 0)
            {
                return Results.BadRequest(new
                {
                    Error = "Select at least one fitness goal."
                });
            }

            var goals = await db.FitnessGoals
                .AsNoTracking()
                .Where(x => selectedIds.Contains(x.Id) && x.IsActive)
                .ToListAsync(cancellationToken);

            if (goals.Count != selectedIds.Count)
            {
                return Results.BadRequest(new
                {
                    Error = "One or more fitness goals are invalid or inactive."
                });
            }

            var existingSelections = await db
                .Set<UserProfileFitnessGoal>()
                .Where(x => x.UserProfileId == profileId)
                .ToListAsync(cancellationToken);

            var existingIds = existingSelections
                .Select(x => x.FitnessGoalId)
                .ToHashSet();

            var requestedIds = selectedIds.ToHashSet();

            var selectionsToRemove = existingSelections
                .Where(x => !requestedIds.Contains(x.FitnessGoalId))
                .ToList();

            db.Set<UserProfileFitnessGoal>()
                .RemoveRange(selectionsToRemove);

            foreach (var goalId in selectedIds)
            {
                if (!existingIds.Contains(goalId))
                {
                    db.Set<UserProfileFitnessGoal>().Add(
                        new UserProfileFitnessGoal
                        {
                            UserProfileId = profileId,
                            FitnessGoalId = goalId,
                            CreatedAtUtc = DateTime.UtcNow
                        });
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                ProfileId = profileId,
                FitnessGoals = goals
                    .OrderBy(x => x.Name)
                    .Select(x => new
                    {
                        x.Id,
                        x.Code,
                        x.Name
                    })
            });
        })
        .WithName("AssignUserFitnessGoals");

        // GET: Retrieve fitness goals assigned to a user profile
        group.MapGet("/{profileId:guid}/fitness-goals", async (
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

            var goals = await db.Set<UserProfileFitnessGoal>()
                .AsNoTracking()
                .Where(x => x.UserProfileId == profileId)
                .Join(
                    db.FitnessGoals,
                    selection => selection.FitnessGoalId,
                    goal => goal.Id,
                    (selection, goal) => new
                    {
                        goal.Id,
                        goal.Code,
                        goal.Name
                    })
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            return Results.Ok(new
            {
                ProfileId = profileId,
                FitnessGoals = goals
            });
        })
        .WithName("GetUserFitnessGoals");

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
}