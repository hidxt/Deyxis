# Phase 08 Delivery - Local Settings and Safe Activity History

## Scope delivered

Phase 8 adds immutable validated settings, do-not-disturb presentation policy, a twenty-entry sanitized Activity history, bounded local JSON persistence, and a minimal independent settings window. The expanded Island exposes the settings window; values change only when the user selects **Apply**, which publishes a validated settings snapshot for the App to persist and apply.

The compact history view displays provider ID, category, state, title, and UTC timestamp only. Its explicit **Clear** action removes the in-memory entries and the fixed history file below the configured local app-data root. Activity descriptions, actions, metadata, lyrics, file identity/path, Agent details, prompts, output, and credentials are not represented by the history summary or UI row models.

Supported settings cover follow-active-monitor, surface mode, Island width, corner radius, opacity, expand-on-hover, hide-in-fullscreen, do-not-disturb, and provider-health presentation. Provider enablement preferences remain present in the immutable snapshot and survive UI apply unchanged. Do-not-disturb remains a Core presentation decision: manual open and Waiting/Failed visibility bypass it; Phase 8 does not add an autonomous notification or prompting surface.

## Verification evidence

Environment: Windows, .NET SDK 10; Release x64 configuration.

- The UI mapping tests were written first and failed with CS0234 because the `Deyxis.UI.Settings` and `Deyxis.UI.History` types did not exist. After implementation, the focused UI suite passed 13/13.
- `dotnet test Deyxis.slnx -c Release -p:Platform=x64 --no-restore`: passed 144/144 (Core 47, Providers 82, UI 13, App 2), with zero failed or skipped.
- `dotnet build Deyxis.slnx -c Release -p:Platform=x64 --no-restore`: succeeded with 0 warnings and 0 errors.
- The bounded SettingsStore/ActivityHistoryStore subset passed 14/14, covering malformed, oversized and partial settings fallback; validated atomic round trip; interrupted replacement cleanup; root path containment; bounded safe history persistence; invalid history fallback; and fixed-file clear behavior.

## Boundaries and observations

Persistence uses versioned bounded JSON below current-user local app data and same-directory temporary replacement. No registry startup keys, cloud sync, accounts, network calls, notification center, or migration framework were added. The settings window was compiled and exercised through its view-model mapping tests, but its visual layout and live desktop interaction were not manually inspected in this delivery. No commit or push was performed by this task worker.
