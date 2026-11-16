namespace FitnessPlatform.Domain.Entities;

public class FitnessGoal
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    // Profiles associated with this fitness goal
    public ICollection<UserProfileFitnessGoal> UserProfiles { get; set; }
    = new List<UserProfileFitnessGoal>();
}