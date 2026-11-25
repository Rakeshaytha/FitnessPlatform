using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Domain.Constants;

public static class WorkoutSessionStatuses
{
    public const string InProgress = "InProgress";

    public const string Paused = "Paused";

    public const string Completed = "Completed";

    public const string Cancelled = "Cancelled";

    public static bool IsUnfinished(string status)
    {
        return status == InProgress ||
               status == Paused;
    }

    public static bool IsTerminal(string status)
    {
        return status == Completed ||
               status == Cancelled;
    }

    public static bool IsValid(string status)
    {
        return status == InProgress ||
               status == Paused ||
               status == Completed ||
               status == Cancelled;
    }
}