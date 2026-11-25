using System;
using System.Collections.Generic;
using System.Text;

using FitnessPlatform.Application.WorkoutSessions;
using FitnessPlatform.Contracts.WorkoutSessions;
using FitnessPlatform.Domain.Constants;
using Xunit;

namespace FitnessPlatform.UnitTests.WorkoutSessions;

public sealed class WorkoutSessionSetValidatorTests
{
    private const string ValidRowVersion = "AAAAAAAAAAA=";

    private static AddWorkoutSessionSetRequest CreateValidAddRequest()
    {
        return new AddWorkoutSessionSetRequest
        {
            SetType = WorkoutSessionSetTypes.Working,
            ActualReps = 12,
            ActualWeightKg = 40m,
            ActualRestSeconds = 60,
            Rpe = 7.5m,
            Notes = "Good set",
            RowVersion = ValidRowVersion
        };
    }

    private static UpdateWorkoutSessionSetRequest CreateValidUpdateRequest()
    {
        return new UpdateWorkoutSessionSetRequest
        {
            SetType = WorkoutSessionSetTypes.Working,
            ActualReps = 15,
            ActualWeightKg = 45m,
            ActualRestSeconds = 60,
            Rpe = 8m,
            Notes = "Updated performance",
            RowVersion = ValidRowVersion
        };
    }

    private static CompleteWorkoutSessionSetRequest CreateValidCompleteRequest()
    {
        return new CompleteWorkoutSessionSetRequest
        {
            ActualReps = 12,
            ActualWeightKg = 40m,
            ActualRestSeconds = 60,
            Rpe = 8m,
            RowVersion = ValidRowVersion
        };
    }

    // ==========================================
    // SET TYPE VALIDATION
    // ==========================================

    [Theory]
    [InlineData("Warmup")]
    [InlineData("Working")]
    [InlineData("Drop")]
    [InlineData("Failure")]
    [InlineData("Cooldown")]
    public void SetType_ValidType_ReturnsNull(string setType)
    {
        Assert.Null(
            WorkoutSessionSetValidator.ValidateSetType(setType));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Invalid")]
    [InlineData("working")]
    [InlineData("WORKING")]
    [InlineData(" Working")]
    [InlineData("Working ")]
    public void SetType_InvalidType_ReturnsError(string? setType)
    {
        Assert.NotNull(
            WorkoutSessionSetValidator.ValidateSetType(setType));
    }

    // ==========================================
    // REPETITION VALIDATION
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(10000)]
    public void Repetitions_ValidValue_ReturnsNull(int? value)
    {
        Assert.Null(
            WorkoutSessionSetValidator.ValidateMeasurements(
                value, null, null, null, null, null));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10001)]
    [InlineData(int.MaxValue)]
    public void Repetitions_InvalidValue_ReturnsError(int value)
    {
        Assert.NotNull(
            WorkoutSessionSetValidator.ValidateMeasurements(
                value, null, null, null, null, null));
    }

    // ==========================================
    // WEIGHT VALIDATION
    // ==========================================

    [Theory]
    [InlineData("0")]
    [InlineData("0.001")]
    [InlineData("2.5")]
    [InlineData("40")]
    [InlineData("40.125")]
    [InlineData("40.1250")]
    [InlineData("2000")]
    public void Weight_ValidValue_ReturnsNull(string input)
    {
        var value = decimal.Parse(
            input,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.Null(
            WorkoutSessionSetValidator.ValidateMeasurements(
                null, value, null, null, null, null));
    }

    [Theory]
    [InlineData("-0.001")]
    [InlineData("2000.001")]
    [InlineData("40.1234")]
    [InlineData("0.0001")]
    public void Weight_InvalidValue_ReturnsError(string input)
    {
        var value = decimal.Parse(
            input,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.NotNull(
            WorkoutSessionSetValidator.ValidateMeasurements(
                null, value, null, null, null, null));
    }

    // ==========================================
    // DURATION VALIDATION
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(1800)]
    [InlineData(86400)]
    public void Duration_ValidValue_ReturnsNull(int? value)
    {
        Assert.Null(
            WorkoutSessionSetValidator.ValidateMeasurements(
                null, null, value, null, null, null));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(86401)]
    [InlineData(int.MaxValue)]
    public void Duration_InvalidValue_ReturnsError(int value)
    {
        Assert.NotNull(
            WorkoutSessionSetValidator.ValidateMeasurements(
                null, null, value, null, null, null));
    }

    // ==========================================
    // DISTANCE VALIDATION
    // ==========================================

    [Theory]
    [InlineData("0")]
    [InlineData("0.01")]
    [InlineData("500")]
    [InlineData("1250.25")]
    [InlineData("1250.250")]
    [InlineData("500000")]
    public void Distance_ValidValue_ReturnsNull(string input)
    {
        var value = decimal.Parse(
            input,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.Null(
            WorkoutSessionSetValidator.ValidateMeasurements(
                null, null, null, value, null, null));
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("500000.01")]
    [InlineData("100.123")]
    [InlineData("0.001")]
    public void Distance_InvalidValue_ReturnsError(string input)
    {
        var value = decimal.Parse(
            input,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.NotNull(
            WorkoutSessionSetValidator.ValidateMeasurements(
                null, null, null, value, null, null));
    }

    // ==========================================
    // REST DURATION VALIDATION
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(180)]
    [InlineData(86400)]
    public void RestDuration_ValidValue_ReturnsNull(int? value)
    {
        Assert.Null(
            WorkoutSessionSetValidator.ValidateMeasurements(
                null, null, null, null, value, null));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(86401)]
    [InlineData(int.MaxValue)]
    public void RestDuration_InvalidValue_ReturnsError(int value)
    {
        Assert.NotNull(
            WorkoutSessionSetValidator.ValidateMeasurements(
                null, null, null, null, value, null));
    }

    // ==========================================
    // RPE VALIDATION
    // ==========================================

    [Theory]
    [InlineData("1")]
    [InlineData("5")]
    [InlineData("7.5")]
    [InlineData("7.50")]
    [InlineData("9.9")]
    [InlineData("10")]
    public void Rpe_ValidValue_ReturnsNull(string input)
    {
        var value = decimal.Parse(
            input,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.Null(
            WorkoutSessionSetValidator.ValidateMeasurements(
                null, null, null, null, null, value));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("0.9")]
    [InlineData("10.1")]
    [InlineData("7.55")]
    public void Rpe_InvalidValue_ReturnsError(string input)
    {
        var value = decimal.Parse(
            input,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.NotNull(
            WorkoutSessionSetValidator.ValidateMeasurements(
                null, null, null, null, null, value));
    }

    // ==========================================
    // COMBINED MEASUREMENT VALIDATION
    // ==========================================

    [Fact]
    public void Measurements_AllNull_ReturnsNull()
    {
        Assert.Null(
            WorkoutSessionSetValidator.ValidateMeasurements(
                null, null, null, null, null, null));
    }

    [Fact]
    public void Measurements_AllMaximumValues_ReturnsNull()
    {
        Assert.Null(
            WorkoutSessionSetValidator.ValidateMeasurements(
                10000,
                2000m,
                86400,
                500000m,
                86400,
                10m));
    }

    // ==========================================
    // ADD SET REQUEST VALIDATION
    // ==========================================

    [Fact]
    public void AddSet_ValidRequest_ReturnsNull()
    {
        Assert.Null(
            WorkoutSessionSetValidator.Validate(
                CreateValidAddRequest()));
    }

    [Fact]
    public void AddSet_EmptyPerformance_ReturnsNull()
    {
        var request = new AddWorkoutSessionSetRequest
        {
            RowVersion = ValidRowVersion
        };

        Assert.Null(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void AddSet_MissingRowVersion_ReturnsError()
    {
        var request = CreateValidAddRequest();

        request.RowVersion = string.Empty;

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void AddSet_InvalidRowVersion_ReturnsError()
    {
        var request = CreateValidAddRequest();

        request.RowVersion = "InvalidToken";

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void AddSet_InvalidSetType_ReturnsError()
    {
        var request = CreateValidAddRequest();

        request.SetType = "Invalid";

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void AddSet_NegativeRepetitions_ReturnsError()
    {
        var request = CreateValidAddRequest();

        request.ActualReps = -1;

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void AddSet_InvalidWeightPrecision_ReturnsError()
    {
        var request = CreateValidAddRequest();

        request.ActualWeightKg = 40.1234m;

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void AddSet_MaximumNotesLength_ReturnsNull()
    {
        var request = CreateValidAddRequest();

        request.Notes = new string('A', 1000);

        Assert.Null(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void AddSet_ExcessiveNotes_ReturnsError()
    {
        var request = CreateValidAddRequest();

        request.Notes = new string('A', 1001);

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    // ==========================================
    // UPDATE SET REQUEST VALIDATION
    // ==========================================

    [Fact]
    public void UpdateSet_ValidRequest_ReturnsNull()
    {
        Assert.Null(
            WorkoutSessionSetValidator.Validate(
                CreateValidUpdateRequest()));
    }

    [Fact]
    public void UpdateSet_ClearOptionalMeasurements_ReturnsNull()
    {
        var request = new UpdateWorkoutSessionSetRequest
        {
            SetType = WorkoutSessionSetTypes.Working,
            RowVersion = ValidRowVersion
        };

        Assert.Null(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void UpdateSet_MissingRowVersion_ReturnsError()
    {
        var request = CreateValidUpdateRequest();

        request.RowVersion = string.Empty;

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void UpdateSet_InvalidRowVersion_ReturnsError()
    {
        var request = CreateValidUpdateRequest();

        request.RowVersion = "InvalidToken";

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void UpdateSet_MissingSetType_ReturnsError()
    {
        var request = CreateValidUpdateRequest();

        request.SetType = string.Empty;

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void UpdateSet_InvalidSetType_ReturnsError()
    {
        var request = CreateValidUpdateRequest();

        request.SetType = "Invalid";

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void UpdateSet_InvalidDuration_ReturnsError()
    {
        var request = CreateValidUpdateRequest();

        request.ActualDurationSeconds = -1;

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void UpdateSet_InvalidRpe_ReturnsError()
    {
        var request = CreateValidUpdateRequest();

        request.Rpe = 11m;

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void UpdateSet_ExcessiveNotes_ReturnsError()
    {
        var request = CreateValidUpdateRequest();

        request.Notes = new string('A', 1001);

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    // ==========================================
    // COMPLETE SET REQUEST VALIDATION
    // ==========================================

    [Fact]
    public void CompleteSet_StrengthExercise_ReturnsNull()
    {
        Assert.Null(
            WorkoutSessionSetValidator.Validate(
                CreateValidCompleteRequest()));
    }

    [Fact]
    public void CompleteSet_BodyweightExercise_ReturnsNull()
    {
        var request = new CompleteWorkoutSessionSetRequest
        {
            ActualReps = 20,
            RowVersion = ValidRowVersion
        };

        Assert.Null(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void CompleteSet_DurationExercise_ReturnsNull()
    {
        var request = new CompleteWorkoutSessionSetRequest
        {
            ActualDurationSeconds = 60,
            RowVersion = ValidRowVersion
        };

        Assert.Null(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void CompleteSet_DistanceExercise_ReturnsNull()
    {
        var request = new CompleteWorkoutSessionSetRequest
        {
            ActualDistanceMeters = 500m,
            RowVersion = ValidRowVersion
        };

        Assert.Null(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void CompleteSet_CardioExercise_ReturnsNull()
    {
        var request = new CompleteWorkoutSessionSetRequest
        {
            ActualDurationSeconds = 1800,
            ActualDistanceMeters = 3000m,
            Rpe = 7m,
            RowVersion = ValidRowVersion
        };

        Assert.Null(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void CompleteSet_EmptyPerformance_ReturnsError()
    {
        var request = new CompleteWorkoutSessionSetRequest
        {
            RowVersion = ValidRowVersion
        };

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void CompleteSet_ZeroPerformance_ReturnsError()
    {
        var request = new CompleteWorkoutSessionSetRequest
        {
            ActualReps = 0,
            ActualDurationSeconds = 0,
            ActualDistanceMeters = 0m,
            RowVersion = ValidRowVersion
        };

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void CompleteSet_WeightOnly_ReturnsError()
    {
        var request = new CompleteWorkoutSessionSetRequest
        {
            ActualWeightKg = 40m,
            RowVersion = ValidRowVersion
        };

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void CompleteSet_RpeOnly_ReturnsError()
    {
        var request = new CompleteWorkoutSessionSetRequest
        {
            Rpe = 8m,
            RowVersion = ValidRowVersion
        };

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void CompleteSet_RestOnly_ReturnsError()
    {
        var request = new CompleteWorkoutSessionSetRequest
        {
            ActualRestSeconds = 60,
            RowVersion = ValidRowVersion
        };

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void CompleteSet_MissingRowVersion_ReturnsError()
    {
        var request = CreateValidCompleteRequest();

        request.RowVersion = string.Empty;

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void CompleteSet_InvalidRowVersion_ReturnsError()
    {
        var request = CreateValidCompleteRequest();

        request.RowVersion = "InvalidToken";

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void CompleteSet_NegativeWeight_ReturnsError()
    {
        var request = CreateValidCompleteRequest();

        request.ActualWeightKg = -1m;

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void CompleteSet_InvalidDistancePrecision_ReturnsError()
    {
        var request = CreateValidCompleteRequest();

        request.ActualDistanceMeters = 100.123m;

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void CompleteSet_InvalidRpe_ReturnsError()
    {
        var request = CreateValidCompleteRequest();

        request.Rpe = 10.1m;

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    [Fact]
    public void CompleteSet_ExcessiveNotes_ReturnsError()
    {
        var request = CreateValidCompleteRequest();

        request.Notes = new string('A', 1001);

        Assert.NotNull(
            WorkoutSessionSetValidator.Validate(request));
    }

    // ==========================================
    // COMPLETED PERFORMANCE VALIDATION
    // ==========================================

    [Fact]
    public void CompletedPerformance_PositiveRepetitions_ReturnsNull()
    {
        Assert.Null(
            WorkoutSessionSetValidator.ValidateCompletedPerformance(
                1, null, null));
    }

    [Fact]
    public void CompletedPerformance_PositiveDuration_ReturnsNull()
    {
        Assert.Null(
            WorkoutSessionSetValidator.ValidateCompletedPerformance(
                null, 1, null));
    }

    [Fact]
    public void CompletedPerformance_PositiveDistance_ReturnsNull()
    {
        Assert.Null(
            WorkoutSessionSetValidator.ValidateCompletedPerformance(
                null, null, 0.01m));
    }

    [Fact]
    public void CompletedPerformance_AllNull_ReturnsError()
    {
        Assert.NotNull(
            WorkoutSessionSetValidator.ValidateCompletedPerformance(
                null, null, null));
    }

    [Fact]
    public void CompletedPerformance_AllZero_ReturnsError()
    {
        Assert.NotNull(
            WorkoutSessionSetValidator.ValidateCompletedPerformance(
                0, 0, 0m));
    }
}