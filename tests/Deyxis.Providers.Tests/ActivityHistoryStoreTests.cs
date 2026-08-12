using Deyxis.Core.Activities;
using Deyxis.Core.History;
using Deyxis.Platform.Windows.Storage;
using Xunit;

namespace Deyxis.Providers.Tests;

public sealed class ActivityHistoryStoreTests
{
    [Fact]
    public async Task Save_then_load_persists_only_the_twenty_safe_summary_fields()
    {
        using var fixture = new StorageFixture();
        var store = new ActivityHistoryStore(fixture.Root);
        var summaries = Enumerable.Range(0, 24)
            .Select(index => new ActivityHistorySummary(
                $"provider-{index}",
                ActivityCategory.Agent,
                ActivityState.Completed,
                $"title-{index}",
                DateTimeOffset.Parse($"2026-08-12T00:{index:D2}:00+00:00")))
            .ToArray();

        await store.SaveAsync(summaries);
        var loaded = await store.LoadAsync();

        Assert.Equal(summaries.Take(20), loaded.Entries);
        var json = await File.ReadAllTextAsync(fixture.PathFor("activity-history.json"));
        Assert.DoesNotContain("description", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("metadata", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prompt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("output", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Malformed_or_schema_invalid_history_loads_as_empty()
    {
        using var malformed = new StorageFixture();
        await File.WriteAllTextAsync(malformed.PathFor("activity-history.json"), "[");
        var malformedStore = new ActivityHistoryStore(malformed.Root);

        Assert.Empty((await malformedStore.LoadAsync()).Entries);

        using var invalid = new StorageFixture();
        await File.WriteAllTextAsync(
            invalid.PathFor("activity-history.json"),
            """{"version":1,"entries":[{"providerId":"","category":99,"state":2,"title":"x","timestamp":"2026-08-12T00:00:00+00:00"}]}""");
        var invalidStore = new ActivityHistoryStore(invalid.Root);

        Assert.Empty((await invalidStore.LoadAsync()).Entries);
    }

    [Fact]
    public async Task Explicit_null_history_entry_loads_as_empty()
    {
        using var fixture = new StorageFixture();
        await File.WriteAllTextAsync(
            fixture.PathFor("activity-history.json"),
            """{"version":1,"entries":[null]}""");
        var store = new ActivityHistoryStore(fixture.Root);

        Assert.Empty((await store.LoadAsync()).Entries);
    }

    [Fact]
    public async Task Clear_removes_only_the_fixed_history_file_inside_the_root()
    {
        using var fixture = new StorageFixture();
        var settingsPath = fixture.PathFor("settings.json");
        await File.WriteAllTextAsync(settingsPath, "keep");
        var store = new ActivityHistoryStore(fixture.Root);
        await store.SaveAsync([
            new("provider", ActivityCategory.Media, ActivityState.Running, "Playing", DateTimeOffset.UtcNow),
        ]);

        await store.ClearAsync();

        Assert.False(File.Exists(fixture.PathFor("activity-history.json")));
        Assert.Equal("keep", await File.ReadAllTextAsync(settingsPath));
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
