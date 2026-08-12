using Deyxis.Core.Island;
using Deyxis.Core.Placement;
using Xunit;

namespace Deyxis.Core.Tests;

public sealed class IslandPlacementControllerTests
{
    [Fact]
    public void Update_preserves_and_restores_state_and_size_after_fullscreen()
    {
        var controller = new IslandPlacementController(new LogicalSize(120, 6), 8);
        var monitor = MonitorAt150PercentDpi();

        var hidden = controller.Update(
            monitor,
            isFullscreen: true,
            IslandPresentationState.Expanded,
            new LogicalSize(720, 360));
        var restored = controller.Update(
            monitor,
            isFullscreen: false,
            IslandPresentationState.HiddenEdge,
            new LogicalSize(120, 6));

        Assert.Equal(IslandPresentationState.HiddenEdge, hidden.PresentationState);
        Assert.Equal(new PixelRect(3110, 0, 180, 9), hidden.Bounds);
        Assert.Equal(IslandPresentationState.Expanded, restored.PresentationState);
        Assert.Equal(new PixelRect(2660, 12, 1080, 540), restored.Bounds);
    }

    [Fact]
    public void Update_converts_logical_dimensions_and_offset_to_monitor_dpi_pixels()
    {
        var controller = new IslandPlacementController(new LogicalSize(120, 6), 8);
        var monitor = new MonitorSnapshot(
            "left",
            new PixelRect(-1600, 0, 1600, 900),
            new PixelRect(-1600, 20, 1600, 840),
            120,
            false);

        var placement = controller.Update(
            monitor,
            isFullscreen: false,
            IslandPresentationState.Idle,
            new LogicalSize(101, 45));

        Assert.Equal(IslandPresentationState.Idle, placement.PresentationState);
        Assert.Equal(new PixelRect(-863, 30, 126, 56), placement.Bounds);
    }

    [Fact]
    public void Repeated_fullscreen_updates_do_not_replace_the_saved_state()
    {
        var controller = new IslandPlacementController(new LogicalSize(120, 6), 8);
        var monitor = MonitorAt150PercentDpi();

        _ = controller.Update(
            monitor,
            isFullscreen: true,
            IslandPresentationState.Hover,
            new LogicalSize(420, 68));
        _ = controller.Update(
            monitor,
            isFullscreen: true,
            IslandPresentationState.HiddenEdge,
            new LogicalSize(120, 6));
        var restored = controller.Update(
            monitor,
            isFullscreen: false,
            IslandPresentationState.HiddenEdge,
            new LogicalSize(120, 6));

        Assert.Equal(IslandPresentationState.Hover, restored.PresentationState);
        Assert.Equal(new PixelRect(2885, 12, 630, 102), restored.Bounds);
    }

    [Fact]
    public void Reveal_restores_the_saved_state_while_fullscreen_remains_active()
    {
        var controller = new IslandPlacementController(new LogicalSize(120, 6), 8);
        var monitor = MonitorAt150PercentDpi();
        _ = controller.Update(
            monitor,
            isFullscreen: true,
            IslandPresentationState.Expanded,
            new LogicalSize(720, 360));

        var revealed = controller.Reveal(monitor);

        Assert.Equal(IslandPresentationState.Expanded, revealed.PresentationState);
        Assert.Equal(new PixelRect(2660, 12, 1080, 540), revealed.Bounds);
    }

    private static MonitorSnapshot MonitorAt150PercentDpi() =>
        new(
            "target",
            new PixelRect(1920, 0, 2560, 1440),
            new PixelRect(1920, 0, 2560, 1400),
            144,
            false);
}
