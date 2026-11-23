using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutPlans;

public sealed class ReorderWorkoutPlanExerciseItem
{
    public Guid AssignmentId { get; set; }

    public int ExerciseOrder { get; set; }
}
