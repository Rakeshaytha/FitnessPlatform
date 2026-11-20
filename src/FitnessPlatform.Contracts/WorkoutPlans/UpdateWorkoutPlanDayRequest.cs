using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutPlans;

public class UpdateWorkoutPlanDayRequest
{
    public int DayNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsRestDay { get; set; }

    public int? EstimatedDurationMinutes { get; set; }
}
