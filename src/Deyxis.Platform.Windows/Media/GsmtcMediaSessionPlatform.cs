using Deyxis.Providers.Media;
using Windows.Media.Control;

namespace Deyxis.Platform.Windows.Media;

public sealed class GsmtcMediaSessionPlatform : IMediaSessionPlatform, IDisposable
{
    private readonly object gate = new();
    private readonly GlobalSystemMediaTransportControlsSessionManager manager;
    private GlobalSystemMediaTransportControlsSession? currentSession;
    private bool disposed;

    private GsmtcMediaSessionPlatform(GlobalSystemMediaTransportControlsSessionManager manager)
    {
        this.manager = manager;
        manager.CurrentSessionChanged += OnManagerCurrentSessionChanged;
        manager.SessionsChanged += OnManagerSessionsChanged;
        SetCurrentSession(manager.GetCurrentSession());
    }

    public event EventHandler? CurrentSessionChanged;

    public static async Task<GsmtcMediaSessionPlatform> RequestAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return new GsmtcMediaSessionPlatform(manager);
    }

    public async Task<MediaSessionSnapshot?> GetCurrentSessionAsync(
        CancellationToken cancellationToken = default)
    {
        GlobalSystemMediaTransportControlsSession? session;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            session = currentSession;
        }

        if (session is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var mediaProperties = await session.TryGetMediaPropertiesAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var playbackInfo = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        var controls = playbackInfo.Controls;
        var duration = timeline.EndTime > timeline.StartTime
            ? timeline.EndTime - timeline.StartTime
            : TimeSpan.Zero;

        return new MediaSessionSnapshot(
            session.SourceAppUserModelId,
            mediaProperties.Title,
            mediaProperties.Artist,
            timeline.Position,
            duration,
            MapPlaybackState(playbackInfo.PlaybackStatus),
            new MediaSessionControls(
                controls.IsPlayPauseToggleEnabled,
                controls.IsNextEnabled,
                controls.IsPreviousEnabled),
            mediaProperties.AlbumTitle,
            session.SourceAppUserModelId);
    }

    public Task<bool> TogglePlayPauseAsync(CancellationToken cancellationToken = default) =>
        InvokeSupportedControlAsync(
            controls => controls.IsPlayPauseToggleEnabled,
            session => session.TryTogglePlayPauseAsync(),
            cancellationToken);

    public Task<bool> SkipNextAsync(CancellationToken cancellationToken = default) =>
        InvokeSupportedControlAsync(
            controls => controls.IsNextEnabled,
            session => session.TrySkipNextAsync(),
            cancellationToken);

    public Task<bool> SkipPreviousAsync(CancellationToken cancellationToken = default) =>
        InvokeSupportedControlAsync(
            controls => controls.IsPreviousEnabled,
            session => session.TrySkipPreviousAsync(),
            cancellationToken);

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            manager.CurrentSessionChanged -= OnManagerCurrentSessionChanged;
            manager.SessionsChanged -= OnManagerSessionsChanged;
            SetCurrentSessionCore(null);
        }
    }

    private async Task<bool> InvokeSupportedControlAsync(
        Func<GlobalSystemMediaTransportControlsSessionPlaybackControls, bool> isSupported,
        Func<GlobalSystemMediaTransportControlsSession, global::Windows.Foundation.IAsyncOperation<bool>> invoke,
        CancellationToken cancellationToken)
    {
        GlobalSystemMediaTransportControlsSession? session;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            session = currentSession;
        }

        if (session is null || !isSupported(session.GetPlaybackInfo().Controls))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = await invoke(session);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private void OnManagerCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args) => RefreshCurrentSession();

    private void OnManagerSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args) => RefreshCurrentSession();

    private void RefreshCurrentSession()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            SetCurrentSessionCore(manager.GetCurrentSession());
        }

        CurrentSessionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetCurrentSession(GlobalSystemMediaTransportControlsSession? session)
    {
        lock (gate)
        {
            SetCurrentSessionCore(session);
        }
    }

    private void SetCurrentSessionCore(GlobalSystemMediaTransportControlsSession? session)
    {
        if (currentSession is not null)
        {
            currentSession.MediaPropertiesChanged -= OnSessionMediaPropertiesChanged;
            currentSession.PlaybackInfoChanged -= OnSessionPlaybackInfoChanged;
            currentSession.TimelinePropertiesChanged -= OnSessionTimelinePropertiesChanged;
        }

        currentSession = session;

        if (currentSession is not null)
        {
            currentSession.MediaPropertiesChanged += OnSessionMediaPropertiesChanged;
            currentSession.PlaybackInfoChanged += OnSessionPlaybackInfoChanged;
            currentSession.TimelinePropertiesChanged += OnSessionTimelinePropertiesChanged;
        }
    }

    private void OnSessionMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args) => RaiseCurrentSessionChanged();

    private void OnSessionPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args) => RaiseCurrentSessionChanged();

    private void OnSessionTimelinePropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        TimelinePropertiesChangedEventArgs args) => RaiseCurrentSessionChanged();

    private void RaiseCurrentSessionChanged()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
        }

        CurrentSessionChanged?.Invoke(this, EventArgs.Empty);
    }

    private static MediaPlaybackState MapPlaybackState(
        GlobalSystemMediaTransportControlsSessionPlaybackStatus status) => status switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => MediaPlaybackState.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => MediaPlaybackState.Paused,
            _ => MediaPlaybackState.Stopped,
        };
}
