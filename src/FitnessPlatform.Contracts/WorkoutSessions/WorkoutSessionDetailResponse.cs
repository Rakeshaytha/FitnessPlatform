using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutSessions;

public sealed class WorkoutSessionDetailResponse
{
    public WorkoutSessionResponse Session { get; set; } = new();

    public List<WorkoutSessionExerciseResponse> Exercises { get; set; }
        = new();
}
