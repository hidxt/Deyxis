# Phase 9 measured performance record

**Measurement date:** 2026-08-12
**Build:** `dotnet build Deyxis.slnx -c Release -p:Platform=x64 --no-restore` — succeeded, 0 warnings, 0 errors.

## Environment

| Item | Observed value |
|---|---|
| OS | Windows 10 Pro 10.0.19045, x64 |
| CPU | Intel Xeon E3-1226 v3, 4 cores / 4 logical processors |
| Memory | 15.89 GiB installed |
| GPUs | NVIDIA GeForce GTX 1070, Intel HD Graphics P4600/P4700, GameViewer virtual display adapter |
| Display exercised | one detected 1920×1080 plug-and-play monitor; multi-monitor behavior not exercised |
| App executable | Release x64 `Deyxis.App.exe`, Windows App SDK self-contained configuration |

## Idle-process measurement

The Release executable was launched once with PowerShell `Start-Process`, allowed to initialize for 5 seconds, then sampled again after a 15-second idle interval. The sampler read the live process's `TotalProcessorTime`, `WorkingSet64`, `PrivateMemorySize64`, `Threads.Count`, and `Responding`. CPU percentage is `(CPU time delta / (15 seconds × 4 logical processors)) × 100`. The process was then explicitly terminated by the measurement script; this is not a shutdown-performance measurement.

| Sample | Responding | CPU time | Working set | Private bytes | Threads | CPU over preceding 15 s |
|---|---|---:|---:|---:|---:|---:|
| 5 s after launch | true | 0.828 s | 120.15 MiB | 99.88 MiB | 47 | n/a (warm-up) |
| 20 s after launch | true | 0.828 s | 119.67 MiB | 99.86 MiB | 46 | 0.000% |

This single bounded observation confirms that this idle run was responsive and consumed no measurable CPU time during the 15-second steady interval. It is not a percentile, benchmark, battery measurement, or claim that the WinUI/Windows App SDK baseline cost is negligible; the approximately 120 MiB working set and 46 threads are recorded as observed baseline resource use.

## Activity and provider coverage

The measured app startup actually publishes the four built-in mock activities, so the idle sample includes the application's initial multi-Activity snapshot. The app also attempts GSMTC startup asynchronously; no live media session was intentionally created. Codex, Claude Code, and OpenCode adapters remain disabled by default, so no real agent-provider workload occurred.

The following required scenarios were **not** measured and must not be inferred from the idle result:

- Expand/collapse animation latency, CPU, or frame smoothness: no safe deterministic UI-driver/instrumented animation harness exists in this phase.
- User focus/foreground transition responsiveness and drag/drop interaction: no interactive human validation was performed in this run.
- Multiple successive Activity updates: initial four activities were present, but no timed update-storm scenario was performed against the UI process.
- Active GSMTC or agent-provider workload: no real media session or supported agent integration was available.
- Windows 11 and multi-monitor behavior: host is Windows 10 and only one usable monitor was observed.

## Follow-up measurement recommendation

Before release, add a separately scoped UI-performance harness or reproducible manual protocol that records ETW/PresentMon or Windows Performance Recorder traces for repeated expand/collapse, a controlled Activity-update burst, and a real supported provider session. Capture several runs on supported Windows 11 single- and multi-monitor hardware, reporting median and range rather than a single sample.
