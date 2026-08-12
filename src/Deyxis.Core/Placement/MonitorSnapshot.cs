namespace Deyxis.Core.Placement;

public sealed record MonitorSnapshot(
    string Id,
    PixelRect Bounds,
    PixelRect WorkArea,
    uint Dpi,
    bool IsPrimary);
