using Microsoft.EntityFrameworkCore;

using FitnessPlatform.Contracts.WorkoutPlans;
using FitnessPlatform.Domain.Entities;
using FitnessPlatform.Infrastructure.Persistence;
using FitnessPlatform.Application.WorkoutPlans;

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
                    RejectionReason = plan.RejectionReason,
                    CreatedAtUtc = plan.CreatedAtUtc,
                    UpdatedAtUtc = plan.UpdatedAtUtc
                });
        })
        .WithName("CreateWorkoutPlan");

        // UPDATE WORKOUT PLAN
        group.MapPut("/{planId:guid}", async (
            Guid planId,
            UpdateWorkoutPlanRequest request,
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

            if (!WorkoutPlanReviewWorkflowValidator.CanEdit(
                    plan.ReviewStatus,
                    plan.IsActive))
            {
                return Results.Conflict(new
                {
                    Error = "Only active Draft or Rejected workout plans can be edited."
                });
            }

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

            plan.Name = request.Name.Trim();
            plan.Description = request.Description?.Trim() ?? string.Empty;
            plan.GoalCode = request.GoalCode.Trim();
            plan.ExperienceLevel = request.ExperienceLevel.Trim();
            plan.TrainingLocation = request.TrainingLocation.Trim();
            plan.DaysPerWeek = request.DaysPerWeek;
            plan.DurationWeeks = request.DurationWeeks;
            plan.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new WorkoutPlanResponse
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
                RejectionReason = plan.RejectionReason,
                CreatedAtUtc = plan.CreatedAtUtc,
                UpdatedAtUtc = plan.UpdatedAtUtc
            });
        })
        .WithName("UpdateWorkoutPlan");
        // DEACTIVATE WORKOUT PLAN
        group.MapPatch("/{planId:guid}/deactivate", async (
            Guid planId,
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

            if (!plan.IsActive)
            {
                return Results.Conflict(new
                {
                    Error = "Workout plan is already inactive."
                });
            }

            plan.IsActive = false;
            plan.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                plan.Id,
                plan.IsActive,
                plan.UpdatedAtUtc
            });
        })
        .WithName("DeactivateWorkoutPlan");

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


            if (!WorkoutPlanReviewWorkflowValidator.CanEdit(
                    plan.ReviewStatus,
                    plan.IsActive))
            {
                return Results.Conflict(new
                {
                    Error = "Only active Draft or Rejected workout plans can be edited."
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

        // UPDATE WORKOUT DAY
        group.MapPut("/{planId:guid}/days/{dayId:guid}", async (
            Guid planId,
            Guid dayId,
            UpdateWorkoutPlanDayRequest request,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            // Step 1: Verify workout plan exists
            var plan = await db.WorkoutPlans
                .FindAsync(new object[] { planId }, cancellationToken);

            if (plan is null)
            {
                return Results.NotFound(new
                {
                    Error = "Workout plan not found."
                });
            }

            // Step 2: Verify workout plan can be edited
            if (!WorkoutPlanReviewWorkflowValidator.CanEdit(
                    plan.ReviewStatus,
                    plan.IsActive))
            {
                return Results.Conflict(new
                {
                    Error = "Only active Draft or Rejected workout plans can be edited."
                });
            }

            // Step 3: Verify workout day exists and belongs to plan
            var day = await db.WorkoutPlanDays
                .FindAsync(new object[] { dayId }, cancellationToken);

            if (day is null || day.WorkoutPlanId != planId)
            {
                return Results.NotFound(new
                {
                    Error = "Workout day not found."
                });
            }

            // Step 4: Validate day number
            if (request.DayNumber < 1 || request.DayNumber > 7)
            {
                return Results.BadRequest(new
                {
                    Error = "DayNumber must be between 1 and 7."
                });
            }

            // Step 5: Validate name and description
            if (string.IsNullOrWhiteSpace(request.Name) ||
                request.Name.Trim().Length > 200 ||
                (request.Description?.Length ?? 0) > 2000)
            {
                return Results.BadRequest(new
                {
                    Error = "Invalid workout day name or description."
                });
            }

            // Step 6: Validate estimated duration
            if (request.EstimatedDurationMinutes.HasValue &&
                (request.EstimatedDurationMinutes.Value < 0 ||
                 request.EstimatedDurationMinutes.Value > 300))
            {
                return Results.BadRequest(new
                {
                    Error = "Estimated duration must be between 0 and 300 minutes."
                });
            }

            // Step 7: Prevent duplicate day numbers
            var duplicateExists = await Microsoft.EntityFrameworkCore
                .EntityFrameworkQueryableExtensions.AnyAsync(
                    db.WorkoutPlanDays,
                    d => d.WorkoutPlanId == planId &&
                         d.DayNumber == request.DayNumber &&
                         d.Id != dayId,
                    cancellationToken);

            if (duplicateExists)
            {
                return Results.Conflict(new
                {
                    Error = "This day already exists in the workout plan."
                });
            }

            // Step 8: Update workout day
            day.DayNumber = request.DayNumber;
            day.Name = request.Name.Trim();
            day.Description = request.Description?.Trim() ?? string.Empty;
            day.IsRestDay = request.IsRestDay;
            day.EstimatedDurationMinutes =
                request.EstimatedDurationMinutes;

            plan.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            // Step 9: Return updated workout day
            return Results.Ok(new WorkoutPlanDayResponse
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
        .WithName("UpdateWorkoutPlanDay");

        // DELETE WORKOUT DAY
        group.MapDelete("/{planId:guid}/days/{dayId:guid}", async (
            Guid planId,
            Guid dayId,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            // Step 1: Verify workout plan exists
            var plan = await db.WorkoutPlans
                .FindAsync(new object[] { planId }, cancellationToken);

            if (plan is null)
            {
                return Results.NotFound(new
                {
                    Error = "Workout plan not found."
                });
            }

            // Step 2: Verify workout plan can be edited
            if (!WorkoutPlanReviewWorkflowValidator.CanEdit(
                    plan.ReviewStatus,
                    plan.IsActive))
            {
                return Results.Conflict(new
                {
                    Error = "Only active Draft or Rejected workout plans can be edited."
                });
            }

            // Step 3: Verify workout day exists and belongs to plan
            var day = await db.WorkoutPlanDays
                .FindAsync(new object[] { dayId }, cancellationToken);

            if (day is null || day.WorkoutPlanId != planId)
            {
                return Results.NotFound(new
                {
                    Error = "Workout day not found."
                });
            }

            // Step 4: Delete workout day
            // Related exercise assignments are deleted through EF Core cascade behavior.
            db.WorkoutPlanDays.Remove(day);

            // Step 5: Update parent workout plan timestamp
            plan.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            // Step 6: Return successful deletion
            return Results.NoContent();
        })
        .WithName("DeleteWorkoutPlanDay");

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


            if (!WorkoutPlanReviewWorkflowValidator.CanEdit(
                    plan.ReviewStatus,
                    plan.IsActive))
            {
                return Results.Conflict(new
                {
                    Error = "Only active Draft or Rejected workout plans can be edited."
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

        // UPDATE EXERCISE ASSIGNMENT IN WORKOUT DAY
        group.MapPut("/{planId:guid}/days/{dayId:guid}/exercises/{assignmentId:guid}", async (
            Guid planId,
            Guid dayId,
            Guid assignmentId,
            UpdateWorkoutPlanExerciseRequest request,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            // Step 1: Verify workout plan exists
            var plan = await db.WorkoutPlans
                .FindAsync(new object[] { planId }, cancellationToken);

            if (plan is null)
            {
                return Results.NotFound(new
                {
                    Error = "Workout plan not found."
                });
            }

            // Step 2: Verify workout plan is editable
            if (!WorkoutPlanReviewWorkflowValidator.CanEdit(
                    plan.ReviewStatus,
                    plan.IsActive))
            {
                return Results.Conflict(new
                {
                    Error = "Only active Draft or Rejected workout plans can be edited."
                });
            }

            // Step 3: Verify workout day belongs to the plan
            var day = await db.WorkoutPlanDays
                .FindAsync(new object[] { dayId }, cancellationToken);

            if (day is null || day.WorkoutPlanId != planId)
            {
                return Results.NotFound(new
                {
                    Error = "Workout day not found in this plan."
                });
            }

            // Step 4: Prevent exercise assignments on rest days
            if (day.IsRestDay)
            {
                return Results.BadRequest(new
                {
                    Error = "Cannot assign exercises to a rest day."
                });
            }

            // Step 5: Verify assignment belongs to the workout day
            var assignment = await db.WorkoutPlanExercises
                .FindAsync(new object[] { assignmentId }, cancellationToken);

            if (assignment is null || assignment.WorkoutPlanDayId != dayId)
            {
                return Results.NotFound(new
                {
                    Error = "Exercise assignment not found in this workout day."
                });
            }

            // Step 6: Validate request values
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

            // Step 7: Verify exercise exists in Exercise Master
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

            // Step 8: Check duplicate exercise order
            // Exclude the assignment currently being updated
            var orderExists =
                await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
                    db.WorkoutPlanExercises,
                    e => e.WorkoutPlanDayId == dayId &&
                         e.ExerciseOrder == request.ExerciseOrder &&
                         e.Id != assignmentId,
                    cancellationToken);

            if (orderExists)
            {
                return Results.Conflict(new
                {
                    Error = "Exercise order already exists for this workout day."
                });
            }

            // Step 9: Update existing assignment
            assignment.ExerciseId = request.ExerciseId;
            assignment.ExerciseOrder = request.ExerciseOrder;
            assignment.Sets = request.Sets;
            assignment.Reps = request.Reps;
            assignment.RestSeconds = request.RestSeconds;
            assignment.DurationSeconds = request.DurationSeconds;
            assignment.Notes = request.Notes?.Trim() ?? string.Empty;

            // Step 10: Update parent workout plan timestamp
            plan.UpdatedAtUtc = DateTime.UtcNow;

            // Step 11: Save changes
            await db.SaveChangesAsync(cancellationToken);

            // Step 12: Return updated assignment
            return Results.Ok(new WorkoutPlanExerciseResponse
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
        .WithName("UpdateWorkoutPlanExercise");
        // DELETE EXERCISE ASSIGNMENT FROM WORKOUT DAY
        group.MapDelete("/{planId:guid}/days/{dayId:guid}/exercises/{assignmentId:guid}", async (
            Guid planId,
            Guid dayId,
            Guid assignmentId,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            // Step 1: Verify workout plan exists
            var plan = await db.WorkoutPlans
                .FirstOrDefaultAsync(
                    p => p.Id == planId,
                    cancellationToken);

            if (plan is null)
            {
                return Results.NotFound();
            }

            // Step 2: Verify workout plan is editable
            if (!WorkoutPlanReviewWorkflowValidator.CanEdit(
                    plan.ReviewStatus,
                    plan.IsActive))
            {
                return Results.Conflict(
                    "Workout plan cannot be modified in its current review status.");
            }

            // Step 3: Verify workout day belongs to the plan
            var day = await db.WorkoutPlanDays
                .FirstOrDefaultAsync(
                    d => d.Id == dayId && d.WorkoutPlanId == planId,
                    cancellationToken);

            if (day is null)
            {
                return Results.NotFound();
            }

            // Step 4: Verify assignment belongs to the workout day
            var assignment = await db.WorkoutPlanExercises
                .FirstOrDefaultAsync(
                    e => e.Id == assignmentId && e.WorkoutPlanDayId == dayId,
                    cancellationToken);

            if (assignment is null)
            {
                return Results.NotFound();
            }

            // Step 5: Delete only the workout exercise assignment
            db.WorkoutPlanExercises.Remove(assignment);

            // Step 6: Update parent workout plan timestamp
            plan.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            // Step 7: Return success without response body
            return Results.NoContent();
        })
        .WithName("DeleteWorkoutPlanExercise");

        // REORDER EXERCISES WITHIN A WORKOUT DAY
        group.MapPut("/{planId:guid}/days/{dayId:guid}/exercises/reorder", async (
            Guid planId,
            Guid dayId,
            ReorderWorkoutPlanExercisesRequest request,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            // Step 1: Verify workout plan exists
            var plan = await db.WorkoutPlans
                .FindAsync(new object[] { planId }, cancellationToken);

            if (plan is null)
            {
                return Results.NotFound(new
                {
                    Error = "Workout plan not found."
                });
            }

            // Step 2: Verify workout plan is editable
            if (!WorkoutPlanReviewWorkflowValidator.CanEdit(
                    plan.ReviewStatus,
                    plan.IsActive))
            {
                return Results.Conflict(new
                {
                    Error = "Only active Draft or Rejected workout plans can be edited."
                });
            }

            // Step 3: Verify workout day belongs to the plan
            var day = await db.WorkoutPlanDays
                .FindAsync(new object[] { dayId }, cancellationToken);

            if (day is null || day.WorkoutPlanId != planId)
            {
                return Results.NotFound(new
                {
                    Error = "Workout day not found in this plan."
                });
            }

            // Step 4: Prevent exercise reordering on rest days
            if (day.IsRestDay)
            {
                return Results.BadRequest(new
                {
                    Error = "Cannot reorder exercises on a rest day."
                });
            }

            // Step 5: Validate request
            if (request?.Exercises is null || request.Exercises.Count == 0)
            {
                return Results.BadRequest(new
                {
                    Error = "At least one exercise assignment is required."
                });
            }

            var requestedItems = request.Exercises;

            if (requestedItems.Any(x =>
                    x is null ||
                    x.AssignmentId == Guid.Empty ||
                    x.ExerciseOrder < 1))
            {
                return Results.BadRequest(new
                {
                    Error = "Invalid exercise reorder values."
                });
            }

            // Step 6: Prevent duplicate assignment IDs
            if (requestedItems
                .Select(x => x.AssignmentId)
                .Distinct()
                .Count() != requestedItems.Count)
            {
                return Results.BadRequest(new
                {
                    Error = "Duplicate exercise assignment IDs are not allowed."
                });
            }

            // Step 7: Validate consecutive positions starting from 1
            var requestedOrders = requestedItems
                .Select(x => x.ExerciseOrder)
                .OrderBy(x => x)
                .ToArray();

            var expectedOrders = Enumerable
                .Range(1, requestedItems.Count)
                .ToArray();

            if (!requestedOrders.SequenceEqual(expectedOrders))
            {
                return Results.BadRequest(new
                {
                    Error = "Exercise positions must be unique and consecutive starting from 1."
                });
            }

            // Step 8: Retrieve all assignments belonging to this workout day
            var assignments = await db.WorkoutPlanExercises
                .Where(x => x.WorkoutPlanDayId == dayId)
                .ToListAsync(cancellationToken);

            // Step 9: Require the complete assignment list
            var existingIds = assignments
                .Select(x => x.Id)
                .ToHashSet();

            var requestedIds = requestedItems
                .Select(x => x.AssignmentId)
                .ToHashSet();

            if (!existingIds.SetEquals(requestedIds))
            {
                return Results.BadRequest(new
                {
                    Error = "Reorder request must contain every exercise assignment belonging to this workout day."
                });
            }

            // Step 10: Start transaction
            await using var transaction =
                await db.Database.BeginTransactionAsync(cancellationToken);

            // Step 11: Move assignments to temporary unused positions
            // Negative positions avoid collisions with existing positive positions.

            for (var i = 0; i < assignments.Count; i++)
            {
                assignments[i].ExerciseOrder = -(i + 1);
            }

            await db.SaveChangesAsync(cancellationToken);

            // Step 12: Apply requested exercise positions
            var requestedOrderById = requestedItems.ToDictionary(
                x => x.AssignmentId,
                x => x.ExerciseOrder);

            foreach (var assignment in assignments)
            {
                assignment.ExerciseOrder = requestedOrderById[assignment.Id];
            }

            // Step 13: Update parent plan timestamp
            plan.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            // Step 14: Commit transaction
            await transaction.CommitAsync(cancellationToken);

            // Step 15: Return reordered assignments
            var response = assignments
                .OrderBy(x => x.ExerciseOrder)
                .Select(x => new WorkoutPlanExerciseResponse
                {
                    Id = x.Id,
                    WorkoutPlanDayId = x.WorkoutPlanDayId,
                    ExerciseId = x.ExerciseId,
                    ExerciseOrder = x.ExerciseOrder,
                    Sets = x.Sets,
                    Reps = x.Reps,
                    RestSeconds = x.RestSeconds,
                    DurationSeconds = x.DurationSeconds,
                    Notes = x.Notes
                })
                .ToList();

            return Results.Ok(response);
        })
        .WithName("ReorderWorkoutPlanExercises");


        // GET WORKOUT PLAN DETAILS WITH DAYS AND EXERCISES
        group.MapGet("/{planId:guid}", async (
            Guid planId,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            var plan = await db.WorkoutPlans
                .AsNoTracking()
                .Where(p => p.Id == planId)
                .Select(p => new WorkoutPlanDetailResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    GoalCode = p.GoalCode,
                    ExperienceLevel = p.ExperienceLevel,
                    TrainingLocation = p.TrainingLocation,
                    DaysPerWeek = p.DaysPerWeek,
                    DurationWeeks = p.DurationWeeks,
                    IsActive = p.IsActive,
                    ReviewStatus = p.ReviewStatus,
                    RejectionReason = p.RejectionReason,
                    CreatedAtUtc = p.CreatedAtUtc,
                    UpdatedAtUtc = p.UpdatedAtUtc,

                    Days = db.WorkoutPlanDays
                        .Where(d => d.WorkoutPlanId == p.Id)
                        .OrderBy(d => d.DayNumber)
                        .Select(d => new WorkoutPlanDayDetailResponse
                        {
                            Id = d.Id,
                            WorkoutPlanId = d.WorkoutPlanId,
                            DayNumber = d.DayNumber,
                            Name = d.Name,
                            Description = d.Description,
                            IsRestDay = d.IsRestDay,
                            EstimatedDurationMinutes = d.EstimatedDurationMinutes,

                            Exercises = db.WorkoutPlanExercises
                                .Where(e => e.WorkoutPlanDayId == d.Id)
                                .OrderBy(e => e.ExerciseOrder)
                                .Select(e => new WorkoutPlanExerciseDetailResponse
                                {
                                    Id = e.Id,
                                    WorkoutPlanDayId = e.WorkoutPlanDayId,
                                    ExerciseId = e.ExerciseId,
                                    ExerciseOrder = e.ExerciseOrder,
                                    Sets = e.Sets,
                                    Reps = e.Reps,
                                    RestSeconds = e.RestSeconds,
                                    DurationSeconds = e.DurationSeconds,
                                    Notes = e.Notes,

                                    ExerciseCode = db.Exercises
                                        .Where(x => x.Id == e.ExerciseId)
                                        .Select(x => x.Code)
                                        .FirstOrDefault() ?? string.Empty,

                                    ExerciseName = db.Exercises
                                        .Where(x => x.Id == e.ExerciseId)
                                        .Select(x => x.Name)
                                        .FirstOrDefault() ?? string.Empty
                                })
                                .ToList()
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (plan is null)
            {
                return Results.NotFound(new
                {
                    Error = "Workout plan not found."
                });
            }

            return Results.Ok(plan);
        })
        .WithName("GetWorkoutPlanDetails");



        // SUBMIT WORKOUT PLAN FOR REVIEW
        group.MapPost("/{planId:guid}/submit-for-review", async (
            Guid planId,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            // Step 1: Find workout plan
            var plan = await db.WorkoutPlans
                .FindAsync(new object[] { planId }, cancellationToken);

            if (plan is null)
            {
                return Results.NotFound(new
                {
                    Error = "Workout plan not found."
                });
            }

            // Step 2: Validate review status
            if (plan.ReviewStatus != "Draft" &&
                plan.ReviewStatus != "Rejected")
            {
                return Results.Conflict(new
                {
                    Error = "Only Draft or Rejected workout plans can be submitted for review."
                });
            }

            // Step 3: Validate active status
            if (!WorkoutPlanReviewWorkflowValidator.CanSubmitForReview(
                    plan.ReviewStatus,
                    plan.IsActive))
            {
                return Results.BadRequest(new
                {
                    Error = "Inactive workout plans cannot be submitted for review."
                });
            }

            // Step 4: Retrieve workout days
            var days = await db.WorkoutPlanDays
                .AsNoTracking()
                .Where(d => d.WorkoutPlanId == planId)
                .ToListAsync(cancellationToken);

            if (days.Count == 0)
            {
                return Results.BadRequest(new
                {
                    Error = "Workout plan must contain at least one workout day."
                });
            }

            // Step 5: Validate every non-rest day
            var workoutDayIds = days
                .Where(d => !d.IsRestDay)
                .Select(d => d.Id)
                .ToList();

            if (workoutDayIds.Count == 0)
            {
                return Results.BadRequest(new
                {
                    Error = "Workout plan must contain at least one training day."
                });
            }

            var daysWithExercises = await db.WorkoutPlanExercises
                .AsNoTracking()
                .Where(e => workoutDayIds.Contains(e.WorkoutPlanDayId))
                .Select(e => e.WorkoutPlanDayId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (workoutDayIds.Except(daysWithExercises).Any())
            {
                return Results.BadRequest(new
                {
                    Error = "Every non-rest workout day must contain at least one exercise."
                });
            }

            // Step 6: Update review status
            plan.ReviewStatus = "In Review";

            // Clear previous rejection reason after successful validation
            plan.RejectionReason = null;

            plan.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            // Step 7: Return response
            return Results.Ok(new
            {
                plan.Id,
                plan.Name,
                plan.ReviewStatus,
                plan.IsActive,
                plan.UpdatedAtUtc,
                Message = "Workout plan submitted for review successfully."
            });
        })
        .WithName("SubmitWorkoutPlanForReview");

        // APPROVE WORKOUT PLAN
        group.MapPost("/{planId:guid}/approve", async (
            Guid planId,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            // Step 1: Find workout plan
            var plan = await db.WorkoutPlans
                .FindAsync(new object[] { planId }, cancellationToken);

            if (plan is null)
            {
                return Results.NotFound(new
                {
                    Error = "Workout plan not found."
                });
            }


            
            // Step 2: Validate review status
            if (plan.ReviewStatus != "In Review")
            {
                return Results.Conflict(new
                {
                    Error = "Only workout plans under review can be approved."
                });
            }

            // Step 3: Validate active status
            if (!WorkoutPlanReviewWorkflowValidator.CanApprove(
                    plan.ReviewStatus,
                    plan.IsActive))
            {
                return Results.BadRequest(new
                {
                    Error = "Inactive workout plans cannot be approved."
                });
            }

            // Step 4: Approve workout plan
            plan.ReviewStatus = "Approved";
            plan.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            // Step 5: Return response
            return Results.Ok(new
            {
                plan.Id,
                plan.Name,
                plan.ReviewStatus,
                plan.IsActive,
                plan.UpdatedAtUtc,
                Message = "Workout plan approved successfully."
            });
        })
        .WithName("ApproveWorkoutPlan");

        // REJECT WORKOUT PLAN
        group.MapPost("/{planId:guid}/reject", async (
            Guid planId,
            RejectWorkoutPlanRequest request,
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

            if (!WorkoutPlanReviewWorkflowValidator.CanReject(
                    plan.ReviewStatus))
            {
                return Results.Conflict(new
                {
                    Error = "Only workout plans under review can be rejected."
                });
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.BadRequest(new
                {
                    Error = "A rejection reason is required."
                });
            }

            if (request.Reason.Trim().Length > 1000)
            {
                return Results.BadRequest(new
                {
                    Error = "Rejection reason cannot exceed 1000 characters."
                });
            }

            plan.ReviewStatus = "Rejected";
            plan.RejectionReason = request.Reason.Trim();
            plan.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                plan.Id,
                plan.Name,
                plan.ReviewStatus,
                plan.RejectionReason,
                plan.UpdatedAtUtc,
                Message = "Workout plan rejected successfully."
            });
        })
        .WithName("RejectWorkoutPlan");

        // REORDER WORKOUT DAYS WITHIN A WORKOUT PLAN
        group.MapPut("/{planId:guid}/days/reorder", async (
            Guid planId,
            ReorderWorkoutPlanDaysRequest request,
            FitnessPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            // Step 1: Verify workout plan exists
            var plan = await db.WorkoutPlans.FindAsync(
                new object[] { planId },
                cancellationToken);

            if (plan is null)
            {
                return Results.NotFound(new
                {
                    Error = "Workout plan not found."
                });
            }

            // Step 2: Verify workout plan is editable
            if (!WorkoutPlanReviewWorkflowValidator.CanEdit(
                plan.ReviewStatus,
                plan.IsActive))
            {
                return Results.Conflict(new
                {
                    Error = "Only active Draft or Rejected workout plans can be edited."
                });
            }

            // Step 3: Validate request
            if (request?.Days is null || request.Days.Count == 0)
            {
                return Results.BadRequest(new
                {
                    Error = "At least one workout day is required."
                });
            }

            var requestedDays = request.Days;

            // Step 4: Validate day IDs and day numbers
            if (requestedDays.Any(x =>
                x is null ||
                x.DayId == Guid.Empty ||
                x.DayNumber < 1))
            {
                return Results.BadRequest(new
                {
                    Error = "Invalid workout day reorder values."
                });
            }

            // Step 5: Reject duplicate workout day IDs
            if (requestedDays
                .Select(x => x.DayId)
                .Distinct()
                .Count() != requestedDays.Count)
            {
                return Results.BadRequest(new
                {
                    Error = "Duplicate workout day IDs are not allowed."
                });
            }

            // Step 6: Verify day numbers are consecutive
            var requestedNumbers = requestedDays
                .Select(x => x.DayNumber)
                .OrderBy(x => x)
                .ToArray();

            var expectedNumbers = Enumerable
                .Range(1, requestedDays.Count)
                .ToArray();

            if (!requestedNumbers.SequenceEqual(expectedNumbers))
            {
                return Results.BadRequest(new
                {
                    Error = "Workout day numbers must be unique and consecutive starting from 1."
                });
            }

            // Step 7: Retrieve existing workout days
            var days = await db.WorkoutPlanDays
                .Where(x => x.WorkoutPlanId == planId)
                .ToListAsync(cancellationToken);

            // Step 8: Ensure all workout days belong to this plan
            var existingIds = days
                .Select(x => x.Id)
                .ToHashSet();

            var requestedIds = requestedDays
                .Select(x => x.DayId)
                .ToHashSet();

            if (!existingIds.SetEquals(requestedIds))
            {
                return Results.BadRequest(new
                {
                    Error = "Reorder request must contain every workout day belonging to this workout plan."
                });
            }

            // Step 9: Begin transaction
            await using var transaction =
                await db.Database.BeginTransactionAsync(cancellationToken);

            // Step 10: Assign temporary negative positions
            // This prevents collisions with the unique index
            // (WorkoutPlanId, DayNumber).
            for (var i = 0; i < days.Count; i++)
            {
                days[i].DayNumber = -(i + 1);
            }

            await db.SaveChangesAsync(cancellationToken);

            // Step 11: Apply requested day numbers
            var requestedNumberById = requestedDays
                .ToDictionary(x => x.DayId, x => x.DayNumber);

            foreach (var day in days)
            {
                day.DayNumber = requestedNumberById[day.Id];
            }

            // Step 12: Update workout plan timestamp
            plan.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            // Step 13: Commit transaction
            await transaction.CommitAsync(cancellationToken);

            // Step 14: Return updated workout days
            var response = days
                .OrderBy(x => x.DayNumber)
                .Select(x => new WorkoutPlanDayResponse
                {
                    Id = x.Id,
                    WorkoutPlanId = x.WorkoutPlanId,
                    DayNumber = x.DayNumber,
                    Name = x.Name,
                    Description = x.Description,
                    IsRestDay = x.IsRestDay,
                    EstimatedDurationMinutes = x.EstimatedDurationMinutes
                })
                .ToList();

            return Results.Ok(response);
        })
        .WithName("ReorderWorkoutPlanDays");

        return app;
    }
}

