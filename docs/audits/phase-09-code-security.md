# Phase 9 code, reliability, and security audit

Date: 2026-08-12
Scope: current `src/`, `tests/`, solution/project/build files, and application composition
Method: single-pass static source review, focused malicious-pattern search, independent baseline review, parent validation, and test-first repairs

## Outcome

No Critical or High security vulnerability and no malicious-code behavior was found. The current application contains no network client, download-and-execute path, credential collection, persistence mechanism, runtime assembly/native-library selection, database/query surface, or shell-command construction.

Four defects were reproduced with failing regression tests and repaired:

| Severity | Finding | Evidence and repair |
| --- | --- | --- |
| Medium reliability | Concurrent provider publications reached the unsynchronized `ActivityManager` dictionary through `ActivityPipeline`, losing updates and allowing notifications to arrive out of snapshot order. | A 2,000-item parallel test reproduced 1,999 retained activities. Pipeline mutation, snapshot creation, notification, and disposal now share one synchronization boundary. Ordering and dispose-waits-for-in-flight-callback tests cover the lifecycle contract. |
| Low reliability | An `EventBus` publish snapshot could still invoke a subscription disposed by an earlier handler in the same publication. | The regression observed one forbidden callback. Subscriptions now have an atomic active state checked immediately before invocation. |
| Low reliability | `MediaProvider.Stop()` left the previously published media activity in the shared pipeline. | The regression timed out waiting for removal. Stop now publishes the stable media activity's `ActivityRemoved` event after cancellation and detachment. |
| Low security | File-drop confirmation reused a pathname without binding later preview/wallpaper use to the bytes that passed validation (CWE-367). | Accepted files now retain a bounded SHA-256 fingerprint plus length and image type. The provider revalidates regular/non-reparse metadata, extension, bounded length, magic bytes, and hash before preview exposure and again before invoking the wallpaper facade. Mismatch removes the pending token and publishes only path-free status. The original is not copied or moved. |

Targeted post-repair results are recorded in the task report. Full solution verification is also run before task handoff.

## Residual validated Low findings

These findings do not meet the Phase 9 mandatory Critical/High repair threshold. Fixes would require broader persistence or product-lifecycle design, so they are documented without feature expansion.

### File-drop live and pending state is unbounded (CWE-400)

Each rejection publishes a new GUID-backed activity that remains in `ActivityManager`, and each accepted unconfirmed drop occupies `pendingDrops` until explicit cancel, confirm, or provider stop. A local interactive user or UI automation can increase memory, snapshot-sort work, and queue-render work indefinitely.

Each individual input is restricted to one local file and 25 MiB, and persisted history is independently capped at twenty entries. Recommended remediation is a small explicit pending limit plus expiry, and coalescing or bounded lifetime for transient rejection activities.

### History clear can race an older fire-and-forget save (CWE-362)

`App.OnActivityUpserted` discards `ActivityHistoryStore.SaveAsync`. Clear deletes the fixed history file independently. An earlier atomic write can finish its final move after clear and recreate records the user intended to delete.

History content is limited to twenty validated summaries, not prompts, descriptions, output, or credentials. Recommended remediation is one owned persistence queue with monotonically increasing generations; clear should order behind or cancel earlier writes and prevent stale generations from replacing the file.

### Expected persistence errors can escape `async void` UI event handlers (CWE-248)

Settings save and history clear are awaited directly from `async void` event handlers. Expected `IOException` or `UnauthorizedAccessException` from locking or permissions can reach the UI synchronization context and terminate the process.

Reads already fall back safely, temporary writes are cleaned up, and failures expose no sensitive details. Recommended remediation is Task-returning persistence orchestration with expected I/O/access failures contained at the UI boundary and represented by a non-sensitive failure state.

## Surface review

### Async, cancellation, events, and disposal

- Media refresh uses cancellation plus a monotonic refresh version to discard stale results; stop detaches the platform event, cancels and disposes its token source, clears state, and now withdraws the media activity.
- GSMTC manager/session handlers are detached on session replacement and disposal. Public operations check disposal and cancellation around WinRT awaits. WinRT operations themselves are not canceled by the supplied token; cancellation prevents their results from being applied but cannot abort the underlying OS request.
- Window/coordinator teardown detaches managed listeners and disposes Win32 registrations. `ActivityPipeline` and `EventBus` received the test-backed lifecycle repairs above.
- Remaining `async void` methods are WinUI lifecycle/event entry points. The two persistence handlers have the residual failure-containment issue described above.

### Win32, WinRT, DPI, and window state

- Native imports name fixed Windows system DLLs and fixed entry points only: `user32.dll`, `shcore.dll`, and `dwmapi.dll`.
- The foreground WinEvent delegate is rooted for the registration lifetime and unhooked on disposal; the display event registration is also detached.
- Native facade failures are contained into empty/null snapshots or explicit false results, excluding fatal CLR failures. Monitor rectangles use checked semantic conversion to non-negative sizes.
- DPI placement uses the monitor's effective DPI with a 96-DPI fallback. No user-controlled library or function name reaches native loading.
- `AllowUnsafeBlocks` is enabled for the Windows platform project, but the reviewed C# source contains no `unsafe` block or direct pointer manipulation.

### Files, configuration, lyrics, and wallpaper

- Settings and history use fixed filenames inside a canonical configured root, bounded byte sizes, version/schema validation, atomic same-root temporary writes, and recoverable-read fallback.
- History persists only provider ID, category, state, title, and timestamp; it caps entries at twenty and bounds persisted strings.
- Local lyrics require safe filename components and a contained `.lrc` path, reject a direct reparse-point file, bound bytes and lines, and parse with a generated bounded regex. Same-user replacement of an ancestor remains a general pathname TOCTOU consideration.
- File drops reject missing/multiple/relative/UNC/traversal/directory/reparse/oversized/unsupported inputs and require matching JPG, PNG, or BMP magic bytes before issuing an opaque confirmation token. A bounded SHA-256 fingerprint, exact length, type, and path safety are checked again before preview exposure and wallpaper use; changed inputs are removed with path-free status. The no-copy/no-move constraint means an unavoidable narrow pathname race still exists between the final validation read and the native API reopening the path; eliminating it fully would require an app-owned immutable copy or a native API accepting an already-open handle.

### Providers, executable probing, and plugin boundary

- Current application composition creates disabled agent providers and performs no executable probe at startup.
- The dormant `ExecutableProbe` accepts only normalized, fully qualified paths from an explicit case-insensitive allowlist; it uses `UseShellExecute = false`, a fixed `--version` `ArgumentList` element, no command interpreter, a three-second default timeout, and retains only the first output line or a fixed failure category.
- The executable allowlist is pathname-based rather than file-identity/signature-bound. Re-review that trust decision before enabling probes in composition.
- `Deyxis.PluginSdk` currently defines a compile-time provider interface only. No directory enumeration, reflection, `Assembly.Load*`, `NativeLibrary.Load`, or other dynamic plugin loading exists.

## Malicious-code and injection search

The source/build scan found no applicable:

- HTTP/FTP client, downloader, socket, remote endpoint, or download-then-execute flow;
- PowerShell/cmd invocation, encoded command, `Invoke-Expression`, shell concatenation, or attacker-controlled process argument;
- runtime assembly/native library loading, reflection-based activation, or executable discovery;
- credential, password, token, API-key, browser-data, environment-secret, or key-store collection;
- registry Run/RunOnce, scheduled-task, service, startup-folder, security-product exclusion, or other persistence/security-control modification;
- SQL/NoSQL query, XML/XPath parser, HTML renderer, redirect, request, deserialization-to-code, or template/code-generation sink;
- custom MSBuild task, `<Exec>`, pre/post-build command, or encoded build payload.

The XAML schema URLs and documentation/license URLs are declarative text only. The wallpaper call is a deliberate, explicit current-user action guarded by preview confirmation.

## Limitations

- This task is a static audit with deterministic unit/integration tests; it is not a malware sandbox, fuzzing campaign, code-signing assessment, or live hostile-process race test.
- Windows 11, multiple physical monitors, live GSMTC sessions, live agent tools, reparse races, and process crash handling were not exercised here.
- Dependency advisories, provenance, licenses, and package supply-chain status belong to Phase 9 Task 2.
- The independent baseline fully reviewed 124 files. The parent review covered the product source, tests, project/build files, and XAML for the requested risk patterns, with deeper data-flow review on the listed security-sensitive surfaces. Historical planning/delivery documents were not treated as executable product source.
