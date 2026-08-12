using Deyxis.Core.Activities;
using Deyxis.Core.Events;
using Deyxis.Platform.Windows.Media;
using Deyxis.Providers.Lyrics;
using Deyxis.Providers.Media;
using Microsoft.UI.Xaml;

namespace Deyxis.App;

public partial class App : Application
{
    private IslandWindow? islandWindow;
    private ActivityPipeline? activityPipeline;
    private MockActivityProvider? activityProvider;
    private CancellationTokenSource? mediaStartupTokenSource;
    private GsmtcMediaSessionPlatform? mediaSessionPlatform;
    private MediaProvider? mediaProvider;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var eventBus = new EventBus();
        activityPipeline = new ActivityPipeline(eventBus, new ActivityManager());
        activityProvider = new MockActivityProvider(eventBus);
        islandWindow = new IslandWindow(activityPipeline.Current);

        activityPipeline.SnapshotChanged += ActivityPipeline_SnapshotChanged;
        islandWindow.Closed += IslandWindow_Closed;

        activityProvider.Start();
        activityProvider.PublishInitialActivities();
        mediaStartupTokenSource = new CancellationTokenSource();
        _ = StartMediaProviderAsync(eventBus, mediaStartupTokenSource.Token);
        islandWindow.ShowWithoutActivation();
    }

    public void PromoteWaitingActivity() => activityProvider?.PromoteWaitingActivity();

    private void ActivityPipeline_SnapshotChanged(object? sender, ActivitySnapshot snapshot)
    {
        islandWindow?.UpdateSnapshot(snapshot, mediaProvider?.CurrentLyrics);
    }

    private void IslandWindow_Closed(object sender, WindowEventArgs args)
    {
        if (islandWindow is not null)
        {
            islandWindow.Closed -= IslandWindow_Closed;
        }

        if (activityPipeline is not null)
        {
            activityPipeline.SnapshotChanged -= ActivityPipeline_SnapshotChanged;
        }

        activityProvider?.Dispose();
        mediaStartupTokenSource?.Cancel();
        mediaProvider?.Dispose();
        mediaSessionPlatform?.Dispose();
        mediaStartupTokenSource?.Dispose();
        activityPipeline?.Dispose();

        activityProvider = null;
        mediaStartupTokenSource = null;
        mediaProvider = null;
        mediaSessionPlatform = null;
        activityPipeline = null;
        islandWindow = null;
    }

    private async Task StartMediaProviderAsync(IEventBus eventBus, CancellationToken cancellationToken)
    {
        GsmtcMediaSessionPlatform? platform = null;
        MediaProvider? provider = null;

        try
        {
            platform = await GsmtcMediaSessionPlatform.RequestAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var lyricsRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Deyxis",
                "Lyrics");
            provider = new MediaProvider(platform, eventBus, new LocalLrcLyricsProvider(lyricsRoot));
            mediaSessionPlatform = platform;
            mediaProvider = provider;
            provider.Start();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            provider?.Dispose();
            platform?.Dispose();
            mediaProvider = null;
            mediaSessionPlatform = null;
        }
        catch (Exception)
        {
            provider?.Dispose();
            platform?.Dispose();
            mediaProvider = null;
            mediaSessionPlatform = null;
        }
    }
}
