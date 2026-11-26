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

        // Start a new workout session.
        memberGroup.MapPost(
            "/start",
            StartWorkoutSessionAsync)
            .WithName("StartWorkoutSession")
            .AddEndpointFilter<SerializableTransactionFilter>()
            .Produces<WorkoutSessionDetailResponse>(
                StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // Retrieve the member's unfinished workout session.
        memberGroup.MapGet(
            "/active",
            GetActiveWorkoutSessionAsync)
            .WithName("GetActiveWorkoutSession")
            .Produces<WorkoutSessionDetailResponse>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // Retrieve a specific workout session owned by the member.
        memberGroup.MapGet(
            "/{sessionId:guid}",
            GetWorkoutSessionByIdAsync)
            .WithName("GetWorkoutSessionById")
            .Produces<WorkoutSessionDetailResponse>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
    private static async Task<IResult> StartWorkoutSessionAsync(
        StartWorkoutSessionRequest request,
        HttpContext httpContext,
        FitnessPlatformDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        // Step 1: Resolve the authenticated user.
        var userId = GetAuthenticatedUserId(httpContext);

        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        // Step 2: Validate the incoming request.
        var validationError =
            WorkoutSessionRequestValidator.Validate(request);

        if (validationError is not null)
        {
            return Results.BadRequest(new
            {
                Error = validationError
            });
        }

        // Step 3: Resolve the authenticated member profile.
        var profileId = await GetMemberProfileIdAsync(
            db,
            userId.Value,
            cancellationToken);

        if (!profileId.HasValue)
        {
            return Results.NotFound(new
            {
                Error = "Member profile not found."
            });
        }

        // Step 4: Retrieve the member's requested assignment.
        var assignment = await db.MemberWorkoutPlanAssignments
            .AsNoTracking()
            .Include(x => x.WorkoutPlan)
            .SingleOrDefaultAsync(
                x => x.Id == request.MemberWorkoutPlanAssignmentId
                    && x.UserProfileId == profileId.Value,
                cancellationToken);

        if (assignment is null)
        {
            return Results.NotFound(new
            {
                Error = "Workout plan assignment not found.",
                Code = "WORKOUT_ASSIGNMENT_NOT_FOUND"
            });
        }

        // Step 5: Verify the assignment is active.
        if (!string.Equals(
            assignment.Status,
            "Active",
            StringComparison.Ordinal))
        {
            return Results.Conflict(new
            {
                Error = "Only active workout plan assignments can be used to start a workout.",
                Code = "WORKOUT_ASSIGNMENT_NOT_ACTIVE"
            });
        }

        // Step 6: Resolve the requested time zone.
        TimeZoneInfo timeZone;

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(
                request.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return Results.BadRequest(new
            {
                Error = "Invalid or unsupported time zone ID.",
                Code = "INVALID_TIME_ZONE"
            });
        }
        catch (InvalidTimeZoneException)
        {
            return Results.BadRequest(new
            {
                Error = "Invalid time zone configuration.",
                Code = "INVALID_TIME_ZONE"
            });
        }

        // Step 7: Capture the server-generated UTC timestamp.
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        // Step 8: Calculate the member's local workout date.
        var localDateTime = TimeZoneInfo.ConvertTimeFromUtc(
            utcNow,
            timeZone);

        var workoutDate = DateOnly.FromDateTime(
            localDateTime);

        // Step 9: Verify the assignment has started.
        if (workoutDate < assignment.StartDate)
        {
            return Results.Conflict(new
            {
                Error = "The workout plan assignment has not started yet.",
                Code = "WORKOUT_ASSIGNMENT_NOT_STARTED"
            });
        }

        // Step 10: Verify the assignment has not expired.
        if (assignment.EndDate.HasValue
            && workoutDate > assignment.EndDate.Value)
        {
            return Results.Conflict(new
            {
                Error = "The workout plan assignment has expired.",
                Code = "WORKOUT_ASSIGNMENT_EXPIRED"
            });
        }

        // Step 11: Retrieve the requested workout day
        // from the member's exact assigned workout plan.
        var workoutDay = await db.WorkoutPlanDays
            .AsNoTracking()
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Exercise)
            .SingleOrDefaultAsync(
                x => x.Id == request.WorkoutPlanDayId
                    && x.WorkoutPlanId == assignment.WorkoutPlanId,
                cancellationToken);

        if (workoutDay is null)
        {
            return Results.NotFound(new
            {
                Error = "The requested workout day was not found in your assigned workout plan.",
                Code = "WORKOUT_DAY_NOT_FOUND"
            });
        }

        // Step 12: Prevent workout sessions on rest days.
        if (workoutDay.IsRestDay)
        {
            return Results.Conflict(new
            {
                Error = "A workout session cannot be started for a rest day.",
                Code = "WORKOUT_DAY_IS_REST_DAY"
            });
        }

        // Step 13: Verify the training day contains exercises.
        if (workoutDay.Exercises.Count == 0)
        {
            return Results.Conflict(new
            {
                Error = "The requested workout day does not contain any exercises.",
                Code = "WORKOUT_DAY_HAS_NO_EXERCISES"
            });
        }


        // Step 14: Check whether the member already has
        // an unfinished workout session.
        var existingSession = await db.WorkoutSessions
            .AsNoTracking()
            .Where(x =>
                x.UserProfileId == profileId.Value
                && (x.Status == WorkoutSessionStatuses.InProgress
                    || x.Status == WorkoutSessionStatuses.Paused))
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.WorkoutPlanName,
                x.WorkoutDayName,
                x.WorkoutDate,
                x.StartedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (existingSession is not null)
        {
            return Results.Conflict(new
            {
                Error = "You already have an unfinished workout session. Complete or cancel it before starting another workout.",
                Code = "UNFINISHED_WORKOUT_SESSION_EXISTS",
                ExistingSession = new
                {
                    WorkoutSessionId = existingSession.Id,
                    existingSession.Status,
                    existingSession.WorkoutPlanName,
                    existingSession.WorkoutDayName,
                    existingSession.WorkoutDate,
                    existingSession.StartedAtUtc
                }
            });
        }

        // Step 15: Create the workout session snapshot.
        var session = new WorkoutSession
        {
            Id = Guid.NewGuid(),

            UserProfileId = profileId.Value,

            MemberWorkoutPlanAssignmentId = assignment.Id,

            WorkoutPlanId = assignment.WorkoutPlanId,

            WorkoutPlanDayId = workoutDay.Id,

            WorkoutPlanName = assignment.WorkoutPlan.Name,

            WorkoutPlanVersionNumber =
                assignment.WorkoutPlan.VersionNumber,

            WorkoutDayName = workoutDay.Name,

            WorkoutDayNumber = workoutDay.DayNumber,

            WorkoutDate = workoutDate,

            TimeZoneId = request.TimeZoneId,

            Status = WorkoutSessionStatuses.InProgress,

            StartedAtUtc = utcNow,

            CompletedAtUtc = null,

            CancelledAtUtc = null,

            PausedAtUtc = null,

            TotalPausedSeconds = 0,

            TotalDurationSeconds = null,

            Notes = WorkoutSessionRequestValidator.NormalizeNotes(
                request.Notes),

            CreatedAtUtc = utcNow,

            UpdatedAtUtc = utcNow
        };

        // Step 16: Create the planned exercise snapshots.
        foreach (var plannedExercise in workoutDay.Exercises
            .OrderBy(x => x.ExerciseOrder))
        {
            var sessionExercise = new WorkoutSessionExercise
            {
                Id = Guid.NewGuid(),

                WorkoutSessionId = session.Id,

                WorkoutSession = session,

                WorkoutPlanExerciseId = plannedExercise.Id,

                ExerciseId = plannedExercise.ExerciseId,

                ExerciseCode = plannedExercise.Exercise.Code,

                ExerciseName = plannedExercise.Exercise.Name,

                MuscleGroup = plannedExercise.Exercise.MuscleGroup,

                Equipment = plannedExercise.Exercise.Equipment,

                ExerciseInstructions =
                    plannedExercise.Exercise.Instructions,

                ExerciseOrder = plannedExercise.ExerciseOrder,

                PlannedSets = plannedExercise.Sets,

                PlannedReps = plannedExercise.Reps,

                PlannedRestSeconds = plannedExercise.RestSeconds,

                PlannedDurationSeconds =
                    plannedExercise.DurationSeconds,

                PlannedNotes = plannedExercise.Notes,

                Status = WorkoutSessionExerciseStatuses.Pending,

                StartedAtUtc = null,

                CompletedAtUtc = null,

                SkippedAtUtc = null,

                Notes = null,

                SkipReason = null,

                CreatedAtUtc = utcNow,

                UpdatedAtUtc = utcNow
            };

            session.Exercises.Add(sessionExercise);
        }

        // Step 17: Persist the workout session and its exercises.
        // The serializable transaction is managed by
        // SerializableTransactionFilter.
        db.WorkoutSessions.Add(session);

        await db.SaveChangesAsync(cancellationToken);

        // Step 18: Generate the workout session detail response.
        var response =
            WorkoutSessionResponseMapper.MapSessionDetailResponse(
                session);

        // Step 19: Return the newly created workout session.
        // SerializableTransactionFilter commits the transaction
        // before the successful HTTP response is executed.
        return Results.Created(
            $"{RoutePrefix}/{session.Id}",
            response);

    }

    private static async Task<IResult> GetActiveWorkoutSessionAsync(
    HttpContext httpContext,
    FitnessPlatformDbContext db,
    CancellationToken cancellationToken)
    {
        // Step 1: Resolve the authenticated user.
        var userId = GetAuthenticatedUserId(httpContext);

        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        // Step 2: Resolve the authenticated member profile.
        var profileId = await GetMemberProfileIdAsync(
            db,
            userId.Value,
            cancellationToken);

        if (!profileId.HasValue)
        {
            return Results.NotFound(new
            {
                Error = "Member profile not found."
            });
        }

        // Step 3: Retrieve the member's unfinished workout
        // session, including exercise and set records.
        var session = await db.WorkoutSessions
            .AsNoTracking()
            .AsSingleQuery()
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .SingleOrDefaultAsync(
                x => x.UserProfileId == profileId.Value
                    && (x.Status == WorkoutSessionStatuses.InProgress
                        || x.Status == WorkoutSessionStatuses.Paused),
                cancellationToken);

        // Step 4: Return no content when the member
        // does not have an unfinished workout session.
        if (session is null)
        {
            return Results.NoContent();
        }

        // Step 5: Generate the complete workout session response.
        var response =
            WorkoutSessionResponseMapper.MapSessionDetailResponse(
                session);

        // Step 6: Return the active workout session.
        return Results.Ok(response);
    }

    private static async Task<IResult> GetWorkoutSessionByIdAsync(
    Guid sessionId,
    HttpContext httpContext,
    FitnessPlatformDbContext db,
    CancellationToken cancellationToken)
    {
        // Step 1: Resolve the authenticated user.
        var userId = GetAuthenticatedUserId(httpContext);

        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        // Step 2: Validate the requested workout session ID.
        if (sessionId == Guid.Empty)
        {
            return Results.BadRequest(new
            {
                Error = "Workout session ID is required.",
                Code = "INVALID_WORKOUT_SESSION_ID"
            });
        }

        // Step 3: Resolve the authenticated member profile.
        var profileId = await GetMemberProfileIdAsync(
            db,
            userId.Value,
            cancellationToken);

        if (!profileId.HasValue)
        {
            return Results.NotFound(new
            {
                Error = "Member profile not found."
            });
        }

        // Step 4: Retrieve the requested workout session,
        // restricted to the authenticated member.
        var session = await db.WorkoutSessions
            .AsNoTracking()
            .AsSingleQuery()
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .SingleOrDefaultAsync(
                x => x.Id == sessionId
                    && x.UserProfileId == profileId.Value,
                cancellationToken);

        // Step 5: Return the same response for missing
        // sessions and sessions owned by another member.
        if (session is null)
        {
            return Results.NotFound(new
            {
                Error = "Workout session not found.",
                Code = "WORKOUT_SESSION_NOT_FOUND"
            });
        }

        // Step 6: Generate the complete workout session response.
        var response =
            WorkoutSessionResponseMapper.MapSessionDetailResponse(
                session);

        // Step 7: Return the requested workout session.
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
