using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Deyxis.Platform.Windows.Placement;

internal sealed partial class User32WindowsDisplayNative : IWindowsDisplayNative
{
    private const uint MonitorInfoPrimary = 0x00000001;
    private const uint DefaultDpi = 96;
    private const uint EffectiveDpi = 0;
    private const uint EventSystemForeground = 0x0003;
    private const uint WinEventOutOfContext = 0x0000;
    private const uint DwmWindowAttributeCloaked = 14;

    public static User32WindowsDisplayNative Instance { get; } = new();

    private User32WindowsDisplayNative()
    {
    }

    public bool TryGetMonitors(out IReadOnlyList<NativeMonitorSnapshot> monitors)
    {
        var collected = new List<NativeMonitorSnapshot>();
        try
        {
            var succeeded = EnumDisplayMonitors(0, 0, (monitor, _, _, _) =>
            {
                var info = new MonitorInfo
                {
                    Size = (uint)Marshal.SizeOf<MonitorInfo>(),
                };
                if (!GetMonitorInfo(monitor, ref info))
                {
                    return false;
                }

                var dpi = DefaultDpi;
                if (GetDpiForMonitor(monitor, EffectiveDpi, out var dpiX, out _) == 0)
                {
                    dpi = dpiX;
                }

                collected.Add(new NativeMonitorSnapshot(
                    monitor,
                    ToNativeRect(info.Monitor),
                    ToNativeRect(info.WorkArea),
                    dpi,
                    (info.Flags & MonitorInfoPrimary) != 0));
                return true;
            }, 0);

            monitors = succeeded ? collected : [];
            return succeeded;
        }
        catch (Exception exception) when (!IsFatalException(exception))
        {
            monitors = [];
            return false;
        }
    }

    public bool TryGetForegroundWindow(out NativeForegroundWindowSnapshot foreground)
    {
        try
        {
            var window = GetForegroundWindow();
            if (window == 0 || !GetWindowRect(window, out var bounds))
            {
                foreground = default;
                return false;
            }

            var cloakedValue = 0;
            var cloaked = DwmGetWindowAttribute(
                window,
                DwmWindowAttributeCloaked,
                out cloakedValue,
                (uint)Marshal.SizeOf<int>()) == 0 && cloakedValue != 0;

            foreground = new NativeForegroundWindowSnapshot(
                ToNativeRect(bounds),
                IsWindowVisible(window),
                IsIconic(window),
                cloaked);
            return true;
        }
        catch (Exception exception) when (!IsFatalException(exception))
        {
            foreground = default;
            return false;
        }
    }

    public IDisposable RegisterDisplayChanged(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        EventHandler handler = (_, _) => callback();
        SystemEvents.DisplaySettingsChanged += handler;
        return new CallbackRegistration(() => SystemEvents.DisplaySettingsChanged -= handler);
    }

    public IDisposable RegisterForegroundChanged(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        WinEventDelegate handler = (_, eventType, _, _, _, _, _) =>
        {
            if (eventType == EventSystemForeground)
            {
                callback();
            }
        };
        var hook = SetWinEventHook(
            EventSystemForeground,
            EventSystemForeground,
            0,
            handler,
            0,
            0,
            WinEventOutOfContext);
        if (hook == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return new CallbackRegistration(() => UnhookWinEvent(hook), handler);
    }

    private static NativeRect ToNativeRect(Rect rectangle) =>
        new(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);

    private static bool IsFatalException(Exception exception) =>
        exception is OutOfMemoryException or StackOverflowException or AccessViolationException;

    private sealed class CallbackRegistration : IDisposable
    {
        private Action? unregister;
        private readonly Delegate? rootedDelegate;

        public CallbackRegistration(Action unregister, Delegate? rootedDelegate = null)
        {
            this.unregister = unregister;
            this.rootedDelegate = rootedDelegate;
        }

        public void Dispose()
        {
            GC.KeepAlive(rootedDelegate);
            Interlocked.Exchange(ref unregister, null)?.Invoke();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
    }

    private delegate bool MonitorEnumDelegate(nint monitor, nint deviceContext, nint bounds, nint data);

    private delegate void WinEventDelegate(
        nint hook,
        uint eventType,
        nint window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumDisplayMonitors(
        nint deviceContext,
        nint clipRectangle,
        MonitorEnumDelegate callback,
        nint data);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo monitorInfo);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(
        nint monitor,
        uint dpiType,
        out uint dpiX,
        out uint dpiY);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint window, out Rect rectangle);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(nint window);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(
        nint window,
        uint attribute,
        out int value,
        uint valueSize);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint SetWinEventHook(
        uint eventMinimum,
        uint eventMaximum,
        nint eventHookModule,
        WinEventDelegate callback,
        uint processId,
        uint threadId,
        uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWinEvent(nint hook);
}
