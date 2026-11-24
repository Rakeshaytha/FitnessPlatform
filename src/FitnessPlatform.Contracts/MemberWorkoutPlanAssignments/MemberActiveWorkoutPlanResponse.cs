using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.MemberWorkoutPlanAssignments;

public sealed class MemberActiveWorkoutPlanResponse
{
    public Guid Id { get; set; }

    public Guid UserProfileId { get; set; }

    public Guid WorkoutPlanId { get; set; }

    public string WorkoutPlanName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string GoalCode { get; set; } = string.Empty;

    public string ExperienceLevel { get; set; } = string.Empty;

    public string TrainingLocation { get; set; } = string.Empty;

    public int DaysPerWeek { get; set; }

    public int DurationWeeks { get; set; }

    public int VersionNumber { get; set; }

    public Guid VersionGroupId { get; set; }

    public bool IsLatestVersion { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DateTime AssignedAtUtc { get; set; }
}