
namespace FitnessPlatform.Domain.Entities;

public class WorkoutPlanExercise
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkoutPlanDayId { get; set; }

    public Guid ExerciseId { get; set; }

    public int ExerciseOrder { get; set; }

    public int Sets { get; set; }

    public int Reps { get; set; }

    public int RestSeconds { get; set; }

    public int? DurationSeconds { get; set; }

    public string Notes { get; set; } = string.Empty;

    public WorkoutPlanDay WorkoutPlanDay { get; set; } = null!;

    public Exercise Exercise { get; set; } = null!;
}
