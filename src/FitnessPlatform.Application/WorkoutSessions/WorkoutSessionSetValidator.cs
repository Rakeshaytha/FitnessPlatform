using System;
using System.Collections.Generic;
using System.Text;

using FitnessPlatform.Contracts.WorkoutSessions;
using FitnessPlatform.Domain.Constants;

namespace FitnessPlatform.Application.WorkoutSessions;

public static class WorkoutSessionSetValidator
{
    public const int MaxActualReps = 10000;

    public const decimal MaxActualWeightKg = 2000m;

    public const int MaxActualDurationSeconds = 86400;

    public const decimal MaxActualDistanceMeters = 500000m;

    public const int MaxActualRestSeconds = 86400;

    public static string? Validate(
        AddWorkoutSessionSetRequest request)
    {
        return WorkoutSessionRequestValidator
                .ValidateRowVersion(request.RowVersion)
            ?? ValidateSetType(request.SetType)
            ?? ValidateMeasurements(
                request.ActualReps,
                request.ActualWeightKg,
                request.ActualDurationSeconds,
                request.ActualDistanceMeters,
                request.ActualRestSeconds,
                request.Rpe)
            ?? WorkoutSessionRequestValidator
                .ValidateNotes(request.Notes);
    }

    public static string? Validate(
        UpdateWorkoutSessionSetRequest request)
    {
        return WorkoutSessionRequestValidator
                .ValidateRowVersion(request.RowVersion)
            ?? ValidateSetType(request.SetType)
            ?? ValidateMeasurements(
                request.ActualReps,
                request.ActualWeightKg,
                request.ActualDurationSeconds,
                request.ActualDistanceMeters,
                request.ActualRestSeconds,
                request.Rpe)
            ?? WorkoutSessionRequestValidator
                .ValidateNotes(request.Notes);
    }

    public static string? Validate(
        CompleteWorkoutSessionSetRequest request)
    {
        var error = WorkoutSessionRequestValidator
                .ValidateRowVersion(request.RowVersion)
            ?? ValidateMeasurements(
                request.ActualReps,
                request.ActualWeightKg,
                request.ActualDurationSeconds,
                request.ActualDistanceMeters,
                request.ActualRestSeconds,
                request.Rpe)
            ?? WorkoutSessionRequestValidator
                .ValidateNotes(request.Notes);

        if (error is not null)
        {
            return error;
        }

        return ValidateCompletedPerformance(
            request.ActualReps,
            request.ActualDurationSeconds,
            request.ActualDistanceMeters);
    }

    public static string? ValidateSetType(string? setType)
    {
        if (string.IsNullOrWhiteSpace(setType))
        {
            return "Set type is required.";
        }

        if (!WorkoutSessionSetTypes.IsValid(setType))
        {
            return "Invalid workout session set type.";
        }

        return null;
    }

    public static string? ValidateMeasurements(
        int? actualReps,
        decimal? actualWeightKg,
        int? actualDurationSeconds,
        decimal? actualDistanceMeters,
        int? actualRestSeconds,
        decimal? rpe)
    {
        if (actualReps is < 0 or > MaxActualReps)
        {
            return $"Actual repetitions must be between 0 and {MaxActualReps}.";
        }

        if (actualWeightKg is < 0m or > MaxActualWeightKg)
        {
            return $"Actual weight must be between 0 and {MaxActualWeightKg} kg.";
        }

        if (!HasValidPrecision(actualWeightKg, 3))
        {
            return "Actual weight supports a maximum of 3 decimal places.";
        }

        if (actualDurationSeconds is < 0 or > MaxActualDurationSeconds)
        {
            return $"Actual duration must be between 0 and {MaxActualDurationSeconds} seconds.";
        }

        if (actualDistanceMeters is < 0m or > MaxActualDistanceMeters)
        {
            return $"Actual distance must be between 0 and {MaxActualDistanceMeters} meters.";
        }

        if (!HasValidPrecision(actualDistanceMeters, 2))
        {
            return "Actual distance supports a maximum of 2 decimal places.";
        }

        if (actualRestSeconds is < 0 or > MaxActualRestSeconds)
        {
            return $"Actual rest must be between 0 and {MaxActualRestSeconds} seconds.";
        }

        if (rpe is < 1m or > 10m)
        {
            return "RPE must be between 1 and 10.";
        }

        if (!HasValidPrecision(rpe, 1))
        {
            return "RPE supports a maximum of 1 decimal place.";
        }

        return null;
    }

    public static string? ValidateCompletedPerformance(
        int? actualReps,
        int? actualDurationSeconds,
        decimal? actualDistanceMeters)
    {
        if (actualReps.GetValueOrDefault() <= 0
            && actualDurationSeconds.GetValueOrDefault() <= 0
            && actualDistanceMeters.GetValueOrDefault() <= 0m)
        {
            return "A completed set requires positive repetitions, duration or distance.";
        }

        return null;
    }

    private static bool HasValidPrecision(
        decimal? value,
        int decimalPlaces)
    {
        return !value.HasValue
            || decimal.Round(value.Value, decimalPlaces)
                == value.Value;
    }
}
