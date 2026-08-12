# Phase 07 Delivery - Monitor-aware Hidden Edge

## Goal and completion

Phase 7 places the single Island on the monitor relevant to the foreground window and moves it to a small top-center `HiddenEdge` reveal strip when a genuine foreground fullscreen window covers that monitor. Leaving fullscreen, or entering the reveal strip, restores the presentation state and logical size that preceded `HiddenEdge`.

## Architecture and behavior

- `MonitorManager` selects the foreground monitor, then the Island monitor, then the primary/first available monitor.
- `FullscreenDetector` requires a visible, non-minimized, non-cloaked foreground window to cover the selected monitor within a DPI-scaled two-logical-pixel tolerance.
- `IslandPlacementController` converts logical sizes and top offsets at the selected monitor DPI, remembers the pre-hidden presentation, and produces deterministic bounds/state decisions.
- `WindowsMonitorForegroundFacade` translates Win32 monitor/foreground data and raises display-setting and foreground-window events. Registrations are disposed with the facade; there is no polling, process-name matching, injection, capture bypass, or game/player hook.
- `IslandWindowPlacementCoordinator` marshals those events through the WinUI dispatcher before reading or applying window state. `IslandWindow` applies bounds with `AppWindow.MoveAndResize`; a dedicated pointer-enter reveal strip requests restoration. Closing the window detaches the source, reveal, and view listeners and disposes native registrations.

## Build and test evidence

Environment: Windows 10.0.19045, .NET SDK 10.0.302; Release x64 configuration.

- The App integration test was written first and failed with CS0246 because the placement coordinator/host/dispatcher contracts did not exist. The focused App suite then passed 2/2, covering foreground-driven `HiddenEdge`, dispatcher-delayed reveal/restoration, and listener disposal.
- `dotnet test Deyxis.slnx -c Release -p:Platform=x64 --no-restore`: passed 116/116 (Core 37, Providers 68, UI 9, App 2), with zero failed or skipped.
- `dotnet build Deyxis.slnx -c Release -p:Platform=x64 --no-restore`: succeeded with 0 warnings and 0 errors.
- A bounded five-second launch of the Release executable stayed running and responsive with a non-zero main-window handle; the launched process was then stopped.
- WMI reported one active monitor record. No display-change event was induced.

## Observation limitations and safety boundaries

No fullscreen application was opened or controlled during verification, so the actual desktop transition into `HiddenEdge`, pointer restoration, and multi-monitor movement remain subject to interactive observation. The environment exposed only one active monitor record, so foreground-to-secondary-monitor behavior was verified by deterministic tests rather than live movement. No input injection, process-name rule, screen capture, high-frequency polling, commit, or push was performed.
