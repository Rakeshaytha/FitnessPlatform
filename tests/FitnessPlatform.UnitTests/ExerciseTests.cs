using FitnessPlatform.Domain.Entities;

namespace FitnessPlatform.UnitTests;

public class ExerciseTests
{
    [Fact]
    public void NewExercise_ShouldGenerateValidId()
    {
        var exercise = new Exercise();

        Assert.NotEqual(Guid.Empty, exercise.Id);
    }

    [Fact]
    public void NewExercise_ShouldBeActiveByDefault()
    {
        var exercise = new Exercise();

        Assert.True(exercise.IsActive);
    }

    [Fact]
    public void NewExercise_ShouldHaveDraftReviewStatus()
    {
        var exercise = new Exercise();

        Assert.Equal("Draft", exercise.ReviewStatus);
    }

    [Fact]
    public void NewExercise_ShouldInitializeUtcTimestamps()
    {
        var beforeCreation = DateTime.UtcNow;

        var exercise = new Exercise();

        var afterCreation = DateTime.UtcNow;

        Assert.InRange(
            exercise.CreatedAtUtc,
            beforeCreation,
            afterCreation);

        Assert.InRange(
            exercise.UpdatedAtUtc,
            beforeCreation,
            afterCreation);

        Assert.Equal(DateTimeKind.Utc, exercise.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, exercise.UpdatedAtUtc.Kind);
    }
}