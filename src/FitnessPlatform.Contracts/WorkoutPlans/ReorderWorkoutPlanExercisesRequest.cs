using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutPlans;

public sealed class ReorderWorkoutPlanExercisesRequest
{
    public List<ReorderWorkoutPlanExerciseItem> Exercises { get; set; } = new();
}
