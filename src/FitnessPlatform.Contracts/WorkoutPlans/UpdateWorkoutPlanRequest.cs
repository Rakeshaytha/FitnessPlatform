using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutPlans;

public class UpdateWorkoutPlanRequest
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string GoalCode { get; set; } = string.Empty;

    public string ExperienceLevel { get; set; } = string.Empty;

    public string TrainingLocation { get; set; } = string.Empty;

    public int DaysPerWeek { get; set; }

    public int DurationWeeks { get; set; }
}
