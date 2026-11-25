using System;
using System.Collections.Generic;
using System.Text;

using FitnessPlatform.Domain.Constants;

namespace FitnessPlatform.Domain.Entities;

public class WorkoutSession
{
    // Primary identifier
    public Guid Id { get; set; } = Guid.NewGuid();

    // Member performing the workout
    public Guid UserProfileId { get; set; }

    // Assignment from which the session was started
    public Guid MemberWorkoutPlanAssignmentId { get; set; }

    // References the exact workout plan version
    public Guid WorkoutPlanId { get; set; }

    // References the selected workout day
    public Guid WorkoutPlanDayId { get; set; }

    // Historical workout plan snapshot
    public string WorkoutPlanName { get; set; } = string.Empty;

    public int WorkoutPlanVersionNumber { get; set; }

    // Historical workout day snapshot
    public string WorkoutDayName { get; set; } = string.Empty;

    public int WorkoutDayNumber { get; set; }

    // Member's local workout date
    public DateOnly WorkoutDate { get; set; }

    // Time zone used to determine workout date
    public string TimeZoneId { get; set; } = string.Empty;

    // Workout session lifecycle
    public string Status { get; set; } =
        WorkoutSessionStatuses.InProgress;

    // Session timing
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    // Pause and resume tracking
    public DateTime? PausedAtUtc { get; set; }

    public int TotalPausedSeconds { get; set; }

    // Final active workout duration
    public int? TotalDurationSeconds { get; set; }

    // Optional member notes
    public string? Notes { get; set; }

    // Audit information
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Optimistic concurrency protection
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation properties
    public UserProfile UserProfile { get; set; } = null!;

    public MemberWorkoutPlanAssignment MemberWorkoutPlanAssignment
    { get; set; } = null!;

    public WorkoutPlan WorkoutPlan { get; set; } = null!;

    public WorkoutPlanDay WorkoutPlanDay { get; set; } = null!;

    // Exercises belonging to this workout session
    public ICollection<WorkoutSessionExercise> Exercises { get; set; } =
        new List<WorkoutSessionExercise>();
}
