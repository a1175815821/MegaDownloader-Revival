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
| `MegaDownloader.Tests/` | Regression harness (1143 assertions) against the migrated code |

## Status

**Phase 4 — servers and platform helpers (complete).** The embedded `HttpServer.dll` is
gone: the browser remote control and the streaming library both run on **Kestrel** inside
`MegaDownloader.Core`, with the same page contract, login throttling, CSRF rules and
RFC 7233 range handling. Secrets go through `ISecretProtector` (DPAPI on Windows, the OS
keychain with an encrypted-key-file fallback on macOS/Linux), `.7z` falls back to managed
SharpCompress extraction when no 7-Zip CLI exists, and `mega://` activations reach the
running window through a portable single-instance gate. The Avalonia shell has the Phase 3
surface — Add links, Properties, Encode links, ELC, the Streaming library page, the
General/Advanced/Language/Pre-shared-key/Streaming settings tabs — plus clipboard intake
and a **Web remote** tab.

**Deep review round (complete).** A full pass over the Avalonia shell and the shared
engine fixed the user-visible faults that made the new build feel broken: archives now
unpack automatically instead of hanging on “Extracting”, the download schedule and the
MEGA quota cooldown are honoured before a start is accepted, changing the web/streaming
port or password now really rebinds the listener, timed-out browser sessions are pruned and
CSRF tokens are rotated on login/logout, a second launch without arguments brings the
running window forward again, `mega://` activations arriving at cold start are queued
instead of dropped, clipboard intake retries after a failure instead of poisoning that
clipboard text, the settings page applies the theme on save and gained the global speed
limit / start-with-Windows / VLC folder options, engine warnings reach the UI instead of
only the log, and the streaming library page finally has a working **Play** button.
The regression harness grew to 1143 assertions, including 22 new ones that cover the
extraction worker, the schedule guard, the web-server reconfiguration, the single-instance
spool and the session lifecycle.

## Build

```bash
dotnet build MegaDownloader.slnx
```

Run the new UI:

```bash
dotnet run --project MegaDownloader.UI/MegaDownloader.UI.csproj
```

Publish a self-contained Windows build (single file, no .NET runtime needed):

```bash
dotnet publish MegaDownloader.UI/MegaDownloader.UI.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
  -o dist/win-x64
```

The result is `dist/win-x64/MegaDownloader.exe` (~67 MB, no runtime dependency).

Run the legacy WinForms app:

```bash
dotnet run --project MegaDownloader.Full/MegaDownloader.Full.csproj
```

## Roadmap

0. Git baseline + Avalonia skeleton (DI, theme tokens, navigation, logging)
1. Extract the download engine and settings into `MegaDownloader.Core`
2. Rebuild the download queue in Avalonia (virtualized list, live progress) — done
3. Port the remaining dialogs and settings tabs — done (add links, properties, encode/ELC, streaming library, settings tabs)
4. Replace the embedded HTTP server and remaining Windows-only helpers — done
5. Package for Windows, macOS and Linux

The WinForms-only state is preserved as tag `winforms-baseline` on the original
history. See [MIGRATION.md](MIGRATION.md) for conversion details and known gaps, and
[PLAN.md](PLAN.md) for the live phase checklist and current progress.
