using FitnessPlatform.Contracts.WorkoutSessions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FitnessPlatform.IntegrationTests.WorkoutSessions;

public sealed class WorkoutSessionConcurrencyIntegrationTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task ConcurrentRequests_CreateOnlyOneSession(
        int requestCount)
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client = WorkoutSessionTestData.CreateClient(factory);

        var fixture = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client);

        var requests = Enumerable.Range(0, requestCount)
            .Select(_ => client.PostAsJsonAsync(
                $"{WorkoutSessionTestData.RoutePrefix}/start",
                WorkoutSessionTestData.CreateStartRequest(fixture)))
            .ToArray();

        var responses = await Task.WhenAll(requests);

        try
        {
            var success = Assert.Single(responses,
                x => x.StatusCode == HttpStatusCode.Created);

            var result = await success.Content
                .ReadFromJsonAsync<WorkoutSessionDetailResponse>();

            Assert.NotNull(result);

            foreach (var response in responses.Where(
                x => x.StatusCode != HttpStatusCode.Created))
            {
                Assert.Equal(HttpStatusCode.Conflict,
                    response.StatusCode);

                using var json = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync());

                Assert.Contains(
                    json.RootElement.GetProperty("code").GetString(),
                    new[]
                    {
                        "CONCURRENCY_CONFLICT",
                        "UNFINISHED_WORKOUT_SESSION_EXISTS"
                    });
            }

            Assert.Equal(1, await WorkoutSessionTestData
                .CountSessionsAsync(
                    factory, fixture.Member.ProfileId));

            Assert.Equal(1, await WorkoutSessionTestData
                .CountUnfinishedSessionsAsync(
                    factory, fixture.Member.ProfileId));

            var saved = await WorkoutSessionExecutionTestData
                .LoadAsync(factory, result.Session.Id);

            Assert.Equal(2, saved.Exercises.Count);

            Assert.Equal(2, saved.Exercises
                .Select(x => x.WorkoutPlanExerciseId)
                .Distinct().Count());
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    [Fact]
    public async Task ConcurrentDifferentMembers_RemainIndependent()
    {
        using var factory = WorkoutSessionTestData.CreateFactory();
        using var client1 = WorkoutSessionTestData.CreateClient(factory);
        using var client2 = WorkoutSessionTestData.CreateClient(factory);

        var first = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client1);

        var second = await WorkoutSessionTestData
            .PrepareAssignedMemberAsync(factory, client2);

        var responses = await Task.WhenAll(
            client1.PostAsJsonAsync(
                $"{WorkoutSessionTestData.RoutePrefix}/start",
                WorkoutSessionTestData.CreateStartRequest(first)),
            client2.PostAsJsonAsync(
                $"{WorkoutSessionTestData.RoutePrefix}/start",
                WorkoutSessionTestData.CreateStartRequest(second)));

        try
        {
            for (var index = 0; index < responses.Length; index++)
            {
                var response = responses[index];

                // SQL Server can detect a serializable deadlock
                // even between otherwise independent members.
                // Retry only a recognized concurrency conflict.
                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    using var json = JsonDocument.Parse(
                        await response.Content.ReadAsStringAsync());

                    Assert.Equal("CONCURRENCY_CONFLICT",
                        json.RootElement.GetProperty("code").GetString());

                    await WorkoutSessionExecutionTestData.StartAsync(
                        index == 0 ? client1 : client2,
                        index == 0 ? first : second);
                }
                else
                {
                    Assert.Equal(HttpStatusCode.Created,
                        response.StatusCode);
                }
            }

            Assert.Equal(1, await WorkoutSessionTestData
                .CountSessionsAsync(factory, first.Member.ProfileId));

            Assert.Equal(1, await WorkoutSessionTestData
                .CountSessionsAsync(factory, second.Member.ProfileId));

            var active1 = await WorkoutSessionExecutionTestData
                .GetActiveAsync(client1);

            var active2 = await WorkoutSessionExecutionTestData
                .GetActiveAsync(client2);

            Assert.NotEqual(active1.Session.Id, active2.Session.Id);
            Assert.Equal(first.Member.ProfileId,
                active1.Session.UserProfileId);
            Assert.Equal(second.Member.ProfileId,
                active2.Session.UserProfileId);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }
}