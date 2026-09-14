# MegaDownloader Revival Project

**Languages**: **English** · [简体中文](docs/README.zh-CN.md) · [繁體中文](docs/README.zh-TW.md) · [日本語](docs/README.ja-JP.md) · [한국어](docs/README.ko-KR.md)

> Restoring the classic MEGA Downloader to working order. Based on a decompiled version of v1.8 source code, with over 60 fixes implemented.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://dotnet.microsoft.com/)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![Language](https://img.shields.io/badge/Language-VB.NET-005a9c.svg)](https://docs.microsoft.com/dotnet/visual-basic/)
[![Build](https://github.com/a1175815821/MegaDownloader-Revival/actions/workflows/build.yml/badge.svg)](../../actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/a1175815821/MegaDownloader-Revival?include_prereleases)](../../releases/latest)
[![Downloads](https://img.shields.io/github/downloads/a1175815821/MegaDownloader-Revival/total)](../../releases)
[![Stars](https://img.shields.io/github/stars/a1175815821/MegaDownloader-Revival?style=social)](../../stargazers)

> ⭐ If this project helps you, please consider giving it a Star in the top right!

---

## What Is This?

MegaDownloader is a MEGA cloud storage download manager created by Spanish developer **Andres Soliño**, known for being lightweight, stable, and supporting multithreading. Maintenance of the original project ceased after v1.8, and MEGA has since changed its link format (`mega.nz/file/...`, `mega.nz/folder/...`), so older versions cannot recognize the new links, rendering core functions inoperable.

This repository is a project to revive it: we decompiled v1.8 to obtain the source code, and have since fixed and refactored it based on that foundation.

**Current version: v2.5.0**. See [CHANGELOG](docs/CHANGELOG.md) for the complete change log.

> ⚠️ **Legal Notice**: This project is derived from the decompilation of third-party published software, with the sole purpose of fixing compatibility issues to restore its usability. If the original author believes this repository infringes upon their rights, please contact us via an Issue, and we will cooperate to resolve the matter.

---

## Quick Start

1. Download from [Releases](../../releases)—choose one of the following:
   - **`MegaDownloader-Revival-win-x86.zip`** —— Portable version. Extract to any directory and double-click `MegaDownloader.exe`
   - **`MegaDownloader.exe`** —— Single-file version. The 12 required DLLs are already embedded; simply double-click after downloading—no need to unzip
2. Copy the MEGA link; the program will automatically detect the clipboard content
3. You can also click **Add Link** on the toolbar to paste manually, or drag the link into the main window
4. Configure the download directory, number of concurrent downloads, and speed limit in **Settings**
5. Switch between dark and light themes in **Settings → General → Theme** (changes take effect immediately after saving)

### Link Examples

New format (supported starting with v1.9):

```
https://mega.nz/file/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/folder/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
```

Old format (still supported):

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
| Multi-threaded Download | Multiple concurrent connections for the same file, significantly boosting speed |
| Resume Downloads | Supports pausing, resuming, and retrying after errors |
| Speed Limit | Global or per-task speed limit |
| Clipboard Monitoring | Automatically detects MEGA links copied to the clipboard |
| Drag-and-Drop Support | Drag links to the main window to add them to the queue |
| MEGA Folders | Recursively parses and downloads entire shared folders; supports downloading only specified subfolders or files |
| Encrypted Links | Supports multiple formats: `enc`, `enc2`, `fenc`, `fenc2`, and `elc` |
| ELC Containers | Import and export encrypted link containers |
| Streaming Playback | Integrated VLC, play while downloading |
| Web Interface | Built-in HTTP server for remote management via browser; supports enabling LAN access |
| Streaming Media Library | Visual management of streaming media resources |
| Stegano | Encoding and decoding of images and videos using steganography |
| Automatic Decompression | Based on SharpCompress; supports RAR / 7Z / ZIP |
| Quota Circuit Breaker | Automatically pauses and displays a countdown when MEGA quota is exhausted; automatically resumes upon expiration (v2.5) |
| Failure Self-Healing | Scheduled automatic retries for failed tasks; permanently failed tasks are automatically excluded (v2.5) |
| Multilingual Interface | Supports 10 languages, expandable |
| Dark/Light Themes | Follows system settings or can be switched manually; Auto mode adjusts in real time |

### Supported Link Formats

- `mega.nz/#...!FileID!FileKey` (Legacy)
- `mega.nz/file/FileID#FileKey` (New Version)
- `mega.nz/folder/FolderID#FolderKey` (New Version Folder)
- MEGA URI Protocol: `mega://#!...`, `mega://enc?...`, `mega://elc?...`

> Starting with v2.0, support for the following discontinued services has been removed: MegaCrypter, YouPaste, LinkCrypter, EncrypterMe.ga, and goo.gl short links.

---

## Build

### System Requirements

- Visual Studio 2019 / 2022
- .NET Framework 4.8 SDK (installed with Visual Studio)
- Windows 7 SP1 or later

### Build Configuration

| Configuration | Description |
| --- | --- |
| `Debug` | Debug version, output to `bin/Debug/` |
| `Release` | Release version, output to `bin/Release/` |
| `Debug_MSD` | Debug version of MegaSearchDesktop integration |
| `Release_MSD` | Release version of MegaSearchDesktop integrated edition, output to `Resources/Installer MSD/` |

### Steps

```bash
git clone https://github.com/a1175815821/MegaDownloader-Revival.git
cd MegaDownloader-Revival
# 用 Visual Studio 打开 MegaDownloader.sln，然后 Ctrl+Shift+B
```

Command-line build:

```bash
msbuild MegaDownloader.sln /p:Configuration=Release /p:Platform=x86
```

The build artifacts are located in `bin/<Configuration>/MegaDownloader.exe`.

### Technology Stack

VB.NET · .NET Framework 4.8 · WinForms · BouncyCastle (encryption) · Newtonsoft.Json · ObjectListView · SharpCompress (decompression) · HttpServer/Fadd (Web) · F5Lib (steganography)

---

## Project Structure

```
MegaDownloader/
├── Clases/                 # 核心类库（加密、下载、配置、主题、更新）
├── Controls/               # 自定义控件
├── Forms/                  # WinForms 窗体（12 个）
├── HttpModule/             # 内置 Web 服务器模块与 HTML 模板
├── Stegano/                # 隐写术窗体
├── Resources/
│   ├── DLLs/               # 第三方 DLL 依赖
│   ├── Language/           # 多语言 XML（10 种）
│   └── Installer MSD/      # WiX 安装包工程
├── docs/                   # Docs (README / CHANGELOG / CONTRIBUTING)
├── My Project/             # VS 项目元数据
└── MegaDownloader.sln
```

See [CONTRIBUTING](docs/CONTRIBUTING.md) for the complete directory tree and file descriptions.

---

## Supported Languages

The software interface includes 10 languages:

| Language | File |
| --- | --- |
| English | `en-US-Language.xml` |
| Español | `es-ES-Language.xml` |
| Simplified Chinese | `zh-CN-Language.xml` |
| Traditional Chinese | `zh-TW-Language.xml` |
| Français | `fr-FR-Language.xml` |
| Deutsch | `de-DE-Language.xml` |
| Italiano | `it-IT-Language.xml` |
| Português (Brasil) | `pt-BR-Language.xml` |
| Magyar | `hu-HU-Language.xml` |
| Română | `ro-RO-Language.xml` |

To add a language or improve an existing translation, see [CONTRIBUTING](docs/CONTRIBUTING.md).

---

## Documentation

| Documentation | Content |
| --- | --- |
| [CHANGELOG](docs/CHANGELOG.md) | Complete version history and details of fixes in each version |
| [CONTRIBUTING](docs/CONTRIBUTING.md) | Contribution process, coding style, and release process |

This documentation is available in multiple languages. All versions are maintained by hand — when you update one language, please update the others as well:

| Language | README | CHANGELOG | CONTRIBUTING | |
| --- | --- | --- | --- | --- |
| English | `README.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` | Manually maintained |
| Simplified Chinese | `docs/README.zh-CN.md` | `docs/CHANGELOG.zh-CN.md` | `docs/CONTRIBUTING.zh-CN.md` | **Authoritative Source** |
| Traditional Chinese | `README.zh-TW.md` | `docs/CHANGELOG.zh-TW.md` | `docs/CONTRIBUTING.zh-TW.md` | Manually maintained |
| Japanese | `README.ja-JP.md` | `docs/CHANGELOG.ja-JP.md` | `docs/CONTRIBUTING.ja-JP.md` | Manually maintained |
| Korean | `README.ko-KR.md` | `docs/CHANGELOG.ko-KR.md` | `docs/CONTRIBUTING.ko-KR.md` | Manually maintained |

> Simplified Chinese is the authoritative source; the source file itself is the Simplified Chinese version, so no separate `README.zh-CN.md` exists at the repository root.

---

## Acknowledgments

- Original MegaDownloader author **Andres Soliño**
- Revival Project maintainer **Yingxue** (v2.0+)
- Open-source dependencies: [BouncyCastle](https://www.bouncycastle.org/) · [Newtonsoft.Json](https://www.newtonsoft.com/json) · [SharpCompress](https://github.com/adamhathcock/sharpcompress) · [ObjectListView](http://objectlistview.sourceforge.net/) · [7-Zip](https://www.7-zip.org/) · [mpress](https://www.matcode.com/mpress.htm)

## License

Released under [MIT License](LICENSE). Original copyright © 2018 Andres Soliño; copyright for the Revival Project fixes © 2026 MegaDownloader Revival Project contributors.

> Third-party DLLs in this repository are subject to their respective original licenses; users are responsible for verifying compliance.
