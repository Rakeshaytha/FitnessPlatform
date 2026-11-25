using FitnessPlatform.Contracts.MemberWorkoutPlanAssignments;
using FitnessPlatform.Domain.Entities;
using FitnessPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Data;
using FitnessPlatform.Contracts.WorkoutPlans;
using FitnessPlatform.Api.Filters;
namespace FitnessPlatform.Api.Endpoints;

public static class MemberWorkoutPlanAssignmentEndpoints
{
    // ============================================================
    // Step 10.30.6.1
    // Retrieve the authenticated member's active workout assignment.
    // ============================================================

    private static async Task<IResult> GetMemberActiveAssignmentAsync(
        FitnessPlatformDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // 1. Retrieve the authenticated user ID from JWT.
        var userId = GetAuthenticatedUserId(httpContext);

        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        // 2. Retrieve the authenticated member's profile.
        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId.Value,
                cancellationToken);

        if (profile == null)
        {
            return Results.NotFound(new
            {
                Error = "Member profile not found."
            });
        }

        // 3. Retrieve the member's active assignment.
        var assignment = await db.MemberWorkoutPlanAssignments
            .AsNoTracking()
            .Include(x => x.WorkoutPlan)
            .SingleOrDefaultAsync(
                x => x.UserProfileId == profile.Id
                    && x.Status == "Active",
                cancellationToken);

        // 4. Verify that an active assignment exists.
        if (assignment == null)
        {
            return Results.NotFound(new
            {
                Error = "You do not have an active workout plan assignment."
            });
        }

        // 5. Return member-facing assignment information.
        // 5. Return strongly typed member-facing assignment information.
        return Results.Ok(MapMemberAssignmentResponse(assignment));
    }

    // ============================================================
    // Step 10.30.6.3.4
    // Retrieve the authenticated member's complete assigned workout plan.
    // ============================================================

    private static async Task<IResult> GetMemberActiveWorkoutPlanDetailsAsync(
        FitnessPlatformDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // 1. Retrieve authenticated user ID from JWT.
        var userId = GetAuthenticatedUserId(httpContext);

        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        // 2. Retrieve the authenticated member's profile.
        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId.Value,
                cancellationToken);

        if (profile == null)
        {
            return Results.NotFound(new
            {
                Error = "Member profile not found."
            });
        }

        // 3. Retrieve the active assignment and exact workout plan version.
        var assignment = await db.MemberWorkoutPlanAssignments
            .AsNoTracking()
            .Include(x => x.WorkoutPlan)
            .SingleOrDefaultAsync(
                x => x.UserProfileId == profile.Id
                    && x.Status == "Active",
                cancellationToken);

        if (assignment == null)
        {
            return Results.NotFound(new
            {
                Error = "You do not have an active workout plan assignment."
            });
        }

        // 4. Retrieve workout days belonging to the assigned version.
        // Include configured exercises and their Exercise master records.
        var workoutDays = await db.Set<WorkoutPlanDay>()
            .AsNoTracking()
            .Where(x => x.WorkoutPlanId == assignment.WorkoutPlanId)
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Exercise)
            .OrderBy(x => x.DayNumber)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        // 5. Map the complete workout plan response.
        var response = MapMemberWorkoutPlanDetailResponse(
            assignment,
            workoutDays);

        // 6. Return the complete assigned workout plan.
        return Results.Ok(response);
    }

    // ============================================================
    // Step 10.30.6.4.1
    // Retrieve all workout days from the member's assigned plan.
    // ============================================================

    private static async Task<IResult> GetMemberActiveWorkoutDaysAsync(
        FitnessPlatformDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // 1. Retrieve authenticated user ID.
        var userId = GetAuthenticatedUserId(httpContext);

        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        // 2. Retrieve authenticated member's profile.
        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId.Value,
                cancellationToken);

        if (profile == null)
        {
            return Results.NotFound(new
            {
                Error = "Member profile not found."
            });
        }

        // 3. Retrieve the member's active assignment.
        var assignment = await db.MemberWorkoutPlanAssignments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserProfileId == profile.Id
                    && x.Status == "Active",
                cancellationToken);

        if (assignment == null)
        {
            return Results.NotFound(new
            {
                Error = "You do not have an active workout plan assignment."
            });
        }

        // 4. Retrieve workout days from the exact assigned version.
        var workoutDays = await db.Set<WorkoutPlanDay>()
            .AsNoTracking()
            .Where(x => x.WorkoutPlanId == assignment.WorkoutPlanId)
            .OrderBy(x => x.DayNumber)
            .ThenBy(x => x.Id)
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
            .ToListAsync(cancellationToken);

        // 5. Return the ordered workout schedule.
        return Results.Ok(workoutDays);
    }


    // ============================================================
    // Step 10.30.5.6
    // Retrieve the complete workout plan assignment history.
    // ============================================================

    private static async Task<IResult> GetAdminAssignmentHistoryAsync(
        Guid profileId,
        FitnessPlatformDbContext db,
        CancellationToken cancellationToken)
    {
        // 1. Validate member profile ID.
        if (profileId == Guid.Empty)
        {
            return Results.BadRequest(new
            {
                Error = "A valid member profile ID is required."
            });
        }

        // 2. Verify that the member profile exists.
        var profileExists = await db.UserProfiles
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == profileId,
                cancellationToken);

        if (!profileExists)
        {
            return Results.NotFound(new
            {
                Error = "Member profile not found."
            });
        }

        // 3. Retrieve all assignments with their exact plan versions.
        var assignments = await db.MemberWorkoutPlanAssignments
            .AsNoTracking()
            .Include(x => x.WorkoutPlan)
            .Where(x => x.UserProfileId == profileId)
            .OrderByDescending(x => x.AssignedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        // 4. Map assignments to response contracts.
        var assignmentResponses = assignments
            .Select(MapAssignmentResponse)
            .ToList();

        // 5. Identify the currently active assignment.
        var activeAssignment = assignmentResponses
            .SingleOrDefault(x => x.Status == "Active");

        // 6. Prepare the complete history response.
        var response = new MemberWorkoutPlanAssignmentHistoryResponse
        {
            UserProfileId = profileId,
            TotalAssignments = assignmentResponses.Count,
            ActiveAssignment = activeAssignment,
            Assignments = assignmentResponses
        };

        // 7. Return assignment history.
        return Results.Ok(response);
    }

    // ============================================================
    // Step 10.30.6.4.2
    // Retrieve a specific workout day and its configured exercises.
    // ============================================================

    private static async Task<IResult> GetMemberActiveWorkoutDayAsync(
        int dayNumber,
        FitnessPlatformDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // 1. Retrieve authenticated user ID.
        var userId = GetAuthenticatedUserId(httpContext);

        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        // 2. Validate the requested day number.
        if (dayNumber <= 0)
        {
            return Results.BadRequest(new
            {
                Error = "Workout day number must be greater than zero."
            });
        }

        // 3. Retrieve the authenticated member's profile.
        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId.Value,
                cancellationToken);

        if (profile == null)
        {
            return Results.NotFound(new
            {
                Error = "Member profile not found."
            });
        }

        // 4. Retrieve the member's active assignment.
        var assignment = await db.MemberWorkoutPlanAssignments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserProfileId == profile.Id
                    && x.Status == "Active",
                cancellationToken);

        if (assignment == null)
        {
            return Results.NotFound(new
            {
                Error = "You do not have an active workout plan assignment."
            });
        }

        // 5. Retrieve the requested day from the exact assigned version.
        var workoutDay = await db.Set<WorkoutPlanDay>()
            .AsNoTracking()
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Exercise)
            .SingleOrDefaultAsync(
                x => x.WorkoutPlanId == assignment.WorkoutPlanId
                    && x.DayNumber == dayNumber,
                cancellationToken);

        if (workoutDay == null)
        {
            return Results.NotFound(new
            {
                Error = "The requested workout day was not found in your assigned workout plan."
            });
        }

        // 6. Map workout day and ordered exercise details.
        var response = MapWorkoutDayDetailResponse(workoutDay);

        // 7. Return the requested workout day.
        return Results.Ok(response);
    }


    // ============================================================
    // Step 10.30.6.5.1
    // Retrieve all exercises for a specific assigned workout day.
    // ============================================================

    private static async Task<IResult> GetMemberActiveWorkoutDayExercisesAsync(
        int dayNumber,
        FitnessPlatformDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // 1. Retrieve authenticated user ID.
        var userId = GetAuthenticatedUserId(httpContext);

        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        // 2. Validate workout day number.
        if (dayNumber <= 0)
        {
            return Results.BadRequest(new
            {
                Error = "Workout day number must be greater than zero."
            });
        }

        // 3. Retrieve authenticated member's profile.
        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId.Value,
                cancellationToken);

        if (profile == null)
        {
            return Results.NotFound(new
            {
                Error = "Member profile not found."
            });
        }

        // 4. Retrieve the member's active assignment.
        var assignment = await db.MemberWorkoutPlanAssignments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserProfileId == profile.Id
                    && x.Status == "Active",
                cancellationToken);

        if (assignment == null)
        {
            return Results.NotFound(new
            {
                Error = "You do not have an active workout plan assignment."
            });
        }

        // 5. Retrieve the requested workout day
        // from the exact assigned workout plan version.
        var workoutDay = await db.Set<WorkoutPlanDay>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.WorkoutPlanId == assignment.WorkoutPlanId
                    && x.DayNumber == dayNumber,
                cancellationToken);

        if (workoutDay == null)
        {
            return Results.NotFound(new
            {
                Error = "The requested workout day was not found in your assigned workout plan."
            });
        }

        // 6. Retrieve configured exercises and Exercise master details.
        var exercises = await db.Set<WorkoutPlanExercise>()
            .AsNoTracking()
            .Include(x => x.Exercise)
            .Where(x => x.WorkoutPlanDayId == workoutDay.Id)
            .OrderBy(x => x.ExerciseOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        // 7. Map exercise details.
        var response = exercises
            .Select(MapWorkoutExerciseDetailResponse)
            .ToList();

        // 8. Return ordered exercises.
        return Results.Ok(response);
    }

    // ============================================================
    // Step 10.30.6.5.2
    // Retrieve an individual configured exercise from an assigned day.
    // ============================================================

    private static async Task<IResult> GetMemberActiveWorkoutExerciseAsync(
        int dayNumber,
        Guid workoutPlanExerciseId,
        FitnessPlatformDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // 1. Retrieve authenticated user ID.
        var userId = GetAuthenticatedUserId(httpContext);

        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        // 2. Validate workout day number.
        if (dayNumber <= 0)
        {
            return Results.BadRequest(new
            {
                Error = "Workout day number must be greater than zero."
            });
        }

        // 3. Validate configured exercise ID.
        if (workoutPlanExerciseId == Guid.Empty)
        {
            return Results.BadRequest(new
            {
                Error = "A valid workout plan exercise ID is required."
            });
        }

        // 4. Retrieve the authenticated member's profile.
        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId.Value,
                cancellationToken);

        if (profile == null)
        {
            return Results.NotFound(new
            {
                Error = "Member profile not found."
            });
        }

        // 5. Retrieve the member's active assignment.
        var assignment = await db.MemberWorkoutPlanAssignments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserProfileId == profile.Id
                    && x.Status == "Active",
                cancellationToken);

        if (assignment == null)
        {
            return Results.NotFound(new
            {
                Error = "You do not have an active workout plan assignment."
            });
        }

        // 6. Retrieve the requested workout day
        // from the exact assigned workout plan version.
        var workoutDay = await db.Set<WorkoutPlanDay>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.WorkoutPlanId == assignment.WorkoutPlanId
                    && x.DayNumber == dayNumber,
                cancellationToken);

        if (workoutDay == null)
        {
            return Results.NotFound(new
            {
                Error = "The requested workout day was not found in your assigned workout plan."
            });
        }

        // 7. Retrieve the requested configured exercise.
        // Ensure it belongs to the member's assigned workout day.
        var exercise = await db.Set<WorkoutPlanExercise>()
            .AsNoTracking()
            .Include(x => x.Exercise)
            .SingleOrDefaultAsync(
                x => x.Id == workoutPlanExerciseId
                    && x.WorkoutPlanDayId == workoutDay.Id,
                cancellationToken);

        if (exercise == null)
        {
            return Results.NotFound(new
            {
                Error = "The requested exercise was not found in your assigned workout day."
            });
        }

        // 8. Map exercise details.
        var response = MapWorkoutExerciseDetailResponse(exercise);

        // 9. Return the requested configured exercise.
        return Results.Ok(response);
    }

    // ============================================================
    // Step 10.30.5.5
    // Retrieve the active workout plan assignment for a member.
    // ============================================================

    private static async Task<IResult> GetAdminActiveAssignmentAsync(
        Guid profileId,
        FitnessPlatformDbContext db,
        CancellationToken cancellationToken)
    {
        // 1. Validate member profile ID.
        if (profileId == Guid.Empty)
        {
            return Results.BadRequest(new
            {
                Error = "A valid member profile ID is required."
            });
        }

        // 2. Verify that the member profile exists.
        var profileExists = await db.UserProfiles
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == profileId,
                cancellationToken);

        if (!profileExists)
        {
            return Results.NotFound(new
            {
                Error = "Member profile not found."
            });
        }

        // 3. Retrieve the active assignment and its exact plan version.
        var assignment = await db.MemberWorkoutPlanAssignments
            .AsNoTracking()
            .Include(x => x.WorkoutPlan)
            .SingleOrDefaultAsync(
                x => x.UserProfileId == profileId
                    && x.Status == "Active",
                cancellationToken);

        // 4. Verify that an active assignment exists.
        if (assignment == null)
        {
            return Results.NotFound(new
            {
                Error = "No active workout plan assignment found for this member."
            });
        }

        // 5. Return the assignment details.
        return Results.Ok(MapAssignmentResponse(assignment));
    }
    public static void MapMemberWorkoutPlanAssignmentEndpoints(
        this WebApplication app)
    {
        // ============================================================
        // Administrator Workout Plan Assignment Endpoints
        // ============================================================

        var adminGroup = app.MapGroup(
            "/api/admin/members/{profileId:guid}/workout-plans");

        adminGroup.RequireAuthorization(
            policy => policy.RequireRole("Admin"));

        // Assign or reassign workout plan.
        adminGroup.MapPost("/assign", AssignWorkoutPlanAsync)
            .WithName("AssignMemberWorkoutPlan")
            .WithTags("Admin Member Workout Plans");

        // Retrieve active assignment.
        adminGroup.MapGet("/active", GetAdminActiveAssignmentAsync)
            .WithName("GetAdminActiveWorkoutPlanAssignment")
            .WithTags("Admin Member Workout Plans");

        // Retrieve assignment history.
        adminGroup.MapGet("/history", GetAdminAssignmentHistoryAsync)
            .WithName("GetAdminWorkoutPlanAssignmentHistory")
            .WithTags("Admin Member Workout Plans");


        // ============================================================
        // Member Workout Plan Assignment Endpoints
        // ============================================================

        var memberGroup = app.MapGroup(
            "/api/member/workout-plans");

        memberGroup.RequireAuthorization(
            policy => policy.RequireRole("Member"));

        // Retrieve active assignment summary.
        memberGroup.MapGet("/active", GetMemberActiveAssignmentAsync)
            .WithName("GetMemberActiveWorkoutPlanAssignment")
            .WithTags("Member Workout Plans")
            .Produces<MemberActiveWorkoutPlanResponse>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // ============================================================
        // Step 10.30.6.4.2
        // Retrieve an individual workout day with exercises.
        // ============================================================

        memberGroup.MapGet(
                "/active/days/{dayNumber:int}",
                GetMemberActiveWorkoutDayAsync)
            .WithName("GetMemberActiveWorkoutDay")
            .WithTags("Member Workout Plans")
            .WithSummary("Retrieve a specific assigned workout day.")
            .WithDescription(
                "Returns the requested workout day and its ordered exercises " +
                "from the authenticated member's exact assigned workout plan version.")
            .Produces<WorkoutPlanDayDetailResponse>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // ============================================================
        // Step 10.30.6.5.1
        // Retrieve all exercises for an assigned workout day.
        // ============================================================

        memberGroup.MapGet(
                "/active/days/{dayNumber:int}/exercises",
                GetMemberActiveWorkoutDayExercisesAsync)
            .WithName("GetMemberActiveWorkoutDayExercises")
            .WithTags("Member Workout Plans")
            .WithSummary("Retrieve exercises for a specific assigned workout day.")
            .WithDescription(
                "Returns the ordered exercises, sets, repetitions, " +
                "rest periods, durations and training notes from " +
                "the member's exact assigned workout plan version.")
            .Produces<List<WorkoutPlanExerciseDetailResponse>>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // ============================================================
        // Step 10.30.6.5.2
        // Retrieve an individual configured workout exercise.
        // ============================================================

        memberGroup.MapGet(
                "/active/days/{dayNumber:int}/exercises/{workoutPlanExerciseId:guid}",
                GetMemberActiveWorkoutExerciseAsync)
            .WithName("GetMemberActiveWorkoutExercise")
            .WithTags("Member Workout Plans")
            .WithSummary("Retrieve an individual assigned workout exercise.")
            .WithDescription(
                "Returns a specific exercise configuration, including " +
                "sets, repetitions, rest periods, duration and training notes " +
                "from the member's exact assigned workout plan version.")
            .Produces<WorkoutPlanExerciseDetailResponse>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // ============================================================
        // Step 10.30.6.3.5
        // Retrieve complete assigned workout plan details.
        // ============================================================

        memberGroup.MapGet(
                "/active/details",
                GetMemberActiveWorkoutPlanDetailsAsync)
            .WithName("GetMemberActiveWorkoutPlanDetails")
            .WithTags("Member Workout Plans")
            .WithSummary(
                "Retrieve the member's complete assigned workout plan.")
            .WithDescription(
                "Returns the authenticated member's active workout plan " +
                "assignment, exact assigned version, workout days, " +
                "rest days and ordered exercises.")
            .Produces<MemberWorkoutPlanDetailResponse>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // ============================================================
        // Step 10.30.6.4.1
        // Retrieve the member's assigned workout schedule.
        // ============================================================

        memberGroup.MapGet(
                "/active/days",
                GetMemberActiveWorkoutDaysAsync)
            .WithName("GetMemberActiveWorkoutDays")
            .WithTags("Member Workout Plans")
            .WithSummary("Retrieve the member's assigned workout schedule.")
            .WithDescription(
                "Returns the ordered workout days and rest days " +
                "from the member's exact assigned workout plan version.")
            .Produces<List<WorkoutPlanDayResponse>>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // ============================================================
        // Step 10.30.8.3
        // Retrieve the member's workout plan assignment history.
        // ============================================================

        memberGroup.MapGet(
                "/history",
                GetMemberAssignmentHistoryAsync)
            .WithName("GetMemberWorkoutPlanAssignmentHistory")
            .WithTags("Member Workout Plans")
            .WithSummary(
                "Retrieve the member's workout plan assignment history.")
            .WithDescription(
                "Returns all current and previous workout plan " +
                "assignments belonging to the authenticated member, " +
                "including their exact assigned workout plan versions.")
            .Produces<MemberWorkoutPlanHistoryResponse>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }


    private static async Task<IResult> AssignWorkoutPlanAsync(
    Guid profileId,
    AssignWorkoutPlanRequest request,
    FitnessPlatformDbContext db,
    HttpContext httpContext,
    CancellationToken cancellationToken)
    {
        // Step 1: Retrieve authenticated administrator ID.
        var adminUserId = GetAuthenticatedUserId(httpContext);

        if (!adminUserId.HasValue)
        {
            return Results.Unauthorized();
        }

        // Step 2: Validate member profile ID.
        if (profileId == Guid.Empty)
        {
            return Results.BadRequest(new
            {
                Error = "A valid member profile ID is required."
            });
        }

        // Step 3: Validate workout plan ID.
        if (request.WorkoutPlanId == Guid.Empty)
        {
            return Results.BadRequest(new
            {
                Error = "A valid workout plan ID is required."
            });
        }

        // Step 4: Validate assignment start date.
        if (request.StartDate == default)
        {
            return Results.BadRequest(new
            {
                Error = "A valid assignment start date is required."
            });
        }

        // Step 5: Validate assignment date range.
        if (request.EndDate.HasValue &&
            request.EndDate.Value < request.StartDate)
        {
            return Results.BadRequest(new
            {
                Error = "End date cannot be earlier than start date."
            });
        }

        // Step 6: Normalize and validate notes.
        var notes = string.IsNullOrWhiteSpace(request.Notes)
            ? null
            : request.Notes.Trim();

        if (notes != null && notes.Length > 1000)
        {
            return Results.BadRequest(new
            {
                Error = "Assignment notes cannot exceed 1000 characters."
            });
        }

        try
        {
            // Step 7: Start serializable transaction.
            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);



            // Step 8: Verify that the member profile exists.
            var profileExists = await db.UserProfiles
                .AnyAsync(
                    x => x.Id == profileId,
                    cancellationToken);

            if (!profileExists)
            {
                return Results.NotFound(new
                {
                    Error = "Member profile not found."
                });
            }

            // Step 9: Retrieve requested workout plan.
            var workoutPlan = await db.WorkoutPlans
                .FirstOrDefaultAsync(
                    x => x.Id == request.WorkoutPlanId,
                    cancellationToken);

            if (workoutPlan == null)
            {
                return Results.NotFound(new
                {
                    Error = "Workout plan not found."
                });
            }

            // Step 10: Verify workout plan approval.
            if (workoutPlan.ReviewStatus != "Approved")
            {
                return Results.Conflict(new
                {
                    Error = "Only approved workout plans can be assigned."
                });
            }

            // Step 11: Verify workout plan activation.
            if (!workoutPlan.IsActive)
            {
                return Results.Conflict(new
                {
                    Error = "Inactive workout plans cannot be assigned."
                });
            }

            // Step 12: Verify latest workout plan version.
            if (!workoutPlan.IsLatestVersion)
            {
                return Results.Conflict(new
                {
                    Error = "Historical workout plan versions cannot be assigned."
                });
            }

            // Step 13: Retrieve existing active assignment.
            var existingAssignment =
                await db.MemberWorkoutPlanAssignments
                    .FirstOrDefaultAsync(
                        x => x.UserProfileId == profileId &&
                             x.Status == "Active",
                        cancellationToken);

            // Step 14: Prevent duplicate active assignments.
            if (existingAssignment != null &&
                existingAssignment.WorkoutPlanId == workoutPlan.Id)
            {
                return Results.Conflict(new
                {
                    Error = "This workout plan is already assigned to the member."
                });
            }

            var now = DateTime.UtcNow;

            // Step 15: Deactivate the previous assignment.
            if (existingAssignment != null)
            {
                existingAssignment.Status = "Inactive";

                existingAssignment.DeactivatedAtUtc = now;

                existingAssignment.UpdatedAtUtc = now;

                // Save deactivation first to satisfy
                // the unique filtered active assignment index.
                await db.SaveChangesAsync(cancellationToken);
            }

            // Step 16: Create the new assignment.
            var assignment = new MemberWorkoutPlanAssignment
            {
                Id = Guid.NewGuid(),

                UserProfileId = profileId,

                WorkoutPlanId = workoutPlan.Id,

                WorkoutPlan = workoutPlan,

                Status = "Active",

                StartDate = request.StartDate,

                EndDate = request.EndDate,

                AssignedAtUtc = now,

                AssignedByUserId = adminUserId.Value,

                Notes = notes,

                DeactivatedAtUtc = null,

                CreatedAtUtc = now,

                UpdatedAtUtc = now
            };

            // Step 17: Save the new assignment.
            db.MemberWorkoutPlanAssignments.Add(assignment);

            await db.SaveChangesAsync(cancellationToken);

            // Step 18: Commit the complete operation.
            await transaction.CommitAsync(cancellationToken);

            // Step 19: Return the assignment response.
            return Results.Json(
                MapAssignmentResponse(assignment),
                statusCode: StatusCodes.Status201Created);
        }
        catch (Exception exception)
    when (SqlConcurrencyExceptionHelper.IsConcurrencyConflict(exception))
        {
            return Results.Conflict(new
            {
                Error = "The workout plan assignment conflicts with another database operation. Refresh the data and try again.",
                Code = "CONCURRENCY_CONFLICT"
            });
        }
    }

    // ============================================================
    // Step 10.30.6.3.3.3
    // Map complete member workout plan details.
    // ============================================================

    private static MemberWorkoutPlanDetailResponse
        MapMemberWorkoutPlanDetailResponse(
            MemberWorkoutPlanAssignment assignment,
            IEnumerable<WorkoutPlanDay> workoutDays)
    {
        return new MemberWorkoutPlanDetailResponse
        {
            Assignment = MapMemberAssignmentResponse(assignment),

            Days = workoutDays
                .OrderBy(x => x.DayNumber)
                .ThenBy(x => x.Id)
                .Select(MapWorkoutDayDetailResponse)
                .ToList()
        };
    }

    // ============================================================
    // Step 10.30.6.3.3.2
    // Map workout day details and ordered exercises.
    // ============================================================

    private static WorkoutPlanDayDetailResponse
        MapWorkoutDayDetailResponse(
            WorkoutPlanDay day)
    {
        return new WorkoutPlanDayDetailResponse
        {
            Id = day.Id,
            WorkoutPlanId = day.WorkoutPlanId,
            DayNumber = day.DayNumber,
            Name = day.Name,
            Description = day.Description,
            IsRestDay = day.IsRestDay,
            EstimatedDurationMinutes = day.EstimatedDurationMinutes,

            Exercises = day.Exercises
                .OrderBy(x => x.ExerciseOrder)
                .ThenBy(x => x.Id)
                .Select(MapWorkoutExerciseDetailResponse)
                .ToList()
        };
    }

    // ============================================================
    // Step 10.30.6.3.3.1
    // Map workout exercise details.
    // ============================================================

    private static WorkoutPlanExerciseDetailResponse
        MapWorkoutExerciseDetailResponse(
            WorkoutPlanExercise exercise)
    {
        return new WorkoutPlanExerciseDetailResponse
        {
            Id = exercise.Id,
            WorkoutPlanDayId = exercise.WorkoutPlanDayId,
            ExerciseId = exercise.ExerciseId,
            ExerciseCode = exercise.Exercise.Code,
            ExerciseName = exercise.Exercise.Name,
            ExerciseOrder = exercise.ExerciseOrder,
            Sets = exercise.Sets,
            Reps = exercise.Reps,
            RestSeconds = exercise.RestSeconds,
            DurationSeconds = exercise.DurationSeconds,
            Notes = exercise.Notes
        };
    }

    // ============================================================
    // Step 10.30.6.2
    // Map assignment details to a member-facing response.
    // ============================================================

    private static MemberActiveWorkoutPlanResponse
        MapMemberAssignmentResponse(
            MemberWorkoutPlanAssignment assignment)
    {
        return new MemberActiveWorkoutPlanResponse
        {
            Id = assignment.Id,
            UserProfileId = assignment.UserProfileId,
            WorkoutPlanId = assignment.WorkoutPlanId,
            WorkoutPlanName = assignment.WorkoutPlan.Name,
            Description = assignment.WorkoutPlan.Description,
            GoalCode = assignment.WorkoutPlan.GoalCode,
            ExperienceLevel = assignment.WorkoutPlan.ExperienceLevel,
            TrainingLocation = assignment.WorkoutPlan.TrainingLocation,
            DaysPerWeek = assignment.WorkoutPlan.DaysPerWeek,
            DurationWeeks = assignment.WorkoutPlan.DurationWeeks,
            VersionNumber = assignment.WorkoutPlan.VersionNumber,
            VersionGroupId = assignment.WorkoutPlan.VersionGroupId,
            IsLatestVersion = assignment.WorkoutPlan.IsLatestVersion,
            Status = assignment.Status,
            StartDate = assignment.StartDate,
            EndDate = assignment.EndDate,
            AssignedAtUtc = assignment.AssignedAtUtc
        };
    }

    private static MemberWorkoutPlanAssignmentResponse
        MapAssignmentResponse(
            MemberWorkoutPlanAssignment assignment)
    {
        return new MemberWorkoutPlanAssignmentResponse
        {
            Id = assignment.Id,

            UserProfileId = assignment.UserProfileId,

            WorkoutPlanId = assignment.WorkoutPlanId,

            WorkoutPlanName = assignment.WorkoutPlan.Name,

            GoalCode = assignment.WorkoutPlan.GoalCode,

            ExperienceLevel =
                assignment.WorkoutPlan.ExperienceLevel,

            TrainingLocation =
                assignment.WorkoutPlan.TrainingLocation,

            DaysPerWeek =
                assignment.WorkoutPlan.DaysPerWeek,

            DurationWeeks =
                assignment.WorkoutPlan.DurationWeeks,

            VersionNumber =
                assignment.WorkoutPlan.VersionNumber,

            VersionGroupId =
                assignment.WorkoutPlan.VersionGroupId,

            IsLatestVersion =
                assignment.WorkoutPlan.IsLatestVersion,

            Status = assignment.Status,

            StartDate = assignment.StartDate,

            EndDate = assignment.EndDate,

            AssignedAtUtc = assignment.AssignedAtUtc,

            AssignedByUserId = assignment.AssignedByUserId,

            Notes = assignment.Notes,

            DeactivatedAtUtc = assignment.DeactivatedAtUtc,

            CreatedAtUtc = assignment.CreatedAtUtc,

            UpdatedAtUtc = assignment.UpdatedAtUtc
        };
    }
    private static async Task<IResult> GetMemberAssignmentHistoryAsync(
    FitnessPlatformDbContext db,
    HttpContext httpContext,
    CancellationToken cancellationToken)
    {
        // Step 1: Retrieve authenticated member ID.
        var userId = GetAuthenticatedUserId(httpContext);

        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        // Step 2: Retrieve member profile.
        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId.Value,
                cancellationToken);

        if (profile is null)
        {
            return Results.NotFound(new
            {
                Error = "Member profile not found."
            });
        }

        // Step 3: Retrieve all current and historical assignments.
        var assignments = await db.MemberWorkoutPlanAssignments
            .AsNoTracking()
            .Include(x => x.WorkoutPlan)
            .Where(x => x.UserProfileId == profile.Id)
            .OrderByDescending(x => x.AssignedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        // Step 4: Map member-facing responses.
        var assignmentResponses = assignments
            .Select(MapMemberAssignmentResponse)
            .ToList();

        // Step 5: Identify active assignment.
        var activeAssignment = assignmentResponses
            .SingleOrDefault(x => x.Status == "Active");

        // Step 6: Prepare response.
        var response = new MemberWorkoutPlanHistoryResponse
        {
            UserProfileId = profile.Id,
            TotalAssignments = assignmentResponses.Count,
            ActiveAssignment = activeAssignment,
            Assignments = assignmentResponses
        };

        return Results.Ok(response);
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

}