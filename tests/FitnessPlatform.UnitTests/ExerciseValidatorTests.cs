using FitnessPlatform.Application.Exercises;
using FitnessPlatform.Contracts.Exercises;
using Xunit;

namespace FitnessPlatform.UnitTests;

public class ExerciseValidatorTests
{
    private static SaveExerciseRequest CreateValidRequest()
    {
        return new SaveExerciseRequest
        {
            Code = "PUSH-UP",
            Name = "Push Up",
            Description = "Upper body exercise",
            MuscleGroup = "Chest",
            Equipment = "Bodyweight",
            DifficultyLevel = "Beginner",
            TrainingLocation = "Home",
            Instructions = "Keep your back straight.",
            VideoUrl = null,
            GifUrl = null
        };
    }

    [Fact]
    public void ValidExercise_ShouldReturnNull()
    {
        var request = CreateValidRequest();

        var result = ExerciseValidator.Validate(request);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void InvalidCode_ShouldReturnRequiredFieldError(string code)
    {
        var request = CreateValidRequest();
        request.Code = code;

        var result = ExerciseValidator.Validate(request);

        Assert.Equal("Required exercise fields are missing.", result);
    }

    [Fact]
    public void MissingName_ShouldReturnRequiredFieldError()
    {
        var request = CreateValidRequest();
        request.Name = "";

        var result = ExerciseValidator.Validate(request);

        Assert.Equal("Required exercise fields are missing.", result);
    }

    [Fact]
    public void MissingMuscleGroup_ShouldReturnRequiredFieldError()
    {
        var request = CreateValidRequest();
        request.MuscleGroup = "";

        var result = ExerciseValidator.Validate(request);

        Assert.Equal("Required exercise fields are missing.", result);
    }

    [Fact]
    public void MissingEquipment_ShouldReturnRequiredFieldError()
    {
        var request = CreateValidRequest();
        request.Equipment = "";

        var result = ExerciseValidator.Validate(request);

        Assert.Equal("Required exercise fields are missing.", result);
    }

    [Fact]
    public void MissingDifficultyLevel_ShouldReturnRequiredFieldError()
    {
        var request = CreateValidRequest();
        request.DifficultyLevel = "";

        var result = ExerciseValidator.Validate(request);

        Assert.Equal("Required exercise fields are missing.", result);
    }

    [Fact]
    public void MissingTrainingLocation_ShouldReturnRequiredFieldError()
    {
        var request = CreateValidRequest();
        request.TrainingLocation = "";

        var result = ExerciseValidator.Validate(request);

        Assert.Equal("Required exercise fields are missing.", result);
    }

    [Fact]
    public void CodeExceeding50Characters_ShouldReturnLengthError()
    {
        var request = CreateValidRequest();
        request.Code = new string('A', 51);

        var result = ExerciseValidator.Validate(request);

        Assert.Equal("One or more fields exceed their maximum length.", result);
    }

    [Fact]
    public void NameExceeding200Characters_ShouldReturnLengthError()
    {
        var request = CreateValidRequest();
        request.Name = new string('A', 201);

        var result = ExerciseValidator.Validate(request);

        Assert.Equal("One or more fields exceed their maximum length.", result);
    }

    [Fact]
    public void DescriptionExceeding2000Characters_ShouldReturnLengthError()
    {
        var request = CreateValidRequest();
        request.Description = new string('A', 2001);

        var result = ExerciseValidator.Validate(request);

        Assert.Equal("One or more fields exceed their maximum length.", result);
    }

    [Fact]
    public void InstructionsExceeding4000Characters_ShouldReturnLengthError()
    {
        var request = CreateValidRequest();
        request.Instructions = new string('A', 4001);

        var result = ExerciseValidator.Validate(request);

        Assert.Equal("One or more fields exceed their maximum length.", result);
    }

    [Fact]
    public void VideoUrlExceeding1000Characters_ShouldReturnLengthError()
    {
        var request = CreateValidRequest();
        request.VideoUrl = new string('A', 1001);

        var result = ExerciseValidator.Validate(request);

        Assert.Equal("One or more fields exceed their maximum length.", result);
    }

    [Fact]
    public void GifUrlExceeding1000Characters_ShouldReturnLengthError()
    {
        var request = CreateValidRequest();
        request.GifUrl = new string('A', 1001);

        var result = ExerciseValidator.Validate(request);

        Assert.Equal("One or more fields exceed their maximum length.", result);
    }

    [Fact]
    public void MaximumAllowedLengths_ShouldReturnNull()
    {
        var request = CreateValidRequest();

        request.Code = new string('A', 50);
        request.Name = new string('A', 200);
        request.Description = new string('A', 2000);
        request.MuscleGroup = new string('A', 100);
        request.Equipment = new string('A', 100);
        request.DifficultyLevel = new string('A', 50);
        request.TrainingLocation = new string('A', 50);
        request.Instructions = new string('A', 4000);
        request.VideoUrl = new string('A', 1000);
        request.GifUrl = new string('A', 1000);

        var result = ExerciseValidator.Validate(request);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("push-up", "PUSH-UP")]
    [InlineData(" push-up ", "PUSH-UP")]
    [InlineData("Squat", "SQUAT")]
    public void NormalizeCode_ShouldTrimAndConvertToUppercase(
        string input,
        string expected)
    {
        var result = ExerciseValidator.NormalizeCode(input);

        Assert.Equal(expected, result);
    }
}