using System;
using System.Collections.Generic;
using System.Text;
using FitnessPlatform.Application.Profiles;

namespace FitnessPlatform.UnitTests;

public class UserProfileValidatorTests
{
    [Fact]
    public void Validate_WithValidProfile_ReturnsNull()
    {
        var result = UserProfileValidator.Validate(
            "Rakesh", "Kumar", 175, 75, "IN");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_WithInvalidFirstName_ReturnsError(string? firstName)
    {
        var result = UserProfileValidator.Validate(
            firstName, "Kumar", 175, 75, "IN");

        Assert.Equal(
            "FirstName must contain between 1 and 100 characters.",
            result);
    }

    [Fact]
    public void Validate_WithFirstNameExceeding100Characters_ReturnsError()
    {
        var result = UserProfileValidator.Validate(
            new string('A', 101), "Kumar", 175, 75, "IN");

        Assert.Equal(
            "FirstName must contain between 1 and 100 characters.",
            result);
    }

    [Fact]
    public void Validate_WithLastNameExceeding100Characters_ReturnsError()
    {
        var result = UserProfileValidator.Validate(
            "Rakesh", new string('A', 101), 175, 75, "IN");

        Assert.Equal(
            "LastName cannot exceed 100 characters.",
            result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(301)]
    public void Validate_WithInvalidHeight_ReturnsError(int height)
    {
        var result = UserProfileValidator.Validate(
            "Rakesh", "Kumar", height, 75, "IN");

        Assert.Equal(
            "HeightCm must be between 0 and 300.",
            result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    public void Validate_WithInvalidWeight_ReturnsError(int weight)
    {
        var result = UserProfileValidator.Validate(
            "Rakesh", "Kumar", 175, weight, "IN");

        Assert.Equal(
            "WeightKg must be between 0 and 500.",
            result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("I")]
    [InlineData("IND")]
    [InlineData(null)]
    public void Validate_WithInvalidCountryCode_ReturnsError(string? countryCode)
    {
        var result = UserProfileValidator.Validate(
            "Rakesh", "Kumar", 175, 75, countryCode);

        Assert.Equal(
            "CountryCode must contain exactly two characters.",
            result);
    }

    [Fact]
    public void Validate_WithBoundaryValues_ReturnsNull()
    {
        var result = UserProfileValidator.Validate(
            new string('A', 100),
            new string('B', 100),
            300,
            500,
            "IN");

        Assert.Null(result);
    }

    [Fact]
    public void Validate_WithEmptyLastName_ReturnsNull()
    {
        var result = UserProfileValidator.Validate(
            "Rakesh", "", 175, 75, "IN");

        Assert.Null(result);
    }
}
