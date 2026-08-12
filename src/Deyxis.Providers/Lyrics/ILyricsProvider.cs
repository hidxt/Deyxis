namespace Deyxis.Providers.Lyrics;

public interface ILyricsProvider
{
    Task<LyricsSnapshot> GetSnapshotAsync(
        string title,
        string artist,
        TimeSpan position,
        CancellationToken cancellationToken = default);
}
