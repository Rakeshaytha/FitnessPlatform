using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutPlans;

public sealed class ReorderWorkoutPlanDaysRequest
{
    public List<ReorderWorkoutPlanDayItem> Days { get; set; } = new();
}
