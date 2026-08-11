using System.Runtime.InteropServices;
using Deyxis.Core.Activities;
using Deyxis.Core.Island;
using Deyxis.UI.Controls;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Deyxis.App;

public sealed class IslandWindow : Window
{
    private const int GwlExStyle = -20;
    private const long WsExNoActivate = 0x08000000L;
    private const int SmCxScreen = 0;
    private const int IdleWidth = 360;
    private const int IdleHeight = 46;
    private const int HoverWidth = 420;
    private const int HoverHeight = 68;
    private const int ExpandedWidth = 720;
    private const int ExpandedHeight = 360;

    private readonly AppWindow appWindow;
    private readonly nint windowHandle;
    private readonly IslandView islandView;
    private readonly IslandStateMachine stateMachine = new();
    private volatile bool closed;

    public IslandWindow(ActivitySnapshot initialSnapshot)
    {
        ArgumentNullException.ThrowIfNull(initialSnapshot);

        Title = "昼隙 / Deyxis";

        islandView = new IslandView();
        islandView.Bind(initialSnapshot, stateMachine);
        islandView.PresentationStateChanged += IslandView_PresentationStateChanged;
        Closed += IslandWindow_Closed;
        Content = islandView;

        windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        appWindow = AppWindow.GetFromWindowId(windowId);

        ConfigureWindow();
        ResizeAndPosition();
    }

    public void ShowWithoutActivation() => appWindow.Show(false);

    public void UpdateSnapshot(ActivitySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (closed)
        {
            return;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            islandView.Bind(snapshot, stateMachine);
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (!closed)
            {
                islandView.Bind(snapshot, stateMachine);
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

    private void IslandView_PresentationStateChanged(object? sender, EventArgs e) => ResizeAndPosition();

    private void IslandWindow_Closed(object sender, WindowEventArgs args)
    {
        closed = true;
        islandView.PresentationStateChanged -= IslandView_PresentationStateChanged;
        Closed -= IslandWindow_Closed;
    }

    private void ResizeAndPosition()
    {
        var (logicalWidth, logicalHeight) = islandView.ViewModel.PresentationState switch
        {
            IslandPresentationState.Hover => (HoverWidth, HoverHeight),
            IslandPresentationState.Expanded => (ExpandedWidth, ExpandedHeight),
            _ => (IdleWidth, IdleHeight),
        };

        var dpiScale = GetDpiForWindow(windowHandle) / 96d;
        var width = (int)Math.Round(logicalWidth * dpiScale);
        var height = (int)Math.Round(logicalHeight * dpiScale);
        var top = (int)Math.Round(8 * dpiScale);
        var left = Math.Max(0, (GetSystemMetrics(SmCxScreen) - width) / 2);

        appWindow.MoveAndResize(new RectInt32(left, top, width, height));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint windowHandle, int index, nint newValue);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);
}
