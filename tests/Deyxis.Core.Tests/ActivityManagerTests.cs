using Deyxis.Core.Activities;
using Xunit;

namespace Deyxis.Core.Tests;

public sealed class ActivityManagerTests
{
    [Fact]
    public void Upsert_replaces_an_activity_with_the_same_identifier()
    {
        var manager = new ActivityManager();
        var id = Guid.NewGuid();

        manager.Upsert(CreateActivity(id: id, title: "Original"));
        manager.Upsert(CreateActivity(id: id, title: "Replacement"));

        var snapshot = manager.Snapshot();

        var activity = Assert.Single(snapshot.OrderedActivities);
        Assert.Equal("Replacement", activity.Title);
    }

    [Fact]
    public void Snapshot_orders_waiting_activity_first_and_retains_every_upserted_activity()
    {
        var manager = new ActivityManager();

        manager.Upsert(CreateActivity(state: ActivityState.Completed, title: "Completed"));
        manager.Upsert(CreateActivity(state: ActivityState.Running, title: "Running"));
        manager.Upsert(CreateActivity(state: ActivityState.Waiting, title: "Waiting"));

        var snapshot = manager.Snapshot();

        Assert.Equal(new[] { "Waiting", "Running", "Completed" }, snapshot.OrderedActivities.Select(activity => activity.Title));
    }

    private static Activity CreateActivity(
        ActivityState state = ActivityState.Idle,
        string title = "Test activity",
        Guid? id = null) => new(
            id ?? Guid.NewGuid(), "test-provider", default, state, 0, title, "Test description", null, DateTimeOffset.UnixEpoch);
}
