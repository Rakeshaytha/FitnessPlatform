using System.Net;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionActiveIntegrationTests
{
    [Theory]
    [InlineData("None")]
    [InlineData("InProgress")]
    [InlineData("Paused")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task ActiveEndpoint_ReturnsExpectedLifecycle(
        string status)
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client = WorkoutSessionTestData.CreateClient(factory);

        var fixture = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client);

        Guid? sessionId = null;

        if (status != "None")
        {
            var started = await WorkoutSessionExecutionTestData
                .StartAsync(client, fixture);

            sessionId = started.Session.Id;

            await WorkoutSessionExecutionTestData.SetStatusAsync(
                factory, sessionId.Value, status);
        }

        if (status is "None" or "Completed" or "Cancelled")
        {
            using var response = await client.GetAsync(
                $"{WorkoutSessionTestData.RoutePrefix}/active");

            Assert.Equal(HttpStatusCode.NoContent,
                response.StatusCode);

            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
        else
        {
            var active = await WorkoutSessionExecutionTestData
                .GetActiveAsync(client);

            Assert.Equal(sessionId, active.Session.Id);
            Assert.Equal(status, active.Session.Status);
        }
    }

    [Theory]
    [InlineData("Exercises")]
    [InlineData("Sets")]
    [InlineData("Progress")]
    [InlineData("InactiveAssignment")]
    [InlineData("ReadOnly")]
    public async Task ActiveEndpoint_ReturnsCompletePersistedData(
        string scenario)
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client = WorkoutSessionTestData.CreateClient(factory);

        var fixture = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client);

        var started = await WorkoutSessionExecutionTestData
            .StartAsync(client, fixture);

        await WorkoutSessionExecutionTestData.AddPerformanceAsync(
            factory, started.Session.Id);

        if (scenario == "InactiveAssignment")
        {
            await WorkoutSessionExecutionTestData
                .DeactivateAssignmentAsync(
                    factory, fixture.AssignmentId);
        }

        var before = await WorkoutSessionExecutionTestData
            .LoadAsync(factory, started.Session.Id);

        var active = await WorkoutSessionExecutionTestData
            .GetActiveAsync(client);

        Assert.Equal(started.Session.Id, active.Session.Id);

        switch (scenario)
        {
            case "Exercises":
                Assert.Equal(2, active.Exercises.Count);
                Assert.Equal(new[] { 1, 2 },
                    active.Exercises.Select(x => x.ExerciseOrder));
                break;

            case "Sets":
                Assert.Equal(2, active.Session.TotalRecordedSets);
                Assert.Equal(2, active.Session.CompletedSets);
                Assert.Equal(new[] { 1, 2 },
                    active.Exercises[0].Sets.Select(x => x.SetNumber));
                Assert.Equal(18L,
                    active.Exercises[0].TotalActualReps);
                Assert.Equal(800m,
                    active.Exercises[0].TotalVolumeKg);
                break;

            case "Progress":
                Assert.Equal(1, active.Session.CompletedExercises);
                Assert.Equal(1, active.Session.PendingExercises);
                Assert.Equal(50m, active.Session.ProgressPercentage);
                break;

            case "InactiveAssignment":
                Assert.Equal(fixture.AssignmentId,
                    active.Session.MemberWorkoutPlanAssignmentId);
                Assert.Equal("InProgress", active.Session.Status);
                break;

            case "ReadOnly":
                var after = await WorkoutSessionExecutionTestData
                    .LoadAsync(factory, started.Session.Id);

                Assert.Equal(before.RowVersion, after.RowVersion);
                Assert.Equal(before.UpdatedAtUtc, after.UpdatedAtUtc);
                Assert.Equal(before.Status, after.Status);
                Assert.Equal(
                    Convert.ToBase64String(before.RowVersion),
                    active.Session.RowVersion);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scenario));
        }
    }
}