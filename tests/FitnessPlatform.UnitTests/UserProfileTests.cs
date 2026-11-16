using FitnessPlatform.Domain.Entities;

namespace FitnessPlatform.UnitTests;

public class UserProfileTests
{
    [Fact]
    public void NewUserProfile_ShouldGenerateValidId()
    {
        // Arrange & Act
        var profile = new UserProfile();

        // Assert
        Assert.NotEqual(Guid.Empty, profile.Id);
    }

    [Fact]
    public void NewUserProfile_ShouldInitializeFitnessGoals()
    {
        // Arrange & Act
        var profile = new UserProfile();

        // Assert
        Assert.NotNull(profile.FitnessGoals);
        Assert.Empty(profile.FitnessGoals);
    }

    [Fact]
    public void NewUserProfile_ShouldInitializeUtcTimestamps()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var profile = new UserProfile();

        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.InRange(
            profile.CreatedAtUtc,
            beforeCreation,
            afterCreation);

        Assert.InRange(
            profile.UpdatedAtUtc,
            beforeCreation,
            afterCreation);

        Assert.Equal(DateTimeKind.Utc, profile.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, profile.UpdatedAtUtc.Kind);
    }
}