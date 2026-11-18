using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutPlans;

public sealed class RejectWorkoutPlanRequest
{
    public string Reason { get; set; } = string.Empty;
}
