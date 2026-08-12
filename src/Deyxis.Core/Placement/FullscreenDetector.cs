namespace Deyxis.Core.Placement;

public static class FullscreenDetector
{
    private const double LogicalTolerance = 2;

    public static bool IsFullscreen(
        ForegroundWindowSnapshot? foregroundWindow,
        MonitorSnapshot monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        if (foregroundWindow is null
            || !foregroundWindow.IsVisible
            || foregroundWindow.IsMinimized
            || foregroundWindow.IsCloaked)
        {
            return false;
        }

        var tolerance = (int)Math.Ceiling(LogicalTolerance * monitor.Dpi / 96d);
        var window = foregroundWindow.Bounds;
        var target = monitor.Bounds;

        return window.X <= target.X + tolerance
            && window.Y <= target.Y + tolerance
            && (long)window.X + window.Width >= (long)target.X + target.Width - tolerance
            && (long)window.Y + window.Height >= (long)target.Y + target.Height - tolerance;
    }
}
