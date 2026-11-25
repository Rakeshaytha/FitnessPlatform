using System;
using System.Collections.Generic;
using System.Text;

using FitnessPlatform.Contracts.WorkoutSessions;

namespace FitnessPlatform.Application.WorkoutSessions;

public static class WorkoutSessionRequestValidator
{
    public const int MaxNotesLength = 1000;

    public const int MaxTimeZoneIdLength = 100;

    public const int RowVersionByteLength = 8;

    public static string? Validate(
        StartWorkoutSessionRequest request)
    {
        if (request.MemberWorkoutPlanAssignmentId == Guid.Empty)
        {
            return "Member workout plan assignment ID is required.";
        }

        if (request.WorkoutPlanDayId == Guid.Empty)
        {
            return "Workout plan day ID is required.";
        }

        var timeZoneError = ValidateTimeZoneId(request.TimeZoneId);

        if (timeZoneError is not null)
        {
            return timeZoneError;
        }

        return ValidateNotes(request.Notes);
    }

    public static string? Validate(
        UpdateWorkoutSessionNotesRequest request)
    {
        return ValidateRowVersion(request.RowVersion)
            ?? ValidateNotes(request.Notes);
    }

    public static string? Validate(
        UpdateWorkoutSessionExerciseNotesRequest request)
    {
        return ValidateRowVersion(request.RowVersion)
            ?? ValidateNotes(request.Notes);
    }

    public static string? Validate(
        WorkoutSessionConcurrencyRequest request)
    {
        return ValidateRowVersion(request.RowVersion);
    }

    public static string? Validate(
        SkipWorkoutSessionExerciseRequest request)
    {
        return ValidateRowVersion(request.RowVersion)
            ?? ValidateNotes(request.SkipReason);
    }

    public static string? Validate(
        SkipWorkoutSessionSetRequest request)
    {
        return ValidateRowVersion(request.RowVersion)
            ?? ValidateNotes(request.Notes);
    }

    public static string? ValidateNotes(string? notes)
    {
        if (notes is not null && notes.Length > MaxNotesLength)
        {
            return $"Notes cannot exceed {MaxNotesLength} characters.";
        }

        return null;
    }

    public static string? NormalizeNotes(string? notes)
    {
        return string.IsNullOrWhiteSpace(notes)
            ? null
            : notes.Trim();
    }

    public static string? ValidateTimeZoneId(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return "Time zone ID is required.";
        }

        if (timeZoneId.Length > MaxTimeZoneIdLength)
        {
            return $"Time zone ID cannot exceed {MaxTimeZoneIdLength} characters.";
        }

        if (!string.Equals(
            timeZoneId,
            timeZoneId.Trim(),
            StringComparison.Ordinal))
        {
            return "Time zone ID cannot contain leading or trailing whitespace.";
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

            return null;
        }
        catch (TimeZoneNotFoundException)
        {
            return "Invalid or unsupported time zone ID.";
        }
        catch (InvalidTimeZoneException)
        {
            return "Invalid time zone configuration.";
        }
    }

    public static string? ValidateRowVersion(string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            return "RowVersion is required.";
        }

        if (!TryDecodeRowVersion(rowVersion, out _))
        {
            return "RowVersion must be a valid Base64-encoded 8-byte value.";
        }

        return null;
    }

    public static bool TryDecodeRowVersion(
        string? rowVersion,
        out byte[] decodedRowVersion)
    {
        decodedRowVersion = Array.Empty<byte>();

        if (string.IsNullOrWhiteSpace(rowVersion)
            || rowVersion.Length != 12)
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[RowVersionByteLength];

        if (!Convert.TryFromBase64String(
            rowVersion,
            buffer,
            out var bytesWritten))
        {
            return false;
        }

        if (bytesWritten != RowVersionByteLength)
        {
            return false;
        }

        if (!string.Equals(
            Convert.ToBase64String(buffer),
            rowVersion,
            StringComparison.Ordinal))
        {
            return false;
        }

        decodedRowVersion = buffer.ToArray();

        return true;
    }
}
