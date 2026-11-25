using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutSessions;

public sealed class StartWorkoutSessionRequest
{
    public Guid MemberWorkoutPlanAssignmentId { get; set; }

    public Guid WorkoutPlanDayId { get; set; }

    public string TimeZoneId { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
