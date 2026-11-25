using System;
using System.Collections.Generic;
using System.Text;

using FitnessPlatform.Contracts.WorkoutSessions;
using FitnessPlatform.Domain.Constants;

namespace FitnessPlatform.Application.WorkoutSessions;

public static class WorkoutSessionHistoryValidator
{
    public const int MaxPageSize = 100;

    public static string? Validate(
        WorkoutSessionHistoryRequest request)
    {
        if (request.PageNumber < 1)
        {
            return "Page number must be greater than or equal to 1.";
        }

        if (request.PageSize < 1
            || request.PageSize > MaxPageSize)
        {
            return $"Page size must be between 1 and {MaxPageSize}.";
        }

        var offset = ((long)request.PageNumber - 1)
            * request.PageSize;

        if (offset > int.MaxValue)
        {
            return "The requested page exceeds the supported pagination range.";
        }

        if (request.FromDate.HasValue
            && request.ToDate.HasValue
            && request.ToDate.Value < request.FromDate.Value)
        {
            return "ToDate cannot be earlier than FromDate.";
        }

        if (request.WorkoutPlanId == Guid.Empty)
        {
            return "Workout plan ID cannot be an empty GUID.";
        }

        if (request.Status is not null
            && request.Status != WorkoutSessionStatuses.Completed
            && request.Status != WorkoutSessionStatuses.Cancelled)
        {
            return "History status must be Completed or Cancelled.";
        }

        return null;
    }
}
