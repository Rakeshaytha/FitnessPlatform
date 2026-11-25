using FitnessPlatform.Domain.Constants;

namespace FitnessPlatform.Domain.Entities;

public class WorkoutSessionSet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkoutSessionExerciseId { get; set; }

    public int SetNumber { get; set; }

    public string SetType { get; set; } = WorkoutSessionSetTypes.Working;

    public int? ActualReps { get; set; }

    public decimal? ActualWeightKg { get; set; }

    public int? ActualDurationSeconds { get; set; }

    public decimal? ActualDistanceMeters { get; set; }

    public int? ActualRestSeconds { get; set; }

    public decimal? Rpe { get; set; }

    public string Status { get; set; } = WorkoutSessionSetStatuses.Pending;

    public string? Notes { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? SkippedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public WorkoutSessionExercise WorkoutSessionExercise { get; set; } = null!;
}
