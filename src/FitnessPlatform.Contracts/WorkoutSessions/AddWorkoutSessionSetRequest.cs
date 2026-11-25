using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutSessions;

public sealed class AddWorkoutSessionSetRequest
{
    public string SetType { get; set; } = "Working";

    public int? ActualReps { get; set; }

    public decimal? ActualWeightKg { get; set; }

    public int? ActualDurationSeconds { get; set; }

    public decimal? ActualDistanceMeters { get; set; }

    public int? ActualRestSeconds { get; set; }

    public decimal? Rpe { get; set; }

    public string? Notes { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}
