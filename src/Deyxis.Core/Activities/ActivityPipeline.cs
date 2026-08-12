using Deyxis.Core.Events;

namespace Deyxis.Core.Activities;

public sealed class ActivityPipeline : IDisposable
{
    private readonly object gate = new();
    private readonly IEventBus eventBus;
    private readonly ActivityManager manager;
    private readonly IDisposable upsertSubscription;
    private readonly IDisposable removeSubscription;
    private bool disposed;

    public ActivityPipeline(IEventBus eventBus, ActivityManager manager)
    {
        this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        Current = manager.Snapshot();
        upsertSubscription = eventBus.Subscribe<ActivityUpserted>(OnUpserted);
        removeSubscription = eventBus.Subscribe<ActivityRemoved>(OnRemoved);
    }

    public ActivitySnapshot Current { get; private set; }

    public event EventHandler<ActivitySnapshot>? SnapshotChanged;

    public void PublishForTest<TEvent>(TEvent message) where TEvent : notnull => eventBus.Publish(message);

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            upsertSubscription.Dispose();
            removeSubscription.Dispose();
        }
    }

    private void OnUpserted(ActivityUpserted message)
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            if (manager.Upsert(message.Activity))
            {
                PublishSnapshot();
            }
        }
    }

    private void OnRemoved(ActivityRemoved message)
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            if (manager.Remove(message.ActivityId))
            {
                PublishSnapshot();
            }
        }
    }

    private void PublishSnapshot()
    {
        Current = manager.Snapshot();
        SnapshotChanged?.Invoke(this, Current);
    }
}
