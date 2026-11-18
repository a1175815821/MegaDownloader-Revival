# Mega Downloader — C# Migration

## What was delivered

The VB.NET project at `I:\v1.9` was converted to C# and placed in `I:\Mega Downloader`.

- `src/` — all 114 converted C# source files, resources, language packs, DLLs, binaries
- `MegaDownloader.Full/` — full C# application (WinForms, .NET 10) with all UI/business features preserved
- `MegaDownloader.Core/` — cross-platform foundation: `IPlatformServices`, Windows DPAPI/registry implementation, Unix key/autostart/protocol implementation, shared models (.NET 10)
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
- Remaining build warnings are mostly platform annotations (`CA1416`) from Windows-only WinForms/P-Invoke code, plus package advisories.

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
- No Git repository or branch was created in `I:\Mega Downloader`; repository/branch work was explicitly cancelled.
