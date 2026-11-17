
namespace FitnessPlatform.Contracts.WorkoutPlans;

public class AddWorkoutPlanExerciseRequest
{
    public Guid ExerciseId { get; set; }

    public int ExerciseOrder { get; set; }

    public int Sets { get; set; }

    public int Reps { get; set; }

    public int RestSeconds { get; set; }

    public int? DurationSeconds { get; set; }

    public string Notes { get; set; } = string.Empty;
}
