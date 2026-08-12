using System.Globalization;
using Deyxis.Core.Placement;

namespace Deyxis.Platform.Windows.Placement;

public sealed class WindowsMonitorForegroundFacade : IDisposable
{
    private readonly IWindowsDisplayNative native;
    private readonly IDisposable displayRegistration;
    private readonly IDisposable foregroundRegistration;
    private bool disposed;

    public WindowsMonitorForegroundFacade()
        : this(User32WindowsDisplayNative.Instance)
    {
    }

    internal WindowsMonitorForegroundFacade(IWindowsDisplayNative native)
    {
        this.native = native ?? throw new ArgumentNullException(nameof(native));
        displayRegistration = native.RegisterDisplayChanged(OnDisplayChanged);
        try
        {
            foregroundRegistration = native.RegisterForegroundChanged(OnForegroundChanged);
        }
        catch
        {
            displayRegistration.Dispose();
            throw;
        }
    }

    public event EventHandler? MonitorsChanged;

    public event EventHandler? ForegroundWindowChanged;

    public IReadOnlyList<MonitorSnapshot> GetMonitors()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!native.TryGetMonitors(out var monitors))
        {
            return [];
        }

        return monitors.Select(monitor => new MonitorSnapshot(
            $"0x{monitor.Handle.ToInt64().ToString("X", CultureInfo.InvariantCulture)}",
            ToPixelRect(monitor.Bounds),
            ToPixelRect(monitor.WorkArea),
            monitor.Dpi,
            monitor.IsPrimary)).ToArray();
    }

    public ForegroundWindowSnapshot? GetForegroundWindow()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!native.TryGetForegroundWindow(out var foreground))
        {
            return null;
        }

        return new ForegroundWindowSnapshot(
            ToPixelRect(foreground.Bounds),
            foreground.IsVisible,
            foreground.IsMinimized,
            foreground.IsCloaked);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        displayRegistration.Dispose();
        foregroundRegistration.Dispose();
    }

    private static PixelRect ToPixelRect(NativeRect rectangle) =>
        new(
            rectangle.Left,
            rectangle.Top,
            Math.Max(0, rectangle.Right - rectangle.Left),
            Math.Max(0, rectangle.Bottom - rectangle.Top));

    private void OnDisplayChanged()
    {
        if (!disposed)
        {
            MonitorsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnForegroundChanged()
    {
        if (!disposed)
        {
            ForegroundWindowChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
