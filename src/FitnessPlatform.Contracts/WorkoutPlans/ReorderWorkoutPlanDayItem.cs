using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutPlans;

public sealed class ReorderWorkoutPlanDayItem
{
    public Guid DayId { get; set; }

    public int DayNumber { get; set; }
}