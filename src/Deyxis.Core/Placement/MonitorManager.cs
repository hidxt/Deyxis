namespace Deyxis.Core.Placement;

public static class MonitorManager
{
    public static MonitorSnapshot SelectTarget(
        IReadOnlyList<MonitorSnapshot> monitors,
        ForegroundWindowSnapshot? foregroundWindow,
        PixelRect? islandBounds)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        if (monitors.Count == 0)
        {
            throw new ArgumentException("At least one monitor is required.", nameof(monitors));
        }

        return FindMonitor(monitors, foregroundWindow?.Bounds)
            ?? FindMonitor(monitors, islandBounds)
            ?? monitors.FirstOrDefault(monitor => monitor.IsPrimary)
            ?? monitors[0];
    }

    private static MonitorSnapshot? FindMonitor(
        IReadOnlyList<MonitorSnapshot> monitors,
        PixelRect? bounds)
    {
        if (bounds is not { Width: > 0, Height: > 0 } rectangle)
        {
            return null;
        }

        MonitorSnapshot? selected = null;
        long greatestIntersection = 0;
        foreach (var monitor in monitors)
        {
            var intersection = monitor.Bounds.IntersectionArea(rectangle);
            if (intersection > greatestIntersection)
            {
                selected = monitor;
                greatestIntersection = intersection;
            }
        }

        return selected;
    }
}
