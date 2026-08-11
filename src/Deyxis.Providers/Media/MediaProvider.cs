using Deyxis.Core.Events;
using Deyxis.PluginSdk;

namespace Deyxis.Providers.Media;

public sealed class MediaProvider : IActivityProvider, IDisposable
{
    private static readonly Guid MediaActivityId = new("d3c8c707-bbbc-4dc0-98a5-ad4be098263d");

    private readonly object gate = new();
    private readonly IMediaSessionPlatform platform;
    private readonly IEventBus eventBus;
    private readonly TimeProvider timeProvider;
    private CancellationTokenSource? stoppingTokenSource;
    private MediaSessionSnapshot? currentSession;
    private int refreshVersion;

    public MediaProvider(
        IMediaSessionPlatform platform,
        IEventBus eventBus,
        TimeProvider? timeProvider = null)
    {
        this.platform = platform ?? throw new ArgumentNullException(nameof(platform));
        this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string Id => "media";

    public ProviderHealth Health { get; private set; } = ProviderHealth.Stopped;

    public void Start()
    {
        lock (gate)
        {
            if (stoppingTokenSource is not null)
            {
                return;
            }

            stoppingTokenSource = new CancellationTokenSource();
            platform.CurrentSessionChanged += OnCurrentSessionChanged;
            Health = ProviderHealth.Running;
        }

        QueueRefresh();
    }

    public void Stop()
    {
        CancellationTokenSource? source;
        lock (gate)
        {
            source = stoppingTokenSource;
            if (source is null)
            {
                return;
            }

            stoppingTokenSource = null;
            currentSession = null;
            Interlocked.Increment(ref refreshVersion);
            platform.CurrentSessionChanged -= OnCurrentSessionChanged;
            Health = ProviderHealth.Stopped;
        }

        source.Cancel();
        source.Dispose();
    }

    public void Dispose() => Stop();

    public Task<bool> TogglePlayPauseAsync(CancellationToken cancellationToken = default) =>
        InvokeControlAsync(
            controls => controls.CanTogglePlayPause,
            platform.TogglePlayPauseAsync,
            cancellationToken);

    public Task<bool> SkipNextAsync(CancellationToken cancellationToken = default) =>
        InvokeControlAsync(
            controls => controls.CanSkipNext,
            platform.SkipNextAsync,
            cancellationToken);

    public Task<bool> SkipPreviousAsync(CancellationToken cancellationToken = default) =>
        InvokeControlAsync(
            controls => controls.CanSkipPrevious,
            platform.SkipPreviousAsync,
            cancellationToken);

    private void OnCurrentSessionChanged(object? sender, EventArgs args) => QueueRefresh();

    private void QueueRefresh()
    {
        CancellationToken cancellationToken;
        int version;
        lock (gate)
        {
            if (stoppingTokenSource is null)
            {
                return;
            }

            cancellationToken = stoppingTokenSource.Token;
            version = Interlocked.Increment(ref refreshVersion);
        }

        _ = RefreshAsync(version, cancellationToken);
    }

    private async Task RefreshAsync(int version, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await platform.GetCurrentSessionAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            lock (gate)
            {
                if (stoppingTokenSource is null || version != refreshVersion)
                {
                    return;
                }

                currentSession = snapshot;
                Health = ProviderHealth.Running;
            }

            if (snapshot is null)
            {
                eventBus.Publish(new ActivityRemoved(MediaActivityId));
                return;
            }

            var activity = snapshot.ToActivity(Id, MediaActivityId, timeProvider.GetUtcNow());
            eventBus.Publish(new ActivityUpserted(activity));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            lock (gate)
            {
                if (stoppingTokenSource is not null && version == refreshVersion)
                {
                    Health = ProviderHealth.Failed;
                }
            }
        }
    }

    private async Task<bool> InvokeControlAsync(
        Func<MediaSessionControls, bool> isEligible,
        Func<CancellationToken, Task<bool>> invoke,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (stoppingTokenSource is null)
            {
                return false;
            }

            if (currentSession is null || !isEligible(currentSession.Controls))
            {
                Health = ProviderHealth.Failed;
                return false;
            }
        }

        try
        {
            var succeeded = await invoke(cancellationToken).ConfigureAwait(false);
            if (!succeeded)
            {
                SetFailedIfRunning();
            }

            return succeeded;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            SetFailedIfRunning();
            return false;
        }
    }

    private void SetFailedIfRunning()
    {
        lock (gate)
        {
            if (stoppingTokenSource is not null)
            {
                Health = ProviderHealth.Failed;
            }
        }
    }
}
