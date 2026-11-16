namespace FitnessPlatform.Domain.Entities;

public class UserProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    public decimal HeightCm { get; set; }

    public decimal WeightKg { get; set; }

    public string CountryCode { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<UserProfileFitnessGoal> FitnessGoals { get; set; }
    = new List<UserProfileFitnessGoal>();
}