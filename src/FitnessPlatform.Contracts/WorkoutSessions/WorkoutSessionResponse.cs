using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutSessions;

public sealed class WorkoutSessionResponse
{
    public Guid Id { get; set; }

    public Guid UserProfileId { get; set; }

    public Guid MemberWorkoutPlanAssignmentId { get; set; }

    public Guid WorkoutPlanId { get; set; }

    public Guid WorkoutPlanDayId { get; set; }

    public string WorkoutPlanName { get; set; } = string.Empty;

    public int WorkoutPlanVersionNumber { get; set; }

    public string WorkoutDayName { get; set; } = string.Empty;

    public int WorkoutDayNumber { get; set; }

    public DateOnly WorkoutDate { get; set; }

    public string TimeZoneId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime? PausedAtUtc { get; set; }

    public int TotalPausedSeconds { get; set; }

    public int? TotalDurationSeconds { get; set; }

    public string? Notes { get; set; }

    public int TotalExercises { get; set; }

    public int PendingExercises { get; set; }

    public int InProgressExercises { get; set; }

    public int CompletedExercises { get; set; }

    public int SkippedExercises { get; set; }

    public int TotalPlannedSets { get; set; }

    public int TotalRecordedSets { get; set; }

    public int CompletedSets { get; set; }

    public int SkippedSets { get; set; }

    public decimal ProgressPercentage { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}
