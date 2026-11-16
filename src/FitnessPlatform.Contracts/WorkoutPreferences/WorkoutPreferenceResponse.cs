using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutPreferences;

public class WorkoutPreferenceResponse
{
    public Guid Id { get; set; }

    public Guid UserProfileId { get; set; }

    public string TrainingLocation { get; set; } = string.Empty;

    public string ExperienceLevel { get; set; } = string.Empty;

    public int DaysPerWeek { get; set; }

    public int SessionDurationMinutes { get; set; }
}
