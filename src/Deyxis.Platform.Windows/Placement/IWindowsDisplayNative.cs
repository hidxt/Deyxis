namespace Deyxis.Platform.Windows.Placement;

internal interface IWindowsDisplayNative
{
    bool TryGetMonitors(out IReadOnlyList<NativeMonitorSnapshot> monitors);

    bool TryGetForegroundWindow(out NativeForegroundWindowSnapshot foreground);

    IDisposable RegisterDisplayChanged(Action callback);

    IDisposable RegisterForegroundChanged(Action callback);
}

internal readonly record struct NativeRect(int Left, int Top, int Right, int Bottom);

internal sealed record NativeMonitorSnapshot(
    nint Handle,
    NativeRect Bounds,
    NativeRect WorkArea,
    uint Dpi,
    bool IsPrimary);

internal readonly record struct NativeForegroundWindowSnapshot(
    NativeRect Bounds,
    bool IsVisible,
    bool IsMinimized,
    bool IsCloaked);
