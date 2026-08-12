using System.Runtime.InteropServices;

namespace Deyxis.Platform.Windows.Wallpaper;

internal sealed class User32SystemParametersInfo : ISystemParametersInfo
{
    public static User32SystemParametersInfo Instance { get; } = new();

    private User32SystemParametersInfo()
    {
    }

    public bool Invoke(uint action, uint parameter, string value, uint flags) =>
        SystemParametersInfo(action, parameter, value, flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        string pvParam,
        uint fWinIni);
}
