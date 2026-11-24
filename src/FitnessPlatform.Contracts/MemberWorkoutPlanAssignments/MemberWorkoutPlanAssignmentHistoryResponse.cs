using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.MemberWorkoutPlanAssignments;

public sealed class MemberWorkoutPlanAssignmentHistoryResponse
{
    public Guid UserProfileId { get; set; }

    public int TotalAssignments { get; set; }

    public MemberWorkoutPlanAssignmentResponse?
        ActiveAssignment
    { get; set; }

    public List<MemberWorkoutPlanAssignmentResponse>
        Assignments
    { get; set; } = new();
}