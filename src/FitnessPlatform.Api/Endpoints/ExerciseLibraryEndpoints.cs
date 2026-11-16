using FitnessPlatform.Contracts.Exercises;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FitnessPlatform.Api.Endpoints;

public static class ExerciseLibraryEndpoints
{
    public static IEndpointRouteBuilder MapExerciseLibraryEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/exercise-library")
            .WithTags("Exercise Library")
            .RequireAuthorization();

        group.MapGet("/", async (
            string? search,
            string? muscleGroup,
            string? equipment,
            string? difficultyLevel,
            string? trainingLocation,
            int? page,
            int? pageSize,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            // STEP 1: Return only Approved and Active exercises
            var query = db.Exercises
                .AsNoTracking()
                .Where(x =>
                    x.ReviewStatus == "Approved" &&
                    x.IsActive);

            // STEP 2: Search by exercise name or code
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchText = search.Trim();

                query = query.Where(x =>
                    x.Name.Contains(searchText) ||
                    x.Code.Contains(searchText));
            }

            // STEP 3: Filter by muscle group
            if (!string.IsNullOrWhiteSpace(muscleGroup))
            {
                var muscleGroupText = muscleGroup.Trim();

                query = query.Where(x =>
                    x.MuscleGroup == muscleGroupText);
            }

            // STEP 4: Filter by equipment
            if (!string.IsNullOrWhiteSpace(equipment))
            {
                var equipmentText = equipment.Trim();

                query = query.Where(x =>
                    x.Equipment == equipmentText);
            }

            // STEP 5: Filter by difficulty level
            if (!string.IsNullOrWhiteSpace(difficultyLevel))
            {
                var difficultyLevelText = difficultyLevel.Trim();

                query = query.Where(x =>
                    x.DifficultyLevel == difficultyLevelText);
            }

            // STEP 6: Filter by training location
            if (!string.IsNullOrWhiteSpace(trainingLocation))
            {
                var trainingLocationText = trainingLocation.Trim();

                query = query.Where(x =>
                    x.TrainingLocation == trainingLocationText);
            }

            // STEP 7: Apply stable ordering
            // Explicit IQueryable type prevents CS0266
            IQueryable<FitnessPlatform.Domain.Entities.Exercise> orderedQuery =
                query
                    .OrderBy(x => x.Name)
                    .ThenBy(x => x.Id);

            // STEP 8: Apply optional pagination
            if (page.HasValue || pageSize.HasValue)
            {
                var currentPage = page ?? 1;
                var currentPageSize = pageSize ?? 20;

                // Validate page number
                if (currentPage < 1)
                {
                    return Results.BadRequest(
                        "Page must be greater than or equal to 1.");
                }

                // Validate page size
                if (currentPageSize < 1 || currentPageSize > 100)
                {
                    return Results.BadRequest(
                        "Page size must be between 1 and 100.");
                }

                // Calculate pagination offset safely
                var skip = ((long)currentPage - 1) * currentPageSize;

                // Prevent integer overflow
                if (skip > int.MaxValue)
                {
                    return Results.BadRequest(
                        "Requested page exceeds the supported range.");
                }

                // Apply pagination to the ordered query
                orderedQuery = orderedQuery
                    .Skip((int)skip)
                    .Take(currentPageSize);
            }

            // STEP 9: Execute filtered and paginated query
            var exercises = await orderedQuery
                .Select(x => new ExerciseResponse
                {
                    Id = x.Id,
                    Code = x.Code,
                    Name = x.Name,
                    Description = x.Description,
                    MuscleGroup = x.MuscleGroup,
                    Equipment = x.Equipment,
                    DifficultyLevel = x.DifficultyLevel,
                    TrainingLocation = x.TrainingLocation,
                    Instructions = x.Instructions,
                    VideoUrl = x.VideoUrl,
                    GifUrl = x.GifUrl,
                    IsActive = x.IsActive,
                    ReviewStatus = x.ReviewStatus
                })
                .ToListAsync(cancellationToken);

            // STEP 10: Return API response
            return Results.Ok(exercises);
        })
        .WithName("GetExerciseLibrary");

        return app;
    }
}