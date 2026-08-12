using System.Collections.Immutable;
using System.Text.Json;
using Deyxis.Core.Settings;

namespace Deyxis.Platform.Windows.Storage;

public sealed class SettingsStore
{
    public const int MaximumFileSizeBytes = 64 * 1024;

    private const int CurrentVersion = 1;
    private const string FileName = "settings.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly LocalJsonFile file;

    public SettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Deyxis"))
    {
    }

    public SettingsStore(string appDataRoot)
    {
        file = new LocalJsonFile(appDataRoot, FileName, MaximumFileSizeBytes);
    }

    public async ValueTask<SettingsSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await file.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (bytes is null)
            {
                return SettingsSnapshot.Default;
            }

            var document = JsonSerializer.Deserialize<SettingsDocument>(bytes, SerializerOptions);
            return document is
                {
                    Version: CurrentVersion,
                    Settings: { Providers: not null } settings,
                } && settings.Providers.All(provider => provider is not null)
                ? SettingsPolicy.Validate(document.Settings.ToSnapshot())
                : SettingsSnapshot.Default;
        }
        catch (Exception exception) when (LocalJsonFile.IsRecoverableReadException(exception))
        {
            return SettingsSnapshot.Default;
        }
    }

    public async ValueTask SaveAsync(
        SettingsSnapshot settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var validated = SettingsPolicy.Validate(settings);
        var document = SettingsDocument.FromSnapshot(validated);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, SerializerOptions);
        await file.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private sealed record SettingsDocument
    {
        public required int Version { get; init; }

        public required SettingsData? Settings { get; init; }

        public static SettingsDocument FromSnapshot(SettingsSnapshot snapshot) => new()
        {
            Version = CurrentVersion,
            Settings = SettingsData.FromSnapshot(snapshot),
        };
    }

    private sealed record SettingsData
    {
        public required bool FollowActiveMonitor { get; init; }

        public required IslandSurfaceMode SurfaceMode { get; init; }

        public required double IslandWidth { get; init; }

        public required double CornerRadius { get; init; }

        public required double Opacity { get; init; }

        public required bool ExpandOnHover { get; init; }

        public required bool HideInFullscreen { get; init; }

        public required bool DoNotDisturb { get; init; }

        public required bool ShowProviderHealth { get; init; }

        public required ProviderData?[]? Providers { get; init; }

        public SettingsSnapshot ToSnapshot() => new(
            FollowActiveMonitor,
            SurfaceMode,
            IslandWidth,
            CornerRadius,
            Opacity,
            ExpandOnHover,
            HideInFullscreen,
            DoNotDisturb,
            ShowProviderHealth,
            Providers!.Select(provider => provider!.ToPreference()).ToImmutableArray());

        public static SettingsData FromSnapshot(SettingsSnapshot snapshot) => new()
        {
            FollowActiveMonitor = snapshot.FollowActiveMonitor,
            SurfaceMode = snapshot.SurfaceMode,
            IslandWidth = snapshot.IslandWidth,
            CornerRadius = snapshot.CornerRadius,
            Opacity = snapshot.Opacity,
            ExpandOnHover = snapshot.ExpandOnHover,
            HideInFullscreen = snapshot.HideInFullscreen,
            DoNotDisturb = snapshot.DoNotDisturb,
            ShowProviderHealth = snapshot.ShowProviderHealth,
            Providers = snapshot.Providers
                .Select(ProviderData.FromPreference)
                .ToArray(),
        };
    }

    private sealed record ProviderData
    {
        public required string ProviderId { get; init; }

        public required bool IsEnabled { get; init; }

        public ProviderPreference ToPreference() => new(ProviderId, IsEnabled);

        public static ProviderData FromPreference(ProviderPreference preference) => new()
        {
            ProviderId = preference.ProviderId,
            IsEnabled = preference.IsEnabled,
        };
    }
}
