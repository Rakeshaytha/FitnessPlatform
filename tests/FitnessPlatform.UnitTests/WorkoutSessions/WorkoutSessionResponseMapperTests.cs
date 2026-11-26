using FitnessPlatform.Application.WorkoutSessions;
using FitnessPlatform.Domain.Constants;
using FitnessPlatform.Domain.Entities;

namespace FitnessPlatform.UnitTests.WorkoutSessions;

public class WorkoutSessionResponseMapperTests
{
    private static readonly DateTime TestUtc =
        new(2026, 11, 26, 6, 0, 0, DateTimeKind.Utc);

    private static WorkoutSession CreateSession(
        string status = WorkoutSessionStatuses.InProgress)
    {
        return new WorkoutSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = Guid.NewGuid(),
            MemberWorkoutPlanAssignmentId = Guid.NewGuid(),
            WorkoutPlanId = Guid.NewGuid(),
            WorkoutPlanDayId = Guid.NewGuid(),

            WorkoutPlanName = "Strength Training",
            WorkoutPlanVersionNumber = 2,

            WorkoutDayName = "Upper Body",
            WorkoutDayNumber = 3,

            WorkoutDate = new DateOnly(2026, 11, 26),
            TimeZoneId = "Asia/Dubai",

            Status = status,

            StartedAtUtc = TestUtc,
            TotalPausedSeconds = 0,

            Notes = "Session notes",

            CreatedAtUtc = TestUtc,
            UpdatedAtUtc = TestUtc,

            RowVersion = new byte[]
            {
                1, 2, 3, 4, 5, 6, 7, 8
            }
        };
    }

    private static WorkoutSessionExercise CreateExercise(
        WorkoutSession session,
        int exerciseOrder = 1,
        string status =
            WorkoutSessionExerciseStatuses.Pending,
        int plannedSets = 3)
    {
        var exercise = new WorkoutSessionExercise
        {
            Id = Guid.NewGuid(),

            WorkoutSessionId = session.Id,
            WorkoutSession = session,

            WorkoutPlanExerciseId = Guid.NewGuid(),
            ExerciseId = Guid.NewGuid(),

            ExerciseCode = $"EX-{exerciseOrder}",
            ExerciseName = $"Exercise {exerciseOrder}",

            MuscleGroup = "Chest",
            Equipment = "Barbell",

            ExerciseInstructions = "Perform with control.",

            ExerciseOrder = exerciseOrder,

            PlannedSets = plannedSets,
            PlannedReps = 12,
            PlannedRestSeconds = 60,
            PlannedDurationSeconds = null,
            PlannedNotes = "Maintain proper form.",

            Status = status,

            CreatedAtUtc = TestUtc,
            UpdatedAtUtc = TestUtc
        };

        session.Exercises.Add(exercise);

        return exercise;
    }

    private static WorkoutSessionSet CreateSet(
        WorkoutSessionExercise exercise,
        int setNumber = 1,
        string status =
            WorkoutSessionSetStatuses.Completed,
        int? reps = null,
        decimal? weightKg = null,
        int? durationSeconds = null,
        decimal? distanceMeters = null,
        int? restSeconds = null,
        decimal? rpe = null,
        string setType = WorkoutSessionSetTypes.Working)
    {
        var set = new WorkoutSessionSet
        {
            Id = Guid.NewGuid(),

            WorkoutSessionExerciseId = exercise.Id,
            WorkoutSessionExercise = exercise,

            SetNumber = setNumber,
            SetType = setType,

            Status = status,

            ActualReps = reps,
            ActualWeightKg = weightKg,
            ActualDurationSeconds = durationSeconds,
            ActualDistanceMeters = distanceMeters,
            ActualRestSeconds = restSeconds,

            Rpe = rpe,

            CompletedAtUtc =
                status == WorkoutSessionSetStatuses.Completed
                    ? TestUtc.AddMinutes(1)
                    : null,

            SkippedAtUtc =
                status == WorkoutSessionSetStatuses.Skipped
                    ? TestUtc.AddMinutes(1)
                    : null,

            CreatedAtUtc = TestUtc,
            UpdatedAtUtc = TestUtc.AddMinutes(1)
        };

        exercise.Sets.Add(set);

        return set;
    }

    [Theory]
    [InlineData(0, 0, 0, "0")]
    [InlineData(1, 0, 0, "0")]
    [InlineData(1, 1, 0, "100")]
    [InlineData(1, 0, 1, "100")]
    [InlineData(4, 1, 0, "25")]
    [InlineData(4, 1, 1, "50")]
    [InlineData(8, 4, 1, "62.5")]
    [InlineData(8, 6, 2, "100")]
    [InlineData(3, 1, 0, "33.33")]
    [InlineData(3, 2, 0, "66.67")]
    [InlineData(6, 1, 0, "16.67")]
    [InlineData(32, 1, 0, "3.13")]
    public void CalculateProgressPercentage_ValidCounts_ReturnsExpectedProgress(
    int total,
    int completed,
    int skipped,
    string expected)
    {
        var result =
            WorkoutSessionResponseMapper.CalculateProgressPercentage(
                total,
                completed,
                skipped);

        Assert.Equal(
            decimal.Parse(
                expected,
                System.Globalization.CultureInfo.InvariantCulture),
            result);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(5, -1, 0)]
    [InlineData(5, 0, -1)]
    public void CalculateProgressPercentage_NegativeCounts_ThrowsException(
    int total,
    int completed,
    int skipped)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WorkoutSessionResponseMapper
                .CalculateProgressPercentage(
                    total,
                    completed,
                    skipped));
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(3, 4, 0)]
    [InlineData(3, 2, 2)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue)]
    public void CalculateProgressPercentage_InvalidTotals_ThrowsException(
    int total,
    int completed,
    int skipped)
    {
        Assert.Throws<ArgumentException>(
            () => WorkoutSessionResponseMapper
                .CalculateProgressPercentage(
                    total,
                    completed,
                    skipped));
    }

    [Theory]
    [InlineData(12, "40", "480")]
    [InlineData(10, "45.5", "455")]
    [InlineData(8, "12.125", "97")]
    [InlineData(20, "0", "0")]
    [InlineData(0, "50", "0")]
    public void CalculateSetVolume_CompletedSet_ReturnsExpectedVolume(
    int reps,
    string weight,
    string expectedVolume)
    {
        var culture =
            System.Globalization.CultureInfo.InvariantCulture;

        var set = new WorkoutSessionSet
        {
            Status = WorkoutSessionSetStatuses.Completed,
            ActualReps = reps,
            ActualWeightKg = decimal.Parse(weight, culture)
        };

        var result =
            WorkoutSessionResponseMapper.CalculateSetVolume(set);

        Assert.Equal<decimal?>(
            decimal.Parse(expectedVolume, culture),
            result);
    }
    [Theory]
    [InlineData(null, null)]
    [InlineData(12, null)]
    [InlineData(null, "40")]
    [InlineData(0, null)]
    public void CalculateSetVolume_MissingMeasurements_ReturnsNull(
        int? reps,
        string? weight)
    {
        var set = new WorkoutSessionSet
        {
            Status = WorkoutSessionSetStatuses.Completed,

            ActualReps = reps,

            ActualWeightKg = weight is null
                ? null
                : decimal.Parse(
                    weight,
                    System.Globalization.CultureInfo.InvariantCulture)
        };

        var result =
            WorkoutSessionResponseMapper.CalculateSetVolume(set);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(WorkoutSessionSetStatuses.Pending)]
    [InlineData(WorkoutSessionSetStatuses.Skipped)]
    public void CalculateSetVolume_NonCompletedSet_ReturnsNull(
    string status)
    {
        var set = new WorkoutSessionSet
        {
            Status = status,
            ActualReps = 12,
            ActualWeightKg = 50m
        };

        var result =
            WorkoutSessionResponseMapper.CalculateSetVolume(set);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(WorkoutSessionSetTypes.Warmup)]
    [InlineData(WorkoutSessionSetTypes.Working)]
    [InlineData(WorkoutSessionSetTypes.Drop)]
    [InlineData(WorkoutSessionSetTypes.Failure)]
    [InlineData(WorkoutSessionSetTypes.Cooldown)]
    public void CalculateSetVolume_CompletedSetTypes_ReturnsVolume(
    string setType)
    {
        var set = new WorkoutSessionSet
        {
            SetType = setType,
            Status = WorkoutSessionSetStatuses.Completed,
            ActualReps = 10,
            ActualWeightKg = 40m
        };

        var result =
            WorkoutSessionResponseMapper.CalculateSetVolume(set);

        Assert.Equal<decimal?>(400m, result);
    }

    [Fact]
    public void MapSessionResponse_MapsSessionProperties()
    {
        var session = CreateSession();

        session.TotalPausedSeconds = 120;
        session.UpdatedAtUtc = TestUtc.AddMinutes(10);

        var result =
            WorkoutSessionResponseMapper.MapSessionResponse(session);

        Assert.Equal(session.Id, result.Id);
        Assert.Equal(session.UserProfileId, result.UserProfileId);

        Assert.Equal(
            session.MemberWorkoutPlanAssignmentId,
            result.MemberWorkoutPlanAssignmentId);

        Assert.Equal(session.WorkoutPlanId, result.WorkoutPlanId);
        Assert.Equal(session.WorkoutPlanDayId, result.WorkoutPlanDayId);

        Assert.Equal(session.WorkoutPlanName, result.WorkoutPlanName);

        Assert.Equal(
            session.WorkoutPlanVersionNumber,
            result.WorkoutPlanVersionNumber);

        Assert.Equal(session.WorkoutDayName, result.WorkoutDayName);
        Assert.Equal(session.WorkoutDayNumber, result.WorkoutDayNumber);

        Assert.Equal(session.WorkoutDate, result.WorkoutDate);
        Assert.Equal(session.TimeZoneId, result.TimeZoneId);

        Assert.Equal(session.Status, result.Status);

        Assert.Equal(session.StartedAtUtc, result.StartedAtUtc);
        Assert.Equal(session.CompletedAtUtc, result.CompletedAtUtc);
        Assert.Equal(session.CancelledAtUtc, result.CancelledAtUtc);
        Assert.Equal(session.PausedAtUtc, result.PausedAtUtc);

        Assert.Equal(
            session.TotalPausedSeconds,
            result.TotalPausedSeconds);

        Assert.Equal(
            session.TotalDurationSeconds,
            result.TotalDurationSeconds);

        Assert.Equal(session.Notes, result.Notes);

        Assert.Equal(session.CreatedAtUtc, result.CreatedAtUtc);
        Assert.Equal(session.UpdatedAtUtc, result.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(WorkoutSessionStatuses.InProgress)]
    [InlineData(WorkoutSessionStatuses.Paused)]
    [InlineData(WorkoutSessionStatuses.Completed)]
    [InlineData(WorkoutSessionStatuses.Cancelled)]
    public void MapSessionResponse_MapsLifecycleStatus(
    string status)
    {
        var session = CreateSession(status);

        if (status == WorkoutSessionStatuses.Paused)
        {
            session.PausedAtUtc = TestUtc.AddMinutes(10);
        }
        else if (status == WorkoutSessionStatuses.Completed)
        {
            session.CompletedAtUtc = TestUtc.AddMinutes(30);
            session.TotalDurationSeconds = 1800;
        }
        else if (status == WorkoutSessionStatuses.Cancelled)
        {
            session.CancelledAtUtc = TestUtc.AddMinutes(15);
            session.TotalDurationSeconds = 900;
        }

        var result =
            WorkoutSessionResponseMapper.MapSessionResponse(session);

        Assert.Equal(status, result.Status);

        Assert.Equal(session.PausedAtUtc, result.PausedAtUtc);
        Assert.Equal(session.CompletedAtUtc, result.CompletedAtUtc);
        Assert.Equal(session.CancelledAtUtc, result.CancelledAtUtc);

        Assert.Equal(
            session.TotalDurationSeconds,
            result.TotalDurationSeconds);
    }

    [Fact]
    public void MapSessionResponse_CalculatesExecutionCounts()
    {
        var session = CreateSession();

        var completed = CreateExercise(
            session,
            1,
            WorkoutSessionExerciseStatuses.Completed);

        var inProgress = CreateExercise(
            session,
            2,
            WorkoutSessionExerciseStatuses.InProgress);

        CreateExercise(
            session,
            3,
            WorkoutSessionExerciseStatuses.Pending);

        CreateExercise(
            session,
            4,
            WorkoutSessionExerciseStatuses.Skipped);

        CreateSet(completed, 1, reps: 12, weightKg: 40m);
        CreateSet(completed, 2, reps: 10, weightKg: 45m);
        CreateSet(completed, 3, reps: 8, weightKg: 50m);

        CreateSet(inProgress, 1, reps: 12, weightKg: 30m);

        CreateSet(
            inProgress,
            2,
            WorkoutSessionSetStatuses.Pending);

        CreateSet(
            inProgress,
            3,
            WorkoutSessionSetStatuses.Skipped);

        var result =
            WorkoutSessionResponseMapper.MapSessionResponse(session);

        Assert.Equal(4, result.TotalExercises);
        Assert.Equal(1, result.PendingExercises);
        Assert.Equal(1, result.InProgressExercises);
        Assert.Equal(1, result.CompletedExercises);
        Assert.Equal(1, result.SkippedExercises);

        Assert.Equal(12, result.TotalPlannedSets);
        Assert.Equal(6, result.TotalRecordedSets);
        Assert.Equal(4, result.CompletedSets);
        Assert.Equal(1, result.SkippedSets);

        Assert.Equal(50m, result.ProgressPercentage);
    }

    [Fact]
    public void MapExerciseResponse_MapsExerciseProperties()
    {
        var session = CreateSession();

        var exercise = CreateExercise(
            session,
            2,
            WorkoutSessionExerciseStatuses.Skipped);

        exercise.PlannedDurationSeconds = 120;
        exercise.StartedAtUtc = TestUtc.AddMinutes(1);
        exercise.SkippedAtUtc = TestUtc.AddMinutes(5);

        exercise.Notes = "Exercise feedback";
        exercise.SkipReason = "Equipment unavailable";

        exercise.UpdatedAtUtc = TestUtc.AddMinutes(5);

        var result =
            WorkoutSessionResponseMapper.MapExerciseResponse(exercise);

        Assert.Equal(exercise.Id, result.Id);

        Assert.Equal(
            exercise.WorkoutSessionId,
            result.WorkoutSessionId);

        Assert.Equal(
            exercise.WorkoutPlanExerciseId,
            result.WorkoutPlanExerciseId);

        Assert.Equal(exercise.ExerciseId, result.ExerciseId);
        Assert.Equal(exercise.ExerciseCode, result.ExerciseCode);
        Assert.Equal(exercise.ExerciseName, result.ExerciseName);

        Assert.Equal(exercise.MuscleGroup, result.MuscleGroup);
        Assert.Equal(exercise.Equipment, result.Equipment);

        Assert.Equal(
            exercise.ExerciseInstructions,
            result.ExerciseInstructions);

        Assert.Equal(exercise.ExerciseOrder, result.ExerciseOrder);
        Assert.Equal(exercise.PlannedSets, result.PlannedSets);
        Assert.Equal(exercise.PlannedReps, result.PlannedReps);

        Assert.Equal(
            exercise.PlannedRestSeconds,
            result.PlannedRestSeconds);

        Assert.Equal(
            exercise.PlannedDurationSeconds,
            result.PlannedDurationSeconds);

        Assert.Equal(exercise.PlannedNotes, result.PlannedNotes);
        Assert.Equal(exercise.Status, result.Status);

        Assert.Equal(exercise.StartedAtUtc, result.StartedAtUtc);
        Assert.Equal(exercise.CompletedAtUtc, result.CompletedAtUtc);
        Assert.Equal(exercise.SkippedAtUtc, result.SkippedAtUtc);

        Assert.Equal(exercise.Notes, result.Notes);
        Assert.Equal(exercise.SkipReason, result.SkipReason);

        Assert.Equal(exercise.CreatedAtUtc, result.CreatedAtUtc);
        Assert.Equal(exercise.UpdatedAtUtc, result.UpdatedAtUtc);
    }

    [Fact]
    public void MapSetResponse_MapsSetProperties()
    {
        var session = CreateSession();
        var exercise = CreateExercise(session);

        var set = CreateSet(
            exercise,
            setNumber: 2,
            reps: 12,
            weightKg: 40.5m,
            durationSeconds: 45,
            distanceMeters: 25.5m,
            restSeconds: 60,
            rpe: 8.5m,
            setType: WorkoutSessionSetTypes.Drop);

        set.Notes = "Set feedback";

        var result =
            WorkoutSessionResponseMapper.MapSetResponse(set);

        Assert.Equal(set.Id, result.Id);

        Assert.Equal(
            set.WorkoutSessionExerciseId,
            result.WorkoutSessionExerciseId);

        Assert.Equal(set.SetNumber, result.SetNumber);
        Assert.Equal(set.SetType, result.SetType);

        Assert.Equal(set.ActualReps, result.ActualReps);
        Assert.Equal(set.ActualWeightKg, result.ActualWeightKg);

        Assert.Equal(
            set.ActualDurationSeconds,
            result.ActualDurationSeconds);

        Assert.Equal(
            set.ActualDistanceMeters,
            result.ActualDistanceMeters);

        Assert.Equal(
            set.ActualRestSeconds,
            result.ActualRestSeconds);

        Assert.Equal(set.Rpe, result.Rpe);

        Assert.Equal<decimal?>(486m, result.VolumeKg);

        Assert.Equal(set.Status, result.Status);
        Assert.Equal(set.Notes, result.Notes);

        Assert.Equal(set.CompletedAtUtc, result.CompletedAtUtc);
        Assert.Equal(set.SkippedAtUtc, result.SkippedAtUtc);

        Assert.Equal(set.CreatedAtUtc, result.CreatedAtUtc);
        Assert.Equal(set.UpdatedAtUtc, result.UpdatedAtUtc);
    }

    [Fact]
    public void MapSessionDetailResponse_OrdersExercisesAndSets()
    {
        var session = CreateSession();

        var third = CreateExercise(session, 3);
        var first = CreateExercise(session, 1);
        var second = CreateExercise(session, 2);

        CreateSet(first, 3, reps: 8);
        CreateSet(first, 1, reps: 12);
        CreateSet(first, 2, reps: 10);

        var result =
            WorkoutSessionResponseMapper.MapSessionDetailResponse(
                session);

        Assert.Equal(session.Id, result.Session.Id);

        Assert.Equal(
            new[] { first.Id, second.Id, third.Id },
            result.Exercises.Select(x => x.Id).ToArray());

        Assert.Equal(
            new[] { 1, 2, 3 },
            result.Exercises[0].Sets
                .Select(x => x.SetNumber)
                .ToArray());
    }

    [Fact]
    public void MapSessionResponse_ValidRowVersion_ReturnsBase64()
    {
        var session = CreateSession();

        var result =
            WorkoutSessionResponseMapper.MapSessionResponse(session);

        Assert.Equal("AQIDBAUGBwg=", result.RowVersion);

        Assert.Equal(
            session.RowVersion,
            Convert.FromBase64String(result.RowVersion));
    }

    [Fact]
    public void MapSessionResponse_EmptyRowVersion_ReturnsEmptyString()
    {
        var session = CreateSession();

        session.RowVersion = Array.Empty<byte>();

        var result =
            WorkoutSessionResponseMapper.MapSessionResponse(session);

        Assert.Equal(string.Empty, result.RowVersion);
    }

    [Fact]
    public void MapExerciseResponse_AggregatesCompletedSetsOnly()
    {
        var session = CreateSession();
        var exercise = CreateExercise(session);

        CreateSet(
            exercise, 1,
            reps: 12,
            weightKg: 40m,
            durationSeconds: 30,
            distanceMeters: 10m);

        CreateSet(
            exercise, 2,
            reps: 10,
            weightKg: 45m,
            durationSeconds: 40,
            distanceMeters: 20m);

        CreateSet(
            exercise, 3,
            WorkoutSessionSetStatuses.Pending,
            reps: 100,
            weightKg: 100m,
            durationSeconds: 1000,
            distanceMeters: 1000m);

        CreateSet(
            exercise, 4,
            WorkoutSessionSetStatuses.Skipped,
            reps: 100,
            weightKg: 100m,
            durationSeconds: 1000,
            distanceMeters: 1000m);

        var result =
            WorkoutSessionResponseMapper.MapExerciseResponse(exercise);

        Assert.Equal(4, result.TotalRecordedSets);
        Assert.Equal(1, result.PendingSets);
        Assert.Equal(2, result.CompletedSets);
        Assert.Equal(1, result.SkippedSets);

        Assert.Equal(22L, result.TotalActualReps);
        Assert.Equal(70L, result.TotalActualDurationSeconds);
        Assert.Equal(30m, result.TotalActualDistanceMeters);
        Assert.Equal(930m, result.TotalVolumeKg);

        Assert.Equal(4, result.Sets.Count);

        Assert.Null(result.Sets[2].VolumeKg);
        Assert.Null(result.Sets[3].VolumeKg);
    }

    [Fact]
    public void MapSessionSummaryResponse_AggregatesMultipleExercises()
    {
        var session = CreateSession();

        var strength = CreateExercise(
            session,
            1,
            WorkoutSessionExerciseStatuses.Completed);

        var cardio = CreateExercise(
            session,
            2,
            WorkoutSessionExerciseStatuses.InProgress);

        CreateSet(
            strength, 1,
            reps: 12,
            weightKg: 40m,
            restSeconds: 60,
            rpe: 7m);

        CreateSet(
            strength, 2,
            reps: 10,
            weightKg: 45m,
            restSeconds: 90,
            rpe: 8m);

        CreateSet(
            cardio, 1,
            durationSeconds: 600,
            distanceMeters: 1000m,
            restSeconds: 30,
            rpe: 9m);

        CreateSet(
            cardio, 2,
            WorkoutSessionSetStatuses.Pending,
            reps: 100,
            weightKg: 100m,
            durationSeconds: 1000,
            distanceMeters: 1000m,
            restSeconds: 1000,
            rpe: 10m);

        var result =
            WorkoutSessionResponseMapper.MapSessionSummaryResponse(
                session);

        Assert.Equal(session.Id, result.WorkoutSessionId);
        Assert.Equal(session.WorkoutPlanName, result.WorkoutPlanName);
        Assert.Equal(session.WorkoutDayName, result.WorkoutDayName);
        Assert.Equal(session.WorkoutDate, result.WorkoutDate);
        Assert.Equal(session.Status, result.Status);
        Assert.Equal(session.Notes, result.Notes);

        Assert.Equal(2, result.TotalExercises);
        Assert.Equal(1, result.CompletedExercises);
        Assert.Equal(0, result.SkippedExercises);

        Assert.Equal(6, result.TotalPlannedSets);
        Assert.Equal(4, result.TotalRecordedSets);
        Assert.Equal(3, result.CompletedSets);
        Assert.Equal(0, result.SkippedSets);

        Assert.Equal(22L, result.TotalActualReps);
        Assert.Equal(930m, result.TotalVolumeKg);
        Assert.Equal(600L, result.TotalActualDurationSeconds);
        Assert.Equal(1000m, result.TotalActualDistanceMeters);
        Assert.Equal(180L, result.TotalActualRestSeconds);

        Assert.Equal<decimal?>(8m, result.AverageRpe);
        Assert.Equal(50m, result.ProgressPercentage);
    }

    [Fact]
    public void MapSessionSummaryResponse_MissingRpe_ReturnsNull()
    {
        var session = CreateSession();
        var exercise = CreateExercise(session);

        CreateSet(exercise, 1, reps: 10);

        var result =
            WorkoutSessionResponseMapper.MapSessionSummaryResponse(
                session);

        Assert.Null(result.AverageRpe);
    }

    [Fact]
    public void MapSessionSummaryResponse_AverageRpe_ExcludesMissingAndNonCompleted()
    {
        var session = CreateSession();
        var exercise = CreateExercise(session);

        CreateSet(exercise, 1, reps: 10, rpe: 7m);
        CreateSet(exercise, 2, reps: 10, rpe: 9m);
        CreateSet(exercise, 3, reps: 10);

        CreateSet(
            exercise, 4,
            WorkoutSessionSetStatuses.Pending,
            rpe: 10m);

        CreateSet(
            exercise, 5,
            WorkoutSessionSetStatuses.Skipped,
            rpe: 1m);

        var result =
            WorkoutSessionResponseMapper.MapSessionSummaryResponse(
                session);

        Assert.Equal<decimal?>(8m, result.AverageRpe);
    }

    [Fact]
    public void MapSessionSummaryResponse_AverageRpe_RoundsToTwoDecimals()
    {
        var session = CreateSession();
        var exercise = CreateExercise(session);

        CreateSet(exercise, 1, reps: 10, rpe: 7m);
        CreateSet(exercise, 2, reps: 10, rpe: 8m);
        CreateSet(exercise, 3, reps: 10, rpe: 8m);

        var result =
            WorkoutSessionResponseMapper.MapSessionSummaryResponse(
                session);

        Assert.Equal<decimal?>(7.67m, result.AverageRpe);
    }

    [Fact]
    public void MapSessionResponse_NoExercises_ReturnsZeroCounts()
    {
        var result =
            WorkoutSessionResponseMapper.MapSessionResponse(
                CreateSession());

        Assert.Equal(0, result.TotalExercises);
        Assert.Equal(0, result.PendingExercises);
        Assert.Equal(0, result.InProgressExercises);
        Assert.Equal(0, result.CompletedExercises);
        Assert.Equal(0, result.SkippedExercises);

        Assert.Equal(0, result.TotalPlannedSets);
        Assert.Equal(0, result.TotalRecordedSets);
        Assert.Equal(0, result.CompletedSets);
        Assert.Equal(0, result.SkippedSets);

        Assert.Equal(0m, result.ProgressPercentage);
    }

    [Fact]
    public void MapExerciseResponse_NoSets_ReturnsZeroPerformance()
    {
        var session = CreateSession();
        var exercise = CreateExercise(session);

        var result =
            WorkoutSessionResponseMapper.MapExerciseResponse(exercise);

        Assert.Empty(result.Sets);

        Assert.Equal(0, result.TotalRecordedSets);
        Assert.Equal(0, result.PendingSets);
        Assert.Equal(0, result.CompletedSets);
        Assert.Equal(0, result.SkippedSets);

        Assert.Equal(0L, result.TotalActualReps);
        Assert.Equal(0L, result.TotalActualDurationSeconds);
        Assert.Equal(0m, result.TotalActualDistanceMeters);
        Assert.Equal(0m, result.TotalVolumeKg);
    }

    [Fact]
    public void MapEmptySession_DetailAndSummary_ReturnEmptyPerformance()
    {
        var session = CreateSession();

        var detail =
            WorkoutSessionResponseMapper.MapSessionDetailResponse(
                session);

        var summary =
            WorkoutSessionResponseMapper.MapSessionSummaryResponse(
                session);

        Assert.Empty(detail.Exercises);

        Assert.Equal(0, summary.TotalExercises);
        Assert.Equal(0, summary.TotalRecordedSets);

        Assert.Equal(0L, summary.TotalActualReps);
        Assert.Equal(0L, summary.TotalActualDurationSeconds);
        Assert.Equal(0L, summary.TotalActualRestSeconds);

        Assert.Equal(0m, summary.TotalActualDistanceMeters);
        Assert.Equal(0m, summary.TotalVolumeKg);
        Assert.Equal(0m, summary.ProgressPercentage);

        Assert.Null(summary.AverageRpe);
    }

    [Fact]
    public void MapPerformance_LargeIntegerTotals_UsesLong()
    {
        var session = CreateSession();
        var exercise = CreateExercise(session);

        CreateSet(
            exercise, 1,
            reps: int.MaxValue,
            durationSeconds: int.MaxValue,
            restSeconds: int.MaxValue);

        CreateSet(
            exercise, 2,
            reps: int.MaxValue,
            durationSeconds: int.MaxValue,
            restSeconds: int.MaxValue);

        var exerciseResult =
            WorkoutSessionResponseMapper.MapExerciseResponse(exercise);

        var summary =
            WorkoutSessionResponseMapper.MapSessionSummaryResponse(
                session);

        const long expected = 4294967294L;

        Assert.Equal(expected, exerciseResult.TotalActualReps);

        Assert.Equal(
            expected,
            exerciseResult.TotalActualDurationSeconds);

        Assert.Equal(expected, summary.TotalActualReps);
        Assert.Equal(expected, summary.TotalActualDurationSeconds);
        Assert.Equal(expected, summary.TotalActualRestSeconds);
    }

    [Fact]
    public void MapSessionResponse_Null_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(
            () => WorkoutSessionResponseMapper
                .MapSessionResponse(null!));
    }

    [Fact]
    public void MapSessionDetailResponse_Null_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(
            () => WorkoutSessionResponseMapper
                .MapSessionDetailResponse(null!));
    }

    [Fact]
    public void MapExerciseResponse_Null_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(
            () => WorkoutSessionResponseMapper
                .MapExerciseResponse(null!));
    }

    [Fact]
    public void MapSetResponse_Null_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(
            () => WorkoutSessionResponseMapper
                .MapSetResponse(null!));
    }

    [Fact]
    public void MapSessionSummaryResponse_Null_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(
            () => WorkoutSessionResponseMapper
                .MapSessionSummaryResponse(null!));
    }

    [Fact]
    public void CalculateSetVolume_Null_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(
            () => WorkoutSessionResponseMapper
                .CalculateSetVolume(null!));
    }

    [Fact]
    public void MapSessionDetailResponse_DoesNotModifySourceEntities()
    {
        var session = CreateSession();

        var second = CreateExercise(session, 2);
        var first = CreateExercise(session, 1);

        var secondSet = CreateSet(
            first, 2,
            reps: 10,
            weightKg: 45m);

        var firstSet = CreateSet(
            first, 1,
            reps: 12,
            weightKg: 40m);

        var originalRowVersion = session.RowVersion.ToArray();

        var result =
            WorkoutSessionResponseMapper.MapSessionDetailResponse(
                session);

        result.Session.Notes = "Modified response";
        result.Exercises[0].ExerciseName = "Modified exercise";
        result.Exercises[0].Sets[0].ActualReps = 999;

        Assert.Equal("Session notes", session.Notes);
        Assert.Equal("Exercise 1", first.ExerciseName);
        Assert.Equal<int?>(12, firstSet.ActualReps);

        Assert.Equal(
            new[] { second.Id, first.Id },
            session.Exercises.Select(x => x.Id).ToArray());

        Assert.Equal(
            new[] { secondSet.Id, firstSet.Id },
            first.Sets.Select(x => x.Id).ToArray());

        Assert.Equal(originalRowVersion, session.RowVersion);
    }

    [Fact]
    public void MapSessionSummaryResponse_CancelledSession_PreservesPerformance()
    {
        var session = CreateSession(
            WorkoutSessionStatuses.Cancelled);

        session.CancelledAtUtc = TestUtc.AddMinutes(15);
        session.TotalDurationSeconds = 900;

        var exercise = CreateExercise(
            session,
            1,
            WorkoutSessionExerciseStatuses.InProgress);

        CreateSet(
            exercise, 1,
            reps: 12,
            weightKg: 40m,
            rpe: 8m);

        CreateSet(
            exercise, 2,
            WorkoutSessionSetStatuses.Pending,
            reps: 10,
            weightKg: 50m);

        var result =
            WorkoutSessionResponseMapper.MapSessionSummaryResponse(
                session);

        Assert.Equal(
            WorkoutSessionStatuses.Cancelled,
            result.Status);

        Assert.Equal(session.CancelledAtUtc, result.CancelledAtUtc);
        Assert.Equal<int?>(900, result.TotalDurationSeconds);

        Assert.Equal(1, result.CompletedSets);
        Assert.Equal(12L, result.TotalActualReps);
        Assert.Equal(480m, result.TotalVolumeKg);
        Assert.Equal<decimal?>(8m, result.AverageRpe);

        Assert.Equal(0m, result.ProgressPercentage);
    }


}
