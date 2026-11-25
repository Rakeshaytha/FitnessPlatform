using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Domain.Constants;

public static class WorkoutSessionSetTypes
{
    public const string Warmup = "Warmup";

    public const string Working = "Working";

    public const string Drop = "Drop";

    public const string Failure = "Failure";

    public const string Cooldown = "Cooldown";

    public static bool IsValid(string setType)
    {
        return setType == Warmup ||
               setType == Working ||
               setType == Drop ||
               setType == Failure ||
               setType == Cooldown;
    }
}
