using Deyxis.Core.Island;

namespace Deyxis.Core.Placement;

public sealed class IslandPlacementController
{
    private readonly LogicalSize hiddenEdgeSize;
    private readonly double topOffset;
    private (IslandPresentationState State, LogicalSize Size)? stateBeforeHiddenEdge;

    public IslandPlacementController(LogicalSize hiddenEdgeSize, double topOffset)
    {
        if (hiddenEdgeSize.Width <= 0 || hiddenEdgeSize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hiddenEdgeSize),
                "Hidden-edge dimensions must be positive.");
        }

        this.hiddenEdgeSize = hiddenEdgeSize;
        this.topOffset = topOffset;
    }

    public IslandPlacement Update(
        MonitorSnapshot monitor,
        bool isFullscreen,
        IslandPresentationState currentState,
        LogicalSize currentSize)
    {
        ArgumentNullException.ThrowIfNull(monitor);

        IslandPresentationState targetState;
        LogicalSize targetSize;
        if (isFullscreen)
        {
            if (currentState != IslandPresentationState.HiddenEdge)
            {
                stateBeforeHiddenEdge = (currentState, currentSize);
            }

            targetState = IslandPresentationState.HiddenEdge;
            targetSize = hiddenEdgeSize;
        }
        else if (currentState == IslandPresentationState.HiddenEdge && stateBeforeHiddenEdge is { } previous)
        {
            targetState = previous.State;
            targetSize = previous.Size;
            stateBeforeHiddenEdge = null;
        }
        else
        {
            targetState = currentState;
            targetSize = currentSize;
        }

        return CreatePlacement(monitor, targetState, targetSize);
    }

    public IslandPlacement Reveal(MonitorSnapshot monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        var restored = stateBeforeHiddenEdge
            ?? throw new InvalidOperationException("The Island is not hidden at the monitor edge.");
        stateBeforeHiddenEdge = null;

        return CreatePlacement(monitor, restored.State, restored.Size);
    }

    private IslandPlacement CreatePlacement(
        MonitorSnapshot monitor,
        IslandPresentationState targetState,
        LogicalSize targetSize)
    {
        var dpiScale = monitor.Dpi / 96d;
        var width = Scale(targetSize.Width, dpiScale);
        var height = Scale(targetSize.Height, dpiScale);
        var left = monitor.WorkArea.X + ((monitor.WorkArea.Width - width) / 2);
        var top = targetState == IslandPresentationState.HiddenEdge
            ? monitor.Bounds.Y
            : monitor.WorkArea.Y + Scale(topOffset, dpiScale);

        return new IslandPlacement(
            monitor,
            new PixelRect(left, top, width, height),
            targetState);
    }

    private static int Scale(double logicalPixels, double scale) =>
        (int)Math.Round(logicalPixels * scale, MidpointRounding.AwayFromZero);
}
