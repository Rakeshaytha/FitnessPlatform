namespace FitnessPlatform.Domain.Entities;

public class WorkoutPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string GoalCode { get; set; } = string.Empty;

    public string ExperienceLevel { get; set; } = string.Empty;

    public string TrainingLocation { get; set; } = string.Empty;

    public int DaysPerWeek { get; set; }

    public int DurationWeeks { get; set; }

    public bool IsActive { get; set; } = true;

    public string ReviewStatus { get; set; } = "Draft";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}