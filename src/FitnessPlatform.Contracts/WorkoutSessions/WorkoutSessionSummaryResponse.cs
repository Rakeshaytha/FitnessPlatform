using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutSessions;

public sealed class WorkoutSessionSummaryResponse
{
    public Guid WorkoutSessionId { get; set; }

    public string WorkoutPlanName { get; set; } = string.Empty;

    public string WorkoutDayName { get; set; } = string.Empty;

    public DateOnly WorkoutDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public int? TotalDurationSeconds { get; set; }

    public int TotalPausedSeconds { get; set; }

    public int TotalExercises { get; set; }

    public int CompletedExercises { get; set; }

    public int SkippedExercises { get; set; }

    public int TotalPlannedSets { get; set; }

    public int TotalRecordedSets { get; set; }

    public int CompletedSets { get; set; }

    public int SkippedSets { get; set; }

    public long TotalActualReps { get; set; }

    public decimal TotalVolumeKg { get; set; }

    public long TotalActualDurationSeconds { get; set; }

    public decimal TotalActualDistanceMeters { get; set; }

    public long TotalActualRestSeconds { get; set; }

    public decimal? AverageRpe { get; set; }

    public decimal ProgressPercentage { get; set; }

    public string? Notes { get; set; }
}
