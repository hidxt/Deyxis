using System.Collections.Immutable;
using Deyxis.Core.Settings;
using Deyxis.UI.Settings;
using Xunit;

namespace Deyxis.UI.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void Apply_maps_changed_fields_to_a_validated_snapshot_and_raises_one_explicit_event()
    {
        var original = SettingsSnapshot.Default with
        {
            Providers = ImmutableArray.Create(new ProviderPreference("media", false)),
        };
        var viewModel = new SettingsViewModel(original)
        {
            FollowActiveMonitor = false,
            SurfaceMode = IslandSurfaceMode.Acrylic,
            IslandWidth = 512,
            CornerRadius = 18,
            Opacity = 0.85,
            ExpandOnHover = false,
            HideInFullscreen = false,
            DoNotDisturb = true,
            ShowProviderHealth = false,
        };
        var events = new List<SettingsSnapshot>();
        viewModel.SettingsChanged += (_, args) => events.Add(args.Settings);

        viewModel.Apply();

        var changed = Assert.Single(events);
        Assert.False(changed.FollowActiveMonitor);
        Assert.Equal(IslandSurfaceMode.Acrylic, changed.SurfaceMode);
        Assert.Equal(512, changed.IslandWidth);
        Assert.Equal(18, changed.CornerRadius);
        Assert.Equal(0.85, changed.Opacity);
        Assert.False(changed.ExpandOnHover);
        Assert.False(changed.HideInFullscreen);
        Assert.True(changed.DoNotDisturb);
        Assert.False(changed.ShowProviderHealth);
        Assert.Equal(original.Providers.ToArray(), changed.Providers.ToArray());
    }

    [Fact]
    public void Apply_normalizes_out_of_range_editable_values()
    {
        var viewModel = new SettingsViewModel(SettingsSnapshot.Default)
        {
            IslandWidth = 100,
            CornerRadius = -1,
            Opacity = 2,
        };
        SettingsSnapshot? changed = null;
        viewModel.SettingsChanged += (_, args) => changed = args.Settings;

        viewModel.Apply();

        Assert.NotNull(changed);
        Assert.Equal(SettingsSnapshot.Default.IslandWidth, changed.IslandWidth);
        Assert.Equal(SettingsSnapshot.Default.CornerRadius, changed.CornerRadius);
        Assert.Equal(SettingsSnapshot.Default.Opacity, changed.Opacity);
    }
}
