using System;
using System.Collections.Generic;

namespace FitnessPlatform.Contracts.Onboarding;

public class UserOnboardingResponse
{
    public Guid ProfileId { get; set; }

    public Guid UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    public decimal HeightCm { get; set; }

    public decimal WeightKg { get; set; }

    public string CountryCode { get; set; } = string.Empty;

    public List<OnboardingFitnessGoalResponse> FitnessGoals { get; set; } = new();

    public OnboardingWorkoutPreferenceResponse? WorkoutPreferences { get; set; }

    public OnboardingDietaryPreferenceResponse? DietaryPreferences { get; set; }

    public bool IsOnboardingComplete { get; set; }
}

public class OnboardingFitnessGoalResponse
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

public class OnboardingWorkoutPreferenceResponse
{
    public string TrainingLocation { get; set; } = string.Empty;

    public string ExperienceLevel { get; set; } = string.Empty;

    public int DaysPerWeek { get; set; }

    public int SessionDurationMinutes { get; set; }
}

public class OnboardingDietaryPreferenceResponse
{
    public string DietType { get; set; } = string.Empty;

    public string PreferredCuisine { get; set; } = string.Empty;

    public bool IncludesEggs { get; set; }

    public bool IncludesMeat { get; set; }
}