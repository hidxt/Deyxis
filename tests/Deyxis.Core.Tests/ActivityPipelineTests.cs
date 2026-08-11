using Deyxis.Core.Activities;
using Deyxis.Core.Events;
using Deyxis.Core.Priority;
using Xunit;

namespace Deyxis.Core.Tests;

public sealed class ActivityPipelineTests
{
    [Fact]
    public void Upsert_event_updates_snapshot_and_notifies_observer()
    {
        using var pipeline = new ActivityPipeline(new EventBus(), new ActivityManager(new ActivityPriorityPolicy()));
        ActivitySnapshot? observed = null;
        pipeline.SnapshotChanged += (_, snapshot) => observed = snapshot;

        pipeline.PublishForTest(new ActivityUpserted(CreateActivity(state: ActivityState.Waiting)));

        Assert.Equal(ActivityState.Waiting, observed!.OrderedActivities[0].State);
    }

    [Fact]
    public void Removal_event_removes_an_existing_activity_from_the_snapshot()
    {
        using var pipeline = new ActivityPipeline(new EventBus(), new ActivityManager());
        var activity = CreateActivity(ActivityState.Running);

        pipeline.PublishForTest(new ActivityUpserted(activity));
        pipeline.PublishForTest(new ActivityRemoved(activity.Id));

        Assert.Empty(pipeline.Current.OrderedActivities);
    }

    [Fact]
    public void Waiting_upsert_becomes_the_primary_activity()
    {
        using var pipeline = new ActivityPipeline(new EventBus(), new ActivityManager());
        pipeline.PublishForTest(new ActivityUpserted(CreateActivity(ActivityState.Running, "Running")));
        pipeline.PublishForTest(new ActivityUpserted(CreateActivity(ActivityState.Waiting, "Waiting")));

        Assert.Equal("Waiting", pipeline.Current.OrderedActivities[0].Title);
    }

    [Fact]
    public void Disposed_pipeline_does_not_apply_later_messages()
    {
        var bus = new EventBus();
        var pipeline = new ActivityPipeline(bus, new ActivityManager());

        pipeline.Dispose();
        bus.Publish(new ActivityUpserted(CreateActivity(ActivityState.Waiting)));

        Assert.Empty(pipeline.Current.OrderedActivities);
    }

    [Fact]
    public void Repeated_identical_upsert_does_not_notify_again()
    {
        using var pipeline = new ActivityPipeline(new EventBus(), new ActivityManager());
        var activity = CreateActivity(ActivityState.Running);
        var notifications = 0;
        pipeline.SnapshotChanged += (_, _) => notifications++;

        pipeline.PublishForTest(new ActivityUpserted(activity));
        pipeline.PublishForTest(new ActivityUpserted(activity));

        Assert.Equal(1, notifications);
    }

    private static Activity CreateActivity(ActivityState state, string title = "Test activity") => new(
        Guid.NewGuid(), "test-provider", default, state, 0, title, "Test description", null, DateTimeOffset.UnixEpoch);
}
