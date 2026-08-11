using Deyxis.Core.Activities;
using Deyxis.Core.Events;
using Deyxis.PluginSdk;

namespace Deyxis.App;

public sealed class MockActivityProvider : IActivityProvider, IDisposable
{
    private static readonly DateTimeOffset InitialTimestamp = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CodexActivityId = Guid.Parse("20000000-0000-0000-0000-000000000002");

    private readonly IEventBus eventBus;
    private bool initialActivitiesPublished;
    private bool disposed;

    public MockActivityProvider(IEventBus eventBus)
    {
        this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public string Id => "mock";

    public ProviderHealth Health { get; private set; } = ProviderHealth.Stopped;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Health = ProviderHealth.Running;
    }

    public void Stop()
    {
        Health = ProviderHealth.Stopped;
    }

    public void PublishInitialActivities()
    {
        EnsureRunning();

        if (initialActivitiesPublished)
        {
            return;
        }

        initialActivitiesPublished = true;
        foreach (var activity in CreateInitialActivities())
        {
            eventBus.Publish(new ActivityUpserted(activity));
        }
    }

    public void PromoteWaitingActivity()
    {
        EnsureRunning();
        eventBus.Publish(new ActivityUpserted(CreateActivity(
            CodexActivityId,
            "codex",
            ActivityState.Waiting,
            "Codex",
            "Waiting for your input",
            InitialTimestamp.AddMinutes(1))));
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Stop();
        disposed = true;
    }

    private static IReadOnlyList<Activity> CreateInitialActivities() =>
    [
        CreateActivity(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            "music",
            ActivityState.Running,
            "Music",
            "Playing · Night Drive",
            InitialTimestamp.AddMinutes(-3)),
        CreateActivity(
            CodexActivityId,
            "codex",
            ActivityState.Running,
            "Codex",
            "Implementing the WinUI host",
            InitialTimestamp.AddMinutes(-2)),
        CreateActivity(
            Guid.Parse("30000000-0000-0000-0000-000000000003"),
            "claude",
            ActivityState.Waiting,
            "Claude",
            "Waiting for your approval",
            InitialTimestamp),
        CreateActivity(
            Guid.Parse("40000000-0000-0000-0000-000000000004"),
            "opencode",
            ActivityState.Completed,
            "OpenCode",
            "Completed repository analysis",
            InitialTimestamp.AddMinutes(-1)),
    ];

    private static Activity CreateActivity(
        Guid id,
        string providerId,
        ActivityState state,
        string title,
        string description,
        DateTimeOffset timestamp) => new(
            id,
            providerId,
            default,
            state,
            0,
            title,
            description,
            null,
            timestamp);

    private void EnsureRunning()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (Health != ProviderHealth.Running)
        {
            throw new InvalidOperationException("The mock activity provider must be started before publishing activities.");
        }
    }
}
