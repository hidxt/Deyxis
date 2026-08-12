using Deyxis.Core.Activities;
using Deyxis.Core.Events;
using Deyxis.PluginSdk;

namespace Deyxis.Providers.Agents;

public abstract class AgentProviderBase : IActivityProvider, IDisposable
{
    private readonly IEventBus eventBus;
    private readonly Guid activityId;
    private bool disposed;

    protected AgentProviderBase(IEventBus eventBus, AgentProviderKind kind)
    {
        this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        Kind = kind;
        Id = kind switch
        {
            AgentProviderKind.Codex => "agent.codex",
            AgentProviderKind.ClaudeCode => "agent.claude-code",
            AgentProviderKind.OpenCode => "agent.opencode",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        activityId = CreateStableActivityId(Id);
    }

    public string Id { get; }

    public AgentProviderKind Kind { get; }

    public ProviderHealth Health { get; private set; } = ProviderHealth.Stopped;

    public bool SupportsLiveSessionObservation => false;

    public virtual void Start()
    {
        ThrowIfDisposed();
        Publish(AgentActivityState.Stopped, DisplayName(Kind));
    }

    public virtual void Stop()
    {
        ThrowIfDisposed();
        Publish(AgentActivityState.Stopped, DisplayName(Kind));
    }

    public void Dispose()
    {
        disposed = true;
        GC.SuppressFinalize(this);
    }

    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    protected void Publish(
        AgentActivityState state,
        string label,
        double? progress = null,
        ExecutableProbeFailureCategory failureCategory = ExecutableProbeFailureCategory.None)
    {
        ThrowIfDisposed();
        var snapshot = new AgentActivitySnapshot(Kind, state, label, progress, failureCategory);
        Health = state switch
        {
            AgentActivityState.Stopped => ProviderHealth.Stopped,
            AgentActivityState.Running => ProviderHealth.Running,
            AgentActivityState.Failed => ProviderHealth.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };

        eventBus.Publish(new ActivityUpserted(new Activity(
            activityId,
            Id,
            ActivityCategory.Agent,
            MapState(snapshot.State),
            Priority: 0,
            snapshot.Label,
            SafeDescription(snapshot.State, snapshot.FailureCategory),
            snapshot.Progress,
            DateTimeOffset.UtcNow)));
    }

    private static ActivityState MapState(AgentActivityState state) => state switch
    {
        AgentActivityState.Stopped => ActivityState.Idle,
        AgentActivityState.Running => ActivityState.Running,
        AgentActivityState.Failed => ActivityState.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static string SafeDescription(
        AgentActivityState state,
        ExecutableProbeFailureCategory failureCategory)
    {
        if (failureCategory == ExecutableProbeFailureCategory.None)
        {
            return state switch
            {
                AgentActivityState.Stopped => "Stopped",
                AgentActivityState.Running => "Running",
                AgentActivityState.Failed => "Failed",
                _ => throw new ArgumentOutOfRangeException(nameof(state)),
            };
        }

        return failureCategory switch
        {
            ExecutableProbeFailureCategory.PathNotAllowed => "Availability probe rejected",
            ExecutableProbeFailureCategory.NotFound => "Unavailable",
            ExecutableProbeFailureCategory.NonZeroExit => "Availability probe failed",
            ExecutableProbeFailureCategory.TimedOut => "Availability probe timed out",
            ExecutableProbeFailureCategory.Cancelled => "Availability probe cancelled",
            ExecutableProbeFailureCategory.StartFailed => "Availability probe failed",
            _ => "Availability probe failed",
        };
    }

    private static string DisplayName(AgentProviderKind kind) => kind switch
    {
        AgentProviderKind.Codex => "Codex",
        AgentProviderKind.ClaudeCode => "Claude Code",
        AgentProviderKind.OpenCode => "OpenCode",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static Guid CreateStableActivityId(string providerId)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(providerId));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
