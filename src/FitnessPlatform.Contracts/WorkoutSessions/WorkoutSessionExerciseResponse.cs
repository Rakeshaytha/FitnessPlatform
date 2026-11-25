using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutSessions;

public sealed class WorkoutSessionExerciseResponse
{
    public Guid Id { get; set; }

    public Guid WorkoutSessionId { get; set; }

    public Guid WorkoutPlanExerciseId { get; set; }

    public Guid ExerciseId { get; set; }

    public string ExerciseCode { get; set; } = string.Empty;

    public string ExerciseName { get; set; } = string.Empty;

    public string MuscleGroup { get; set; } = string.Empty;

    public string Equipment { get; set; } = string.Empty;

    public string ExerciseInstructions { get; set; } = string.Empty;

    public int ExerciseOrder { get; set; }

    public int PlannedSets { get; set; }

    public int PlannedReps { get; set; }

    public int PlannedRestSeconds { get; set; }

    public int? PlannedDurationSeconds { get; set; }

    public string? PlannedNotes { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? SkippedAtUtc { get; set; }

    public string? Notes { get; set; }

    public string? SkipReason { get; set; }

    public int TotalRecordedSets { get; set; }

    public int PendingSets { get; set; }

    public int CompletedSets { get; set; }

    public int SkippedSets { get; set; }

    public long TotalActualReps { get; set; }
    public long TotalActualDurationSeconds { get; set; }

    public decimal TotalActualDistanceMeters { get; set; }

    public decimal TotalVolumeKg { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<WorkoutSessionSetResponse> Sets { get; set; }
        = new();
}
