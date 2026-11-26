using FitnessPlatform.Contracts.WorkoutSessions;
using FitnessPlatform.Domain.Constants;
using FitnessPlatform.Domain.Entities;

namespace FitnessPlatform.Application.WorkoutSessions;

public static class WorkoutSessionResponseMapper
{
    // ============================================================
    // Session Response
    // ============================================================

    public static WorkoutSessionResponse MapSessionResponse(
        WorkoutSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var exercises = session.Exercises.ToList();

        var sets = exercises
            .SelectMany(x => x.Sets)
            .ToList();

        var completedExercises = exercises.Count(
            x => x.Status ==
                 WorkoutSessionExerciseStatuses.Completed);

        var skippedExercises = exercises.Count(
            x => x.Status ==
                 WorkoutSessionExerciseStatuses.Skipped);

        return new WorkoutSessionResponse
        {
            Id = session.Id,
            UserProfileId = session.UserProfileId,

            MemberWorkoutPlanAssignmentId =
                session.MemberWorkoutPlanAssignmentId,

            WorkoutPlanId = session.WorkoutPlanId,
            WorkoutPlanDayId = session.WorkoutPlanDayId,

            WorkoutPlanName = session.WorkoutPlanName,

            WorkoutPlanVersionNumber =
                session.WorkoutPlanVersionNumber,

            WorkoutDayName = session.WorkoutDayName,
            WorkoutDayNumber = session.WorkoutDayNumber,

            WorkoutDate = session.WorkoutDate,
            TimeZoneId = session.TimeZoneId,

            Status = session.Status,

            StartedAtUtc = session.StartedAtUtc,
            CompletedAtUtc = session.CompletedAtUtc,
            CancelledAtUtc = session.CancelledAtUtc,
            PausedAtUtc = session.PausedAtUtc,

            TotalPausedSeconds =
                session.TotalPausedSeconds,

            TotalDurationSeconds =
                session.TotalDurationSeconds,

            Notes = session.Notes,

            TotalExercises = exercises.Count,

            PendingExercises = exercises.Count(
                x => x.Status ==
                     WorkoutSessionExerciseStatuses.Pending),

            InProgressExercises = exercises.Count(
                x => x.Status ==
                     WorkoutSessionExerciseStatuses.InProgress),

            CompletedExercises = completedExercises,
            SkippedExercises = skippedExercises,

            TotalPlannedSets = exercises.Sum(
                x => x.PlannedSets),

            TotalRecordedSets = sets.Count,

            CompletedSets = sets.Count(
                x => x.Status ==
                     WorkoutSessionSetStatuses.Completed),

            SkippedSets = sets.Count(
                x => x.Status ==
                     WorkoutSessionSetStatuses.Skipped),

            ProgressPercentage =
                CalculateProgressPercentage(
                    exercises.Count,
                    completedExercises,
                    skippedExercises),

            CreatedAtUtc = session.CreatedAtUtc,
            UpdatedAtUtc = session.UpdatedAtUtc,

            RowVersion = Convert.ToBase64String(
                session.RowVersion)
        };
    }

    // ============================================================
    // Complete Session Detail Response
    // ============================================================

    public static WorkoutSessionDetailResponse
        MapSessionDetailResponse(
            WorkoutSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new WorkoutSessionDetailResponse
        {
            Session = MapSessionResponse(session),

            Exercises = session.Exercises
                .OrderBy(x => x.ExerciseOrder)
                .Select(MapExerciseResponse)
                .ToList()
        };
    }

    // ============================================================
    // Exercise Response
    // ============================================================

    public static WorkoutSessionExerciseResponse
        MapExerciseResponse(
            WorkoutSessionExercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        var sets = exercise.Sets
            .OrderBy(x => x.SetNumber)
            .ToList();

        var performance =
            CalculatePerformanceTotals(sets);

        return new WorkoutSessionExerciseResponse
        {
            Id = exercise.Id,

            WorkoutSessionId =
                exercise.WorkoutSessionId,

            WorkoutPlanExerciseId =
                exercise.WorkoutPlanExerciseId,

            ExerciseId = exercise.ExerciseId,

            ExerciseCode = exercise.ExerciseCode,
            ExerciseName = exercise.ExerciseName,
            MuscleGroup = exercise.MuscleGroup,
            Equipment = exercise.Equipment,

            ExerciseInstructions =
                exercise.ExerciseInstructions,

            ExerciseOrder = exercise.ExerciseOrder,

            PlannedSets = exercise.PlannedSets,
            PlannedReps = exercise.PlannedReps,

            PlannedRestSeconds =
                exercise.PlannedRestSeconds,

            PlannedDurationSeconds =
                exercise.PlannedDurationSeconds,

            PlannedNotes = exercise.PlannedNotes,

            Status = exercise.Status,

            StartedAtUtc = exercise.StartedAtUtc,
            CompletedAtUtc = exercise.CompletedAtUtc,
            SkippedAtUtc = exercise.SkippedAtUtc,

            Notes = exercise.Notes,
            SkipReason = exercise.SkipReason,

            TotalRecordedSets = sets.Count,

            PendingSets = sets.Count(
                x => x.Status ==
                     WorkoutSessionSetStatuses.Pending),

            CompletedSets = sets.Count(
                x => x.Status ==
                     WorkoutSessionSetStatuses.Completed),

            SkippedSets = sets.Count(
                x => x.Status ==
                     WorkoutSessionSetStatuses.Skipped),

            TotalActualReps =
                performance.TotalActualReps,

            TotalActualDurationSeconds =
                performance.TotalActualDurationSeconds,

            TotalActualDistanceMeters =
                performance.TotalActualDistanceMeters,

            TotalVolumeKg =
                performance.TotalVolumeKg,

            CreatedAtUtc = exercise.CreatedAtUtc,
            UpdatedAtUtc = exercise.UpdatedAtUtc,

            Sets = sets
                .Select(MapSetResponse)
                .ToList()
        };
    }

    // ============================================================
    // Individual Set Response
    // ============================================================

    public static WorkoutSessionSetResponse MapSetResponse(
        WorkoutSessionSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        return new WorkoutSessionSetResponse
        {
            Id = set.Id,

            WorkoutSessionExerciseId =
                set.WorkoutSessionExerciseId,

            SetNumber = set.SetNumber,
            SetType = set.SetType,

            ActualReps = set.ActualReps,
            ActualWeightKg = set.ActualWeightKg,

            ActualDurationSeconds =
                set.ActualDurationSeconds,

            ActualDistanceMeters =
                set.ActualDistanceMeters,

            ActualRestSeconds =
                set.ActualRestSeconds,

            Rpe = set.Rpe,

            VolumeKg = CalculateSetVolume(set),

            Status = set.Status,
            Notes = set.Notes,

            CompletedAtUtc = set.CompletedAtUtc,
            SkippedAtUtc = set.SkippedAtUtc,

            CreatedAtUtc = set.CreatedAtUtc,
            UpdatedAtUtc = set.UpdatedAtUtc
        };
    }

    // ============================================================
    // Workout Session Performance Summary
    // ============================================================

    public static WorkoutSessionSummaryResponse
        MapSessionSummaryResponse(
            WorkoutSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var response = MapSessionResponse(session);

        var sets = session.Exercises
            .SelectMany(x => x.Sets)
            .ToList();

        var performance =
            CalculatePerformanceTotals(sets);

        return new WorkoutSessionSummaryResponse
        {
            WorkoutSessionId = session.Id,

            WorkoutPlanName = session.WorkoutPlanName,
            WorkoutDayName = session.WorkoutDayName,

            WorkoutDate = session.WorkoutDate,
            Status = session.Status,

            StartedAtUtc = session.StartedAtUtc,
            CompletedAtUtc = session.CompletedAtUtc,
            CancelledAtUtc = session.CancelledAtUtc,

            TotalDurationSeconds =
                session.TotalDurationSeconds,

            TotalPausedSeconds =
                session.TotalPausedSeconds,

            TotalExercises = response.TotalExercises,

            CompletedExercises =
                response.CompletedExercises,

            SkippedExercises =
                response.SkippedExercises,

            TotalPlannedSets =
                response.TotalPlannedSets,

            TotalRecordedSets =
                response.TotalRecordedSets,

            CompletedSets = response.CompletedSets,
            SkippedSets = response.SkippedSets,

            TotalActualReps =
                performance.TotalActualReps,

            TotalVolumeKg =
                performance.TotalVolumeKg,

            TotalActualDurationSeconds =
                performance.TotalActualDurationSeconds,

            TotalActualDistanceMeters =
                performance.TotalActualDistanceMeters,

            TotalActualRestSeconds =
                performance.TotalActualRestSeconds,

            AverageRpe = performance.AverageRpe,

            ProgressPercentage =
                response.ProgressPercentage,

            Notes = session.Notes
        };
    }

    // ============================================================
    // Progress Calculation
    // ============================================================

    public static decimal CalculateProgressPercentage(
        int totalExercises,
        int completedExercises,
        int skippedExercises)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(
            totalExercises);

        ArgumentOutOfRangeException.ThrowIfNegative(
            completedExercises);

        ArgumentOutOfRangeException.ThrowIfNegative(
            skippedExercises);

        var finishedExercises =
            (long)completedExercises + skippedExercises;

        if (finishedExercises > totalExercises)
        {
            throw new ArgumentException(
                "Completed and skipped exercises cannot " +
                "exceed the total number of exercises.");
        }

        if (totalExercises == 0)
        {
            return 0m;
        }

        var progress =
            finishedExercises * 100m / totalExercises;

        return Math.Round(
            progress,
            2,
            MidpointRounding.AwayFromZero);
    }

    // ============================================================
    // Set Volume Calculation
    // ============================================================

    public static decimal? CalculateSetVolume(
        WorkoutSessionSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        if (set.Status !=
            WorkoutSessionSetStatuses.Completed)
        {
            return null;
        }

        if (!set.ActualWeightKg.HasValue ||
            !set.ActualReps.HasValue)
        {
            return null;
        }

        return set.ActualWeightKg.Value *
               set.ActualReps.Value;
    }

    // ============================================================
    // Reusable Performance Aggregation
    // ============================================================

    private static PerformanceTotals
        CalculatePerformanceTotals(
            IEnumerable<WorkoutSessionSet> sets)
    {
        long totalActualReps = 0;

        long totalActualDurationSeconds = 0;

        decimal totalActualDistanceMeters = 0m;

        long totalActualRestSeconds = 0;

        decimal totalVolumeKg = 0m;

        decimal totalRpe = 0m;

        long rpeCount = 0;

        foreach (var set in sets)
        {
            if (set.Status !=
                WorkoutSessionSetStatuses.Completed)
            {
                continue;
            }

            totalActualReps +=
                set.ActualReps ?? 0;

            totalActualDurationSeconds +=
                set.ActualDurationSeconds ?? 0;

            totalActualDistanceMeters +=
                set.ActualDistanceMeters ?? 0m;

            totalActualRestSeconds +=
                set.ActualRestSeconds ?? 0;

            totalVolumeKg +=
                CalculateSetVolume(set) ?? 0m;

            if (set.Rpe.HasValue)
            {
                totalRpe += set.Rpe.Value;
                rpeCount++;
            }
        }

        decimal? averageRpe = rpeCount == 0
            ? null
            : Math.Round(
                totalRpe / rpeCount,
                2,
                MidpointRounding.AwayFromZero);

        return new PerformanceTotals(
            totalActualReps,
            totalActualDurationSeconds,
            totalActualDistanceMeters,
            totalActualRestSeconds,
            totalVolumeKg,
            averageRpe);
    }

    // ============================================================
    // Internal Performance Result
    // ============================================================

    private sealed record PerformanceTotals(
        long TotalActualReps,
        long TotalActualDurationSeconds,
        decimal TotalActualDistanceMeters,
        long TotalActualRestSeconds,
        decimal TotalVolumeKg,
        decimal? AverageRpe);
}