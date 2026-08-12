using System.Collections.Immutable;
using Deyxis.Core.Settings;
using Xunit;

namespace Deyxis.Core.Tests;

public sealed class SettingsPolicyTests
{
    [Fact]
    public void Missing_settings_fall_back_to_immutable_defaults()
    {
        var settings = SettingsPolicy.Validate(null);

        Assert.Same(SettingsSnapshot.Default, settings);
        Assert.True(settings.FollowActiveMonitor);
        Assert.Equal(IslandSurfaceMode.Solid, settings.SurfaceMode);
        Assert.Equal(420, settings.IslandWidth);
        Assert.Equal(22, settings.CornerRadius);
        Assert.Equal(1, settings.Opacity);
        Assert.True(settings.ExpandOnHover);
        Assert.True(settings.HideInFullscreen);
        Assert.False(settings.DoNotDisturb);
        Assert.True(settings.ShowProviderHealth);
        Assert.Empty(settings.Providers);
    }

    [Fact]
    public void Invalid_setting_values_fall_back_without_discarding_valid_values()
    {
        var candidate = SettingsSnapshot.Default with
        {
            FollowActiveMonitor = false,
            SurfaceMode = (IslandSurfaceMode)99,
            IslandWidth = double.NaN,
            CornerRadius = -1,
            Opacity = 2,
            Providers = ImmutableArray.Create(
                new ProviderPreference("", false),
                new ProviderPreference(new string('x', 129), true),
                new ProviderPreference("media", false),
                new ProviderPreference("media", true)),
        };

        var settings = SettingsPolicy.Validate(candidate);

        Assert.False(settings.FollowActiveMonitor);
        Assert.Equal(SettingsSnapshot.Default.SurfaceMode, settings.SurfaceMode);
        Assert.Equal(SettingsSnapshot.Default.IslandWidth, settings.IslandWidth);
        Assert.Equal(SettingsSnapshot.Default.CornerRadius, settings.CornerRadius);
        Assert.Equal(SettingsSnapshot.Default.Opacity, settings.Opacity);
        var provider = Assert.Single(settings.Providers);
        Assert.Equal(new ProviderPreference("media", false), provider);
    }
}
