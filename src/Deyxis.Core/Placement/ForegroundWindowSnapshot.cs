namespace Deyxis.Core.Placement;

public sealed record ForegroundWindowSnapshot(
    PixelRect Bounds,
    bool IsVisible,
    bool IsMinimized,
    bool IsCloaked);
