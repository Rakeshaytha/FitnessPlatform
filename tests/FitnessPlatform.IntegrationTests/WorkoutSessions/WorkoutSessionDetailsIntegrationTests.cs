using System.Net;
using System.Text.Json;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionDetailsIntegrationTests
{
    [Theory]
    [InlineData("InProgress")]
    [InlineData("Paused")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task DetailsEndpoint_ReturnsAllSessionStatuses(
        string status)
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client = WorkoutSessionTestData.CreateClient(factory);

        var fixture = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client);

        var started = await WorkoutSessionExecutionTestData
            .StartAsync(client, fixture);

        await WorkoutSessionExecutionTestData.SetStatusAsync(
            factory, started.Session.Id, status);

        var result = await WorkoutSessionExecutionTestData
            .GetAsync(client, started.Session.Id);

        Assert.Equal(started.Session.Id, result.Session.Id);
        Assert.Equal(status, result.Session.Status);
        Assert.Equal(fixture.Member.ProfileId,
            result.Session.UserProfileId);

        if (status == "Paused")
            Assert.NotNull(result.Session.PausedAtUtc);

        if (status == "Completed")
        {
            Assert.NotNull(result.Session.CompletedAtUtc);
            Assert.Equal(600, result.Session.TotalDurationSeconds);
            Assert.Equal(100m, result.Session.ProgressPercentage);
        }

        if (status == "Cancelled")
        {
            Assert.NotNull(result.Session.CancelledAtUtc);
            Assert.Equal(600, result.Session.TotalDurationSeconds);
        }
    }

    [Theory]
    [InlineData("Empty", 400)]
    [InlineData("Missing", 404)]
    [InlineData("Malformed", 404)]
    public async Task DetailsEndpoint_RejectsInvalidIdentifiers(
        string scenario,
        int expectedStatus)
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client = WorkoutSessionTestData.CreateClient(factory);

        await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client);

        var identifier = scenario switch
        {
            "Empty" => Guid.Empty.ToString(),
            "Missing" => Guid.NewGuid().ToString(),
            "Malformed" => "invalid-guid",
            _ => throw new ArgumentOutOfRangeException(
                nameof(scenario))
        };

        using var response = await client.GetAsync(
            $"{WorkoutSessionTestData.RoutePrefix}/{identifier}");

        Assert.Equal(expectedStatus, (int)response.StatusCode);

        if (scenario != "Malformed")
        {
            using var json = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

            Assert.Equal(
                scenario == "Empty"
                    ? "INVALID_WORKOUT_SESSION_ID"
                    : "WORKOUT_SESSION_NOT_FOUND",
                json.RootElement.GetProperty("code").GetString());
        }
    }

    [Theory]
    [InlineData("Ordering")]
    [InlineData("ReadOnly")]
    [InlineData("ResponseConsistency")]
    public async Task DetailsEndpoint_ReturnsConsistentGraph(
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

        var before = await WorkoutSessionExecutionTestData
            .LoadAsync(factory, started.Session.Id);

        var details = await WorkoutSessionExecutionTestData
            .GetAsync(client, started.Session.Id);

        switch (scenario)
        {
            case "Ordering":
                Assert.Equal(new[] { 1, 2 },
                    details.Exercises.Select(x => x.ExerciseOrder));

                Assert.Equal(new[] { 1, 2 },
                    details.Exercises[0].Sets.Select(x => x.SetNumber));

                Assert.Equal(800m,
                    details.Exercises[0].TotalVolumeKg);
                break;

            case "ReadOnly":
                var after = await WorkoutSessionExecutionTestData
                    .LoadAsync(factory, started.Session.Id);

                Assert.Equal(before.RowVersion, after.RowVersion);
                Assert.Equal(before.UpdatedAtUtc, after.UpdatedAtUtc);
                Assert.Equal(before.Notes, after.Notes);

                Assert.Equal(
                    before.Exercises.SelectMany(x => x.Sets)
                        .Select(x => x.Id).OrderBy(x => x),
                    after.Exercises.SelectMany(x => x.Sets)
                        .Select(x => x.Id).OrderBy(x => x));
                break;

            case "ResponseConsistency":
                var active = await WorkoutSessionExecutionTestData
                    .GetActiveAsync(client);

                Assert.Equal(
                    JsonSerializer.Serialize(details),
                    JsonSerializer.Serialize(active));
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scenario));
        }
    }
}