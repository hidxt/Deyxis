using Deyxis.Core.Activities;
using Deyxis.Core.Priority;
using Xunit;

namespace Deyxis.Core.Tests;

public sealed class ActivityPriorityPolicyTests
{
    [Fact]
    public void Waiting_activity_ranks_ahead_of_running_and_completed_activities()
    {
        var ordered = new ActivityPriorityPolicy().Order(new[]
        {
            CreateActivity(state: ActivityState.Running, title: "Codex"),
            CreateActivity(state: ActivityState.Completed, title: "OpenCode"),
            CreateActivity(state: ActivityState.Waiting, title: "Claude"),
        });

        Assert.Equal("Claude", ordered[0].Title);
    }

    [Fact]
    public void Newer_timestamp_ranks_first_when_activities_have_equal_state()
    {
        var ordered = new ActivityPriorityPolicy().Order(new[]
        {
            CreateActivity(ActivityState.Running, "Older", timestamp: DateTimeOffset.UnixEpoch),
            CreateActivity(ActivityState.Running, "Newer", timestamp: DateTimeOffset.UnixEpoch.AddSeconds(1)),
        });

        Assert.Equal("Newer", ordered[0].Title);
    }

    [Fact]
    public void Lower_identifier_ranks_first_when_activities_have_equal_state_and_timestamp()
    {
        var lowerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higherId = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var ordered = new ActivityPriorityPolicy().Order(new[]
        {
            CreateActivity(ActivityState.Running, "Higher", id: higherId),
            CreateActivity(ActivityState.Running, "Lower", id: lowerId),
        });

        Assert.Equal("Lower", ordered[0].Title);
    }

    private static Activity CreateActivity(
        ActivityState state,
        string title,
        Guid? id = null,
        DateTimeOffset? timestamp = null) => new(
            id ?? Guid.NewGuid(),
            "test-provider",
            default,
            state,
            0,
            title,
            "Test description",
            null,
            timestamp ?? DateTimeOffset.UnixEpoch);
}
