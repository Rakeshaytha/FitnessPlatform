using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionDuplicateIntegrationTests
{
    [Theory]
    [InlineData("InProgress")]
    [InlineData("Paused")]
    public async Task UnfinishedSession_BlocksNewSession(
        string status)
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client = WorkoutSessionTestData.CreateClient(factory);

        var fixture = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client);

        var first = await WorkoutSessionExecutionTestData
            .StartAsync(client, fixture);

        await WorkoutSessionExecutionTestData.SetStatusAsync(
            factory, first.Session.Id, status);

        using var response = await client.PostAsJsonAsync(
            $"{WorkoutSessionTestData.RoutePrefix}/start",
            WorkoutSessionTestData.CreateStartRequest(fixture));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal("UNFINISHED_WORKOUT_SESSION_EXISTS",
            json.RootElement.GetProperty("code").GetString());

        var existing = json.RootElement.GetProperty("existingSession");

        Assert.Equal(first.Session.Id,
            existing.GetProperty("workoutSessionId").GetGuid());

        Assert.Equal(status,
            existing.GetProperty("status").GetString());

        Assert.Equal(1, await WorkoutSessionTestData
            .CountSessionsAsync(factory, fixture.Member.ProfileId));
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task TerminalSession_AllowsNewSession(
        string status)
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client = WorkoutSessionTestData.CreateClient(factory);

        var fixture = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client);

        var first = await WorkoutSessionExecutionTestData
            .StartAsync(client, fixture);

        await WorkoutSessionExecutionTestData.SetStatusAsync(
            factory, first.Session.Id, status);

        var second = await WorkoutSessionExecutionTestData
            .StartAsync(client, fixture);

        Assert.NotEqual(first.Session.Id, second.Session.Id);
        Assert.Equal(first.Session.WorkoutDate,
            second.Session.WorkoutDate);

        Assert.Equal(2, await WorkoutSessionTestData
            .CountSessionsAsync(factory, fixture.Member.ProfileId));

        Assert.Equal(1, await WorkoutSessionTestData
            .CountUnfinishedSessionsAsync(
                factory, fixture.Member.ProfileId));
    }

    [Fact]
    public async Task DuplicateRequest_ReturnsExistingSessionSnapshot()
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client = WorkoutSessionTestData.CreateClient(factory);

        var fixture = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client);

        var first = await WorkoutSessionExecutionTestData
            .StartAsync(client, fixture);

        using var response = await client.PostAsJsonAsync(
            $"{WorkoutSessionTestData.RoutePrefix}/start",
            WorkoutSessionTestData.CreateStartRequest(fixture));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var existing = json.RootElement.GetProperty("existingSession");

        Assert.Equal(first.Session.Id,
            existing.GetProperty("workoutSessionId").GetGuid());
        Assert.Equal(first.Session.WorkoutPlanName,
            existing.GetProperty("workoutPlanName").GetString());
        Assert.Equal(first.Session.WorkoutDayName,
            existing.GetProperty("workoutDayName").GetString());
        Assert.Equal(first.Session.StartedAtUtc,
            existing.GetProperty("startedAtUtc").GetDateTime());
        Assert.Equal(first.Session.WorkoutDate.ToString("yyyy-MM-dd"),
            existing.GetProperty("workoutDate").GetString());
    }

    [Fact]
    public async Task DuplicateRequest_DoesNotModifyExistingSession()
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client = WorkoutSessionTestData.CreateClient(factory);

        var fixture = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client);

        var first = await WorkoutSessionExecutionTestData
            .StartAsync(client, fixture, "Original notes");

        using var response = await client.PostAsJsonAsync(
            $"{WorkoutSessionTestData.RoutePrefix}/start",
            WorkoutSessionTestData.CreateStartRequest(
                fixture, notes: "Replacement notes"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var saved = await WorkoutSessionExecutionTestData
            .LoadAsync(factory, first.Session.Id);

        Assert.Equal("Original notes", saved.Notes);
        Assert.Equal(first.Session.RowVersion,
            Convert.ToBase64String(saved.RowVersion));
        Assert.Equal(2, saved.Exercises.Count);
    }

    [Fact]
    public async Task AnotherMembersSession_DoesNotBlockCreation()
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client1 = WorkoutSessionTestData.CreateClient(factory);
        using var client2 = WorkoutSessionTestData.CreateClient(factory);

        var fixture1 = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client1);

        var fixture2 = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client2);

        var first = await WorkoutSessionExecutionTestData
            .StartAsync(client1, fixture1);

        var second = await WorkoutSessionExecutionTestData
            .StartAsync(client2, fixture2);

        Assert.NotEqual(first.Session.Id, second.Session.Id);
        Assert.NotEqual(first.Session.UserProfileId,
            second.Session.UserProfileId);
    }

    [Fact]
    public async Task MultipleCompletedSessions_SameDate_AreAllowed()
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client = WorkoutSessionTestData.CreateClient(factory);

        var fixture = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client);

        var first = await WorkoutSessionExecutionTestData
            .StartAsync(client, fixture);

        await WorkoutSessionExecutionTestData.SetStatusAsync(
            factory, first.Session.Id, "Completed");

        var second = await WorkoutSessionExecutionTestData
            .StartAsync(client, fixture);

        await WorkoutSessionExecutionTestData.SetStatusAsync(
            factory, second.Session.Id, "Completed");

        var third = await WorkoutSessionExecutionTestData
            .StartAsync(client, fixture);

        Assert.Equal(first.Session.WorkoutDate,
            third.Session.WorkoutDate);

        Assert.Equal(3, await WorkoutSessionTestData
            .CountSessionsAsync(factory, fixture.Member.ProfileId));

        Assert.Equal(1, await WorkoutSessionTestData
            .CountUnfinishedSessionsAsync(
                factory, fixture.Member.ProfileId));
    }
}