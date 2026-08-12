using Deyxis.Core.Placement;
using Xunit;

namespace Deyxis.Core.Tests;

public sealed class MonitorManagerTests
{
    [Fact]
    public void SelectTarget_prefers_foreground_monitor_over_island_and_primary_fallbacks()
    {
        var primary = Monitor("primary", 0, isPrimary: true);
        var island = Monitor("island", 1920);
        var foreground = Monitor("foreground", 3840);
        var monitors = new[] { primary, island, foreground };

        var selected = MonitorManager.SelectTarget(
            monitors,
            new ForegroundWindowSnapshot(new PixelRect(4000, 100, 900, 700), true, false, false),
            new PixelRect(2200, 10, 300, 100));

        Assert.Equal("foreground", selected.Id);
    }

    [Fact]
    public void SelectTarget_uses_island_monitor_when_foreground_is_off_screen()
    {
        var primary = Monitor("primary", 0, isPrimary: true);
        var island = Monitor("island", 1920);

        var selected = MonitorManager.SelectTarget(
            new[] { primary, island },
            new ForegroundWindowSnapshot(new PixelRect(-3000, 100, 800, 600), true, false, false),
            new PixelRect(2200, 10, 300, 100));

        Assert.Equal("island", selected.Id);
    }

    [Fact]
    public void SelectTarget_uses_primary_monitor_when_windows_do_not_intersect_a_monitor()
    {
        var secondary = Monitor("secondary", 1920);
        var primary = Monitor("primary", 0, isPrimary: true);

        var selected = MonitorManager.SelectTarget(
            new[] { secondary, primary },
            new ForegroundWindowSnapshot(new PixelRect(-3000, 100, 800, 600), true, false, false),
            new PixelRect(-2000, 10, 300, 100));

        Assert.Equal("primary", selected.Id);
    }

    private static MonitorSnapshot Monitor(string id, int left, bool isPrimary = false) =>
        new(
            id,
            new PixelRect(left, 0, 1920, 1080),
            new PixelRect(left, 0, 1920, 1040),
            96,
            isPrimary);
}
