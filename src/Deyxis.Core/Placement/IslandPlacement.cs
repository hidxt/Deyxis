using Deyxis.Core.Island;

namespace Deyxis.Core.Placement;

public sealed record IslandPlacement(
    MonitorSnapshot Monitor,
    PixelRect Bounds,
    IslandPresentationState PresentationState);
