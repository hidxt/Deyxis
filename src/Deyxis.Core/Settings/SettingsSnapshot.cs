using System.Collections.Immutable;

namespace Deyxis.Core.Settings;

public sealed record SettingsSnapshot(
    bool FollowActiveMonitor,
    IslandSurfaceMode SurfaceMode,
    double IslandWidth,
    double CornerRadius,
    double Opacity,
    bool ExpandOnHover,
    bool HideInFullscreen,
    bool DoNotDisturb,
    bool ShowProviderHealth,
    ImmutableArray<ProviderPreference> Providers)
{
    public static SettingsSnapshot Default { get; } = new(
        FollowActiveMonitor: true,
        SurfaceMode: IslandSurfaceMode.Solid,
        IslandWidth: 420,
        CornerRadius: 22,
        Opacity: 1,
        ExpandOnHover: true,
        HideInFullscreen: true,
        DoNotDisturb: false,
        ShowProviderHealth: true,
        Providers: []);
}
