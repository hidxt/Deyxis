namespace Deyxis.Providers.Lyrics;

public sealed class LyricsTimeline
{
    private readonly IReadOnlyList<LyricLine> lines;

    public LyricsTimeline(IEnumerable<LyricLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        this.lines = lines.ToArray();
    }

    public IReadOnlyList<LyricLine> Lines => lines;

    public LyricLine? GetLineAt(TimeSpan position)
    {
        var low = 0;
        var high = lines.Count - 1;
        var candidate = -1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (lines[middle].Timestamp <= position)
            {
                candidate = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return candidate >= 0 ? lines[candidate] : null;
    }
}
