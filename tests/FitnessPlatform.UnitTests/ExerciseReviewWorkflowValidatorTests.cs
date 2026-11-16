using FitnessPlatform.Application.Exercises;
using Xunit;

namespace FitnessPlatform.UnitTests;

public class ExerciseReviewWorkflowValidatorTests
{
    [Theory]
    [InlineData("Draft", true)]
    [InlineData("In Review", false)]
    [InlineData("Approved", false)]
    [InlineData("Retired", false)]
    public void CanSubmitForReview_ShouldValidateStatus(
        string status,
        bool expected)
    {
        var result = ExerciseReviewWorkflowValidator
            .CanSubmitForReview(status);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("In Review", true, true)]
    [InlineData("In Review", false, false)]
    [InlineData("Draft", true, false)]
    [InlineData("Approved", true, false)]
    [InlineData("Retired", true, false)]
    public void CanApprove_ShouldValidateStatusAndActiveFlag(
        string status,
        bool isActive,
        bool expected)
    {
        var result = ExerciseReviewWorkflowValidator
            .CanApprove(status, isActive);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Approved", true)]
    [InlineData("Draft", false)]
    [InlineData("In Review", false)]
    [InlineData("Retired", false)]
    public void CanRetire_ShouldValidateStatus(
        string status,
        bool expected)
    {
        var result = ExerciseReviewWorkflowValidator
            .CanRetire(status);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Draft", true, true)]
    [InlineData("Draft", false, false)]
    [InlineData("In Review", true, false)]
    [InlineData("Approved", true, false)]
    [InlineData("Retired", true, false)]
    public void CanBulkSubmitForReview_ShouldValidateStatusAndActiveFlag(
        string status,
        bool isActive,
        bool expected)
    {
        var result = ExerciseReviewWorkflowValidator
            .CanBulkSubmitForReview(status, isActive);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("In Review", true, true)]
    [InlineData("In Review", false, false)]
    [InlineData("Draft", true, false)]
    [InlineData("Approved", true, false)]
    [InlineData("Retired", true, false)]
    public void CanBulkApprove_ShouldValidateStatusAndActiveFlag(
        string status,
        bool isActive,
        bool expected)
    {
        var result = ExerciseReviewWorkflowValidator
            .CanBulkApprove(status, isActive);

        Assert.Equal(expected, result);
    }
}