namespace Deyxis.Platform.Windows.Wallpaper;

internal interface ISystemParametersInfo
{
    bool Invoke(uint action, uint parameter, string value, uint flags);
}
