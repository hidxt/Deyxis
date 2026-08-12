namespace Deyxis.Providers.Lyrics;

using System.Text;

public sealed class LocalLrcLyricsProvider : ILyricsProvider
{
    private const long MaximumFileSize = 1_000_000;
    private const int MaximumLineCount = 10_000;
    private readonly string rootPath;
    private readonly string rootPathPrefix;

    public LocalLrcLyricsProvider(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        this.rootPath = Path.GetFullPath(rootPath);
        rootPathPrefix = Path.EndsInDirectorySeparator(this.rootPath)
            ? this.rootPath
            : this.rootPath + Path.DirectorySeparatorChar;
    }

    public async Task<LyricsSnapshot> GetSnapshotAsync(
        string title,
        string artist,
        TimeSpan position,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildFileName(title, artist, out var fileName))
        {
            return LyricsSnapshot.Empty;
        }

        try
        {
            var path = Path.GetFullPath(Path.Combine(rootPath, fileName));
            if (!path.StartsWith(rootPathPrefix, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(path), ".lrc", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(path))
            {
                return LyricsSnapshot.Empty;
            }

            var fileInfo = new FileInfo(path);
            if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0 ||
                fileInfo.Length > MaximumFileSize)
            {
                return LyricsSnapshot.Empty;
            }

            var content = await ReadBoundedTextAsync(path, cancellationToken).ConfigureAwait(false);
            if (content is null)
            {
                return LyricsSnapshot.Empty;
            }

            if (CountLines(content) > MaximumLineCount)
            {
                return LyricsSnapshot.Empty;
            }

            var lines = LrcParser.Parse(content).Lines;
            var currentIndex = -1;
            for (var index = 0; index < lines.Count && lines[index].Timestamp <= position; index++)
            {
                currentIndex = index;
            }

            if (currentIndex < 0)
            {
                return LyricsSnapshot.Empty;
            }

            return new LyricsSnapshot(
                currentIndex > 0 ? lines[currentIndex - 1].Text : null,
                lines[currentIndex].Text,
                currentIndex + 1 < lines.Count ? lines[currentIndex + 1].Text : null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return LyricsSnapshot.Empty;
        }
    }

    private static bool TryBuildFileName(string title, string artist, out string fileName)
    {
        fileName = string.Empty;
        if (!IsSafeComponent(title) || !IsSafeComponent(artist))
        {
            return false;
        }

        fileName = $"{title.Trim()} - {artist.Trim()}.lrc";
        return string.Equals(Path.GetExtension(fileName), ".lrc", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeComponent(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Contains("..", StringComparison.Ordinal) &&
        value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !value.Contains(Path.DirectorySeparatorChar) &&
        !value.Contains(Path.AltDirectorySeparatorChar);

    private static int CountLines(string content)
    {
        var count = content.Length == 0 ? 0 : 1;
        foreach (var character in content)
        {
            if (character == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private static async Task<string?> ReadBoundedTextAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > MaximumFileSize)
        {
            return null;
        }

        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return Decode(bytes);
    }

    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
            ? 3
            : 0;
        return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
    }
}
