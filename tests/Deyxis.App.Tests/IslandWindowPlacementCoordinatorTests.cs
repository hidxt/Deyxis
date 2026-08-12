using Deyxis.App;
using Deyxis.Core.Island;
using Deyxis.Core.Placement;
using Xunit;

namespace Deyxis.App.Tests;

public sealed class IslandWindowPlacementCoordinatorTests
{
    [Fact]
    public void Foreground_change_and_reveal_apply_hidden_edge_and_restored_placement_on_ui_dispatcher()
    {
        var monitor = new MonitorSnapshot(
            "secondary",
            new PixelRect(1920, 0, 2560, 1440),
            new PixelRect(1920, 0, 2560, 1400),
            144,
            false);
        var source = new RecordingMonitorForegroundSource
        {
            Monitors = [monitor],
        };
        var dispatcher = new RecordingUiDispatcher { HasThreadAccess = true };
        var host = new RecordingPlacementHost
        {
            CurrentBounds = new PixelRect(2660, 12, 1080, 540),
            CurrentPresentationState = IslandPresentationState.Expanded,
            CurrentLogicalSize = new LogicalSize(720, 360),
        };
        using var coordinator = new IslandWindowPlacementCoordinator(
            source,
            dispatcher,
            host,
            new IslandPlacementController(new LogicalSize(120, 6), 8));
        coordinator.Start();

        source.Foreground = new ForegroundWindowSnapshot(
            monitor.Bounds,
            IsVisible: true,
            IsMinimized: false,
            IsCloaked: false);
        source.RaiseForegroundWindowChanged();

        Assert.Equal(IslandPresentationState.HiddenEdge, host.CurrentPresentationState);
        Assert.Equal(new PixelRect(3110, 0, 180, 9), host.CurrentBounds);

        dispatcher.HasThreadAccess = false;
        host.RaiseRevealRequested();

        Assert.Equal(IslandPresentationState.HiddenEdge, host.CurrentPresentationState);
        Assert.Single(dispatcher.Pending);

        dispatcher.RunNext();

        Assert.Equal(IslandPresentationState.Expanded, host.CurrentPresentationState);
        Assert.Equal(new PixelRect(2660, 12, 1080, 540), host.CurrentBounds);
    }

    [Fact]
    public void Dispose_detaches_monitor_foreground_and_reveal_listeners()
    {
        var source = new RecordingMonitorForegroundSource();
        var dispatcher = new RecordingUiDispatcher { HasThreadAccess = false };
        var host = new RecordingPlacementHost();
        var coordinator = new IslandWindowPlacementCoordinator(
            source,
            dispatcher,
            host,
            new IslandPlacementController(new LogicalSize(120, 6), 8));
        coordinator.Start();
        dispatcher.Pending.Clear();

        coordinator.Dispose();
        source.RaiseMonitorsChanged();
        source.RaiseForegroundWindowChanged();
        host.RaiseRevealRequested();

        Assert.True(source.IsDisposed);
        Assert.Empty(dispatcher.Pending);
    }

    private sealed class RecordingMonitorForegroundSource : IMonitorForegroundSource
    {
        public event EventHandler? MonitorsChanged;

        public event EventHandler? ForegroundWindowChanged;

        public IReadOnlyList<MonitorSnapshot> Monitors { get; init; } = [];

        public ForegroundWindowSnapshot? Foreground { get; set; }

        public bool IsDisposed { get; private set; }

        public IReadOnlyList<MonitorSnapshot> GetMonitors() => Monitors;

        public ForegroundWindowSnapshot? GetForegroundWindow() => Foreground;

        public void RaiseMonitorsChanged() => MonitorsChanged?.Invoke(this, EventArgs.Empty);

        public void RaiseForegroundWindowChanged() =>
            ForegroundWindowChanged?.Invoke(this, EventArgs.Empty);

        public void Dispose() => IsDisposed = true;
    }

    private sealed class RecordingUiDispatcher : IIslandUiDispatcher
    {
        public bool HasThreadAccess { get; set; }

        public List<Action> Pending { get; } = [];

        public bool TryEnqueue(Action callback)
        {
            Pending.Add(callback);
            return true;
        }

        public void RunNext()
        {
            var callback = Pending[0];
            Pending.RemoveAt(0);
            callback();
        }
    }

    private sealed class RecordingPlacementHost : IIslandPlacementHost
    {
        public event EventHandler? RevealRequested;

        public PixelRect CurrentBounds { get; set; }

        public IslandPresentationState CurrentPresentationState { get; set; }

        public LogicalSize CurrentLogicalSize { get; set; } = new(360, 46);

        public void ApplyPlacement(IslandPlacement placement)
        {
            CurrentBounds = placement.Bounds;
            CurrentPresentationState = placement.PresentationState;
            CurrentLogicalSize = placement.PresentationState == IslandPresentationState.HiddenEdge
                ? new LogicalSize(120, 6)
                : CurrentLogicalSize;
        }

        public void RaiseRevealRequested() => RevealRequested?.Invoke(this, EventArgs.Empty);
    }
}
