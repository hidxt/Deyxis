# Phase 9 dependency and supply-chain audit

**Audit date:** 2026-08-12
**Scope:** `Deyxis.slnx`, all project files, resolved NuGet graph, and the Windows-native/runtime surface used by the application.

## Method and result

The following direct-network commands completed successfully (the local proxy was not used):

```powershell
dotnet list Deyxis.slnx package --include-transitive
dotnet list Deyxis.slnx package --vulnerable --include-transitive
```

Restore/audit used the configured official NuGet v3 feed, `https://api.nuget.org/v3/index.json`. The supported NuGet advisory check reported **no vulnerable packages** for all ten projects at the time of the command. This is an advisory-database result, not a guarantee against undisclosed or future vulnerabilities.

## Authored direct dependencies

| Package | Version | Used by | Purpose | License evidence | Source posture |
|---|---:|---|---|---|---|
| Microsoft.WindowsAppSDK | 1.8.260710003 | App, UI | WinUI 3 host/runtime | Package metadata points to Microsoft/Windows App SDK; license is published with the package/repository | Official NuGet; version pinned |
| Microsoft.Win32.SystemEvents | 10.0.0 | Platform.Windows | Windows display/system event bridge | MIT in package metadata | Official NuGet; version pinned |
| Microsoft.NET.Test.Sdk | 17.12.0 | all four test projects | test host | MIT in package metadata | Official NuGet; version pinned, test-only |
| xunit | 2.9.2 | all four test projects | unit-test framework | Apache-2.0 in package metadata | Official NuGet; version pinned, test-only |
| xunit.runner.visualstudio | 3.0.2 | all four test projects | VS/test runner adapter | Apache-2.0 in package metadata | Official NuGet; version pinned, test-only |

There is no `NuGet.Config` in the repository and no authored package lock file. Package versions are explicit in project files, but transitive versions resolve through NuGet metadata rather than a committed lock graph.

## Resolved notable transitives

The runtime graph is primarily Microsoft Windows App SDK components: `Microsoft.WindowsAppSDK.{Base,Foundation,Runtime,WinUI,DWrite,Widgets,InteractiveExperiences,AI,ML}`, `Microsoft.Windows.SDK.BuildTools`, and `Microsoft.Web.WebView2` 1.0.3179.45. The test graph additionally resolves `Microsoft.CodeCoverage`, test platform components, xUnit implementation packages, `Newtonsoft.Json` 13.0.1, and `System.Numerics.Tensors` 9.0.0. The full resolved list was captured from `dotnet list ... --include-transitive`; no unpinned direct package reference was found.

## Native and platform dependencies

The production app targets `net10.0-windows10.0.19041.0`, `win-x64`, is self-contained through Windows App SDK, and uses supported Windows APIs (WinUI 3/Windows App SDK, GSMTC/WinRT, system event notifications, and wallpaper interop). These are platform/runtime dependencies, not downloaded executable payloads. Their servicing posture follows Windows and Windows App SDK servicing; they must be retested whenever the target Windows App SDK or Windows SDK is upgraded.

## Supply-chain conclusions and follow-up

- Direct references are few, version-pinned, and restored from the official NuGet feed in this run.
- The repository contains no custom NuGet source configuration, vendored binary dependency, npm/pip dependency, or runtime plugin-DLL loading mechanism.
- Advisory scan found no currently listed vulnerabilities. Re-run it in CI and before releases because advisory data changes.
- **Residual low risk:** no lock file means a future restore can select changed transitive versions permitted by upstream metadata. Consider enabling NuGet lock files and locked-mode restore as a separately reviewed build/reproducibility change.
- **Residual low risk:** license evidence above is package metadata, not legal advice. Produce a release SBOM/license review when distributing outside the development environment.
