using FitnessPlatform.Api.Filters;
using FitnessPlatform.Application.WorkoutSessions;
using FitnessPlatform.Contracts.WorkoutSessions;
using FitnessPlatform.Domain.Constants;
using FitnessPlatform.Domain.Entities;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FitnessPlatform.Api.Endpoints;

public static class WorkoutSessionEndpoints
{
    private const string RoutePrefix =
        "/api/member/workout-sessions";

    private const string EndpointTag =
        "Member Workout Sessions";

    public static void MapWorkoutSessionEndpoints(
        this WebApplication app)
    {
        var memberGroup = app.MapGroup(RoutePrefix);

        memberGroup.RequireAuthorization(
            policy => policy.RequireRole("Member"));

        memberGroup.WithTags(EndpointTag);
    }

    private static Guid? GetAuthenticatedUserId(
    HttpContext httpContext)
    {
        var userIdClaim =
            httpContext.User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdClaim, out var userId)
            || userId == Guid.Empty)
        {
            return null;
        }

        return userId;
    }

    private static async Task<Guid?> GetMemberProfileIdAsync(
    FitnessPlatformDbContext db,
    Guid userId,
    CancellationToken cancellationToken)
    {
        return await db.UserProfiles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }


}