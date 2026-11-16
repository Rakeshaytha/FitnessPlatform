using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.WorkoutPreferences;

public class SaveWorkoutPreferenceRequest
{
    public string TrainingLocation { get; set; } = "Gym";

    public string ExperienceLevel { get; set; } = "Beginner";

    public int DaysPerWeek { get; set; } = 3;

    public int SessionDurationMinutes { get; set; } = 45;
}