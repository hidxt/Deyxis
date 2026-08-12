using Deyxis.Platform.Windows.Wallpaper;
using Xunit;

namespace Deyxis.Providers.Tests;

public sealed class WindowsCurrentUserWallpaperTests
{
    [Fact]
    public async Task Setting_wallpaper_uses_current_user_persistence_and_change_notification_flags()
    {
        var native = new RecordingSystemParametersInfo();
        var wallpaper = new WindowsCurrentUserWallpaper(native);

        var result = await wallpaper.TrySetAsync(@"C:\images\photo.png");

        Assert.True(result);
        Assert.Equal(0x0014u, native.Action);
        Assert.Equal(0u, native.Parameter);
        Assert.Equal(@"C:\images\photo.png", native.Value);
        Assert.Equal(0x0001u | 0x0002u, native.Flags);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_failure_is_returned_without_escaping_the_wallpaper_boundary(bool throwException)
    {
        var native = new RecordingSystemParametersInfo
        {
            Result = false,
            Exception = throwException ? new InvalidOperationException("native failure") : null,
        };
        var wallpaper = new WindowsCurrentUserWallpaper(native);

        var result = await wallpaper.TrySetAsync(@"C:\images\photo.png");

        Assert.False(result);
    }

    private sealed class RecordingSystemParametersInfo : ISystemParametersInfo
    {
        public bool Result { get; init; } = true;

        public Exception? Exception { get; init; }

        public uint Action { get; private set; }

        public uint Parameter { get; private set; }

        public string? Value { get; private set; }

        public uint Flags { get; private set; }

        public bool Invoke(uint action, uint parameter, string value, uint flags)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            Action = action;
            Parameter = parameter;
            Value = value;
            Flags = flags;
            return Result;
        }
    }
}
