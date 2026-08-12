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

    [Fact]
    public void Concurrent_upserts_are_serialized_without_losing_activities()
    {
        using var pipeline = new ActivityPipeline(new EventBus(), new ActivityManager());
        var activities = Enumerable.Range(0, 2_000)
            .Select(index => CreateActivity(ActivityState.Running, $"Activity {index}"))
            .ToArray();

        Parallel.ForEach(
            activities,
            activity => pipeline.PublishForTest(new ActivityUpserted(activity)));

        Assert.Equal(activities.Length, pipeline.Current.OrderedActivities.Count);
    }

    [Fact]
    public async Task Concurrent_upsert_notifications_are_delivered_in_snapshot_order()
    {
        using var pipeline = new ActivityPipeline(new EventBus(), new ActivityManager());
        using var releaseFirstNotification = new ManualResetEventSlim();
        var firstNotificationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondNotificationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedCounts = new List<int>();
        pipeline.SnapshotChanged += (_, snapshot) =>
        {
            if (snapshot.OrderedActivities.Count == 1)
            {
                firstNotificationStarted.TrySetResult();
                releaseFirstNotification.Wait(TimeSpan.FromSeconds(5));
            }
            else
            {
                secondNotificationStarted.TrySetResult();
            }

            lock (observedCounts)
            {
                observedCounts.Add(snapshot.OrderedActivities.Count);
            }
        };

        var firstPublish = Task.Run(
            () => pipeline.PublishForTest(new ActivityUpserted(CreateActivity(ActivityState.Running, "First"))));
        await firstNotificationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondPublish = Task.Run(
            () => pipeline.PublishForTest(new ActivityUpserted(CreateActivity(ActivityState.Running, "Second"))));
        await Task.Delay(100);
        Assert.False(secondNotificationStarted.Task.IsCompleted);
        releaseFirstNotification.Set();
        await Task.WhenAll(firstPublish, secondPublish).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal([1, 2], observedCounts);
    }

    [Fact]
    public async Task Dispose_waits_for_an_in_progress_notification_to_finish()
    {
        var pipeline = new ActivityPipeline(new EventBus(), new ActivityManager());
        using var releaseNotification = new ManualResetEventSlim();
        var notificationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pipeline.SnapshotChanged += (_, _) =>
        {
            notificationStarted.TrySetResult();
            releaseNotification.Wait(TimeSpan.FromSeconds(5));
        };
        var publish = Task.Run(
            () => pipeline.PublishForTest(new ActivityUpserted(CreateActivity(ActivityState.Running))));
        await notificationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var dispose = Task.Run(pipeline.Dispose);
        await Task.Delay(100);
        Assert.False(dispose.IsCompleted);
        releaseNotification.Set();
        await Task.WhenAll(publish, dispose).WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static Activity CreateActivity(ActivityState state, string title = "Test activity") => new(
        Guid.NewGuid(), "test-provider", default, state, 0, title, "Test description", null, DateTimeOffset.UnixEpoch);
}
