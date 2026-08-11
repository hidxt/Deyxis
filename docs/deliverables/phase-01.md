# Phase 01 Delivery — Deyxis Foundation

## Goal

Deliver the first Windows 11 x64 shell for **昼隙 / Deyxis**: a state-driven island window with the Phase 1 mock Activity pipeline and Split Panel presentation.

## Completed work

- Created .NET 10 solution projects for App, Core, UI, PluginSdk, Core tests, and UI tests.
- Added immutable Activity contracts, deterministic priority ordering, activity snapshots/upserts, and `Idle`/`Hover`/`Expanded` state transitions.
- Added fixed Music Playing, Codex Running, Claude Waiting, and OpenCode Completed mock activities. Claude Waiting ranks as the primary activity.
- Added a borderless, topmost, non-activating WinUI island host, compact/hover/expanded views, and a primary/queue Split Panel.
- Added only a contract-only Plugin SDK; no DLL discovery or dynamic loading is implemented.
- Added local placeholder app asset and standard build-output/user-local Git ignores.

## Architecture and important files

`Deyxis.Core` contains the Activity model, `ActivityManager`, `ActivityPriorityPolicy`, and `IslandStateMachine`. `Deyxis.UI` maps a Core snapshot into an `IslandViewModel` and XAML controls. `Deyxis.App` composes the four mocks and hosts the single island window. `Deyxis.PluginSdk` exposes no runtime loading path.

Key additions include `src/Deyxis.Core/Activities/ActivityManager.cs`, `src/Deyxis.Core/Priority/ActivityPriorityPolicy.cs`, `src/Deyxis.Core/Island/IslandStateMachine.cs`, `src/Deyxis.UI/IslandViewModel.cs`, `src/Deyxis.App/IslandWindow.cs`, and `tests/` projects.

## Build and test results

Test environment: Windows 11 x64 desktop, .NET SDK 10.0.302, Release x64.

- `dotnet test Deyxis.slnx -c Release -p:Platform=x64 --no-restore --disable-build-servers`: passed; Core 9/9 and UI 1/1.
- `dotnet build Deyxis.slnx -c Release -p:Platform=x64 --no-restore --disable-build-servers`: passed with 0 warnings and 0 errors.

## Runtime validation and resource observation

A bounded Release x64 launch smoke test started `Deyxis.App.exe`, waited five seconds, and observed a responsive process. At the observation point it used 112.3 MB Working Set and 40 threads; the process was then intentionally stopped. This is a single startup sample, not an idle or animation benchmark.

The available automated GUI helper did not provide reliable bounded interaction verification because it waited for the foreground application to close. Therefore the following remain unverified in a real interactive Windows desktop session: rendered capsule appearance, hover summary, click-to-expand, collapse control, visible four-item Split Panel, focus behavior, and animation smoothness. No CPU/Working Set/threads measurements were taken during animation, multiple dynamic activity changes, or real provider work.

## Issues found and repaired

- Initial build output directories appeared in the working diff. They were removed and root `.gitignore` now excludes `**/bin/` and `**/obj/`.

## Security changes

No external provider, IPC, network request, elevated operation, file drop, or third-party DLL load was introduced. The Plugin SDK is only a future interface boundary and does not scan or load plugins.

## Known limitations and next-phase notes

- Phase 1 uses fixed mocks only; no real media or AI-agent integration exists.
- Runtime interaction/animation acceptance needs a manual Windows 11 desktop pass before claiming the phase is fully accepted.
- A later hardening pass should log/report native window-style failures in `IslandWindow` rather than silently tolerate them.
- The next phase can consume Core snapshots/events to introduce a proper EventBus and mock-provider lifecycle, without allowing providers to touch UI controls.
