using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Domain.Constants;

public static class WorkoutSessionSetStatuses
{
    public const string Pending = "Pending";

    public const string Completed = "Completed";

    public const string Skipped = "Skipped";

    public static bool IsTerminal(string status)
    {
        return status == Completed ||
               status == Skipped;
    }

    public static bool IsValid(string status)
    {
        return status == Pending ||
               status == Completed ||
               status == Skipped;
    }
}