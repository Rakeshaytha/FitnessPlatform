using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.MemberWorkoutPlanAssignments;

public sealed class MemberWorkoutPlanHistoryResponse
{
    public Guid UserProfileId { get; set; }

    public int TotalAssignments { get; set; }

    public MemberActiveWorkoutPlanResponse? ActiveAssignment
    {
        get;
        set;
    }

    public List<MemberActiveWorkoutPlanResponse> Assignments
    {
        get;
        set;
    } = new();
}