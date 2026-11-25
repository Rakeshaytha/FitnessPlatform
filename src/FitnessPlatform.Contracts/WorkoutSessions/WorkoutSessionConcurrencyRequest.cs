using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutSessions;

public sealed class WorkoutSessionConcurrencyRequest
{
    public string RowVersion { get; set; } = string.Empty;
}
