using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.MemberWorkoutPlanAssignments;

public sealed class AssignWorkoutPlanRequest
{
    public Guid WorkoutPlanId { get; set; }

    public DateOnly StartDate { get; set; }
        = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly? EndDate { get; set; }

    public string? Notes { get; set; }
}
