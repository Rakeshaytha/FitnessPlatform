using System.Net;
using System.Text.Json;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionSecurityIntegrationTests
{
    [Theory]
    [InlineData("InProgress")]
    [InlineData("Paused")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task Member_CannotRetrieveAnotherMembersSession(
        string status)
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var ownerClient = WorkoutSessionTestData.CreateClient(factory);
        using var otherClient = WorkoutSessionTestData.CreateClient(factory);

        var owner = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, ownerClient);

        await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, otherClient);

        var session = await WorkoutSessionExecutionTestData
            .StartAsync(ownerClient, owner);

        await WorkoutSessionExecutionTestData.SetStatusAsync(
            factory, session.Session.Id, status);

        using var response = await otherClient.GetAsync(
            $"{WorkoutSessionTestData.RoutePrefix}/{session.Session.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal("WORKOUT_SESSION_NOT_FOUND",
            json.RootElement.GetProperty("code").GetString());

        var ownDetails = await WorkoutSessionExecutionTestData
            .GetAsync(ownerClient, session.Session.Id);

        Assert.Equal(status, ownDetails.Session.Status);
    }

    [Theory]
    [InlineData("OwnActive")]
    [InlineData("NoActive")]
    [InlineData("EquivalentNotFound")]
    [InlineData("ReadOnly")]
    [InlineData("SharedPlan")]
    [InlineData("PerformanceIsolation")]
    public async Task MemberWorkoutData_RemainsIsolated(
        string scenario)
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client1 = WorkoutSessionTestData.CreateClient(factory);
        using var client2 = WorkoutSessionTestData.CreateClient(factory);

        var first = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client1);

        var secondMember = await WorkoutSessionTestData
            .CreateMemberAsync(factory);

        var secondAssignment = await WorkoutSessionTestData
            .CreateWorkoutAssignmentAsync(
                factory,
                secondMember.ProfileId,
                first.Plan.PlanId);

        await WorkoutSessionTestData.AuthenticateAsync(
            client2, secondMember.Email);

        var second = new WorkoutSessionTestData.AssignedMember(
            secondMember,
            first.Plan,
            secondAssignment);

        var session1 = await WorkoutSessionExecutionTestData
            .StartAsync(client1, first);

        switch (scenario)
        {
            case "OwnActive":
                var session2 = await WorkoutSessionExecutionTestData
                    .StartAsync(client2, second);

                var active = await WorkoutSessionExecutionTestData
                    .GetActiveAsync(client2);

                Assert.Equal(session2.Session.Id, active.Session.Id);
                Assert.NotEqual(session1.Session.Id, active.Session.Id);
                break;

            case "NoActive":
                using (var response = await client2.GetAsync(
                    $"{WorkoutSessionTestData.RoutePrefix}/active"))
                {
                    Assert.Equal(HttpStatusCode.NoContent,
                        response.StatusCode);
                }
                break;

            case "EquivalentNotFound":
                using (var foreign = await client2.GetAsync(
                    $"{WorkoutSessionTestData.RoutePrefix}/{session1.Session.Id}"))
                using (var missing = await client2.GetAsync(
                    $"{WorkoutSessionTestData.RoutePrefix}/{Guid.NewGuid()}"))
                {
                    Assert.Equal(HttpStatusCode.NotFound,
                        foreign.StatusCode);
                    Assert.Equal(HttpStatusCode.NotFound,
                        missing.StatusCode);

                    Assert.Equal(
                        await foreign.Content.ReadAsStringAsync(),
                        await missing.Content.ReadAsStringAsync());
                }
                break;

            case "ReadOnly":
                var before = await WorkoutSessionExecutionTestData
                    .LoadAsync(factory, session1.Session.Id);

                using (var response = await client2.GetAsync(
                    $"{WorkoutSessionTestData.RoutePrefix}/{session1.Session.Id}"))
                {
                    Assert.Equal(HttpStatusCode.NotFound,
                        response.StatusCode);
                }

                var after = await WorkoutSessionExecutionTestData
                    .LoadAsync(factory, session1.Session.Id);

                Assert.Equal(before.RowVersion, after.RowVersion);
                Assert.Equal(before.UpdatedAtUtc, after.UpdatedAtUtc);
                break;

            case "SharedPlan":
                var shared = await WorkoutSessionExecutionTestData
                    .StartAsync(client2, second);

                Assert.Equal(session1.Session.WorkoutPlanId,
                    shared.Session.WorkoutPlanId);

                Assert.NotEqual(session1.Session.Id, shared.Session.Id);
                Assert.NotEqual(session1.Session.UserProfileId,
                    shared.Session.UserProfileId);

                Assert.Empty(session1.Exercises.Select(x => x.Id)
                    .Intersect(shared.Exercises.Select(x => x.Id)));
                break;

            case "PerformanceIsolation":
                var independent = await WorkoutSessionExecutionTestData
                    .StartAsync(client2, second);

                await WorkoutSessionExecutionTestData
                    .AddPerformanceAsync(factory, session1.Session.Id);

                var firstDetails = await WorkoutSessionExecutionTestData
                    .GetAsync(client1, session1.Session.Id);

                var secondDetails = await WorkoutSessionExecutionTestData
                    .GetAsync(client2, independent.Session.Id);

                Assert.Equal(2, firstDetails.Session.TotalRecordedSets);
                Assert.Equal(0, secondDetails.Session.TotalRecordedSets);

                Assert.Equal(50m,
                    firstDetails.Session.ProgressPercentage);
                Assert.Equal(0m,
                    secondDetails.Session.ProgressPercentage);

                Assert.All(secondDetails.Exercises,
                    x => Assert.Empty(x.Sets));
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scenario));
        }
    }
}