# MegaDownloader Revival

> Making the classic MEGA downloader usable again. Rebuilt from the decompiled v1.8 source, with 60+ fixes applied.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://dotnet.microsoft.com/)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![Language](https://img.shields.io/badge/Language-VB.NET-005a9c.svg)](https://docs.microsoft.com/dotnet/visual-basic/)
[![Build](https://github.com/a1175815821/MegaDownloader-Revival/actions/workflows/build.yml/badge.svg)](../../actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/a1175815821/MegaDownloader-Revival?include_prereleases)](../../releases/latest)
[![Downloads](https://img.shields.io/github/downloads/a1175815821/MegaDownloader-Revival/total)](../../releases)
[![Stars](https://img.shields.io/github/stars/a1175815821/MegaDownloader-Revival?style=social)](../../stargazers)

<!-- i18n:nav -->
**Languages**: **English** · [简体中文](docs/README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja-JP.md) · [한국어](README.ko-KR.md)
<!-- /i18n:nav -->

---

## What is this

MegaDownloader is a MEGA download manager by the Spanish developer **Andres Soliño**, known for being lightweight, stable, and multi-threaded. The original project was abandoned after v1.8, and since MEGA has since changed its link format (`mega.nz/file/...`, `mega.nz/folder/...`), the old build can no longer recognize new links — its core functionality is broken.

This repository is a revival project: the v1.8 source was recovered by decompilation and is being repaired and refactored from there.

**Current version: v2.5 RC2** (test pre-release). See the [CHANGELOG](docs/CHANGELOG.md) for the full history.

> ⚠️ **Legal notice**: This project originates from the decompilation of third-party published software, solely for the purpose of fixing compatibility problems and restoring its usability. If the original author believes this repository infringes their rights, please reach out via an Issue and we will cooperate.

---

## Quick start

1. Download from [Releases](../../releases) — pick one:
   - **`MegaDownloader-Revival-win-x86.zip`** — portable build. Extract anywhere and run `MegaDownloader.exe`
   - **`MegaDownloader.exe`** — single-file build. All 12 dependency DLLs are embedded, so just double-click the downloaded file — no extraction needed
2. Copy a MEGA link; the app picks it up from the clipboard automatically
3. Or click **Add links** in the toolbar to paste manually, or drag a link into the main window
4. Configure the download folder, concurrency, and speed limit under **Settings**
5. Switch between dark/light themes under **Settings → General → Theme** (applies immediately on save)

### Example links

New format (supported since v1.9):

```
https://mega.nz/file/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/folder/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
```

Legacy format (still supported):

```
https://mega.nz/#!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
https://mega.co.nz/#F!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
```

Encrypted links:

```
mega://enc?_xlPqemSILarh5VBKbhSTFyQQQ0
mega://enc2?abcDEFgh-IjklMNop
```

---

## Features

| Feature | Description |
| --- | --- |
| Multi-threaded downloads | Multiple concurrent connections per file for much higher throughput |
| Resume support | Pause, resume, and retry on error |
| Speed limiting | Global or per-task throttling |
| Clipboard monitoring | Automatically detects MEGA links copied to the clipboard |
| Drag & drop | Drop a link onto the main window to queue it |
| MEGA folders | Recursively parses and downloads an entire shared folder; can download only selected subfolders/files |
| Encrypted links | Supports `enc` / `enc2` / `fenc` / `fenc2` / `elc` formats |
| ELC containers | Import and export encrypted link containers |
| Streaming playback | Integrated VLC — watch while downloading |
| Web interface | Built-in HTTP server for remote management from a browser; optional LAN access |
| Streaming library | Visual management of streaming resources |
| Stegano steganography | Steganographic encoding/decoding for images and video |
| Auto-extract | Powered by SharpCompress; supports RAR / 7Z / ZIP |
| Quota circuit breaker | Auto-pauses with a countdown when the MEGA quota is exhausted, then resumes (v2.5) |
| Failure self-healing | Failed tasks are retried on a timer; permanently failed ones are excluded (v2.5) |
| Multi-language UI | 10 languages, extensible |
| Dark/light theme | Follow the system or switch manually; Auto mode tracks in real time |

### Supported link formats

- `mega.nz/#...!FileID!FileKey` (legacy)
- `mega.nz/file/FileID#FileKey` (new)
- `mega.nz/folder/FolderID#FolderKey` (new folder)
- MEGA URI protocol: `mega://#!...`, `mega://enc?...`, `mega://elc?...`

> Support for the following discontinued services was removed in v2.0: MegaCrypter, YouPaste, LinkCrypter, EncrypterMe.ga, and goo.gl short links.

---

## Building

### Requirements

- Visual Studio 2019 / 2022
- .NET Framework 4.8 SDK (installed with Visual Studio)
- Windows 7 SP1 or later

### Build configurations

| Configuration | Description |
| --- | --- |
| `Debug` | Debug build, output to `bin/Debug/` |
| `Release` | Release build, output to `bin/Release/` |
| `Debug_MSD` | Debug build with MegaSearchDesktop integration |
| `Release_MSD` | Release build with MegaSearchDesktop integration, output to `Resources/Installer MSD/` |

### Steps

```bash
git clone https://github.com/a1175815821/MegaDownloader-Revival.git
cd MegaDownloader-Revival
# Open MegaDownloader.sln in Visual Studio, then press Ctrl+Shift+B
```

Command-line build:

```bash
msbuild MegaDownloader.sln /p:Configuration=Release /p:Platform=x86
```

The output lands in `bin/<Configuration>/MegaDownloader.exe`.

### Tech stack

VB.NET · .NET Framework 4.8 · WinForms · BouncyCastle (crypto) · Newtonsoft.Json · ObjectListView · SharpCompress (extraction) · HttpServer/Fadd (web) · F5Lib (steganography)

---

## Project structure

```
MegaDownloader/
├── Clases/                 # Core class library (crypto, download, config, theme, updater)
├── Controls/               # Custom controls
├── Forms/                  # WinForms forms (12)
├── HttpModule/             # Built-in web server modules and HTML templates
├── Stegano/                # Steganography forms
├── Resources/
│   ├── DLLs/               # Third-party DLL dependencies
│   ├── Language/           # Multi-language XML (10 languages)
│   └── Installer MSD/      # WiX installer project
├── docs/                   # Documentation (changelog, contributing)
├── My Project/             # VS project metadata
└── MegaDownloader.sln
```

See [CONTRIBUTING](docs/CONTRIBUTING.md) for the full directory tree and the purpose of each file.

---

## Supported languages

The app UI ships with 10 languages:

| Language | File |
| --- | --- |
| English | `en-US-Language.xml` |
| Español | `es-ES-Language.xml` |
| 简体中文 | `zh-CN-Language.xml` |
| 繁體中文 | `zh-TW-Language.xml` |
| Français | `fr-FR-Language.xml` |
| Deutsch | `de-DE-Language.xml` |
| Italiano | `it-IT-Language.xml` |
| Português (Brasil) | `pt-BR-Language.xml` |
| Magyar | `hu-HU-Language.xml` |
| Română | `ro-RO-Language.xml` |

To add a language or improve an existing translation, see [CONTRIBUTING](docs/CONTRIBUTING.md#adding-a-new-ui-language).

### Documentation languages

This documentation is available in several languages. The **Simplified Chinese files under `docs/` are the only hand-maintained copies**; every other language is generated automatically by GitHub Actions.

| Language | README | CHANGELOG | CONTRIBUTING | |
| --- | --- | --- | --- | --- |
| English | `README.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` | generated |
| 简体中文 | `docs/README.zh-CN.md` | `docs/CHANGELOG.zh-CN.md` | `docs/CONTRIBUTING.zh-CN.md` | **source** |
| 繁體中文 | `README.zh-TW.md` | `docs/CHANGELOG.zh-TW.md` | `docs/CONTRIBUTING.zh-TW.md` | generated |
| 日本語 | `README.ja-JP.md` | `docs/CHANGELOG.ja-JP.md` | `docs/CONTRIBUTING.ja-JP.md` | generated |
| 한국어 | `README.ko-KR.md` | `docs/CHANGELOG.ko-KR.md` | `docs/CONTRIBUTING.ko-KR.md` | generated |

> Simplified Chinese is the authoritative source, so there is no separate generated `README.zh-CN.md` — the source file *is* the Simplified Chinese edition. The generated files are not committed until the translation workflow runs for the first time. See [i18n/README.md](i18n/README.md) for how the pipeline works and how to add a language.

---

## Documentation

| Document | Contents |
| --- | --- |
| [CHANGELOG](docs/CHANGELOG.md) | Full version history with per-release fix details |
| [CONTRIBUTING](docs/CONTRIBUTING.md) | Contribution workflow, code style, release process |
| [i18n/README.md](i18n/README.md) | How the multilingual documentation pipeline works |

---

## Credits

- Original MegaDownloader author: **Andres Soliño**
- Revival project maintainer: **Yingxue** (v2.0+)
- Open-source dependencies: [BouncyCastle](https://www.bouncycastle.org/) · [Newtonsoft.Json](https://www.newtonsoft.com/json) · [SharpCompress](https://github.com/adamhathcock/sharpcompress) · [ObjectListView](http://objectlistview.sourceforge.net/) · [7-Zip](https://www.7-zip.org/) · [mpress](https://www.matcode.com/mpress.htm)

## License

Released under the [MIT License](LICENSE). Original copyright © 2018 Andres Soliño; revival fixes copyright © 2026 MegaDownloader Revival Project contributors.

> Third-party DLLs bundled in this repository remain under their respective original licenses; users are responsible for verifying compliance.
