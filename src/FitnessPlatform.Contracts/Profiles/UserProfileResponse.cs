using System;
namespace FitnessPlatform.Contracts.Profiles;

public sealed class UserProfileResponse
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    public decimal HeightCm { get; set; }

    public decimal WeightKg { get; set; }

    public string CountryCode { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}