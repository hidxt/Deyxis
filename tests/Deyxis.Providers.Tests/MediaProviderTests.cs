using Deyxis.Core.Activities;
using Deyxis.Core.Events;
using Deyxis.PluginSdk;
using Deyxis.Providers.Lyrics;
using Deyxis.Providers.Media;
using Xunit;

namespace Deyxis.Providers.Tests;

public sealed class MediaProviderTests
{
    [Fact]
    public async Task Lyric_snapshot_follows_the_refreshed_media_position()
    {
        var platform = new FakeMediaSessionPlatform
        {
            CurrentSession = CreateSession(
                canTogglePlayPause: true,
                canSkipNext: true,
                position: TimeSpan.FromSeconds(12)),
        };
        var lyrics = new PositionLyricsProvider();
        var eventBus = new EventBus();
        var published = new TaskCompletionSource<ActivityUpserted>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = eventBus.Subscribe<ActivityUpserted>(message => published.TrySetResult(message));
        using var provider = new MediaProvider(platform, eventBus, lyrics);

        provider.Start();
        await published.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Line at 12s", provider.CurrentLyrics.CurrentLine);

        published = new TaskCompletionSource<ActivityUpserted>(TaskCreationOptions.RunContinuationsAsynchronously);
        platform.CurrentSession = CreateSession(
            canTogglePlayPause: true,
            canSkipNext: true,
            position: TimeSpan.FromSeconds(38));
        platform.RaiseCurrentSessionChanged();
        await published.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Line at 38s", provider.CurrentLyrics.CurrentLine);
    }

    [Fact]
    public async Task Absent_lyrics_leave_the_media_activity_unchanged()
    {
        var session = CreateSession(
            canTogglePlayPause: true,
            canSkipNext: true,
            position: TimeSpan.FromMinutes(2));
        var platform = new FakeMediaSessionPlatform { CurrentSession = session };
        var eventBus = new EventBus();
        var published = new TaskCompletionSource<ActivityUpserted>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = eventBus.Subscribe<ActivityUpserted>(message => published.TrySetResult(message));
        using var provider = new MediaProvider(platform, eventBus, new EmptyLyricsProvider());

        provider.Start();

        var upsert = await published.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(LyricsSnapshot.Empty, provider.CurrentLyrics);
        Assert.Equal("Clair de Lune", upsert.Activity.Title);
        Assert.Equal("Claude Debussy", upsert.Activity.Description);
        Assert.Equal(0.25, upsert.Activity.Progress);
        Assert.Equal(ActivityState.Running, upsert.Activity.State);
    }

    [Fact]
    public void Playing_snapshot_maps_media_metadata_and_timeline_to_a_running_activity()
    {
        var snapshot = new MediaSessionSnapshot(
            "session-42",
            "Clair de Lune",
            "Claude Debussy",
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(8),
            MediaPlaybackState.Playing,
            new MediaSessionControls(CanTogglePlayPause: true, CanSkipNext: true, CanSkipPrevious: true));

        var activity = snapshot.ToActivity(
            "media",
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            DateTimeOffset.UnixEpoch);

        Assert.Equal(ActivityCategory.Media, activity.Category);
        Assert.Equal(ActivityState.Running, activity.State);
        Assert.Equal("Clair de Lune", activity.Title);
        Assert.Equal("Claude Debussy", activity.Description);
        Assert.Equal(0.25, activity.Progress);
    }

    [Fact]
    public async Task Active_session_publishes_media_upsert_and_unavailable_session_publishes_removal()
    {
        var platform = new FakeMediaSessionPlatform
        {
            CurrentSession = new MediaSessionSnapshot(
                "player-app",
                "Clair de Lune",
                "Claude Debussy",
                TimeSpan.FromMinutes(2),
                TimeSpan.FromMinutes(8),
                MediaPlaybackState.Playing,
                new MediaSessionControls(CanTogglePlayPause: true, CanSkipNext: true, CanSkipPrevious: true),
                AlbumTitle: "Suite bergamasque",
                SourceAppUserModelId: "player-app"),
        };
        var eventBus = new EventBus();
        var upserted = new TaskCompletionSource<ActivityUpserted>(TaskCreationOptions.RunContinuationsAsynchronously);
        var removed = new TaskCompletionSource<ActivityRemoved>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var upsertSubscription = eventBus.Subscribe<ActivityUpserted>(upserted.SetResult);
        using var removalSubscription = eventBus.Subscribe<ActivityRemoved>(removed.SetResult);
        var provider = new MediaProvider(platform, eventBus);

        provider.Start();

        var upsert = await upserted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("media", upsert.Activity.ProviderId);
        Assert.Equal(ActivityCategory.Media, upsert.Activity.Category);
        Assert.Equal(ActivityState.Running, upsert.Activity.State);
        Assert.Equal("Clair de Lune", upsert.Activity.Title);
        Assert.Equal("Claude Debussy — Suite bergamasque · player-app", upsert.Activity.Description);
        Assert.Equal(0.25, upsert.Activity.Progress);

        platform.CurrentSession = null;
        platform.RaiseCurrentSessionChanged();

        var removal = await removed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(upsert.Activity.Id, removal.ActivityId);

        provider.Stop();
    }

    [Fact]
    public async Task Disabled_next_control_is_not_delegated_to_the_platform()
    {
        var platform = new FakeMediaSessionPlatform
        {
            CurrentSession = CreateSession(canTogglePlayPause: true, canSkipNext: false),
        };
        var eventBus = new EventBus();
        var published = new TaskCompletionSource<ActivityUpserted>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = eventBus.Subscribe<ActivityUpserted>(published.SetResult);
        using var provider = new MediaProvider(platform, eventBus);
        provider.Start();
        await published.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var result = await provider.SkipNextAsync();

        Assert.False(result);
        Assert.Equal(0, platform.SkipNextInvocationCount);
        Assert.Equal(ProviderHealth.Failed, provider.Health);
    }

    [Fact]
    public async Task Enabled_play_pause_control_delegates_to_the_platform_once()
    {
        var platform = new FakeMediaSessionPlatform
        {
            CurrentSession = CreateSession(canTogglePlayPause: true, canSkipNext: false),
            TogglePlayPauseResult = true,
        };
        var eventBus = new EventBus();
        var published = new TaskCompletionSource<ActivityUpserted>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = eventBus.Subscribe<ActivityUpserted>(published.SetResult);
        using var provider = new MediaProvider(platform, eventBus);
        provider.Start();
        await published.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var result = await provider.TogglePlayPauseAsync();

        Assert.True(result);
        Assert.Equal(1, platform.TogglePlayPauseInvocationCount);
    }

    [Fact]
    public async Task False_control_result_marks_the_provider_failed()
    {
        var platform = new FakeMediaSessionPlatform
        {
            CurrentSession = CreateSession(canTogglePlayPause: true, canSkipNext: false),
            TogglePlayPauseResult = false,
        };
        var eventBus = new EventBus();
        var published = new TaskCompletionSource<ActivityUpserted>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = eventBus.Subscribe<ActivityUpserted>(published.SetResult);
        using var provider = new MediaProvider(platform, eventBus);
        provider.Start();
        await published.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var result = await provider.TogglePlayPauseAsync();

        Assert.False(result);
        Assert.Equal(ProviderHealth.Failed, provider.Health);
    }

    [Fact]
    public async Task Control_exception_is_contained_and_marks_the_provider_failed()
    {
        var platform = new FakeMediaSessionPlatform
        {
            CurrentSession = CreateSession(canTogglePlayPause: true, canSkipNext: false),
            TogglePlayPauseException = new InvalidOperationException("control unavailable"),
        };
        var eventBus = new EventBus();
        var published = new TaskCompletionSource<ActivityUpserted>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = eventBus.Subscribe<ActivityUpserted>(published.SetResult);
        using var provider = new MediaProvider(platform, eventBus);
        provider.Start();
        await published.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var result = await provider.TogglePlayPauseAsync();

        Assert.False(result);
        Assert.Equal(ProviderHealth.Failed, provider.Health);
    }

    private static MediaSessionSnapshot CreateSession(
        bool canTogglePlayPause,
        bool canSkipNext,
        TimeSpan? position = null) => new(
        "player-app",
        "Clair de Lune",
        "Claude Debussy",
        position ?? TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(8),
        MediaPlaybackState.Playing,
        new MediaSessionControls(canTogglePlayPause, canSkipNext, CanSkipPrevious: false));

    private sealed class PositionLyricsProvider : ILyricsProvider
    {
        public Task<LyricsSnapshot> GetSnapshotAsync(
            string title,
            string artist,
            TimeSpan position,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LyricsSnapshot(null, $"Line at {position.TotalSeconds:0}s", null));
    }

    private sealed class EmptyLyricsProvider : ILyricsProvider
    {
        public Task<LyricsSnapshot> GetSnapshotAsync(
            string title,
            string artist,
            TimeSpan position,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(LyricsSnapshot.Empty);
    }

    private sealed class FakeMediaSessionPlatform : IMediaSessionPlatform
    {
        public event EventHandler? CurrentSessionChanged;

        public MediaSessionSnapshot? CurrentSession { get; set; }

        public bool TogglePlayPauseResult { get; set; }

        public Exception? TogglePlayPauseException { get; set; }

        public int TogglePlayPauseInvocationCount { get; private set; }

        public int SkipNextInvocationCount { get; private set; }

        public Task<MediaSessionSnapshot?> GetCurrentSessionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CurrentSession);

        public Task<bool> TogglePlayPauseAsync(CancellationToken cancellationToken = default)
        {
            TogglePlayPauseInvocationCount++;
            return TogglePlayPauseException is null
                ? Task.FromResult(TogglePlayPauseResult)
                : Task.FromException<bool>(TogglePlayPauseException);
        }

        public Task<bool> SkipNextAsync(CancellationToken cancellationToken = default)
        {
            SkipNextInvocationCount++;
            return Task.FromResult(false);
        }

        public Task<bool> SkipPreviousAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public void RaiseCurrentSessionChanged() => CurrentSessionChanged?.Invoke(this, EventArgs.Empty);
    }
}
