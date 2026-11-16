using System;

namespace FitnessPlatform.Contracts.Profiles;

public sealed class CreateUserProfileRequest
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    public decimal HeightCm { get; set; }

    public decimal WeightKg { get; set; }

    public string CountryCode { get; set; } = string.Empty;
}