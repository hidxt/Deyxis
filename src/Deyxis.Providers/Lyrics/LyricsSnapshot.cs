namespace Deyxis.Providers.Lyrics;

public sealed record LyricsSnapshot(
    string? PreviousLine,
    string? CurrentLine,
    string? NextLine)
{
    public static LyricsSnapshot Empty { get; } = new(null, null, null);
}
