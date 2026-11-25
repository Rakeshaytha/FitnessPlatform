using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutSessions;

public sealed class WorkoutSessionHistoryRequest
{
    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    public string? Status { get; set; }

    public Guid? WorkoutPlanId { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
