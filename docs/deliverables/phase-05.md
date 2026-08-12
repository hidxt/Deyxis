# Phase 05 Delivery - Agent Provider Boundaries

## Goal and completion

Phase 5 establishes provider boundaries for Codex, Claude Code, and OpenCode. The app constructs all three adapters in a stopped-only composition and disposes them when the island closes. App startup does not call `Start`, probe an executable, run a command, or publish an Agent activity.

## Architecture and files

- `AgentProviderBase` maps the deliberately small Agent lifecycle model to Core Activities through `IEventBus` and reports live-session observation as unsupported.
- `CodexProvider` exposes an explicit availability-probe boundary; constructing or starting it does not invoke that probe.
- `ClaudeCodeProvider` and `OpenCodeProvider` are stopped explicit-run boundaries only. Phase 5 adds no run request or command execution for them.
- `ExecutableProbe` requires fully-qualified, explicitly allowlisted paths and a bounded timeout. It launches `--version` without a shell and returns only a first-line version or a failure category.
- `AgentProviderComposition.CreateDisabled` creates the three adapters with stopped health and an inert Codex probe. `App` owns this composition and disposes it during closure.

## Build and test evidence

Environment: Windows 10 Pro 10.0.19045 AMD64, .NET SDK 10.0.302; Release x64 configuration.

- Focused stopped-composition test: passed 1/1 after first failing to compile because the composition boundary did not yet exist.
- Provider suite: passed 36/36, zero failed/skipped.
- `dotnet test Deyxis.slnx -c Release -p:Platform=x64 --no-restore`: passed 66/66 (Core 25, Providers 36, UI 5), zero failed/skipped.
- `dotnet build Deyxis.slnx -c Release -p:Platform=x64 --no-restore`: succeeded with 0 warnings and 0 errors.

## Direct tool checks

Only the explicitly named tools were enumerated. Only an installed tool was given a bounded direct `--version` invocation, with standard output and standard error left unredirected.

- Codex: executable present at the installed WindowsApps path. Direct `codex --version` did not start because Windows returned `Access is denied`; no version is claimed.
- Claude Code: command absent; no version command was run.
- OpenCode: command absent; no version command was run.

These checks were manual delivery verification only. App construction does not repeat them or retain their output.

## Safety changes and limitations

No prompt, response, token, credential, file content, or command output is published as activity data. No process discovery, running-session attachment, terminal/log/database/IPC inspection, permission automation, CLI installation, or automatic background agent execution was added. The Codex availability boundary exists for a future explicit configuration flow; it is disabled in current app composition. Claude Code and OpenCode remain non-running boundaries.

No GUI launch was performed, so desktop presentation remains subject to interactive validation. No commit or push was performed for this task.
