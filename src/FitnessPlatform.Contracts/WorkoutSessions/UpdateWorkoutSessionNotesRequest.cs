using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutSessions;

public sealed class UpdateWorkoutSessionNotesRequest
{
    public string? Notes { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}
