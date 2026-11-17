
using FitnessPlatform.Contracts.WorkoutPlans;
using FitnessPlatform.Domain.Entities;
using FitnessPlatform.Infrastructure.Persistence;

namespace FitnessPlatform.Api.Endpoints;

public static class WorkoutPlanEndpoints
{
    public static IEndpointRouteBuilder MapWorkoutPlanEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/workout-plans")
            .WithTags("Workout Plans")
            .RequireAuthorization(policy =>
                policy.RequireRole("Admin"));

        // CREATE WORKOUT PLAN
        group.MapPost("/", async (
            CreateWorkoutPlanRequest request,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (request is null ||
                string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.GoalCode) ||
                string.IsNullOrWhiteSpace(request.ExperienceLevel) ||
                string.IsNullOrWhiteSpace(request.TrainingLocation))
            {
                return Results.BadRequest(new
                {
                    Error = "Name, GoalCode, ExperienceLevel and TrainingLocation are required."
                });
            }

            if (request.Name.Trim().Length > 200 ||
                (request.Description?.Length ?? 0) > 2000 ||
                request.GoalCode.Trim().Length > 50 ||
                request.ExperienceLevel.Trim().Length > 50 ||
                request.TrainingLocation.Trim().Length > 50)
            {
                return Results.BadRequest(new
                {
                    Error = "One or more fields exceed the allowed length."
                });
            }

            if (request.DaysPerWeek < 1 ||
                request.DaysPerWeek > 7)
            {
                return Results.BadRequest(new
                {
                    Error = "DaysPerWeek must be between 1 and 7."
                });
            }

            if (request.DurationWeeks < 1 ||
                request.DurationWeeks > 52)
            {
                return Results.BadRequest(new
                {
                    Error = "DurationWeeks must be between 1 and 52."
                });
            }

            var now = DateTime.UtcNow;

            var plan = new WorkoutPlan
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Description = request.Description?.Trim() ?? string.Empty,
                GoalCode = request.GoalCode.Trim(),
                ExperienceLevel = request.ExperienceLevel.Trim(),
                TrainingLocation = request.TrainingLocation.Trim(),
                DaysPerWeek = request.DaysPerWeek,
                DurationWeeks = request.DurationWeeks,
                IsActive = true,
                ReviewStatus = "Draft",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            db.WorkoutPlans.Add(plan);

            await db.SaveChangesAsync(cancellationToken);

            return Results.Created(
                $"/api/admin/workout-plans/{plan.Id}",
                new WorkoutPlanResponse
                {
                    Id = plan.Id,
                    Name = plan.Name,
                    Description = plan.Description,
                    GoalCode = plan.GoalCode,
                    ExperienceLevel = plan.ExperienceLevel,
                    TrainingLocation = plan.TrainingLocation,
                    DaysPerWeek = plan.DaysPerWeek,
                    DurationWeeks = plan.DurationWeeks,
                    IsActive = plan.IsActive,
                    ReviewStatus = plan.ReviewStatus,
                    CreatedAtUtc = plan.CreatedAtUtc,
                    UpdatedAtUtc = plan.UpdatedAtUtc
                });
        })
        .WithName("CreateWorkoutPlan");



        // ADD WORKOUT DAY
        group.MapPost("/{planId:guid}/days", async (
            Guid planId,
            CreateWorkoutPlanDayRequest request,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            var plan = await db.WorkoutPlans
                .FindAsync(new object[] { planId }, cancellationToken);

            if (plan is null)
            {
                return Results.NotFound(new
                {
                    Error = "Workout plan not found."
                });
            }

            if (request.DayNumber < 1 ||
                request.DayNumber > 7)
            {
                return Results.BadRequest(new
                {
                    Error = "DayNumber must be between 1 and 7."
                });
            }

            if (string.IsNullOrWhiteSpace(request.Name) ||
                request.Name.Trim().Length > 200 ||
                (request.Description?.Length ?? 0) > 2000)
            {
                return Results.BadRequest(new
                {
                    Error = "Invalid workout day name or description."
                });
            }

            if (request.EstimatedDurationMinutes.HasValue &&
                (request.EstimatedDurationMinutes.Value < 0 ||
                 request.EstimatedDurationMinutes.Value > 300))
            {
                return Results.BadRequest(new
                {
                    Error = "Estimated duration must be between 0 and 300 minutes."
                });
            }


            var exists = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
                db.WorkoutPlanDays,
                d => d.WorkoutPlanId == planId &&
                     d.DayNumber == request.DayNumber,
                cancellationToken);


            if (exists)
            {
                return Results.Conflict(new
                {
                    Error = "This day already exists in the workout plan."
                });
            }

            var day = new WorkoutPlanDay
            {
                Id = Guid.NewGuid(),
                WorkoutPlanId = planId,
                DayNumber = request.DayNumber,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim() ?? string.Empty,
                IsRestDay = request.IsRestDay,
                EstimatedDurationMinutes =
                    request.EstimatedDurationMinutes
            };

            db.WorkoutPlanDays.Add(day);
            plan.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            return Results.Created(
                $"/api/admin/workout-plans/{planId}/days/{day.Id}",
                new WorkoutPlanDayResponse
                {
                    Id = day.Id,
                    WorkoutPlanId = day.WorkoutPlanId,
                    DayNumber = day.DayNumber,
                    Name = day.Name,
                    Description = day.Description,
                    IsRestDay = day.IsRestDay,
                    EstimatedDurationMinutes =
                        day.EstimatedDurationMinutes
                });
        })
        .WithName("AddWorkoutPlanDay");



        // ADD EXERCISE TO WORKOUT DAY
        group.MapPost("/{planId:guid}/days/{dayId:guid}/exercises", async (
            Guid planId,
            Guid dayId,
            AddWorkoutPlanExerciseRequest request,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            var plan = await db.WorkoutPlans
                .FindAsync(new object[] { planId }, cancellationToken);

            if (plan is null)
            {
                return Results.NotFound(new
                {
                    Error = "Workout plan not found."
                });
            }

            var day = await db.WorkoutPlanDays
                .FindAsync(new object[] { dayId }, cancellationToken);

            if (day is null || day.WorkoutPlanId != planId)
            {
                return Results.NotFound(new
                {
                    Error = "Workout day not found in this plan."
                });
            }

            if (day.IsRestDay)
            {
                return Results.BadRequest(new
                {
                    Error = "Cannot assign exercises to a rest day."
                });
            }

            if (request.ExerciseId == Guid.Empty ||
                request.ExerciseOrder < 1 ||
                request.Sets < 1 ||
                request.Sets > 20 ||
                request.Reps < 0 ||
                request.Reps > 500 ||
                request.RestSeconds < 0 ||
                request.RestSeconds > 3600 ||
                (request.DurationSeconds.HasValue &&
                 (request.DurationSeconds.Value < 1 ||
                  request.DurationSeconds.Value > 14400)) ||
                (request.Notes?.Length ?? 0) > 2000)
            {
                return Results.BadRequest(new
                {
                    Error = "Invalid exercise assignment values."
                });
            }

            var exerciseExists =
                await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
                    db.Exercises,
                    e => e.Id == request.ExerciseId,
                    cancellationToken);

            if (!exerciseExists)
            {
                return Results.NotFound(new
                {
                    Error = "Exercise not found in Exercise Master."
                });
            }

            var orderExists =
                await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
                    db.WorkoutPlanExercises,
                    e => e.WorkoutPlanDayId == dayId &&
                         e.ExerciseOrder == request.ExerciseOrder,
                    cancellationToken);

            if (orderExists)
            {
                return Results.Conflict(new
                {
                    Error = "Exercise order already exists for this workout day."
                });
            }

            var assignment = new WorkoutPlanExercise
            {
                Id = Guid.NewGuid(),
                WorkoutPlanDayId = dayId,
                ExerciseId = request.ExerciseId,
                ExerciseOrder = request.ExerciseOrder,
                Sets = request.Sets,
                Reps = request.Reps,
                RestSeconds = request.RestSeconds,
                DurationSeconds = request.DurationSeconds,
                Notes = request.Notes?.Trim() ?? string.Empty
            };

            db.WorkoutPlanExercises.Add(assignment);
            plan.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            return Results.Created(
                $"/api/admin/workout-plans/{planId}/days/{dayId}/exercises/{assignment.Id}",
                new WorkoutPlanExerciseResponse
                {
                    Id = assignment.Id,
                    WorkoutPlanDayId = assignment.WorkoutPlanDayId,
                    ExerciseId = assignment.ExerciseId,
                    ExerciseOrder = assignment.ExerciseOrder,
                    Sets = assignment.Sets,
                    Reps = assignment.Reps,
                    RestSeconds = assignment.RestSeconds,
                    DurationSeconds = assignment.DurationSeconds,
                    Notes = assignment.Notes
                });
        })
        .WithName("AddWorkoutPlanExercise");


        return app;
    }
}
