using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Application.Profiles;

public static class UserProfileValidator
{
    public static string? Validate(
        string? firstName,
        string? lastName,
        decimal heightCm,
        decimal weightKg,
        string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(firstName) ||
            firstName.Length > 100)
        {
            return "FirstName must contain between 1 and 100 characters.";
        }

        if (lastName is null ||
            lastName.Length > 100)
        {
            return "LastName cannot exceed 100 characters.";
        }

        if (heightCm <= 0 || heightCm > 300)
        {
            return "HeightCm must be between 0 and 300.";
        }

        if (weightKg <= 0 || weightKg > 500)
        {
            return "WeightKg must be between 0 and 500.";
        }

        if (string.IsNullOrWhiteSpace(countryCode) ||
            countryCode.Length != 2)
        {
            return "CountryCode must contain exactly two characters.";
        }

        return null;
    }
}
