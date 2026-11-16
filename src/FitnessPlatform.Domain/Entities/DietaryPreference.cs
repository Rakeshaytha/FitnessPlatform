namespace FitnessPlatform.Domain.Entities;

public class DietaryPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserProfileId { get; set; }

    public string DietType { get; set; } = string.Empty;

    public string PreferredCuisine { get; set; } = string.Empty;

    public bool IncludesEggs { get; set; }

    public bool IncludesMeat { get; set; }
}