
namespace FitnessPlatform.Domain.Entities;

public class WorkoutPlanDay
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkoutPlanId { get; set; }

    public int DayNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsRestDay { get; set; }

    public int? EstimatedDurationMinutes { get; set; }

    public WorkoutPlan WorkoutPlan { get; set; } = null!;

    public ICollection<WorkoutPlanExercise> Exercises { get; set; }
        = new List<WorkoutPlanExercise>();
}
