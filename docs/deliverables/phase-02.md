# Phase 02 Delivery — Activity Event Pipeline

## Goal and completed work

Phase 2 replaces Phase 1's direct fixed mock composition with an in-process, event-driven Activity pipeline. It adds a typed `EventBus`, normalized upsert/removal messages, `ActivityPipeline`, provider health/lifecycle contract, timer-free `MockActivityProvider`, and dispatcher-safe UI snapshot refresh.

The enforced flow is `MockActivityProvider -> EventBus -> ActivityPipeline/ActivityManager -> snapshot -> IslandViewModel`. Providers never reference UI controls. Subscriptions dispose cleanly and subscriber exceptions are isolated.

## Build and test evidence

Environment: Windows 11 x64, .NET SDK 10.0.302, Release x64.

- Full solution tests: Core 25 and UI 3 passed (28 total).
- Release x64 build: passed with 0 warnings and 0 errors.
- `git diff --check`: passed.

## Runtime validation

No unbounded GUI run was used. The app source wires the mock provider's one-time publication and explicit Waiting-promotion action through the pipeline. Interactive visual confirmation of the update in a desktop session remains pending and is not claimed as complete.

## Security and limitations

No real provider, IPC, process observation, network request, loader, DLL discovery, OCR/UI automation, persistence, or settings capability was added. Phase 2 remains mock-only. Later work must validate real providers through documented stable integrations and retain the EventBus boundary.

## Next-phase notes

Phase 3 can add Windows media-session input through a dedicated provider, publishing normalized events only. Preserve disposal, exception isolation, and UI-free provider boundaries.
