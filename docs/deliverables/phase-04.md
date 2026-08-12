# Phase 04 Delivery — Local LRC Lyrics

## Goal and completion

Phase 4 adds offline, local LRC lyrics for Media activities. It includes parser/timeline selection, a root-constrained local lyrics provider, MediaProvider integration, and a restrained current-lyric region in the Media primary view.

## Architecture and files

- `LrcParser` creates immutable timed lines with multi-tag support, metadata skipping, malformed-tag tolerance, stable ordering, and binary-search selection.
- `LocalLrcLyricsProvider` resolves only sanitized artist/title names under `%LOCALAPPDATA%\Deyxis\Lyrics`, returning empty snapshots for missing/invalid material.
- File reads enforce exact `.lrc` naming, canonical root containment, reparse-point rejection, 1 MB limit, 10,000-line limit, and UTF-8/UTF-16 decoding.
- `MediaProvider` owns an injected lyrics provider and `CurrentLyrics`; lyric failures do not alter media activity or provider health.
- UI receives an optional lyrics snapshot separately from Core Activity data, and only exposes it for Media activity presentation.

## Build and test evidence

Environment: Windows 10 Pro 10.0.19045 AMD64, .NET SDK 10.0.302; project target remains Windows x64.

- `dotnet test Deyxis.slnx -c Release -p:Platform=x64 --no-restore`: passed 50/50 (Core 25, Providers 20, UI 5), zero failed/skipped.
- `dotnet build Deyxis.slnx -c Release -p:Platform=x64 --no-restore`: succeeded with 0 warnings and 0 errors.
- `git diff --check`: passed.
- Bounded local UTF-16 fixture lookup, timeline-position refresh, and missing-lyrics fallback: 3/3 passed.

## Runtime and resource observations

No unbounded GUI launch was performed. The bounded fixture validates the local read/parse/update path, but desktop rendering and animation smoothness await interactive Windows 11 validation. No resource performance claim is made beyond the bounded automated tests.

## Security changes and limitations

No network lyric service, player scraping, upload, file picker, persistence, or settings was introduced. The configured local root is fixed in app composition for this phase; user configuration belongs to Phase 8. This host lacks symlink creation privilege, so a reparse-point test exits when setup cannot create one; production code still rejects reparse-point candidates.

## Next-phase notes

Provider-specific Agent integrations must preserve the EventBus and UI-free provider boundary. The lyrics UI uses a separate presentation snapshot and must not be encoded into generic Activity descriptions.
