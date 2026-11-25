using System;
using System.Collections.Generic;
using System.Text;

using FitnessPlatform.Application.WorkoutSessions;
using FitnessPlatform.Domain.Constants;
using Xunit;

namespace FitnessPlatform.UnitTests.WorkoutSessions;

public sealed class WorkoutSessionLifecycleValidatorTests
{
    private static readonly string[] SessionStatuses =
    {
        WorkoutSessionStatuses.InProgress,
        WorkoutSessionStatuses.Paused,
        WorkoutSessionStatuses.Completed,
        WorkoutSessionStatuses.Cancelled
    };

    private static readonly string[] ExerciseStatuses =
    {
        WorkoutSessionExerciseStatuses.Pending,
        WorkoutSessionExerciseStatuses.InProgress,
        WorkoutSessionExerciseStatuses.Completed,
        WorkoutSessionExerciseStatuses.Skipped
    };

    private static readonly string[] SetStatuses =
    {
        WorkoutSessionSetStatuses.Pending,
        WorkoutSessionSetStatuses.Completed,
        WorkoutSessionSetStatuses.Skipped
    };

    // ==========================================
    // TEST DATA GENERATORS
    // ==========================================

    public static IEnumerable<object[]> SessionStatusCases()
    {
        foreach (var sessionStatus in SessionStatuses)
        {
            yield return new object[]
            {
                sessionStatus
            };
        }
    }

    public static IEnumerable<object[]> ExerciseStatusCases()
    {
        foreach (var sessionStatus in SessionStatuses)
        {
            foreach (var exerciseStatus in ExerciseStatuses)
            {
                yield return new object[]
                {
                    sessionStatus,
                    exerciseStatus
                };
            }
        }
    }

    public static IEnumerable<object[]> SetStatusCases()
    {
        foreach (var sessionStatus in SessionStatuses)
        {
            foreach (var exerciseStatus in ExerciseStatuses)
            {
                foreach (var setStatus in SetStatuses)
                {
                    yield return new object[]
                    {
                        sessionStatus,
                        exerciseStatus,
                        setStatus
                    };
                }
            }
        }
    }

    // ==========================================
    // SESSION LIFECYCLE VALIDATION
    // ==========================================

    [Theory]
    [MemberData(nameof(SessionStatusCases))]
    public void CanPauseSession_ReturnsExpectedResult(
        string status)
    {
        var expected =
            status == WorkoutSessionStatuses.InProgress;

        Assert.Equal(
            expected,
            WorkoutSessionLifecycleValidator
                .CanPauseSession(status));
    }

    [Theory]
    [MemberData(nameof(SessionStatusCases))]
    public void CanResumeSession_ReturnsExpectedResult(
        string status)
    {
        var expected =
            status == WorkoutSessionStatuses.Paused;

        Assert.Equal(
            expected,
            WorkoutSessionLifecycleValidator
                .CanResumeSession(status));
    }

    [Theory]
    [MemberData(nameof(SessionStatusCases))]
    public void CanCompleteSession_ReturnsExpectedResult(
        string status)
    {
        var expected =
            status == WorkoutSessionStatuses.InProgress;

        Assert.Equal(
            expected,
            WorkoutSessionLifecycleValidator
                .CanCompleteSession(status));
    }

    [Theory]
    [MemberData(nameof(SessionStatusCases))]
    public void CanCancelSession_ReturnsExpectedResult(
        string status)
    {
        var expected =
            status == WorkoutSessionStatuses.InProgress
            || status == WorkoutSessionStatuses.Paused;

        Assert.Equal(
            expected,
            WorkoutSessionLifecycleValidator
                .CanCancelSession(status));
    }

    [Theory]
    [MemberData(nameof(SessionStatusCases))]
    public void CanUpdateSessionNotes_ReturnsExpectedResult(
        string status)
    {
        var expected =
            status == WorkoutSessionStatuses.InProgress;

        Assert.Equal(
            expected,
            WorkoutSessionLifecycleValidator
                .CanUpdateSessionNotes(status));
    }

    // ==========================================
    // EXERCISE LIFECYCLE VALIDATION
    // ==========================================

    [Theory]
    [MemberData(nameof(ExerciseStatusCases))]
    public void CanStartExercise_ReturnsExpectedResult(
        string sessionStatus,
        string exerciseStatus)
    {
        var expected =
            sessionStatus == WorkoutSessionStatuses.InProgress
            && exerciseStatus ==
                WorkoutSessionExerciseStatuses.Pending;

        Assert.Equal(
            expected,
            WorkoutSessionLifecycleValidator
                .CanStartExercise(
                    sessionStatus,
                    exerciseStatus));
    }

    [Theory]
    [MemberData(nameof(ExerciseStatusCases))]
    public void CanCompleteExercise_ReturnsExpectedResult(
        string sessionStatus,
        string exerciseStatus)
    {
        var expected =
            sessionStatus == WorkoutSessionStatuses.InProgress
            && exerciseStatus ==
                WorkoutSessionExerciseStatuses.InProgress;

        Assert.Equal(
            expected,
            WorkoutSessionLifecycleValidator
                .CanCompleteExercise(
                    sessionStatus,
                    exerciseStatus));
    }

    [Theory]
    [MemberData(nameof(ExerciseStatusCases))]
    public void CanSkipExercise_ReturnsExpectedResult(
        string sessionStatus,
        string exerciseStatus)
    {
        var expected =
            sessionStatus == WorkoutSessionStatuses.InProgress
            && (exerciseStatus ==
                    WorkoutSessionExerciseStatuses.Pending
                || exerciseStatus ==
                    WorkoutSessionExerciseStatuses.InProgress);

        Assert.Equal(
            expected,
            WorkoutSessionLifecycleValidator
                .CanSkipExercise(
                    sessionStatus,
                    exerciseStatus));
    }

    [Theory]
    [MemberData(nameof(ExerciseStatusCases))]
    public void CanUpdateExerciseNotes_ReturnsExpectedResult(
        string sessionStatus,
        string exerciseStatus)
    {
        var expected =
            sessionStatus == WorkoutSessionStatuses.InProgress
            && (exerciseStatus ==
                    WorkoutSessionExerciseStatuses.Pending
                || exerciseStatus ==
                    WorkoutSessionExerciseStatuses.InProgress);

        Assert.Equal(
            expected,
            WorkoutSessionLifecycleValidator
                .CanUpdateExerciseNotes(
                    sessionStatus,
                    exerciseStatus));
    }

    [Theory]
    [MemberData(nameof(ExerciseStatusCases))]
    public void CanAddSet_ReturnsExpectedResult(
        string sessionStatus,
        string exerciseStatus)
    {
        var expected =
            sessionStatus == WorkoutSessionStatuses.InProgress
            && exerciseStatus ==
                WorkoutSessionExerciseStatuses.InProgress;

        Assert.Equal(
            expected,
            WorkoutSessionLifecycleValidator
                .CanAddSet(
                    sessionStatus,
                    exerciseStatus));
    }

    // ==========================================
    // SET LIFECYCLE VALIDATION
    // ==========================================

    [Theory]
    [MemberData(nameof(SetStatusCases))]
    public void CanUpdateSet_ReturnsExpectedResult(
        string sessionStatus,
        string exerciseStatus,
        string setStatus)
    {
        var expected =
            sessionStatus == WorkoutSessionStatuses.InProgress
            && exerciseStatus ==
                WorkoutSessionExerciseStatuses.InProgress
            && (setStatus == WorkoutSessionSetStatuses.Pending
                || setStatus ==
                    WorkoutSessionSetStatuses.Completed);

        Assert.Equal(
            expected,
            WorkoutSessionLifecycleValidator
                .CanUpdateSet(
                    sessionStatus,
                    exerciseStatus,
                    setStatus));
    }

    [Theory]
    [MemberData(nameof(SetStatusCases))]
    public void CanCompleteSet_ReturnsExpectedResult(
        string sessionStatus,
        string exerciseStatus,
        string setStatus)
    {
        var expected =
            sessionStatus == WorkoutSessionStatuses.InProgress
            && exerciseStatus ==
                WorkoutSessionExerciseStatuses.InProgress
            && setStatus == WorkoutSessionSetStatuses.Pending;

        Assert.Equal(
            expected,
            WorkoutSessionLifecycleValidator
                .CanCompleteSet(
                    sessionStatus,
                    exerciseStatus,
                    setStatus));
    }

    [Theory]
    [MemberData(nameof(SetStatusCases))]
    public void CanSkipSet_ReturnsExpectedResult(
        string sessionStatus,
        string exerciseStatus,
        string setStatus)
    {
        var expected =
            sessionStatus == WorkoutSessionStatuses.InProgress
            && exerciseStatus ==
                WorkoutSessionExerciseStatuses.InProgress
            && setStatus == WorkoutSessionSetStatuses.Pending;

        Assert.Equal(
            expected,
            WorkoutSessionLifecycleValidator
                .CanSkipSet(
                    sessionStatus,
                    exerciseStatus,
                    setStatus));
    }

    [Theory]
    [MemberData(nameof(SetStatusCases))]
    public void CanDeleteSet_ReturnsExpectedResult(
        string sessionStatus,
        string exerciseStatus,
        string setStatus)
    {
        var expected =
            sessionStatus == WorkoutSessionStatuses.InProgress
            && exerciseStatus ==
                WorkoutSessionExerciseStatuses.InProgress
            && setStatus == WorkoutSessionSetStatuses.Pending;

        Assert.Equal(
            expected,
            WorkoutSessionLifecycleValidator
                .CanDeleteSet(
                    sessionStatus,
                    exerciseStatus,
                    setStatus));
    }

    // ==========================================
    // INVALID SESSION STATUS VALIDATION
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Invalid")]
    [InlineData("inprogress")]
    [InlineData("INPROGRESS")]
    [InlineData(" InProgress")]
    [InlineData("InProgress ")]
    [InlineData("paused")]
    [InlineData("Pending")]
    [InlineData("Skipped")]
    public void InvalidSessionStatus_AllOperationsReturnFalse(
        string? status)
    {
        Assert.False(
            WorkoutSessionLifecycleValidator
                .CanPauseSession(status!));

        Assert.False(
            WorkoutSessionLifecycleValidator
                .CanResumeSession(status!));

        Assert.False(
            WorkoutSessionLifecycleValidator
                .CanCompleteSession(status!));

        Assert.False(
            WorkoutSessionLifecycleValidator
                .CanCancelSession(status!));

        Assert.False(
            WorkoutSessionLifecycleValidator
                .CanUpdateSessionNotes(status!));
    }

    // ==========================================
    // INVALID PARENT SESSION STATUS
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Invalid")]
    [InlineData("inprogress")]
    [InlineData("INPROGRESS")]
    [InlineData(" InProgress")]
    [InlineData("InProgress ")]
    public void InvalidParentSessionStatus_ExerciseOperationsReturnFalse(
        string? sessionStatus)
    {
        foreach (var exerciseStatus in ExerciseStatuses)
        {
            Assert.False(
                WorkoutSessionLifecycleValidator
                    .CanStartExercise(
                        sessionStatus!,
                        exerciseStatus));

            Assert.False(
                WorkoutSessionLifecycleValidator
                    .CanCompleteExercise(
                        sessionStatus!,
                        exerciseStatus));

            Assert.False(
                WorkoutSessionLifecycleValidator
                    .CanSkipExercise(
                        sessionStatus!,
                        exerciseStatus));

            Assert.False(
                WorkoutSessionLifecycleValidator
                    .CanUpdateExerciseNotes(
                        sessionStatus!,
                        exerciseStatus));

            Assert.False(
                WorkoutSessionLifecycleValidator
                    .CanAddSet(
                        sessionStatus!,
                        exerciseStatus));
        }
    }

    // ==========================================
    // INVALID EXERCISE STATUS VALIDATION
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Invalid")]
    [InlineData("pending")]
    [InlineData("inprogress")]
    [InlineData("INPROGRESS")]
    [InlineData(" InProgress")]
    [InlineData("InProgress ")]
    [InlineData("Paused")]
    [InlineData("Cancelled")]
    public void InvalidExerciseStatus_AllOperationsReturnFalse(
        string? exerciseStatus)
    {
        var sessionStatus =
            WorkoutSessionStatuses.InProgress;

        Assert.False(
            WorkoutSessionLifecycleValidator
                .CanStartExercise(
                    sessionStatus,
                    exerciseStatus!));

        Assert.False(
            WorkoutSessionLifecycleValidator
                .CanCompleteExercise(
                    sessionStatus,
                    exerciseStatus!));

        Assert.False(
            WorkoutSessionLifecycleValidator
                .CanSkipExercise(
                    sessionStatus,
                    exerciseStatus!));

        Assert.False(
            WorkoutSessionLifecycleValidator
                .CanUpdateExerciseNotes(
                    sessionStatus,
                    exerciseStatus!));

        Assert.False(
            WorkoutSessionLifecycleValidator
                .CanAddSet(
                    sessionStatus,
                    exerciseStatus!));
    }

    // ==========================================
    // INVALID SET STATUS VALIDATION
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Invalid")]
    [InlineData("pending")]
    [InlineData("completed")]
    [InlineData("PENDING")]
    [InlineData(" Pending")]
    [InlineData("Pending ")]
    [InlineData("InProgress")]
    [InlineData("Cancelled")]
    public void InvalidSetStatus_AllOperationsReturnFalse(
        string? setStatus)
    {
        AssertAllSetOperationsDenied(
            WorkoutSessionStatuses.InProgress,
            WorkoutSessionExerciseStatuses.InProgress,
            setStatus!);
    }

    // ==========================================
    // INVALID SET PARENT SESSION STATUS
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Invalid")]
    [InlineData("inprogress")]
    [InlineData("INPROGRESS")]
    [InlineData(" InProgress")]
    [InlineData("InProgress ")]
    public void InvalidSetParentSessionStatus_AllOperationsReturnFalse(
        string? sessionStatus)
    {
        foreach (var setStatus in SetStatuses)
        {
            AssertAllSetOperationsDenied(
                sessionStatus!,
                WorkoutSessionExerciseStatuses.InProgress,
                setStatus);
        }
    }

    // ==========================================
    // INVALID SET PARENT EXERCISE STATUS
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Invalid")]
    [InlineData("inprogress")]
    [InlineData("INPROGRESS")]
    [InlineData(" InProgress")]
    [InlineData("InProgress ")]
    public void InvalidSetParentExerciseStatus_AllOperationsReturnFalse(
        string? exerciseStatus)
    {
        foreach (var setStatus in SetStatuses)
        {
            AssertAllSetOperationsDenied(
                WorkoutSessionStatuses.InProgress,
                exerciseStatus!,
                setStatus);
        }
    }

    // ==========================================
    // SESSION DOMAIN STATUS CLASSIFICATION
    // ==========================================

    [Theory]
    [InlineData("InProgress", true, false)]
    [InlineData("Paused", true, false)]
    [InlineData("Completed", false, true)]
    [InlineData("Cancelled", false, true)]
    public void SessionStatus_Classification_ReturnsExpectedResult(
        string status,
        bool expectedUnfinished,
        bool expectedTerminal)
    {
        Assert.True(
            WorkoutSessionStatuses.IsValid(status));

        Assert.Equal(
            expectedUnfinished,
            WorkoutSessionStatuses.IsUnfinished(status));

        Assert.Equal(
            expectedTerminal,
            WorkoutSessionStatuses.IsTerminal(status));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Invalid")]
    [InlineData("inprogress")]
    [InlineData("Pending")]
    [InlineData("Skipped")]
    public void SessionStatus_InvalidClassification_ReturnsFalse(
        string? status)
    {
        Assert.False(
            WorkoutSessionStatuses.IsValid(status!));

        Assert.False(
            WorkoutSessionStatuses.IsUnfinished(status!));

        Assert.False(
            WorkoutSessionStatuses.IsTerminal(status!));
    }

    // ==========================================
    // EXERCISE DOMAIN STATUS CLASSIFICATION
    // ==========================================

    [Theory]
    [InlineData("Pending", true, false)]
    [InlineData("InProgress", true, false)]
    [InlineData("Completed", false, true)]
    [InlineData("Skipped", false, true)]
    public void ExerciseStatus_Classification_ReturnsExpectedResult(
        string status,
        bool expectedUnfinished,
        bool expectedTerminal)
    {
        Assert.True(
            WorkoutSessionExerciseStatuses.IsValid(status));

        Assert.Equal(
            expectedUnfinished,
            WorkoutSessionExerciseStatuses.IsUnfinished(status));

        Assert.Equal(
            expectedTerminal,
            WorkoutSessionExerciseStatuses.IsTerminal(status));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Invalid")]
    [InlineData("pending")]
    [InlineData("Paused")]
    [InlineData("Cancelled")]
    public void ExerciseStatus_InvalidClassification_ReturnsFalse(
        string? status)
    {
        Assert.False(
            WorkoutSessionExerciseStatuses.IsValid(status!));

        Assert.False(
            WorkoutSessionExerciseStatuses.IsUnfinished(status!));

        Assert.False(
            WorkoutSessionExerciseStatuses.IsTerminal(status!));
    }

    // ==========================================
    // SET DOMAIN STATUS CLASSIFICATION
    // ==========================================

    [Theory]
    [InlineData("Pending", false)]
    [InlineData("Completed", true)]
    [InlineData("Skipped", true)]
    public void SetStatus_Classification_ReturnsExpectedResult(
        string status,
        bool expectedTerminal)
    {
        Assert.True(
            WorkoutSessionSetStatuses.IsValid(status));

        Assert.Equal(
            expectedTerminal,
            WorkoutSessionSetStatuses.IsTerminal(status));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Invalid")]
    [InlineData("pending")]
    [InlineData("InProgress")]
    [InlineData("Cancelled")]
    public void SetStatus_InvalidClassification_ReturnsFalse(
        string? status)
    {
        Assert.False(
            WorkoutSessionSetStatuses.IsValid(status!));

        Assert.False(
            WorkoutSessionSetStatuses.IsTerminal(status!));
    }

    // ==========================================
    // DOMAIN CONSTANT VALUE VERIFICATION
    // ==========================================

    [Fact]
    public void SessionStatusConstants_HaveExpectedValues()
    {
        Assert.Equal(
            "InProgress",
            WorkoutSessionStatuses.InProgress);

        Assert.Equal(
            "Paused",
            WorkoutSessionStatuses.Paused);

        Assert.Equal(
            "Completed",
            WorkoutSessionStatuses.Completed);

        Assert.Equal(
            "Cancelled",
            WorkoutSessionStatuses.Cancelled);
    }

    [Fact]
    public void ExerciseStatusConstants_HaveExpectedValues()
    {
        Assert.Equal(
            "Pending",
            WorkoutSessionExerciseStatuses.Pending);

        Assert.Equal(
            "InProgress",
            WorkoutSessionExerciseStatuses.InProgress);

        Assert.Equal(
            "Completed",
            WorkoutSessionExerciseStatuses.Completed);

        Assert.Equal(
            "Skipped",
            WorkoutSessionExerciseStatuses.Skipped);
    }

    [Fact]
    public void SetStatusConstants_HaveExpectedValues()
    {
        Assert.Equal(
            "Pending",
            WorkoutSessionSetStatuses.Pending);

        Assert.Equal(
            "Completed",
            WorkoutSessionSetStatuses.Completed);

        Assert.Equal(
            "Skipped",
            WorkoutSessionSetStatuses.Skipped);
    }

    // ==========================================
    // PRIVATE ASSERTION HELPERS
    // ==========================================

    private static void AssertAllSetOperationsDenied(
        string sessionStatus,
        string exerciseStatus,
        string setStatus)
    {
        Assert.False(
            WorkoutSessionLifecycleValidator
                .CanUpdateSet(
                    sessionStatus,
                    exerciseStatus,
                    setStatus));

        Assert.False(
            WorkoutSessionLifecycleValidator
                .CanCompleteSet(
                    sessionStatus,
                    exerciseStatus,
                    setStatus));

        Assert.False(
            WorkoutSessionLifecycleValidator
                .CanSkipSet(
                    sessionStatus,
                    exerciseStatus,
                    setStatus));

        Assert.False(
            WorkoutSessionLifecycleValidator
                .CanDeleteSet(
                    sessionStatus,
                    exerciseStatus,
                    setStatus));
    }
}