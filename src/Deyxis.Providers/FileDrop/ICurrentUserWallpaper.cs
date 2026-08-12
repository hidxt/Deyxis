namespace Deyxis.Providers.FileDrop;

public interface ICurrentUserWallpaper
{
    ValueTask<bool> TrySetAsync(
        string canonicalPath,
        CancellationToken cancellationToken = default);
}
