using Deyxis.Providers.Lyrics;
using System.Text;
using Xunit;

namespace Deyxis.Providers.Tests;

public sealed class LocalLrcLyricsProviderTests
{
    [Fact]
    public async Task Traversal_components_cannot_read_a_file_outside_the_configured_root()
    {
        using var fixture = new TemporaryLyricsRoot();
        var outsidePath = Path.Combine(fixture.ParentPath, "escaped - Artist.lrc");
        await File.WriteAllTextAsync(outsidePath, "[00:01.00]Outside secret");
        var provider = new LocalLrcLyricsProvider(fixture.RootPath);

        var snapshot = await provider.GetSnapshotAsync("..\\escaped", "Artist", TimeSpan.FromSeconds(1));

        Assert.Equal(LyricsSnapshot.Empty, snapshot);
    }

    [Fact]
    public async Task Valid_utf16_file_returns_the_lines_surrounding_the_playback_position()
    {
        using var fixture = new TemporaryLyricsRoot();
        await fixture.WriteAsync(
            "Clair de Lune - Claude Debussy.lrc",
            "[00:01.00]First\n[00:03.00]Current\n[00:05.00]Next",
            Encoding.Unicode);
        var provider = new LocalLrcLyricsProvider(fixture.RootPath);

        var snapshot = await provider.GetSnapshotAsync(
            "Clair de Lune",
            "Claude Debussy",
            TimeSpan.FromSeconds(3));

        Assert.Equal(new LyricsSnapshot("First", "Current", "Next"), snapshot);
    }

    [Fact]
    public async Task Missing_lrc_file_returns_an_empty_snapshot()
    {
        using var fixture = new TemporaryLyricsRoot();
        var provider = new LocalLrcLyricsProvider(fixture.RootPath);

        var snapshot = await provider.GetSnapshotAsync("Missing", "Artist", TimeSpan.FromSeconds(3));

        Assert.Equal(LyricsSnapshot.Empty, snapshot);
    }

    [Fact]
    public async Task Oversized_lrc_file_is_rejected_without_parsing_its_valid_prefix()
    {
        using var fixture = new TemporaryLyricsRoot();
        var content = "[00:01.00]Must not be read\n" + new string('x', 1_000_000);
        await fixture.WriteAsync("Large - Artist.lrc", content, Encoding.UTF8);
        var provider = new LocalLrcLyricsProvider(fixture.RootPath);

        var snapshot = await provider.GetSnapshotAsync("Large", "Artist", TimeSpan.FromSeconds(1));

        Assert.Equal(LyricsSnapshot.Empty, snapshot);
    }

    [Fact]
    public async Task Lrc_file_with_excessive_lines_is_rejected()
    {
        using var fixture = new TemporaryLyricsRoot();
        var content = "[00:01.00]Must not be read\n" + string.Concat(Enumerable.Repeat("\n", 10_001));
        await fixture.WriteAsync("Many lines - Artist.lrc", content, Encoding.UTF8);
        var provider = new LocalLrcLyricsProvider(fixture.RootPath);

        var snapshot = await provider.GetSnapshotAsync("Many lines", "Artist", TimeSpan.FromSeconds(1));

        Assert.Equal(LyricsSnapshot.Empty, snapshot);
    }

    [Fact]
    public async Task Reparse_point_file_cannot_escape_the_configured_root()
    {
        using var fixture = new TemporaryLyricsRoot();
        var outsidePath = Path.Combine(fixture.ParentPath, "outside.lrc");
        await File.WriteAllTextAsync(outsidePath, "[00:01.00]Outside secret");
        var linkPath = Path.Combine(fixture.RootPath, "Linked - Artist.lrc");
        try
        {
            File.CreateSymbolicLink(linkPath, outsidePath);
        }
        catch (IOException) when (OperatingSystem.IsWindows())
        {
            return;
        }
        var provider = new LocalLrcLyricsProvider(fixture.RootPath);

        var snapshot = await provider.GetSnapshotAsync("Linked", "Artist", TimeSpan.FromSeconds(1));

        Assert.Equal(LyricsSnapshot.Empty, snapshot);
    }

    [Fact]
    public async Task Invalid_utf8_bytes_are_decoded_with_replacement_fallback()
    {
        using var fixture = new TemporaryLyricsRoot();
        await File.WriteAllBytesAsync(
            Path.Combine(fixture.RootPath, "Damaged - Artist.lrc"),
            [.. Encoding.UTF8.GetBytes("[00:01.00]Before"), 0xFF, .. Encoding.UTF8.GetBytes("After")]);
        var provider = new LocalLrcLyricsProvider(fixture.RootPath);

        var snapshot = await provider.GetSnapshotAsync("Damaged", "Artist", TimeSpan.FromSeconds(1));

        Assert.Equal("Before\uFFFDAfter", snapshot.CurrentLine);
    }

    [Fact]
    public async Task A_non_lrc_file_is_never_selected_for_matching_metadata()
    {
        using var fixture = new TemporaryLyricsRoot();
        await fixture.WriteAsync("Other - Artist.txt", "[00:01.00]Wrong extension", Encoding.UTF8);
        var provider = new LocalLrcLyricsProvider(fixture.RootPath);

        var snapshot = await provider.GetSnapshotAsync("Other", "Artist", TimeSpan.FromSeconds(1));

        Assert.Equal(LyricsSnapshot.Empty, snapshot);
    }

    private sealed class TemporaryLyricsRoot : IDisposable
    {
        public TemporaryLyricsRoot()
        {
            ParentPath = Path.Combine(Path.GetTempPath(), $"deyxis-lyrics-{Guid.NewGuid():N}");
            RootPath = Path.Combine(ParentPath, "root");
            Directory.CreateDirectory(RootPath);
        }

        public string ParentPath { get; }

        public string RootPath { get; }

        public Task WriteAsync(string fileName, string content, Encoding encoding) =>
            File.WriteAllTextAsync(Path.Combine(RootPath, fileName), content, encoding);

        public void Dispose() => Directory.Delete(ParentPath, recursive: true);
    }
}
