using Deyxis.Core.Placement;
using Xunit;

namespace Deyxis.Core.Tests;

public sealed class FullscreenDetectorTests
{
    [Theory]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void IsFullscreen_honors_dpi_scaled_tolerance_boundary(
        int uncoveredPixels,
        bool expected)
    {
        var monitor = new MonitorSnapshot(
            "target",
            new PixelRect(100, 50, 1920, 1080),
            new PixelRect(100, 50, 1920, 1040),
            192,
            true);
        var foreground = new ForegroundWindowSnapshot(
            new PixelRect(
                100 + uncoveredPixels,
                50 + uncoveredPixels,
                1920 - (2 * uncoveredPixels),
                1080 - (2 * uncoveredPixels)),
            true,
            false,
            false);

        Assert.Equal(expected, FullscreenDetector.IsFullscreen(foreground, monitor));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void IsFullscreen_rejects_ineligible_foreground_windows(
        bool isVisible,
        bool isMinimized,
        bool isCloaked)
    {
        var monitor = new MonitorSnapshot(
            "target",
            new PixelRect(0, 0, 1920, 1080),
            new PixelRect(0, 0, 1920, 1040),
            96,
            true);
        var foreground = new ForegroundWindowSnapshot(
            new PixelRect(0, 0, 1920, 1080),
            isVisible,
            isMinimized,
            isCloaked);

        Assert.False(FullscreenDetector.IsFullscreen(foreground, monitor));
    }
}
