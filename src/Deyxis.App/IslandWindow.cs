using System.Runtime.InteropServices;
using Deyxis.Core.Activities;
using Deyxis.Core.Island;
using Deyxis.Core.Placement;
using Deyxis.Core.Settings;
using Deyxis.Providers.FileDrop;
using Deyxis.Providers.Lyrics;
using Deyxis.UI.Controls;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Deyxis.App;

public sealed class IslandWindow : Window, IIslandPlacementHost
{
    private const int GwlExStyle = -20;
    private const long WsExNoActivate = 0x08000000L;
    private const int IdleWidth = 360;
    private const int IdleHeight = 46;
    private const int HoverWidth = 420;
    private const int HoverHeight = 68;
    private const int ExpandedWidth = 720;
    private const int ExpandedHeight = 360;

    private readonly AppWindow appWindow;
    private readonly nint windowHandle;
    private readonly IslandView islandView;
    private readonly FileDropProvider fileDropProvider;
    private readonly IslandStateMachine stateMachine = new();
    private readonly IslandWindowPlacementCoordinator placementCoordinator;
    private bool applyingPlacement;
    private volatile bool closed;
    private double islandWidth = SettingsSnapshot.Default.IslandWidth;

    public IslandWindow(ActivitySnapshot initialSnapshot, FileDropProvider fileDropProvider)
    {
        ArgumentNullException.ThrowIfNull(initialSnapshot);
        this.fileDropProvider = fileDropProvider ?? throw new ArgumentNullException(nameof(fileDropProvider));

        Title = "昼隙 / Deyxis";

        islandView = new IslandView();
        islandView.Bind(initialSnapshot, stateMachine);
        islandView.PresentationStateChanged += IslandView_PresentationStateChanged;
        islandView.FilesDropped += IslandView_FilesDropped;
        islandView.FileDropConfirmRequested += IslandView_FileDropConfirmRequested;
        islandView.FileDropCancelRequested += IslandView_FileDropCancelRequested;
        islandView.RevealRequested += IslandView_RevealRequested;
        islandView.SettingsRequested += IslandView_SettingsRequested;
        Closed += IslandWindow_Closed;
        Content = islandView;

        windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        appWindow = AppWindow.GetFromWindowId(windowId);

        ConfigureWindow();
        placementCoordinator = new IslandWindowPlacementCoordinator(
            new WindowsMonitorForegroundSource(),
            new DispatcherQueueAdapter(DispatcherQueue),
            this,
            new IslandPlacementController(new LogicalSize(120, 6), 8));
        placementCoordinator.Start();
    }

    public event EventHandler? RevealRequested;

    public event EventHandler? SettingsRequested;

    public PixelRect CurrentBounds => new(
        appWindow.Position.X,
        appWindow.Position.Y,
        appWindow.Size.Width,
        appWindow.Size.Height);

    public IslandPresentationState CurrentPresentationState => stateMachine.Current;

    public LogicalSize CurrentLogicalSize => GetLogicalSize(stateMachine.Current);

    public void ShowWithoutActivation() => appWindow.Show(false);

    public void ApplySettings(SettingsSnapshot settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        islandWidth = settings.IslandWidth;
        islandView.ApplySettings(settings);
        placementCoordinator.Configure(settings.FollowActiveMonitor, settings.HideInFullscreen);
    }

    public void UpdateSnapshot(ActivitySnapshot snapshot, LyricsSnapshot? lyrics = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (closed)
        {
            return;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            islandView.Bind(snapshot, stateMachine, lyrics);
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (!closed)
            {
                islandView.Bind(snapshot, stateMachine, lyrics);
            }
        });
    }

    private void ConfigureWindow()
    {
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
        }

        var extendedStyle = GetWindowLongPtr(windowHandle, GwlExStyle).ToInt64();
        _ = SetWindowLongPtr(windowHandle, GwlExStyle, new nint(extendedStyle | WsExNoActivate));
    }

    public void ApplyPlacement(IslandPlacement placement)
    {
        applyingPlacement = true;
        try
        {
            islandView.SetPresentationState(placement.PresentationState);
            appWindow.MoveAndResize(new RectInt32(
                placement.Bounds.X,
                placement.Bounds.Y,
                placement.Bounds.Width,
                placement.Bounds.Height));
        }
        finally
        {
            applyingPlacement = false;
        }
    }

    private void IslandView_PresentationStateChanged(object? sender, EventArgs e)
    {
        if (!applyingPlacement)
        {
            placementCoordinator.Refresh();
        }
    }

    private void IslandView_RevealRequested(object? sender, EventArgs e) =>
        RevealRequested?.Invoke(this, EventArgs.Empty);

    private void IslandView_SettingsRequested(object? sender, EventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void IslandWindow_Closed(object sender, WindowEventArgs args)
    {
        closed = true;
        placementCoordinator.Dispose();
        islandView.PresentationStateChanged -= IslandView_PresentationStateChanged;
        islandView.FilesDropped -= IslandView_FilesDropped;
        islandView.FileDropConfirmRequested -= IslandView_FileDropConfirmRequested;
        islandView.FileDropCancelRequested -= IslandView_FileDropCancelRequested;
        islandView.RevealRequested -= IslandView_RevealRequested;
        islandView.SettingsRequested -= IslandView_SettingsRequested;
        Closed -= IslandWindow_Closed;
    }

    private async Task IslandView_FilesDropped(IReadOnlyList<string> paths)
    {
        var result = await fileDropProvider.HandleDropAsync(paths);
        if (!result.Accepted)
        {
            return;
        }

        islandView.SetValidatedFileDrop(
            result.ActivityId,
            result.ConfirmationToken,
            Path.GetFullPath(paths[0]));
    }

    private Task IslandView_FileDropConfirmRequested(Guid confirmationToken) =>
        fileDropProvider.ConfirmAsync(confirmationToken);

    private void IslandView_FileDropCancelRequested(Guid confirmationToken) =>
        fileDropProvider.Cancel(confirmationToken);

    private LogicalSize GetLogicalSize(IslandPresentationState state)
    {
        var (logicalWidth, logicalHeight) = state switch
        {
            IslandPresentationState.Hover => ((int)Math.Round(islandWidth), HoverHeight),
            IslandPresentationState.Expanded when islandView.ViewModel.HasFileDropPreview =>
                (Math.Max((int)Math.Round(islandWidth), ExpandedWidth), 500),
            IslandPresentationState.Expanded =>
                (Math.Max((int)Math.Round(islandWidth), ExpandedWidth), ExpandedHeight),
            _ => ((int)Math.Round(islandWidth), IdleHeight),
        };
        return new LogicalSize(logicalWidth, logicalHeight);
    }

    private sealed class DispatcherQueueAdapter(Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue)
        : IIslandUiDispatcher
    {
        public bool HasThreadAccess => dispatcherQueue.HasThreadAccess;

        public bool TryEnqueue(Action callback) => dispatcherQueue.TryEnqueue(() => callback());
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint windowHandle, int index, nint newValue);

}
