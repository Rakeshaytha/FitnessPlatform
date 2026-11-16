namespace FitnessPlatform.Domain.Entities;

public class WorkoutPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserProfileId { get; set; }

    public string TrainingLocation { get; set; } = "Gym";

    public string ExperienceLevel { get; set; } = "Beginner";

    public int DaysPerWeek { get; set; } = 3;

    public int SessionDurationMinutes { get; set; } = 45;
}