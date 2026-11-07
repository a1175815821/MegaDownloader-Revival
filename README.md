<h1 align="center">MegaDownloader Revival</h1>

<p align="center">
  A portable MEGA download manager for Windows.<br>
  Resume interrupted transfers, preview shared folders, and keep large queues moving.
</p>

<p align="center">
  <a href="https://github.com/a1175815821/MegaDownloader-Revival/releases/latest"><img src="https://img.shields.io/badge/Download-Latest%20Release-FF6B1F?style=for-the-badge&logo=windows&logoColor=white" alt="Download the latest release"></a>
</p>

<p align="center">
  Windows 7 SP1+ · .NET Framework 4.8 · No installer or administrator rights
</p>

<p align="center">
  <strong>English</strong> · <a href="docs/README.zh-CN.md">简体中文</a> · <a href="docs/README.ru-RU.md">Русский</a>
</p>

<p align="center">
  <a href="#quick-start">Quick start</a> · <a href="#features">Features</a> · <a href="#faq">FAQ</a> · <a href="#build-and-contribute">Build &amp; contribute</a>
</p>

<p align="center"><sub>Unofficial project; not affiliated with MEGA (MEGA Ltd.).</sub></p>

## Quick start

1. Open the [latest release](https://github.com/a1175815821/MegaDownloader-Revival/releases/latest) and choose a download:
   - **`MegaDownloader.exe`** — a single executable with its dependency DLLs embedded.
   - **`MegaDownloader-Revival-win-x86.zip`** — a portable archive; extract it before running the executable.
2. Run the program. Windows 7 SP1 or later and .NET Framework 4.8 are required.
3. Choose **Add Link**, paste a MEGA link, select the files you want, and confirm. You can also use clipboard monitoring or drag and drop.

## Interface

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/main-dark.png">
    <img src="docs/screenshots/main-light.png" alt="MegaDownloader workbench showing a sample download queue" width="100%">
  </picture>
</p>

<p align="center"><sub>Example queue shown in Simplified Chinese. The interface supports 11 languages and light and dark themes.</sub></p>

[Add links](docs/screenshots/add-links.png) · [Select files](docs/screenshots/preflight.png) · [More screenshots](docs/screenshots/README.md)

## Features

- **Reliable downloads** — multiple connections per file, pause and resume across restarts, and automatic retries for failed tasks.
- **Quota recovery** — pause when MEGA's transfer quota is exhausted and attempt to resume after the countdown.
- **Folder and batch downloads** — inspect shared folders, search and select files, and check their total size before downloading.
- **Queue controls** — search by name or path, set global or per-task speed limits, and schedule a one-off or daily download window.
- **Automatic extraction and verification** — extract RAR, 7Z and ZIP archives without overwriting existing files; verify downloads when a MetaMAC is available.
- **More ways to use it** — add links from the clipboard, by drag and drop or through `mega://`; import and export ELC containers; manage downloads through the built-in web interface.
- **Extras** — stream with a configured VLC installation while downloading, manage streaming sources, and use the included steganography tools.

<details>
<summary>Supported link formats</summary>

- Current: `mega.nz/file/ID#KEY` and `mega.nz/folder/ID#KEY`, including links to files and subfolders within a share.
- Legacy: `mega.nz/#!ID!KEY` and `mega.co.nz/#F!ID!KEY`.
- Encrypted: `mega://enc`, `mega://enc2`, `mega://fenc`, `mega://fenc2` and `mega://elc` links, plus ELC containers.

Support for MegaCrypter, YouPaste, LinkCrypter, EncrypterMe.ga and goo.gl short links was removed in v2.0.

</details>

## Latest update

**Version 2.6.2** moves queue recovery into the background, protects existing files when tasks are deleted, improves resume and retry behavior, and adds Russian to the interface. See the [latest release notes](https://github.com/a1175815821/MegaDownloader-Revival/releases/latest) or the [full changelog](docs/CHANGELOG.md) (Simplified Chinese).

## FAQ

### What happens when the MEGA quota is exhausted?

The app pauses the queue and attempts to resume after the countdown. MEGA controls the quota; the legacy continuous-retry mode is available in Settings and is off by default.

### Where are settings and the download queue stored?

In `%LOCALAPPDATA%\MegaDownloader\`. Back up that directory before reinstalling Windows; moving the program folder does not move your settings.

### Can I run it on macOS or Linux?

The application runs on Windows. Its optional web interface lets you manage downloads from a browser on another device.

### Will scheduled downloads wake my computer?

No. The application must be running, and the computer must be awake during the scheduled window.

## Build and contribute

Build on Windows with Visual Studio 2019 or 2022 and the .NET Framework 4.8 developer pack. From a **Developer PowerShell for Visual Studio**:

```powershell
git clone https://github.com/a1175815821/MegaDownloader-Revival.git
cd MegaDownloader-Revival
msbuild MegaDownloader.sln /p:Configuration=Release /p:Platform=x86
```

The executable is written to `bin/Release/MegaDownloader.exe`. To run the regression checks, install the .NET SDK and run:

```powershell
.\Tests\Run-RegressionTests.ps1
```

See the [contributing guide](docs/CONTRIBUTING.md), [regression test guide](Tests/README.md) (Simplified Chinese), and [code of conduct](CODE_OF_CONDUCT.md). Bug reports and translation improvements are welcome through [Issues](https://github.com/a1175815821/MegaDownloader-Revival/issues). For vulnerabilities, please follow [SECURITY.md](SECURITY.md).

## About and license

MegaDownloader was originally created by Andres Soliño. This revival is based on decompiled v1.8 source and maintained to restore compatibility with current MEGA links. Thanks to the original author, maintainer Yingxue, and everyone who has contributed fixes, reports, and translations.

The project is distributed under the [MIT License](LICENSE); third-party components have their own licenses. See [NOTICE](NOTICE) for attribution and the project's origin. If the original author believes this repository infringes their rights, please contact the maintainers through an Issue; we will cooperate to resolve the matter.

<p align="center"><sub>If this project helps you, consider giving it a ⭐.</sub></p>
