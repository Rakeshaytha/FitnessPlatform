using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Domain.Constants;

public static class WorkoutSessionExerciseStatuses
{
    public const string Pending = "Pending";

    public const string InProgress = "InProgress";

    public const string Completed = "Completed";

    public const string Skipped = "Skipped";

    public static bool IsUnfinished(string status)
    {
        return status == Pending ||
               status == InProgress;
    }

    public static bool IsTerminal(string status)
    {
        return status == Completed ||
               status == Skipped;
    }

    public static bool IsValid(string status)
    {
        return status == Pending ||
               status == InProgress ||
               status == Completed ||
               status == Skipped;
    }
}
