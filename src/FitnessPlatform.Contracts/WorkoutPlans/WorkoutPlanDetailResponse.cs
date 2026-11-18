namespace FitnessPlatform.Contracts.WorkoutPlans;

public class WorkoutPlanDetailResponse : WorkoutPlanResponse
{
    public List<WorkoutPlanDayDetailResponse> Days { get; set; } = new();
}

public class WorkoutPlanDayDetailResponse : WorkoutPlanDayResponse
{
    public List<WorkoutPlanExerciseDetailResponse> Exercises { get; set; } = new();
}

public class WorkoutPlanExerciseDetailResponse : WorkoutPlanExerciseResponse
{
    public string ExerciseCode { get; set; } = string.Empty;
    public string ExerciseName { get; set; } = string.Empty;
}
