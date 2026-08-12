using Deyxis.Providers.FileDrop;

namespace Deyxis.Platform.Windows.Wallpaper;

public sealed class WindowsCurrentUserWallpaper : ICurrentUserWallpaper
{
    private const uint SetDesktopWallpaper = 0x0014;
    private const uint UpdateUserProfile = 0x0001;
    private const uint SendSettingChange = 0x0002;

    private readonly ISystemParametersInfo systemParametersInfo;

    public WindowsCurrentUserWallpaper()
        : this(User32SystemParametersInfo.Instance)
    {
    }

    internal WindowsCurrentUserWallpaper(ISystemParametersInfo systemParametersInfo)
    {
        this.systemParametersInfo = systemParametersInfo ??
            throw new ArgumentNullException(nameof(systemParametersInfo));
    }

    public ValueTask<bool> TrySetAsync(
        string canonicalPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPath);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return ValueTask.FromResult(systemParametersInfo.Invoke(
                SetDesktopWallpaper,
                parameter: 0,
                canonicalPath,
                UpdateUserProfile | SendSettingChange));
        }
        catch (Exception exception) when (!IsFatalException(exception))
        {
            return ValueTask.FromResult(false);
        }
    }

    private static bool IsFatalException(Exception exception) =>
        exception is OutOfMemoryException or StackOverflowException or AccessViolationException;
}
