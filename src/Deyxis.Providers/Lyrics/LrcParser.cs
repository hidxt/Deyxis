using System.Globalization;
using System.Text.RegularExpressions;

namespace Deyxis.Providers.Lyrics;

public static partial class LrcParser
{
    private const int MaximumInputLength = 1_000_000;

    public static LyricsTimeline Parse(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return new LyricsTimeline(Array.Empty<LyricLine>());
        }

        var boundedContent = content.Length > MaximumInputLength ? content[..MaximumInputLength] : content;
        var parsedLines = new List<(LyricLine Line, int Order)>();
        var order = 0;

        foreach (var sourceLine in boundedContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var matches = TimestampTag().Matches(sourceLine);
            if (matches.Count == 0)
            {
                continue;
            }

            var lastMatch = matches[matches.Count - 1];
            var text = sourceLine[(lastMatch.Index + lastMatch.Length)..];
            foreach (Match match in matches)
            {
                if (TryParseTimestamp(match, out var timestamp))
                {
                    parsedLines.Add((new LyricLine(timestamp, text), order++));
                }
            }
        }

        return new LyricsTimeline(parsedLines
            .OrderBy(entry => entry.Line.Timestamp)
            .ThenBy(entry => entry.Order)
            .Select(entry => entry.Line));
    }

    private static bool TryParseTimestamp(Match match, out TimeSpan timestamp)
    {
        timestamp = default;
        if (!int.TryParse(match.Groups["minutes"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
            !int.TryParse(match.Groups["seconds"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds >= 60)
        {
            return false;
        }

        var fractionalText = match.Groups["fraction"].Value;
        var fraction = fractionalText.Length == 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(double.Parse($"0.{fractionalText}", CultureInfo.InvariantCulture));
        timestamp = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds) + fraction;
        return true;
    }

    [GeneratedRegex(@"\[(?<minutes>\d{1,4}):(?<seconds>\d{2})(?:\.(?<fraction>\d{1,3}))?\]", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampTag();
}
