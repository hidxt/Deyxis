# Phase 03 Delivery — Windows GSMTC Media Provider

## Goal and completed work

Phase 3 adds a documented Windows Global System Media Transport Controls (GSMTC) integration behind an injectable platform facade. The active session is normalized into the existing Activity event pipeline, including playback state, metadata, source identity, timeline progress, and advertised control eligibility.

`MediaProvider` now exposes play/pause, next, and previous operations. It invokes the platform only when the last current-session snapshot advertises the requested control. Unsupported controls return `false` without platform invocation. Platform `false` results and exceptions are contained and set provider health to `Failed`; a later successful refresh restores `Running` health.

The app starts GSMTC asynchronously alongside the existing `MockActivityProvider`. Window closure cancels pending startup and disposes the media provider and GSMTC subscriptions without changing the mock provider's initial publication or promotion flow.

## Build and test evidence

Environment: Microsoft Windows 10 Pro 10.0.19045, AMD64, .NET SDK 10.0.302. The project continues to target Windows x64 and the Windows 10.0.19041 API baseline.

- `dotnet test Deyxis.slnx -c Release -p:Platform=x64 --no-restore`: passed; Core 25, Providers 6, and UI 3 tests (34 total), with 0 failures and 0 skipped.
- `dotnet build Deyxis.slnx -c Release -p:Platform=x64 --no-restore`: passed with 0 warnings and 0 errors.
- Provider controls are covered by red-first tests for disabled-next suppression, one-time enabled play/pause delegation, false-result health handling, and exception containment.

## Runtime validation

- A direct GSMTC manager request completed successfully. It reported `SESSION_COUNT=0` and `CURRENT_SESSION=NONE` in this environment.
- Because no media session was available, no real playback control was eligible or invoked. Actual-session discovery, metadata, and control behavior remain unverified here and are not claimed.
- A bounded direct launch of the Release x64 executable returned exit code 0 after approximately 4.3 seconds. This non-interactive environment did not provide sustained visual confirmation of the island window.

Windows 11 x64 runtime confirmation remains pending because this validation host is Windows 10 22H2 and had no active GSMTC session.

## Security and limitations

The provider uses only documented `Windows.Media.Control` APIs. It adds no player-specific reverse engineering, network access, IPC, DLL loading, polling, UI automation, persistence, or background service. It observes one selected session, replaces its event subscriptions when selection changes, and removes them during shutdown.

Media control methods are implemented at the provider boundary but are not yet connected to new UI buttons in this phase. Provider health remains the existing coarse `Stopped`/`Running`/`Failed` contract, so it does not retain detailed diagnostic text.
