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

        return app;
    }
}

