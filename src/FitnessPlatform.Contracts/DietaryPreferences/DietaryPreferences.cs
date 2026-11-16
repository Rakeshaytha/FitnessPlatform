using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.DietaryPreferences;

public class SaveDietaryPreferenceRequest
{
    public string DietType { get; set; } = string.Empty;

    public string PreferredCuisine { get; set; } = string.Empty;

    public bool IncludesEggs { get; set; }

    public bool IncludesMeat { get; set; }
}
