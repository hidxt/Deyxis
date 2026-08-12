using Deyxis.Core.Island;
using Deyxis.Core.Placement;
using Deyxis.Platform.Windows.Placement;

namespace Deyxis.App;

public interface IMonitorForegroundSource : IDisposable
{
    event EventHandler? MonitorsChanged;

    event EventHandler? ForegroundWindowChanged;

    IReadOnlyList<MonitorSnapshot> GetMonitors();

    ForegroundWindowSnapshot? GetForegroundWindow();
}

public interface IIslandUiDispatcher
{
    bool HasThreadAccess { get; }

    bool TryEnqueue(Action callback);
}

public interface IIslandPlacementHost
{
    event EventHandler? RevealRequested;

    PixelRect CurrentBounds { get; }

    IslandPresentationState CurrentPresentationState { get; }

    LogicalSize CurrentLogicalSize { get; }

    void ApplyPlacement(IslandPlacement placement);
}

public sealed class IslandWindowPlacementCoordinator : IDisposable
{
    private readonly IMonitorForegroundSource source;
    private readonly IIslandUiDispatcher dispatcher;
    private readonly IIslandPlacementHost host;
    private readonly IslandPlacementController controller;
    private bool started;
    private bool disposed;
    private bool followActiveMonitor = true;
    private bool hideInFullscreen = true;

    public IslandWindowPlacementCoordinator(
        IMonitorForegroundSource source,
        IIslandUiDispatcher dispatcher,
        IIslandPlacementHost host,
        IslandPlacementController controller)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started)
        {
            return;
        }

        started = true;
        source.MonitorsChanged += Source_Changed;
        source.ForegroundWindowChanged += Source_Changed;
        host.RevealRequested += Host_RevealRequested;
        Refresh();
    }

    public void Refresh()
    {
        if (!started || disposed)
        {
            return;
        }

        Dispatch(ApplyCurrentPlacement);
    }

    public void Configure(bool followActiveMonitor, bool hideInFullscreen)
    {
        this.followActiveMonitor = followActiveMonitor;
        this.hideInFullscreen = hideInFullscreen;
        Refresh();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (started)
        {
            source.MonitorsChanged -= Source_Changed;
            source.ForegroundWindowChanged -= Source_Changed;
            host.RevealRequested -= Host_RevealRequested;
        }

        source.Dispose();
    }

    private void Source_Changed(object? sender, EventArgs args) => Refresh();

    private void Host_RevealRequested(object? sender, EventArgs args) => Dispatch(ApplyReveal);

    private void ApplyCurrentPlacement()
    {
        if (disposed)
        {
            return;
        }

        var monitors = source.GetMonitors();
        if (monitors.Count == 0)
        {
            return;
        }

        var foreground = source.GetForegroundWindow();
        var monitor = MonitorManager.SelectTarget(
            monitors,
            followActiveMonitor ? foreground : null,
            host.CurrentBounds);
        host.ApplyPlacement(controller.Update(
            monitor,
            hideInFullscreen && FullscreenDetector.IsFullscreen(foreground, monitor),
            host.CurrentPresentationState,
            host.CurrentLogicalSize));
    }

    private void ApplyReveal()
    {
        if (disposed || host.CurrentPresentationState != IslandPresentationState.HiddenEdge)
        {
            return;
        }

        var monitors = source.GetMonitors();
        if (monitors.Count == 0)
        {
            return;
        }

        var monitor = MonitorManager.SelectTarget(monitors, null, host.CurrentBounds);
        host.ApplyPlacement(controller.Reveal(monitor));
    }

    private void Dispatch(Action callback)
    {
        if (dispatcher.HasThreadAccess)
        {
            callback();
        }
        else
        {
            _ = dispatcher.TryEnqueue(callback);
        }
    }
}

internal sealed class WindowsMonitorForegroundSource : IMonitorForegroundSource
{
    private readonly WindowsMonitorForegroundFacade facade = new();

    public event EventHandler? MonitorsChanged
    {
        add => facade.MonitorsChanged += value;
        remove => facade.MonitorsChanged -= value;
    }

    public event EventHandler? ForegroundWindowChanged
    {
        add => facade.ForegroundWindowChanged += value;
        remove => facade.ForegroundWindowChanged -= value;
    }

    public IReadOnlyList<MonitorSnapshot> GetMonitors() => facade.GetMonitors();

    public ForegroundWindowSnapshot? GetForegroundWindow() => facade.GetForegroundWindow();

    public void Dispose() => facade.Dispose();
}
