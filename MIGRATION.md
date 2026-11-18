# Mega Downloader — C# Migration

## What was delivered

The VB.NET project at `I:\v1.9` was converted to C# and placed in `I:\Mega Downloader`.

- `src/` — all 114 converted C# source files, resources, language packs, DLLs, binaries
- `MegaDownloader.Full/` — full C# application (WinForms, .NET 10) with all UI/business features preserved
- `MegaDownloader.Core/` — cross-platform foundation: download engine (network, files, packages, scheduling, quota, verification, extraction, streaming library), settings and localization, `IPlatformServices`, Windows DPAPI/registry implementation, Unix key/autostart/protocol implementation, shared models (.NET 10)
- `MegaDownloader.UI/` — Avalonia cross-platform UI shell (.NET 10, Windows/macOS/Linux capable)
- `MegaDownloader.Tests/` — regression suite ported to .NET 10
- `tools/` — CodeConverter CLI used for the initial VB → C# conversion

## Conversion completeness

- All 107 original `.vb` source files have a same-basename `.cs` counterpart under `src/`.
- No `#error`, `TODO ERROR: Skipped`, or uncommented VB syntax remains in the converted source tree.
- The three VB `#If MSD` branches were converted to real C# `#if MSD` blocks in `Language.cs`, `InternalConfiguration.cs`, and `Updater.cs`; `Debug_MSD` / `Release_MSD` now build with rebranding behavior.
- The generated `My` namespace facade was cleaned up; active behavior is unchanged.
- Language packs, `InternalConfig.xml`, `7zr.exe`, bundled DLLs, and binaries are embedded/copied as required by the original behavior.

## Build status

| Project | Status |
|---|---|
| `MegaDownloader.Full` (Debug) | **Builds: 0 errors** |
| `MegaDownloader.Full` (Release_MSD) | **Builds: 0 errors** |
| `MegaDownloader.Tests` | **Builds: 0 errors** |
| `MegaDownloader.Core` | **Builds: 0 errors** |
| `MegaDownloader.UI` (Avalonia shell) | **Builds: 0 errors** |

## Command line

```bash
cd "I:/Mega Downloader"
dotnet build MegaDownloader.slnx
dotnet build MegaDownloader.Full/MegaDownloader.Full.csproj
dotnet build MegaDownloader.Full/MegaDownloader.Full.csproj -c Release_MSD
dotnet build MegaDownloader.Core/MegaDownloader.Core.csproj
dotnet build MegaDownloader.UI/MegaDownloader.UI.csproj
dotnet build MegaDownloader.Tests/MegaDownloader.Tests.csproj
dotnet run --project MegaDownloader.Tests/MegaDownloader.Tests.csproj --no-build -- "I:/Mega Downloader" "I:/Mega Downloader/MegaDownloader.Full/bin/Debug/net10.0-windows"
```

## Functional validation

The .NET 10 regression harness executes the original behavioral assertions against the C# assembly. Full suite result: **PASS: 1121 assertions**.

Verified groups include:

- download/resume/corrupt-final cases
- duplicate/parallel tasks, deletion ownership, locked primary/backup recovery
- speed parsing and preallocation
- quota/retry/scheduling and B1–B8 fixes
- language `GetText` 20k loop and zh-CN quality checks
- 16 MB MetaMAC verification
- delivery-audit cases (locale, stop/restart, deferred startup, live services, concurrent library edits, backup recovery)
- 7z cancellation and archive extraction edge cases
- process resource monitoring and 100k preflight selection
- native UI first-open, settings, workbench, theme, and feature dialog checks
- final release cases (queue backup, extraction transaction, multipart lifecycle, stop race, update reminder, proxy)

## .NET 10 notes

- `System.Resources.Extensions` was upgraded to 10.0.0.
- Legacy `ObjectListView.dll` was replaced with the `ObjectListView.Repack.NET6Plus` net8 assembly (`ObjectListView2022NET.dll`) because the old DLL embedded BinaryFormatter resources, which .NET 10 removes. Public `BrightIdeasSoftware` APIs remain compatible; no application source changes were needed.
- The stale legacy DLL is excluded from output copying.
- Test reflection accessors were aligned with the migrated C# field/property shapes, and test resource paths now point to `src/Resources`.
- `SharpCompress` was upgraded from 0.39.0 to 0.48.1 to clear GHSA-6c8g-7p36-r338 / CVE-2026-44788; SharpCompress API calls were migrated to `OpenArchive`/`OpenReader` and init-only `ReaderOptions.Password`.
- `MegaDownloader.UI` targets Avalonia 12.1.6 (Avalonia, Desktop, Fluent theme, Inter fonts) with `CommunityToolkit.Mvvm` 8.4.2; `Avalonia.ReactiveUI` was removed in favor of plain MVVM and compiled bindings. `Avalonia.Diagnostics` has no 12.x build yet and is intentionally omitted.
- `WindowsPlatformServices` is annotated `[SupportedOSPlatform("windows")]`; the composition root selects it behind `OperatingSystem.IsWindows()`, which clears the former `CA1416` noise for the platform layer.
- Remaining build warnings come from the WinForms/P-Invoke code in `MegaDownloader.Full`.
- Package advisories are tracked with `dotnet list package --vulnerable --include-transitive` and should be re-checked before each release.

## Avalonia shell (Phase 0)

The new UI is a fresh application, not a port of the WinForms forms:

- `App.axaml.cs` owns the composition root (`Microsoft.Extensions.DependencyInjection`): platform services, logger, settings, theme, navigation and dialog services, page and shell view models.
- `Themes/Colors.axaml` mirrors the 30+ `Md.*` tokens from the WinForms `ThemeManager` as `ThemeDictionaries` for Light and Dark; `Themes/Controls.axaml` adds the shared window/text/card/navigation styles and `Themes/Button.axaml` replaces the Fluent button theme so normal, hover, pressed, disabled and `.accent` states all come from the tokens.
- `IThemeService` maps Auto/Light/Dark onto `Application.RequestedThemeVariant` and persists the choice to `ui-settings.json` under the app data directory.
- `INavigationService` + `ViewLocator` host page view models in the shell; `MainWindow` provides the navigation rail, toolbar and status bar.
- `IAppLogger`/`FileLogger` write a rolling daily log next to the settings file; `Program` also records unhandled exceptions to `crash-yyyyMMdd.log`.
- Downloads and Settings pages are deliberately thin placeholders so the engine port in Phase 1 has a stable host to plug into.

## Engine extraction (Phase 1)

The download engine and settings were moved out of the WinForms shell into
`MegaDownloader.Core` without changing behavior:

- `Engine/` holds the 41 engine files (download, packages, scheduling, quota, MD5/MetaMAC,
  extraction, streaming library, logging, updater) plus `Cryptography/` and
  `StreamingLibrary/`.
- `Settings/` holds `Configuracion`, `ConfiguracionUI`, `InternalConfiguration`,
  `Language` and `UltimaConfiguracionUsada`.
- Engine-owned resources (language packs, `InternalConfig.xml`, `7zr.exe`) are embedded
  in Core and read relative to `typeof(...).Assembly`; Full keeps its own copies for the
  existing tests.
- WinForms seams were replaced with `UserNotification.Sink`, `StreamingLibraryRoutes`,
  `StartupRegistration` (moved back to Full) and an `OperatingSystem.IsWindows()` guard
  around the 7-Zip registry probe.
- Core grants `InternalsVisibleTo("MegaDownloader")` so the WinForms shell keeps using
  engine members that stay internal; the Avalonia UI only consumes the public surface.
- Result: `MegaDownloader.Full` builds with 0 errors and the regression harness still
  reports **PASS: 1121 assertions**.

## Avalonia download queue (Phase 2)

The Avalonia shell now drives the shared engine instead of showing a placeholder page:

- `MegaDownloader.Core/Services/DownloadEngineService.cs` is the public façade over the
  internal engine: it loads and saves `DownloadList.xml` (atomic write + `.bak`), runs a
  500 ms scheduling tick, enforces `DescargasSimultaneas`, respects quota quarantine and
  the configured download schedule, and exposes whole-queue and per-file start/pause/stop
  plus reference-identity removal with optional local-file deletion.
- `DownloadsPageViewModel` polls the service on the UI thread and reconciles a stable row
  set keyed by the engine's `Paquete`/`Fichero` instances, so selection and expansion
  survive refreshes. Package rows can be expanded/collapsed, filtered by status, and each
  row offers start/pause/remove; a context menu adds “remove and delete local files”.
- `DownloadsPageView.axaml` is a fresh design (virtualized `ListBox`, status strip, empty
  states, token-based styles), not a port of the WinForms `ObjectListView` layout.
- `App.axaml.cs` registers the service as a singleton and stops the page timer, saves the
  queue and disposes the engine when the desktop app exits.

Remaining queue work: drag-to-reorder priority, the error-retry policy UI, per-file speed
limits, and the remaining dialogs.

## Avalonia dialogs and settings (Phase 3, in progress)

- `AddLinksWindow` / `AddLinksViewModel` reuse the Core preflight engine
  (`PreflightSession`, `BatchLinkPreflight`, `PreflightSelection`) instead of the WinForms
  `BatchPreflightView`: link parsing, folder expansion, per-file metadata, duplicate and
  already-queued detection, progress, filtering, select-all/clear, and selection totals.
- Selected entries are rebuilt into a `Paquete` with `DownloadPackageBuilder.Build` on a
  background thread and appended through the new `DownloadEngineService.AddPackage`; the
  queue snapshot (`GetQueuedLinkSnapshot`) is re-checked immediately before enqueueing.
- The window carries the WinForms options (folder, package name, archive password,
  create-subfolder, extract, start-after-adding) and remembers the last used values.
- The Settings page now edits the download defaults in `Configuration.xml` (default
  folder, simultaneous downloads, connections per file, package defaults, start-on-launch)
  in addition to the theme selector.
- The shell toolbar's Start/Pause/Stop buttons are bound to the queue page commands and
  live enabled state.

Still to port: the properties dialog, encode/ELC dialogs, streaming-library management,
and the proxy/schedule/language/pre-shared-key/ELC-account settings tabs.

## Cross-platform status

- Platform-neutral service interface: `MegaDownloader.Core/Platform/IPlatformServices.cs`
- Windows: `WindowsPlatformServices.cs` (DPAPI, HKCU autostart, mega:// protocol, shutdown)
- macOS/Linux: `UnixPlatformServices.cs` (encrypted secret store, XDG/LaunchAgents autostart, xdg-mime, open/shutdown)
- Avalonia shell: `MegaDownloader.UI/` starts on Windows/macOS/Linux; business features are progressively movable from `MegaDownloader.Full` into `MegaDownloader.Core` behind the platform interfaces

Major items still required before a full cross-platform release:

1. Port WinForms-specific forms/designers to Avalonia views and view models.
2. Replace embedded `HttpServer.dll` with ASP.NET Core/Kestrel.
3. Replace `ObjectListView` with a virtualized Avalonia tree/grid.
4. Handle `7zr.exe` per RID or migrate to managed SharpCompress-only extraction.
5. Move DPAPI call sites to `IPlatformServices` and add keychain-backed secret storage on macOS/Linux.
6. Replace registry/P-Invoke-dependent helpers (clipboard, theme, URI protocol, autostart) with service implementations.
7. Update the Avalonia dependency set to address the current transitive `Tmds.DBus.Protocol` advisory.

## Notes

- The original `I:\v1.9` project remains untouched.
- Repository: <https://github.com/a1175815821/MegaDownloader-Revival>. Active work happens on `refactor/avalonia`; tag `winforms-baseline` marks the last state where only the WinForms application existed.
- `tools/` (the 206 MB CodeConverter store) is intentionally ignored by Git and must be re-created locally if the conversion needs to be repeated.
