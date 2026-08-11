using Deyxis.Core.Activities;
using Deyxis.Core.Events;
using Microsoft.UI.Xaml;

namespace Deyxis.App;

public partial class App : Application
{
    private IslandWindow? islandWindow;
    private ActivityPipeline? activityPipeline;
    private MockActivityProvider? activityProvider;

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
        islandWindow.ShowWithoutActivation();
    }

    public void PromoteWaitingActivity() => activityProvider?.PromoteWaitingActivity();

    private void ActivityPipeline_SnapshotChanged(object? sender, ActivitySnapshot snapshot)
    {
        islandWindow?.UpdateSnapshot(snapshot);
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
        activityPipeline?.Dispose();

        activityProvider = null;
        activityPipeline = null;
        islandWindow = null;
    }
}
