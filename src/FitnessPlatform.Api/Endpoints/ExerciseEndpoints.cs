using FitnessPlatform.Contracts.Exercises;
using FitnessPlatform.Domain.Entities;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using FitnessPlatform.Application.Exercises;
using FitnessPlatform.Api.Filters;

namespace FitnessPlatform.Api.Endpoints;

public static class ExerciseEndpoints
{
    public static IEndpointRouteBuilder MapExerciseEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/exercises")
            .WithTags("Exercises")
            .RequireAuthorization(policy =>
                policy.RequireRole("Admin"));

        // GET ALL EXERCISES

        // GET EXERCISE BY ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            var exercise = await db.Exercises
                .AsNoTracking()
                .Where(x => x.Id == id)
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
                .FirstOrDefaultAsync(cancellationToken);

            if (exercise is null)
            {
                return Results.NotFound(new
                {
                    Error = "Exercise not found."
                });
            }

            return Results.Ok(exercise);
        })
        .WithName("GetExerciseById");

        // CREATE EXERCISE
        group.MapPost("/", async (
            SaveExerciseRequest request,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            var validationError = ExerciseValidator.Validate(request);

            if (validationError is not null)
            {
                return Results.BadRequest(new
                {
                    Error = validationError
                });
            }

            var code = ExerciseValidator.NormalizeCode(request.Code);

            var codeExists = await db.Exercises
                .AnyAsync(x => x.Code == code, cancellationToken);

            if (codeExists)
            {
                return Results.Conflict(new
                {
                    Error = "Exercise code already exists."
                });
            }

            var exercise = new Exercise
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = request.Name.Trim(),
                Description = request.Description.Trim(),
                MuscleGroup = request.MuscleGroup.Trim(),
                Equipment = request.Equipment.Trim(),
                DifficultyLevel = request.DifficultyLevel.Trim(),
                TrainingLocation = request.TrainingLocation.Trim(),
                Instructions = request.Instructions.Trim(),
                VideoUrl = request.VideoUrl,
                GifUrl = request.GifUrl,
                IsActive = true,
                ReviewStatus = "Draft",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            db.Exercises.Add(exercise);

            await db.SaveChangesAsync(cancellationToken);

            return Results.Created(
                $"/api/exercises/{exercise.Id}",
                new ExerciseResponse
                {
                    Id = exercise.Id,
                    Code = exercise.Code,
                    Name = exercise.Name,
                    Description = exercise.Description,
                    MuscleGroup = exercise.MuscleGroup,
                    Equipment = exercise.Equipment,
                    DifficultyLevel = exercise.DifficultyLevel,
                    TrainingLocation = exercise.TrainingLocation,
                    Instructions = exercise.Instructions,
                    VideoUrl = exercise.VideoUrl,
                    GifUrl = exercise.GifUrl,
                    IsActive = exercise.IsActive,
                    ReviewStatus = exercise.ReviewStatus
                });
        })
        .WithName("CreateExercise");

        // UPDATE EXERCISE - ADMIN ONLY, DRAFT STATUS

        group.MapPut("/{id:guid}", async (
            Guid id,
            SaveExerciseRequest request,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            // STEP 1: Find existing exercise
            var exercise = await db.Exercises
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

            if (exercise is null)
            {
                return Results.NotFound(new
                {
                    Error = "Exercise not found."
                });
            }

            // STEP 2: Only Draft exercises can be updated
            if (exercise.ReviewStatus != "Draft")
            {
                return Results.Conflict(new
                {
                    Error = "Only Draft exercises can be updated."
                });
            }

            // STEP 3: Validate request
            var validationError = ExerciseValidator.Validate(request);

            if (validationError is not null)
            {
                return Results.BadRequest(new
                {
                    Error = validationError
                });
            }

            // STEP 4: Normalize exercise code
            var code = ExerciseValidator.NormalizeCode(request.Code);

            // STEP 5: Check duplicate code excluding current exercise
            var codeExists = await db.Exercises
                .AnyAsync(
                    x => x.Code == code && x.Id != id,
                    cancellationToken);

            if (codeExists)
            {
                return Results.Conflict(new
                {
                    Error = "Exercise code already exists."
                });
            }

            // STEP 6: Update exercise details
            exercise.Code = code;
            exercise.Name = request.Name.Trim();
            exercise.Description = request.Description.Trim();
            exercise.MuscleGroup = request.MuscleGroup.Trim();
            exercise.Equipment = request.Equipment.Trim();
            exercise.DifficultyLevel = request.DifficultyLevel.Trim();
            exercise.TrainingLocation = request.TrainingLocation.Trim();
            exercise.Instructions = request.Instructions.Trim();
            exercise.VideoUrl = request.VideoUrl;
            exercise.GifUrl = request.GifUrl;

            // Preserve ID, CreatedAtUtc, IsActive and ReviewStatus
            exercise.UpdatedAtUtc = DateTime.UtcNow;

            // STEP 7: Save changes
            await db.SaveChangesAsync(cancellationToken);

            // STEP 8: Return updated exercise
            return Results.Ok(new ExerciseResponse
            {
                Id = exercise.Id,
                Code = exercise.Code,
                Name = exercise.Name,
                Description = exercise.Description,
                MuscleGroup = exercise.MuscleGroup,
                Equipment = exercise.Equipment,
                DifficultyLevel = exercise.DifficultyLevel,
                TrainingLocation = exercise.TrainingLocation,
                Instructions = exercise.Instructions,
                VideoUrl = exercise.VideoUrl,
                GifUrl = exercise.GifUrl,
                IsActive = exercise.IsActive,
                ReviewStatus = exercise.ReviewStatus
            });
        })
        .WithName("UpdateExercise").AddEndpointFilter<SerializableTransactionFilter>();

        // GET EXERCISES WITH FILTERING AND PAGINATION
        group.MapGet("/", async (
            string? muscleGroup,
            string? equipment,
            string? difficultyLevel,
            string? trainingLocation,
            string? search,
            string? reviewStatus,
            bool? activeOnly,
            int? page,
            int? pageSize,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            int currentPage = page ?? 1;
            int currentPageSize = pageSize ?? 20;

            if (currentPage < 1)
            {
                return Results.BadRequest(new
                {
                    Error = "Page must be greater than zero."
                });
            }

            if (currentPageSize < 1 || currentPageSize > 100)
            {
                return Results.BadRequest(new
                {
                    Error = "PageSize must be between 1 and 100."
                });
            }

            var query = db.Exercises
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(muscleGroup))
            {
                string value = muscleGroup.Trim();

                query = query.Where(x => x.MuscleGroup == value);
            }

            if (!string.IsNullOrWhiteSpace(equipment))
            {
                string value = equipment.Trim();

                query = query.Where(x => x.Equipment == value);
            }

            if (!string.IsNullOrWhiteSpace(difficultyLevel))
            {
                string value = difficultyLevel.Trim();

                query = query.Where(x => x.DifficultyLevel == value);
            }

            if (!string.IsNullOrWhiteSpace(trainingLocation))
            {
                string value = trainingLocation.Trim();

                query = query.Where(x =>
                    x.TrainingLocation == value ||
                    x.TrainingLocation == "Both");
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string value = search.Trim();

                query = query.Where(x =>
                    x.Name.Contains(value) ||
                    x.Code.Contains(value));
            }

            if (activeOnly == true)
            {
                query = query.Where(x => x.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(reviewStatus))
            {
                string value = reviewStatus.Trim();

                query = query.Where(x => x.ReviewStatus == value);
            }

            int totalCount = await query.CountAsync(cancellationToken);

            var exercises = await query
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Id)
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
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

            var response = new PagedExerciseResponse
            {
                Page = currentPage,
                PageSize = currentPageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(
                    totalCount / (double)currentPageSize),
                Items = exercises
            };

            return Results.Ok(response);
        })
        .WithName("GetExercises");

        // SUBMIT EXERCISE FOR REVIEW
        group.MapPost("/{id:guid}/submit-review", async (
     Guid id,
     FitnessPlatformDbContext db,
     HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var exercise = await db.Exercises
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (exercise is null)
            {
                return Results.NotFound(new
                {
                    Error = "Exercise not found."
                });
            }

            if (!ExerciseReviewWorkflowValidator.CanSubmitForReview(exercise.ReviewStatus))
            {
                return Results.Conflict(new
                {
                    Error = "Only Draft exercises can be submitted for review."
                });
            }

            var previousStatus = exercise.ReviewStatus;
            var updatedAtUtc = DateTime.UtcNow;

            exercise.ReviewStatus = "In Review";
            exercise.UpdatedAtUtc = updatedAtUtc;

            db.ExerciseReviewAudits.Add(new ExerciseReviewAudit
            {
                ExerciseId = exercise.Id,
                PreviousStatus = previousStatus,
                NewStatus = "In Review",
                Action = "Submit",
                ActionSource = "Individual",
                PerformedByUserId = httpContext.User.FindFirstValue("sub"),
                PerformedAtUtc = updatedAtUtc
            });

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                exercise.Id,
                exercise.Code,
                exercise.Name,
                exercise.ReviewStatus,
                exercise.UpdatedAtUtc
            });
        })
        .WithName("SubmitExerciseForReview").AddEndpointFilter<SerializableTransactionFilter>();

        // APPROVE EXERCISE
        group.MapPut("/{id:guid}/approve", async (
            Guid id,
            FitnessPlatformDbContext db,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var exercise = await db.Exercises
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (exercise is null)
            {
                return Results.NotFound(new
                {
                    Error = "Exercise not found."
                });
            }

            if (!ExerciseReviewWorkflowValidator.CanApprove(exercise.ReviewStatus, exercise.IsActive))
            {
                if (exercise.ReviewStatus != "In Review")
                {
                    return Results.Conflict(new
                    {
                        Error = "Only exercises in review can be approved."
                    });
                }

                return Results.Conflict(new
                {
                    Error = "Inactive exercises cannot be approved."
                });
            }

            var previousStatus = exercise.ReviewStatus;
            var updatedAtUtc = DateTime.UtcNow;

            exercise.ReviewStatus = "Approved";
            exercise.UpdatedAtUtc = updatedAtUtc;

            db.ExerciseReviewAudits.Add(new ExerciseReviewAudit
            {
                ExerciseId = exercise.Id,
                PreviousStatus = previousStatus,
                NewStatus = "Approved",
                Action = "Approve",
                ActionSource = "Individual",
                PerformedByUserId = httpContext.User.FindFirstValue("sub"),
                PerformedAtUtc = updatedAtUtc
            });
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                exercise.Id,
                exercise.Code,
                exercise.Name,
                exercise.ReviewStatus,
                exercise.IsActive,
                exercise.UpdatedAtUtc
            });
        })
        .WithName("ApproveExercise").AddEndpointFilter<SerializableTransactionFilter>();

        // RETIRE EXERCISE
        group.MapPut("/{id:guid}/retire", async (
            Guid id,
            FitnessPlatformDbContext db,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var exercise = await db.Exercises
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (exercise is null)
            {
                return Results.NotFound(new
                {
                    Error = "Exercise not found."
                });
            }

            if (!ExerciseReviewWorkflowValidator.CanRetire(exercise.ReviewStatus))
            {
                return Results.Conflict(new
                {
                    Error = "Only Approved exercises can be retired."
                });
            }

            var previousStatus = exercise.ReviewStatus;
            var updatedAtUtc = DateTime.UtcNow;

            exercise.ReviewStatus = "Retired";
            exercise.IsActive = false;
            exercise.UpdatedAtUtc = updatedAtUtc;

            db.ExerciseReviewAudits.Add(new ExerciseReviewAudit
            {
                ExerciseId = exercise.Id,
                PreviousStatus = previousStatus,
                NewStatus = "Retired",
                Action = "Retire",
                ActionSource = "Individual",
                PerformedByUserId = httpContext.User.FindFirstValue("sub"),
                PerformedAtUtc = updatedAtUtc
            });

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                exercise.Id,
                exercise.Code,
                exercise.Name,
                exercise.ReviewStatus,
                exercise.IsActive,
                exercise.UpdatedAtUtc
            });
        })
        .WithName("RetireExercise").AddEndpointFilter<SerializableTransactionFilter>();

        // BULK SUBMIT EXERCISES FOR REVIEW
        group.MapPut("/bulk/submit-review", async (
            BulkExerciseReviewRequest request,
            FitnessPlatformDbContext db,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (request.ExerciseIds is null || request.ExerciseIds.Count == 0)
            {
                return Results.BadRequest(new
                {
                    Error = "At least one exercise ID is required."
                });
            }

            var ids = request.ExerciseIds.Distinct().ToList();

            var exercises = await db.Exercises
                .Where(x => ids.Contains(x.Id))
                .ToListAsync(cancellationToken);

            if (exercises.Count != ids.Count)
            {
                return Results.BadRequest(new
                {
                    Error = "One or more exercise IDs were not found."
                });
            }

            if (exercises.Any(x => !ExerciseReviewWorkflowValidator.CanBulkSubmitForReview(x.ReviewStatus, x.IsActive)))
            {
                return Results.Conflict(new
                {
                    Error = "All exercises must be active and in Draft status."
                });
            }

            var updatedAtUtc = DateTime.UtcNow;

            foreach (var exercise in exercises)
            {
                var previousStatus = exercise.ReviewStatus;

                exercise.ReviewStatus = "In Review";
                exercise.UpdatedAtUtc = updatedAtUtc;

                db.ExerciseReviewAudits.Add(new ExerciseReviewAudit
                {
                    ExerciseId = exercise.Id,
                    PreviousStatus = previousStatus,
                    NewStatus = "In Review",
                    Action = "Submit",
                    ActionSource = "Bulk",
                    PerformedByUserId = httpContext.User.FindFirstValue("sub"),
                    PerformedAtUtc = updatedAtUtc
                });
            }

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                UpdatedCount = exercises.Count,
                ReviewStatus = "In Review"
            });
        })
        .WithName("BulkSubmitExercisesForReview").AddEndpointFilter<SerializableTransactionFilter>();

        // BULK APPROVE EXERCISES
        group.MapPut("/bulk/approve", async (
            BulkExerciseReviewRequest request,
            FitnessPlatformDbContext db,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (request.ExerciseIds is null || request.ExerciseIds.Count == 0)
            {
                return Results.BadRequest(new
                {
                    Error = "At least one exercise ID is required."
                });
            }

            var ids = request.ExerciseIds.Distinct().ToList();

            var exercises = await db.Exercises
                .Where(x => ids.Contains(x.Id))
                .ToListAsync(cancellationToken);

            if (exercises.Count != ids.Count)
            {
                return Results.BadRequest(new
                {
                    Error = "One or more exercise IDs were not found."
                });
            }

            if (exercises.Any(x => !ExerciseReviewWorkflowValidator.CanBulkApprove(x.ReviewStatus, x.IsActive)))
            {
                return Results.Conflict(new
                {
                    Error = "All exercises must be active and in review before approval."
                });
            }

            var updatedAtUtc = DateTime.UtcNow;

            foreach (var exercise in exercises)
            {
                var previousStatus = exercise.ReviewStatus;

                exercise.ReviewStatus = "Approved";
                exercise.UpdatedAtUtc = updatedAtUtc;

                db.ExerciseReviewAudits.Add(new ExerciseReviewAudit
                {
                    ExerciseId = exercise.Id,
                    PreviousStatus = previousStatus,
                    NewStatus = "Approved",
                    Action = "Approve",
                    ActionSource = "Bulk",
                    PerformedByUserId = httpContext.User.FindFirstValue("sub"),
                    PerformedAtUtc = updatedAtUtc
                });
            }

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                UpdatedCount = exercises.Count,
                ReviewStatus = "Approved"
            });
        })
        .WithName("BulkApproveExercises").AddEndpointFilter<SerializableTransactionFilter>();

        // GET EXERCISE AUDIT HISTORY
        group.MapGet("/{id:guid}/audit-history", async (
            Guid id,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            var exerciseExists = await db.Exercises
                .AnyAsync(x => x.Id == id, cancellationToken);

            if (!exerciseExists)
            {
                return Results.NotFound(new
                {
                    Error = "Exercise not found."
                });
            }

            var history = await db.ExerciseReviewAudits
                .AsNoTracking()
                .Where(x => x.ExerciseId == id)
                .OrderByDescending(x => x.PerformedAtUtc)
                .ThenByDescending(x => x.Id)
                .Select(x => new ExerciseReviewAuditResponse
                {
                    Id = x.Id,
                    ExerciseId = x.ExerciseId,
                    PreviousStatus = x.PreviousStatus,
                    NewStatus = x.NewStatus,
                    Action = x.Action,
                    ActionSource = x.ActionSource,
                    PerformedByUserId = x.PerformedByUserId,
                    PerformedAtUtc = x.PerformedAtUtc
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(new
            {
                ExerciseId = id,
                TotalRecords = history.Count,
                History = history
            });
        })
        .WithName("GetExerciseAuditHistory");
        return app;
    }


}
