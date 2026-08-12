# Phase 9 delivery — hardening and release-readiness audit

Date: 2026-08-12
Product: 昼隙 / Deyxis

## Goal

Complete an evidence-backed reliability, security, malicious-code, dependency, and bounded runtime review without expanding the product scope. Repair validated defects with regression tests, record actual measurements, and leave clear release follow-up work.

## Actual completion

- Repaired concurrent Activity-pipeline mutation/notification handling, an EventBus disposal race, and stale GSMTC media activity removal; each repair has a regression test.
- Hardened the file-drop confirmation flow against changed content between validation, preview, and wallpaper application. Accepted files are now bound to a bounded SHA-256 fingerprint, length, type, magic bytes, and safe-path checks, revalidated before preview and again before wallpaper use. The original is neither copied nor moved.
- Completed static reliability/security and malicious-code review, NuGet dependency/advisory review, and a bounded Release x64 idle-process measurement.
- Preserved the existing Provider/PluginSdk boundary; no dynamic DLL loading, provider auto-discovery, or new feature work was introduced.

## Main architecture and code changes

| Area | Change |
| --- | --- |
| Activity flow | `ActivityPipeline` now serializes mutation, snapshot publication, and disposal at one synchronization boundary so concurrent provider publications cannot lose activities or notify from an inconsistent snapshot. |
| Event lifetime | `EventBus` subscriptions use an atomic active-state check immediately before callback invocation, preventing a subscription disposed by an earlier callback in the same publish from running. |
| Media lifecycle | `MediaProvider.Stop()` withdraws its stable activity after event detachment and cancellation. |
| File-drop boundary | `FileDropProvider` stores only opaque confirmation state plus bounded validation evidence; it rejects changed or unsafe input before preview/wallpaper use and emits path-free status. |

## Important files

- `src/Deyxis.Core/Activities/ActivityPipeline.cs`
- `src/Deyxis.Core/Events/EventBus.cs`
- `src/Deyxis.Providers/FileDrop/FileDropProvider.cs`
- `src/Deyxis.Providers/Media/MediaProvider.cs`
- `src/Deyxis.App/IslandWindow.cs`
- `tests/Deyxis.Core.Tests/ActivityPipelineTests.cs`
- `tests/Deyxis.Core.Tests/EventBusTests.cs`
- `tests/Deyxis.Providers.Tests/FileDropProviderTests.cs`
- `tests/Deyxis.Providers.Tests/MediaProviderTests.cs`
- `docs/audits/phase-09-code-security.md`
- `docs/audits/phase-09-dependencies.md`
- `docs/audits/phase-09-performance.md`

## Build and test verification

Final independent verification for this delivery ran:

```powershell
dotnet build Deyxis.slnx -c Release -p:Platform=x64 --no-restore
dotnet test Deyxis.slnx -c Release -p:Platform=x64 --no-build
git diff --check
```

The Release x64 build completed with **0 warnings and 0 errors**. The full xUnit suite completed with **151 passed, 0 failed, 0 skipped**. `git diff --check` reported no whitespace errors. Targeted regression coverage includes the 2,000-concurrent-upsert pipeline case, disposed subscription during publication, media stop activity removal, and changed file-drop content rejection.

## Runtime validation and performance

On Windows 10 Pro 10.0.19045 x64, Intel Xeon E3-1226 v3 (4 logical processors), 15.89 GiB RAM, and one exercised 1920x1080 monitor, a Release x64 app instance was launched, allowed to initialize for 5 seconds, and sampled again after 15 seconds idle. It was responsive at both samples. At 20 seconds it used 119.67 MiB working set, 99.86 MiB private bytes, and 46 threads; CPU usage over the preceding 15 seconds was 0.000%. The initial four mock activities and asynchronous GSMTC startup were present in that run.

This is one bounded observation, not a benchmark. Expand/collapse animation, Activity-update bursts, interactive drag/drop, active GSMTC/agent workloads, Windows 11, and multi-monitor behavior were not measured. The observed roughly 120 MiB/46-thread WinUI/Windows App SDK baseline is recorded rather than characterized as negligible. Full method and environment are in `docs/audits/phase-09-performance.md`.

## Security, malicious-code, and dependency results

The audit found no Critical or High security issue and no malicious-code behavior. Reviewed source/build paths contain no network or downloader/execution flow, shell/encoded-command construction, credential collection, persistence/security-control changes, dynamic assembly/native-library loading, custom build execution, or runtime third-party plugin loading. Native calls use fixed Windows DLLs and fixed entry points. The dormant executable probe has a normalized explicit allowlist, fixed `ArgumentList`, `UseShellExecute = false`, and a bounded timeout.

The direct NuGet vulnerability audit against the official NuGet v3 feed completed without proxy use and reported no vulnerable packages for all ten projects. Direct dependencies are version-pinned. There is no authored lock file, so restore reproducibility and a release SBOM/license review remain recommended. The complete evidence is in `docs/audits/phase-09-code-security.md` and `docs/audits/phase-09-dependencies.md`.

## Problems found and repaired

| Severity | Resolved issue |
| --- | --- |
| Medium reliability | Parallel provider publications could lose an Activity because the activity dictionary was unsynchronized. |
| Low reliability | A callback disposed earlier in an EventBus publish could still execute from the captured snapshot. |
| Low reliability | Stopping the media provider could leave its activity visible. |
| Low security | A same-user pathname time-of-check/time-of-use window could replace a file after drop validation (CWE-367). |

## Known limits and residual risks

- Pending file-drop tokens/rejection activities have no explicit expiry or count limit (low resource-exhaustion risk).
- History clear can race an earlier fire-and-forget save; expected settings/history I/O errors can escape `async void` UI event handlers (low reliability/privacy-intent risks).
- Because the product must not copy or move a dropped original, a narrow same-user race remains between final revalidation and the Windows wallpaper API opening the pathname. Fully eliminating it requires an app-owned immutable copy or handle-based platform API.
- Live agent integrations remain intentionally disabled by default; GSMTC, Windows 11, and multi-monitor behavior need supported-environment validation.

## Security-related changes

The changed-file file-drop repair adds content identity/revalidation without logging paths or accepting untrusted dynamic code. No new network, credential, persistence, elevation, native-loading, or third-party-DLL execution surface was added.

## Run and next-phase notes

Restore/build/test with the commands above, then run `src/Deyxis.App/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/Deyxis.App.exe` on a supported Windows desktop. Before public release, add a bounded UI performance protocol on Windows 11, decide the history-write serialization model, bound transient file-drop state, consider NuGet lock files/SBOM generation, and re-evaluate executable trust before enabling real agent probing.
