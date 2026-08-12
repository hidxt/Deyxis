using Deyxis.Core.Activities;
using Deyxis.Core.Events;
using Deyxis.Core.History;
using Deyxis.Core.Settings;
using Deyxis.Platform.Windows.Media;
using Deyxis.Platform.Windows.Storage;
using Deyxis.Platform.Windows.Wallpaper;
using Deyxis.Providers.Agents;
using Deyxis.Providers.FileDrop;
using Deyxis.Providers.Lyrics;
using Deyxis.Providers.Media;
using Microsoft.UI.Xaml;
using Deyxis.UI.Settings;

namespace Deyxis.App;

public partial class App : Application
{
    private IslandWindow? islandWindow;
    private ActivityPipeline? activityPipeline;
    private MockActivityProvider? activityProvider;
    private CancellationTokenSource? mediaStartupTokenSource;
    private GsmtcMediaSessionPlatform? mediaSessionPlatform;
    private MediaProvider? mediaProvider;
    private AgentProviderComposition? agentProviderComposition;
    private FileDropProvider? fileDropProvider;
    private SettingsWindow? settingsWindow;
    private IDisposable? historySubscription;
    private readonly SettingsStore settingsStore = new();
    private readonly ActivityHistoryStore historyStore = new();
    private readonly object historyGate = new();
    private SettingsSnapshot settings = SettingsSnapshot.Default;
    private ActivityHistoryRing history = new();

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        settings = await settingsStore.LoadAsync();
        history = await historyStore.LoadAsync();
        var eventBus = new EventBus();
        historySubscription = eventBus.Subscribe<ActivityUpserted>(OnActivityUpserted);
        activityPipeline = new ActivityPipeline(eventBus, new ActivityManager());
        activityProvider = new MockActivityProvider(eventBus);
        agentProviderComposition = AgentProviderComposition.CreateDisabled(eventBus);
        fileDropProvider = new FileDropProvider(eventBus, new WindowsCurrentUserWallpaper());
        islandWindow = new IslandWindow(activityPipeline.Current, fileDropProvider);
        islandWindow.ApplySettings(settings);

        activityPipeline.SnapshotChanged += ActivityPipeline_SnapshotChanged;
        islandWindow.Closed += IslandWindow_Closed;
        islandWindow.SettingsRequested += IslandWindow_SettingsRequested;

        activityProvider.Start();
        fileDropProvider.Start();
        activityProvider.PublishInitialActivities();
        mediaStartupTokenSource = new CancellationTokenSource();
        _ = StartMediaProviderAsync(eventBus, mediaStartupTokenSource.Token);
        islandWindow.ShowWithoutActivation();
    }

    public void PromoteWaitingActivity() => activityProvider?.PromoteWaitingActivity();

    private void IslandWindow_SettingsRequested(object? sender, EventArgs e)
    {
        if (settingsWindow is not null)
        {
            settingsWindow.Activate();
            return;
        }

        settingsWindow = new SettingsWindow(settings, history.Entries);
        settingsWindow.SettingsChanged += SettingsWindow_SettingsChanged;
        settingsWindow.HistoryClearRequested += SettingsWindow_HistoryClearRequested;
        settingsWindow.Closed += SettingsWindow_Closed;
        settingsWindow.Activate();
    }

    private async void SettingsWindow_SettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        settings = e.Settings;
        islandWindow?.ApplySettings(settings);
        await settingsStore.SaveAsync(settings);
    }

    private async void SettingsWindow_HistoryClearRequested(object? sender, EventArgs e)
    {
        lock (historyGate)
        {
            history.Clear();
        }
        await historyStore.ClearAsync();
    }

    private void SettingsWindow_Closed(object sender, WindowEventArgs args)
    {
        if (settingsWindow is null)
        {
            return;
        }

        settingsWindow.SettingsChanged -= SettingsWindow_SettingsChanged;
        settingsWindow.HistoryClearRequested -= SettingsWindow_HistoryClearRequested;
        settingsWindow.Closed -= SettingsWindow_Closed;
        settingsWindow = null;
    }

    private void OnActivityUpserted(ActivityUpserted message)
    {
        ActivityHistorySummary[] entries;
        lock (historyGate)
        {
            history.Add(message.Activity);
            entries = history.Entries.ToArray();
        }

        settingsWindow?.RefreshHistory(entries);
        _ = historyStore.SaveAsync(entries);
    }

    private void ActivityPipeline_SnapshotChanged(object? sender, ActivitySnapshot snapshot)
    {
        islandWindow?.UpdateSnapshot(snapshot, mediaProvider?.CurrentLyrics);
    }

    private void IslandWindow_Closed(object sender, WindowEventArgs args)
    {
        if (islandWindow is not null)
        {
            islandWindow.Closed -= IslandWindow_Closed;
            islandWindow.SettingsRequested -= IslandWindow_SettingsRequested;
        }

        if (activityPipeline is not null)
        {
            activityPipeline.SnapshotChanged -= ActivityPipeline_SnapshotChanged;
        }

        activityProvider?.Dispose();
        historySubscription?.Dispose();
        agentProviderComposition?.Dispose();
        fileDropProvider?.Stop();
        mediaStartupTokenSource?.Cancel();
        mediaProvider?.Dispose();
        mediaSessionPlatform?.Dispose();
        mediaStartupTokenSource?.Dispose();
        activityPipeline?.Dispose();

        activityProvider = null;
        agentProviderComposition = null;
        fileDropProvider = null;
        mediaStartupTokenSource = null;
        mediaProvider = null;
        mediaSessionPlatform = null;
        activityPipeline = null;
        historySubscription = null;
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
