
namespace FitnessPlatform.Contracts.WorkoutPlans;

public class WorkoutPlanResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string GoalCode { get; set; } = string.Empty;

    public string ExperienceLevel { get; set; } = string.Empty;

    public string TrainingLocation { get; set; } = string.Empty;

    public int DaysPerWeek { get; set; }

    public int DurationWeeks { get; set; }

    public bool IsActive { get; set; }

    public string ReviewStatus { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
