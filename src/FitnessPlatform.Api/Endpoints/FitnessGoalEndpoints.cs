using FitnessPlatform.Contracts.FitnessGoals;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FitnessPlatform.Api.Endpoints;

public static class FitnessGoalEndpoints
{
    public static IEndpointRouteBuilder MapFitnessGoalEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/fitness-goals")
            .WithTags("Fitness Goals");

        // GET: Retrieve all fitness goals
        group.MapGet("/", async (
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            var goals = await db.FitnessGoals
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new FitnessGoalResponse
                {
                    Id = x.Id,
                    Code = x.Code,
                    Name = x.Name
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(goals);
        })
        .WithName("GetFitnessGoals");

        // GET: Retrieve fitness goal by ID
        group.MapGet("/{id:int}", async (
            int id,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            var goal = await db.FitnessGoals
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new FitnessGoalResponse
                {
                    Id = x.Id,
                    Code = x.Code,
                    Name = x.Name
                })
                .FirstOrDefaultAsync(cancellationToken);

            return goal is null
                ? Results.NotFound()
                : Results.Ok(goal);
        })
        .WithName("GetFitnessGoalById");

        return app;
    }
}