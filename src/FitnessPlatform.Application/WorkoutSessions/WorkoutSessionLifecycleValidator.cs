using System;
using System.Collections.Generic;
using System.Text;

using FitnessPlatform.Domain.Constants;

namespace FitnessPlatform.Application.WorkoutSessions;

public static class WorkoutSessionLifecycleValidator
{
    public static bool CanPauseSession(string status)
    {
        return status == WorkoutSessionStatuses.InProgress;
    }

    public static bool CanResumeSession(string status)
    {
        return status == WorkoutSessionStatuses.Paused;
    }

    public static bool CanCompleteSession(string status)
    {
        return status == WorkoutSessionStatuses.InProgress;
    }

    public static bool CanCancelSession(string status)
    {
        return WorkoutSessionStatuses.IsUnfinished(status);
    }

    public static bool CanUpdateSessionNotes(string status)
    {
        return status == WorkoutSessionStatuses.InProgress;
    }

    public static bool CanStartExercise(
        string sessionStatus,
        string exerciseStatus)
    {
        return sessionStatus == WorkoutSessionStatuses.InProgress
            && exerciseStatus == WorkoutSessionExerciseStatuses.Pending;
    }

    public static bool CanCompleteExercise(
        string sessionStatus,
        string exerciseStatus)
    {
        return sessionStatus == WorkoutSessionStatuses.InProgress
            && exerciseStatus == WorkoutSessionExerciseStatuses.InProgress;
    }

    public static bool CanSkipExercise(
        string sessionStatus,
        string exerciseStatus)
    {
        return sessionStatus == WorkoutSessionStatuses.InProgress
            && WorkoutSessionExerciseStatuses.IsUnfinished(exerciseStatus);
    }

    public static bool CanUpdateExerciseNotes(
        string sessionStatus,
        string exerciseStatus)
    {
        return sessionStatus == WorkoutSessionStatuses.InProgress
            && WorkoutSessionExerciseStatuses.IsUnfinished(exerciseStatus);
    }

    public static bool CanAddSet(
        string sessionStatus,
        string exerciseStatus)
    {
        return sessionStatus == WorkoutSessionStatuses.InProgress
            && exerciseStatus == WorkoutSessionExerciseStatuses.InProgress;
    }

    public static bool CanUpdateSet(
        string sessionStatus,
        string exerciseStatus,
        string setStatus)
    {
        return CanAddSet(sessionStatus, exerciseStatus)
            && (setStatus == WorkoutSessionSetStatuses.Pending
                || setStatus == WorkoutSessionSetStatuses.Completed);
    }

    public static bool CanCompleteSet(
        string sessionStatus,
        string exerciseStatus,
        string setStatus)
    {
        return CanAddSet(sessionStatus, exerciseStatus)
            && setStatus == WorkoutSessionSetStatuses.Pending;
    }

    public static bool CanSkipSet(
        string sessionStatus,
        string exerciseStatus,
        string setStatus)
    {
        return CanAddSet(sessionStatus, exerciseStatus)
            && setStatus == WorkoutSessionSetStatuses.Pending;
    }

    public static bool CanDeleteSet(
        string sessionStatus,
        string exerciseStatus,
        string setStatus)
    {
        return CanAddSet(sessionStatus, exerciseStatus)
            && setStatus == WorkoutSessionSetStatuses.Pending;
    }
}
