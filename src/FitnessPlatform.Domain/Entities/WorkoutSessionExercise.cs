using System;
using System.Collections.Generic;
using System.Text;

using FitnessPlatform.Domain.Constants;

namespace FitnessPlatform.Domain.Entities;

public class WorkoutSessionExercise
{
    // Primary identifier
    public Guid Id { get; set; } = Guid.NewGuid();

    // Parent workout session
    public Guid WorkoutSessionId { get; set; }

    // Original workout plan exercise reference
    public Guid WorkoutPlanExerciseId { get; set; }

    // Exercise master reference
    public Guid ExerciseId { get; set; }

    // Historical exercise snapshot
    public string ExerciseCode { get; set; } = string.Empty;

    public string ExerciseName { get; set; } = string.Empty;

    public string MuscleGroup { get; set; } = string.Empty;

    public string Equipment { get; set; } = string.Empty;

    public string ExerciseInstructions { get; set; } = string.Empty;

    // Original exercise sequence
    public int ExerciseOrder { get; set; }

    // Planned exercise parameters
    public int PlannedSets { get; set; }

    public int PlannedReps { get; set; }

    public int PlannedRestSeconds { get; set; }

    public int? PlannedDurationSeconds { get; set; }

    public string? PlannedNotes { get; set; }

    // Exercise execution status
    public string Status { get; set; } =
        WorkoutSessionExerciseStatuses.Pending;

    // Exercise execution timestamps
    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? SkippedAtUtc { get; set; }

    // Member feedback
    public string? Notes { get; set; }

    public string? SkipReason { get; set; }

    // Audit information
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public WorkoutSession WorkoutSession { get; set; } = null!;

    public WorkoutPlanExercise WorkoutPlanExercise { get; set; } = null!;

    public Exercise Exercise { get; set; } = null!;

    // Performance sets belonging to this exercise
    public ICollection<WorkoutSessionSet> Sets { get; set; } =
        new List<WorkoutSessionSet>();
}