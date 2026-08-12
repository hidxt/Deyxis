using Deyxis.Core.Placement;
using Deyxis.Platform.Windows.Placement;
using Xunit;

namespace Deyxis.Providers.Tests;

public sealed class WindowsMonitorForegroundFacadeTests
{
    [Fact]
    public void Queries_convert_native_monitor_and_foreground_snapshots_into_core_models()
    {
        var native = new RecordingWindowsDisplayNative
        {
            Monitors =
            [
                new NativeMonitorSnapshot(
                    new nint(0x42),
                    new NativeRect(-1920, 0, 0, 1080),
                    new NativeRect(-1920, 0, 0, 1040),
                    144,
                    true),
            ],
            Foreground = new NativeForegroundWindowSnapshot(
                new NativeRect(-1900, 20, -100, 1060),
                IsVisible: true,
                IsMinimized: false,
                IsCloaked: true),
        };
        using var facade = new WindowsMonitorForegroundFacade(native);

        var monitor = Assert.Single(facade.GetMonitors());
        var foreground = facade.GetForegroundWindow();

        Assert.Equal("0x42", monitor.Id);
        Assert.Equal(new PixelRect(-1920, 0, 1920, 1080), monitor.Bounds);
        Assert.Equal(new PixelRect(-1920, 0, 1920, 1040), monitor.WorkArea);
        Assert.Equal(144u, monitor.Dpi);
        Assert.True(monitor.IsPrimary);
        Assert.Equal(
            new ForegroundWindowSnapshot(
                new PixelRect(-1900, 20, 1800, 1040),
                IsVisible: true,
                IsMinimized: false,
                IsCloaked: true),
            foreground);
    }

    [Fact]
    public void Failed_native_queries_return_empty_monitor_list_and_no_foreground_window()
    {
        var native = new RecordingWindowsDisplayNative
        {
            MonitorQuerySucceeds = false,
            ForegroundQuerySucceeds = false,
        };
        using var facade = new WindowsMonitorForegroundFacade(native);

        Assert.Empty(facade.GetMonitors());
        Assert.Null(facade.GetForegroundWindow());
    }

    [Fact]
    public void Native_display_and_foreground_changes_raise_facade_events()
    {
        var native = new RecordingWindowsDisplayNative();
        using var facade = new WindowsMonitorForegroundFacade(native);
        var monitorChanges = 0;
        var foregroundChanges = 0;
        facade.MonitorsChanged += (_, _) => monitorChanges++;
        facade.ForegroundWindowChanged += (_, _) => foregroundChanges++;

        native.RaiseDisplayChanged();
        native.RaiseForegroundChanged();

        Assert.Equal(1, monitorChanges);
        Assert.Equal(1, foregroundChanges);
    }

    [Fact]
    public void Dispose_unregisters_display_and_foreground_change_callbacks()
    {
        var native = new RecordingWindowsDisplayNative();
        var facade = new WindowsMonitorForegroundFacade(native);
        var monitorChanges = 0;
        var foregroundChanges = 0;
        facade.MonitorsChanged += (_, _) => monitorChanges++;
        facade.ForegroundWindowChanged += (_, _) => foregroundChanges++;

        facade.Dispose();
        native.RaiseDisplayChanged();
        native.RaiseForegroundChanged();

        Assert.True(native.DisplayRegistration.IsDisposed);
        Assert.True(native.ForegroundRegistration.IsDisposed);
        Assert.Equal(0, monitorChanges);
        Assert.Equal(0, foregroundChanges);
    }

    [Fact]
    public void Foreground_registration_failure_unregisters_the_display_callback()
    {
        var native = new RecordingWindowsDisplayNative
        {
            ForegroundRegistrationException = new InvalidOperationException("hook unavailable"),
        };

        Assert.Throws<InvalidOperationException>(() => new WindowsMonitorForegroundFacade(native));

        Assert.True(native.DisplayRegistration.IsDisposed);
    }

    private sealed class RecordingWindowsDisplayNative : IWindowsDisplayNative
    {
        private Action? displayChanged;
        private Action? foregroundChanged;

        public bool MonitorQuerySucceeds { get; init; } = true;

        public bool ForegroundQuerySucceeds { get; init; } = true;

        public IReadOnlyList<NativeMonitorSnapshot> Monitors { get; init; } = [];

        public NativeForegroundWindowSnapshot Foreground { get; init; }

        public Exception? ForegroundRegistrationException { get; init; }

        public RecordingRegistration DisplayRegistration { get; } = new();

        public RecordingRegistration ForegroundRegistration { get; } = new();

        public bool TryGetMonitors(out IReadOnlyList<NativeMonitorSnapshot> monitors)
        {
            monitors = Monitors;
            return MonitorQuerySucceeds;
        }

        public bool TryGetForegroundWindow(out NativeForegroundWindowSnapshot foreground)
        {
            foreground = Foreground;
            return ForegroundQuerySucceeds;
        }

        public IDisposable RegisterDisplayChanged(Action callback)
        {
            displayChanged = callback;
            DisplayRegistration.OnDispose = () => displayChanged = null;
            return DisplayRegistration;
        }

        public IDisposable RegisterForegroundChanged(Action callback)
        {
            if (ForegroundRegistrationException is not null)
            {
                throw ForegroundRegistrationException;
            }

            foregroundChanged = callback;
            ForegroundRegistration.OnDispose = () => foregroundChanged = null;
            return ForegroundRegistration;
        }

        public void RaiseDisplayChanged() => displayChanged?.Invoke();

        public void RaiseForegroundChanged() => foregroundChanged?.Invoke();
    }

    private sealed class RecordingRegistration : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public Action? OnDispose { get; set; }

        public void Dispose()
        {
            IsDisposed = true;
            OnDispose?.Invoke();
        }
    }
}
