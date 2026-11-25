using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutSessions;

public sealed class WorkoutSessionSetResponse
{
    public Guid Id { get; set; }

    public Guid WorkoutSessionExerciseId { get; set; }

    public int SetNumber { get; set; }

    public string SetType { get; set; } = string.Empty;

    public int? ActualReps { get; set; }

    public decimal? ActualWeightKg { get; set; }

    public int? ActualDurationSeconds { get; set; }

    public decimal? ActualDistanceMeters { get; set; }

    public int? ActualRestSeconds { get; set; }

    public decimal? Rpe { get; set; }

    public decimal? VolumeKg { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? SkippedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}