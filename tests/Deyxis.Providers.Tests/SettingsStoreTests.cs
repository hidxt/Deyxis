using System.Collections.Immutable;
using Deyxis.Core.Settings;
using Deyxis.Platform.Windows.Storage;
using Xunit;

namespace Deyxis.Providers.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public async Task Load_returns_defaults_when_settings_json_is_corrupt()
    {
        using var fixture = new StorageFixture();
        await File.WriteAllTextAsync(fixture.PathFor("settings.json"), "{not-json");
        var store = new SettingsStore(fixture.Root);

        var settings = await store.LoadAsync();

        Assert.Equal(SettingsSnapshot.Default, settings);
    }

    [Fact]
    public async Task Load_returns_defaults_when_settings_file_exceeds_the_read_limit()
    {
        using var fixture = new StorageFixture();
        await File.WriteAllBytesAsync(
            fixture.PathFor("settings.json"),
            new byte[SettingsStore.MaximumFileSizeBytes + 1]);
        var store = new SettingsStore(fixture.Root);

        var settings = await store.LoadAsync();

        Assert.Equal(SettingsSnapshot.Default, settings);
    }

    [Fact]
    public async Task Load_returns_defaults_for_a_partial_versioned_document()
    {
        using var fixture = new StorageFixture();
        await File.WriteAllTextAsync(
            fixture.PathFor("settings.json"),
            """{"version":1,"settings":{"followActiveMonitor":false}}""");
        var store = new SettingsStore(fixture.Root);

        var settings = await store.LoadAsync();

        Assert.Equal(SettingsSnapshot.Default, settings);
    }

    [Fact]
    public async Task Save_then_load_round_trips_a_validated_settings_snapshot()
    {
        using var fixture = new StorageFixture();
        var store = new SettingsStore(fixture.Root);
        var expected = SettingsSnapshot.Default with
        {
            FollowActiveMonitor = false,
            SurfaceMode = IslandSurfaceMode.Acrylic,
            IslandWidth = 512,
            CornerRadius = 18,
            Opacity = 0.85,
            DoNotDisturb = true,
            Providers = ImmutableArray.Create(new ProviderPreference("media", false)),
        };

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.Equal(expected with { Providers = [] }, actual with { Providers = [] });
        var provider = Assert.Single(actual.Providers);
        Assert.Equal("media", provider.ProviderId);
        Assert.False(provider.IsEnabled);
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "*.tmp"));
    }

    [Fact]
    public async Task Rejected_oversized_save_preserves_the_previous_complete_document()
    {
        using var fixture = new StorageFixture();
        var store = new SettingsStore(fixture.Root);
        var expected = SettingsSnapshot.Default with { DoNotDisturb = true };
        await store.SaveAsync(expected);
        var manyProviders = Enumerable.Range(0, 2_000)
            .Select(index => new ProviderPreference($"provider-{index:D4}", true))
            .ToImmutableArray();

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await store.SaveAsync(SettingsSnapshot.Default with { Providers = manyProviders }));

        Assert.Equal(expected, await store.LoadAsync());
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "*.tmp"));
    }

    [Fact]
    public async Task Failed_replacement_preserves_the_previous_complete_document_and_removes_partial_file()
    {
        using var fixture = new StorageFixture();
        var store = new SettingsStore(fixture.Root);
        var expected = SettingsSnapshot.Default with { DoNotDisturb = true };
        await store.SaveAsync(expected);

        await using (var locked = new FileStream(
            fixture.PathFor("settings.json"),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read))
        {
            var exception = await Record.ExceptionAsync(async () =>
                await store.SaveAsync(SettingsSnapshot.Default with { IslandWidth = 600 }));
            Assert.True(exception is IOException or UnauthorizedAccessException);
        }

        Assert.Equal(expected, await store.LoadAsync());
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "*.tmp"));
    }

    [Fact]
    public async Task Explicit_null_schema_member_returns_defaults()
    {
        using var fixture = new StorageFixture();
        await File.WriteAllTextAsync(
            fixture.PathFor("settings.json"),
            """{"version":1,"settings":null}""");
        var store = new SettingsStore(fixture.Root);

        Assert.Equal(SettingsSnapshot.Default, await store.LoadAsync());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    public async Task Invalid_provider_schema_returns_defaults(string providersJson)
    {
        using var fixture = new StorageFixture();
        await File.WriteAllTextAsync(
            fixture.PathFor("settings.json"),
            "{\"version\":1,\"settings\":{\"followActiveMonitor\":true," +
            "\"surfaceMode\":0,\"islandWidth\":420,\"cornerRadius\":22," +
            "\"opacity\":1,\"expandOnHover\":true,\"hideInFullscreen\":true," +
            "\"doNotDisturb\":false,\"showProviderHealth\":true," +
            $"\"providers\":{providersJson}}}}}");
        var store = new SettingsStore(fixture.Root);

        Assert.Equal(SettingsSnapshot.Default, await store.LoadAsync());
    }

    [Fact]
    public void Storage_path_rejects_escape_from_the_configured_root()
    {
        using var fixture = new StorageFixture();

        var exception = Assert.Throws<ArgumentException>(() =>
            new LocalJsonFile(fixture.Root, Path.Combine("..", "outside.json"), 1024));

        Assert.Contains("inside", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StorageFixture : IDisposable
    {
        public StorageFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "Deyxis.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string PathFor(string fileName) => Path.Combine(Root, fileName);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
