namespace Deyxis.Providers.Media;

public interface IMediaSessionPlatform
{
    event EventHandler? CurrentSessionChanged;

    Task<MediaSessionSnapshot?> GetCurrentSessionAsync(CancellationToken cancellationToken = default);

    Task<bool> TogglePlayPauseAsync(CancellationToken cancellationToken = default);

    Task<bool> SkipNextAsync(CancellationToken cancellationToken = default);

    Task<bool> SkipPreviousAsync(CancellationToken cancellationToken = default);
}
