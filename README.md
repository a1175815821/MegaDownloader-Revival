# Mega Downloader — Revival

Cross-platform revival of Mega Downloader. The original VB.NET application has been
converted to C# on .NET 10; the UI is being rebuilt from scratch in Avalonia while the
existing WinForms application stays runnable as a reference implementation.

Repository: <https://github.com/a1175815821/MegaDownloader-Revival>

## Layout

| Path | Purpose |
|---|---|
| `src/` | Converted C# source, resources, language packs, bundled binaries |
| `MegaDownloader.Core/` | Download engine, settings, localization and platform abstractions (cross-platform, net10.0) |
| `MegaDownloader.UI/` | Avalonia shell — the new cross-platform UI (net10.0) |
| `MegaDownloader.Full/` | Migrated WinForms application (net10.0-windows), feature-complete |
| `MegaDownloader.Tests/` | Regression harness (1121 assertions) against the migrated code |

## Status

**Phase 3 — dialogs and settings (complete).** The Avalonia shell runs the shared queue
through `DownloadEngineService` and has the **Add links** window (preflight, selection,
duplicate detection, enqueue), a **Properties** dialog for files and packages, **Encode
links**, **ELC** generation with account management, a native **Streaming library** page,
and a Settings page with General, Advanced (proxy/schedule), Language, Pre-shared-key and
Streaming tabs. The streaming server itself is replaced with Kestrel in Phase 4.

## Build

```bash
dotnet build MegaDownloader.slnx
```

Run the new UI:

```bash
dotnet run --project MegaDownloader.UI/MegaDownloader.UI.csproj
```

Run the legacy WinForms app:

```bash
dotnet run --project MegaDownloader.Full/MegaDownloader.Full.csproj
```

## Roadmap

0. Git baseline + Avalonia skeleton (DI, theme tokens, navigation, logging)
1. Extract the download engine and settings into `MegaDownloader.Core`
2. Rebuild the download queue in Avalonia (virtualized list, live progress) — done
3. Port the remaining dialogs and settings tabs — done (add links, properties, encode/ELC, streaming library, settings tabs)
4. Replace the embedded HTTP server and remaining Windows-only helpers
5. Package for Windows, macOS and Linux

The WinForms-only state is preserved as tag `winforms-baseline` on the original
history. See [MIGRATION.md](MIGRATION.md) for conversion details and known gaps, and
[PLAN.md](PLAN.md) for the live phase checklist and current progress.
