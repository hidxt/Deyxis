# Phase 06 Delivery - Validated Image Wallpaper Drop

## Goal and completion

Phase 6 adds a single-local-image drag/drop flow. Deyxis validates the file, publishes a temporary FileDrop Activity, and only then expands an image preview with explicit **Set wallpaper** and **Cancel** controls. Validation and preview do not change the wallpaper.

## Architecture and behavior

- `FileDropProvider` canonicalizes and validates one JPG/JPEG, PNG, or BMP before publishing an accepted Activity. It rejects empty or multiple drops, directories, traversal, UNC and reparse paths, unsupported extensions, oversized files, and mismatched magic headers.
- The accepted result keeps its canonical path inside provider/UI state and returns a private confirmation token; neither the Activity title nor description contains the path.
- `IslandWindow` forwards dropped storage-item paths to the provider. It assigns the UI preview source only when the provider returns `Accepted`, so rejected data is never decoded by the preview.
- `IslandViewModel` exposes preview/actions only while the matching validated FileDrop Activity remains in the snapshot, whether primary or queued. Removal clears the path and token context.
- **Set wallpaper** is the only UI path to `ConfirmAsync`. **Cancel** consumes the token and removes the temporary Activity without invoking the wallpaper facade.
- `WindowsCurrentUserWallpaper` applies a confirmed canonical path to the current user through the injected `SystemParametersInfo` boundary. Native failures are contained as a failed, still-cancellable Activity.

## Build and test evidence

Environment: Microsoft Windows NT 10.0.19045.0, .NET SDK 10.0.302; Release x64 configuration.

- UI mapping tests were written first and failed because the validated preview/action API did not exist. The focused UI suite then passed 9/9.
- Bounded local fixture validation passed 1/1. The test creates an eight-byte PNG-header file in a unique temporary directory, validates it, asserts no wallpaper call occurred, cancels it, reasserts no wallpaper call occurred, and deletes the fixture.
- `dotnet test Deyxis.slnx -c Release -p:Platform=x64 --no-restore`: passed 97/97 (Core 25, Providers 63, UI 9), with zero failed or skipped.
- `dotnet build Deyxis.slnx -c Release -p:Platform=x64 --no-restore`: succeeded with 0 warnings and 0 errors.
- `git diff --check`: passed; Git emitted only existing working-copy LF-to-CRLF conversion notices during diff display.

## Safety boundaries and limitations

No live wallpaper confirmation was performed during delivery verification. No GUI launch was performed, so desktop drag/drop presentation remains subject to interactive validation. Phase 6 does not upload, copy, move, delete, scan, edit, retain history for, synchronize, or batch-process images, and it does not add monitor-specific wallpaper selection or network access.

No commit or push was performed for this task.
