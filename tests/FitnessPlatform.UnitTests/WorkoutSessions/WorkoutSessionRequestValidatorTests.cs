using System;
using System.Collections.Generic;
using System.Text;

using FitnessPlatform.Application.WorkoutSessions;
using FitnessPlatform.Contracts.WorkoutSessions;
using Xunit;

namespace FitnessPlatform.UnitTests.WorkoutSessions;

public sealed class WorkoutSessionRequestValidatorTests
{
    private const string ValidRowVersion = "AAAAAAAAAAA=";

    private static StartWorkoutSessionRequest CreateValidRequest()
    {
        return new StartWorkoutSessionRequest
        {
            MemberWorkoutPlanAssignmentId = Guid.NewGuid(),
            WorkoutPlanDayId = Guid.NewGuid(),
            TimeZoneId = "UTC",
            Notes = "Morning workout"
        };
    }

    [Fact]
    public void StartRequest_ValidRequest_ReturnsNull()
    {
        var request = CreateValidRequest();

        Assert.Null(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void StartRequest_EmptyAssignmentId_ReturnsError()
    {
        var request = CreateValidRequest();

        request.MemberWorkoutPlanAssignmentId = Guid.Empty;

        Assert.NotNull(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void StartRequest_EmptyWorkoutDayId_ReturnsError()
    {
        var request = CreateValidRequest();

        request.WorkoutPlanDayId = Guid.Empty;

        Assert.NotNull(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void StartRequest_InvalidTimeZone_ReturnsError()
    {
        var request = CreateValidRequest();

        request.TimeZoneId = "Invalid/TimeZone";

        Assert.NotNull(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void StartRequest_NotesExceedLimit_ReturnsError()
    {
        var request = CreateValidRequest();

        request.Notes = new string('A', 1001);

        Assert.NotNull(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Theory]
    [InlineData("UTC")]
    [InlineData("Asia/Dubai")]
    [InlineData("Asia/Kolkata")]
    [InlineData("Europe/London")]
    [InlineData("America/New_York")]
    public void TimeZone_ValidId_ReturnsNull(string timeZoneId)
    {
        Assert.Null(
            WorkoutSessionRequestValidator
                .ValidateTimeZoneId(timeZoneId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Invalid/TimeZone")]
    [InlineData(" UTC")]
    [InlineData("UTC ")]
    public void TimeZone_InvalidId_ReturnsError(string? timeZoneId)
    {
        Assert.NotNull(
            WorkoutSessionRequestValidator
                .ValidateTimeZoneId(timeZoneId));
    }

    [Fact]
    public void TimeZone_ExceedsMaximumLength_ReturnsError()
    {
        Assert.NotNull(
            WorkoutSessionRequestValidator
                .ValidateTimeZoneId(new string('A', 101)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Workout notes")]
    public void Notes_ValidValue_ReturnsNull(string? notes)
    {
        Assert.Null(
            WorkoutSessionRequestValidator.ValidateNotes(notes));
    }

    [Fact]
    public void Notes_MaximumLength_ReturnsNull()
    {
        Assert.Null(
            WorkoutSessionRequestValidator
                .ValidateNotes(new string('A', 1000)));
    }

    [Fact]
    public void Notes_ExceedsMaximumLength_ReturnsError()
    {
        Assert.NotNull(
            WorkoutSessionRequestValidator
                .ValidateNotes(new string('A', 1001)));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" ", null)]
    [InlineData("  Good workout  ", "Good workout")]
    [InlineData("Good workout", "Good workout")]
    public void NormalizeNotes_ReturnsExpectedValue(
        string? input,
        string? expected)
    {
        Assert.Equal(
            expected,
            WorkoutSessionRequestValidator.NormalizeNotes(input));
    }

    [Theory]
    [InlineData("AAAAAAAAAAA=")]
    [InlineData("AQIDBAUGBwg=")]
    [InlineData("//////////8=")]
    public void RowVersion_ValidToken_ReturnsNull(string rowVersion)
    {
        Assert.Null(
            WorkoutSessionRequestValidator
                .ValidateRowVersion(rowVersion));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("InvalidToken")]
    [InlineData("AQID")]
    [InlineData("AQIDBAUGBwgJ")]
    [InlineData("AAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAB=")]
    [InlineData(" AAAAAAAAAAA=")]
    [InlineData("AAAAAAAAAAA= ")]
    public void RowVersion_InvalidToken_ReturnsError(
        string? rowVersion)
    {
        Assert.NotNull(
            WorkoutSessionRequestValidator
                .ValidateRowVersion(rowVersion));
    }

    [Fact]
    public void DecodeRowVersion_ValidToken_ReturnsExpectedBytes()
    {
        var result = WorkoutSessionRequestValidator
            .TryDecodeRowVersion(
                "AQIDBAUGBwg=",
                out var decoded);

        Assert.True(result);

        Assert.Equal(
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
            decoded);
    }

    [Fact]
    public void DecodeRowVersion_InvalidToken_ReturnsEmptyArray()
    {
        var result = WorkoutSessionRequestValidator
            .TryDecodeRowVersion(
                "InvalidToken",
                out var decoded);

        Assert.False(result);

        Assert.Empty(decoded);
    }

    [Fact]
    public void SessionNotes_ValidRequest_ReturnsNull()
    {
        var request = new UpdateWorkoutSessionNotesRequest
        {
            Notes = "Good workout",
            RowVersion = ValidRowVersion
        };

        Assert.Null(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void SessionNotes_MissingRowVersion_ReturnsError()
    {
        var request = new UpdateWorkoutSessionNotesRequest
        {
            Notes = "Good workout"
        };

        Assert.NotNull(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void SessionNotes_ExcessiveNotes_ReturnsError()
    {
        var request = new UpdateWorkoutSessionNotesRequest
        {
            Notes = new string('A', 1001),
            RowVersion = ValidRowVersion
        };

        Assert.NotNull(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void ExerciseNotes_ValidRequest_ReturnsNull()
    {
        var request = new UpdateWorkoutSessionExerciseNotesRequest
        {
            Notes = "Increase weight next time",
            RowVersion = ValidRowVersion
        };

        Assert.Null(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void ExerciseNotes_MissingRowVersion_ReturnsError()
    {
        var request = new UpdateWorkoutSessionExerciseNotesRequest();

        Assert.NotNull(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void ExerciseNotes_ExcessiveNotes_ReturnsError()
    {
        var request = new UpdateWorkoutSessionExerciseNotesRequest
        {
            Notes = new string('A', 1001),
            RowVersion = ValidRowVersion
        };

        Assert.NotNull(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void ConcurrencyRequest_ValidToken_ReturnsNull()
    {
        var request = new WorkoutSessionConcurrencyRequest
        {
            RowVersion = ValidRowVersion
        };

        Assert.Null(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void ConcurrencyRequest_MissingToken_ReturnsError()
    {
        var request = new WorkoutSessionConcurrencyRequest();

        Assert.NotNull(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void SkipExercise_ValidRequest_ReturnsNull()
    {
        var request = new SkipWorkoutSessionExerciseRequest
        {
            SkipReason = "Equipment unavailable",
            RowVersion = ValidRowVersion
        };

        Assert.Null(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void SkipExercise_MissingRowVersion_ReturnsError()
    {
        var request = new SkipWorkoutSessionExerciseRequest();

        Assert.NotNull(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void SkipExercise_ExcessiveReason_ReturnsError()
    {
        var request = new SkipWorkoutSessionExerciseRequest
        {
            SkipReason = new string('A', 1001),
            RowVersion = ValidRowVersion
        };

        Assert.NotNull(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void SkipSet_ValidRequest_ReturnsNull()
    {
        var request = new SkipWorkoutSessionSetRequest
        {
            Notes = "Insufficient recovery",
            RowVersion = ValidRowVersion
        };

        Assert.Null(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void SkipSet_MissingRowVersion_ReturnsError()
    {
        var request = new SkipWorkoutSessionSetRequest();

        Assert.NotNull(
            WorkoutSessionRequestValidator.Validate(request));
    }

    [Fact]
    public void SkipSet_ExcessiveNotes_ReturnsError()
    {
        var request = new SkipWorkoutSessionSetRequest
        {
            Notes = new string('A', 1001),
            RowVersion = ValidRowVersion
        };

        Assert.NotNull(
            WorkoutSessionRequestValidator.Validate(request));
    }
}
