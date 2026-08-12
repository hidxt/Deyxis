using System.Collections.Immutable;

namespace Deyxis.Core.Settings;

public static class SettingsPolicy
{
    public const int MaximumProviderIdLength = 128;
    public const double MinimumIslandWidth = 240;
    public const double MaximumIslandWidth = 800;
    public const double MaximumCornerRadius = 64;
    public const double MinimumOpacity = 0.3;

    public static SettingsSnapshot Validate(SettingsSnapshot? candidate)
    {
        if (candidate is null)
        {
            return SettingsSnapshot.Default;
        }

        return candidate with
        {
            SurfaceMode = Enum.IsDefined(candidate.SurfaceMode)
                ? candidate.SurfaceMode
                : SettingsSnapshot.Default.SurfaceMode,
            IslandWidth = IsInRange(candidate.IslandWidth, MinimumIslandWidth, MaximumIslandWidth)
                ? candidate.IslandWidth
                : SettingsSnapshot.Default.IslandWidth,
            CornerRadius = IsInRange(candidate.CornerRadius, 0, MaximumCornerRadius)
                ? candidate.CornerRadius
                : SettingsSnapshot.Default.CornerRadius,
            Opacity = IsInRange(candidate.Opacity, MinimumOpacity, 1)
                ? candidate.Opacity
                : SettingsSnapshot.Default.Opacity,
            Providers = ValidateProviders(candidate.Providers),
        };
    }

    private static bool IsInRange(double value, double minimum, double maximum) =>
        double.IsFinite(value) && value >= minimum && value <= maximum;

    private static ImmutableArray<ProviderPreference> ValidateProviders(
        ImmutableArray<ProviderPreference> providers)
    {
        if (providers.IsDefaultOrEmpty)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var valid = ImmutableArray.CreateBuilder<ProviderPreference>();
        foreach (var provider in providers)
        {
            if (provider is null ||
                string.IsNullOrWhiteSpace(provider.ProviderId) ||
                provider.ProviderId.Length > MaximumProviderIdLength ||
                !seen.Add(provider.ProviderId))
            {
                continue;
            }

            valid.Add(provider);
        }

        return valid.ToImmutable();
    }
}
