using System;
using System.Collections.Generic;
using System.Text;

using FitnessPlatform.Contracts.WorkoutPlans;

namespace FitnessPlatform.Contracts.MemberWorkoutPlanAssignments;

public sealed class MemberWorkoutPlanDetailResponse
{
    public MemberActiveWorkoutPlanResponse Assignment { get; set; }
        = new();

    public List<WorkoutPlanDayDetailResponse> Days { get; set; }
        = new();
}
