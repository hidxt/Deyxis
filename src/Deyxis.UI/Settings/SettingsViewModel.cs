using System.Collections.Immutable;
using Deyxis.Core.Settings;

namespace Deyxis.UI.Settings;

public sealed class SettingsViewModel
{
    private readonly ImmutableArray<ProviderPreference> providers;

    public SettingsViewModel(SettingsSnapshot settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        FollowActiveMonitor = settings.FollowActiveMonitor;
        SurfaceMode = settings.SurfaceMode;
        IslandWidth = settings.IslandWidth;
        CornerRadius = settings.CornerRadius;
        Opacity = settings.Opacity;
        ExpandOnHover = settings.ExpandOnHover;
        HideInFullscreen = settings.HideInFullscreen;
        DoNotDisturb = settings.DoNotDisturb;
        ShowProviderHealth = settings.ShowProviderHealth;
        providers = settings.Providers;
    }

    public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    public bool FollowActiveMonitor { get; set; }

    public IslandSurfaceMode SurfaceMode { get; set; }

    public double IslandWidth { get; set; }

    public double CornerRadius { get; set; }

    public double Opacity { get; set; }

    public bool ExpandOnHover { get; set; }

    public bool HideInFullscreen { get; set; }

    public bool DoNotDisturb { get; set; }

    public bool ShowProviderHealth { get; set; }

    public void Apply()
    {
        var changed = SettingsPolicy.Validate(new SettingsSnapshot(
            FollowActiveMonitor,
            SurfaceMode,
            IslandWidth,
            CornerRadius,
            Opacity,
            ExpandOnHover,
            HideInFullscreen,
            DoNotDisturb,
            ShowProviderHealth,
            providers));
        SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(changed));
    }
}
