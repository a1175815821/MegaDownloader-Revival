# MegaDownloader 復活計畫

**語言**：[English](README.md) · [简体中文](README.zh-CN.md) · **繁體中文** · [日本語](README.ja-JP.md) · [한국어](README.ko-KR.md)

> 讓經典 MEGA 下載器重新可用。基於 v1.8 反編譯原始碼修復而成，已完成 60+ 項修復。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://dotnet.microsoft.com/)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![Language](https://img.shields.io/badge/Language-VB.NET-005a9c.svg)](https://docs.microsoft.com/dotnet/visual-basic/)
[![Build](https://github.com/a1175815821/MegaDownloader-Revival/actions/workflows/build.yml/badge.svg)](../../../actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/a1175815821/MegaDownloader-Revival?include_prereleases)](../../../releases/latest)
[![Downloads](https://img.shields.io/github/downloads/a1175815821/MegaDownloader-Revival/total)](../../../releases)
[![Stars](https://img.shields.io/github/stars/a1175815821/MegaDownloader-Revival?style=social)](../../../stargazers)

> ⭐ 如果這個專案對你有幫助，歡迎點右上角 Star 支持一下！

---

## 這是什麼

MegaDownloader 是西班牙開發者 **Andres Soliño** 製作的 MEGA 網碟下載管理器，以輕量、穩定、支援多執行緒著稱。原專案在 v1.8 後停止維護，而 MEGA 已更換連結格式（`mega.nz/file/...`、`mega.nz/folder/...`），舊版因此無法識別新連結，核心功能失效。

本倉庫是它的復活計畫：反編譯 v1.8 拿到原始碼，在其基礎上修復與重構。

**目前版本：v2.5.1**。完整變更歷史見 [CHANGELOG](CHANGELOG.zh-TW.md)。

> ⚠️ **法律聲明**：本專案源自對第三方已發佈軟體的反編譯，目的僅在於修復相容性問題以恢復其可用性。若原作者認為本倉庫侵犯了其權益，請透過 Issue 聯繫，我們將配合處理。

---

## 快速開始

1. 從 [Releases](../../../releases) 下載，二選一：
   - **`MegaDownloader-Revival-win-x86.zip`** —— 綠色版。解壓縮到任意目錄，雙擊 `MegaDownloader.exe`
   - **`MegaDownloader.exe`** —— 單檔案版。12 個相依 DLL 已內嵌，下載後直接雙擊，無需解壓縮
2. 複製 MEGA 連結，程式自動識別剪貼簿內容
3. 也可點工具列 **添加連結** 手動貼上，或把連結拖進主視窗
4. 在 **設定** 中配置下載目錄、並發數、限速
5. 在 **設定 → 一般 → 主題** 中切換深/淺色（儲存後立即生效）

### 連結範例

新版格式（v1.9 起支援）：

```
https://mega.nz/file/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/folder/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
```

舊版格式（繼續支援）：

```
https://mega.nz/#!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
https://mega.co.nz/#F!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
```

加密連結：

```
mega://enc?_xlPqemSILarh5VBKbhSTFyQQQ0
mega://enc2?abcDEFgh-IjklMNop
```

---

## 功能特性

| 功能 | 說明 |
| --- | --- |
| 多執行緒下載 | 同一檔案多路並發連接，大幅提升速度 |
| 斷點續傳 | 支援暫停、恢復、錯誤重試 |
| 速度限制 | 全域或單任務限速 |
| 剪貼簿監控 | 自動識別複製到剪貼簿的 MEGA 連結 |
| 拖拽支援 | 拖連結到主視窗即可加入佇列 |
| MEGA 資料夾 | 遞迴解析並下載整個分享資料夾，支援只下載指定子資料夾/檔案 |
| 加密連結 | 支援 `enc` / `enc2` / `fenc` / `fenc2` / `elc` 多種格式 |
| ELC 容器 | 加密連結容器的匯入與匯出 |
| 串流播放 | 整合 VLC，邊下邊播 |
| Web 介面 | 內建 HttpServer，可瀏覽器遠端管理；支援開啟區域網路存取 |
| 串流媒體庫 | 視覺化管理串流資源 |
| Stegano 隱寫 | 圖片/影片的隱寫編碼與解碼 |
| 自動解壓縮 | 基於 SharpCompress，支援 RAR / 7Z / ZIP |
| 配額熔斷 | MEGA 配額耗盡時自動暫停並倒數計時，到點自動恢復（v2.5） |
| 失敗自癒 | 失敗任務定時自動重試，永久失敗自動排除（v2.5） |
| 多語言介面 | 支援 10 種語言，可擴展 |
| 深/淺色主題 | 跟隨系統或手動切換，Auto 模式即時跟隨 |

### 支援的連結格式

- `mega.nz/#...!FileID!FileKey`（舊版）
- `mega.nz/file/FileID#FileKey`（新版）
- `mega.nz/folder/FolderID#FolderKey`（新版資料夾）
- MEGA URI 協定：`mega://#!...`、`mega://enc?...`、`mega://elc?...`

> v2.0 起已移除對以下下線服務的支援：MegaCrypter、YouPaste、LinkCrypter、EncrypterMe.ga、goo.gl 短連結。

---

## 建置

### 環境要求

- Visual Studio 2019 / 2022
- .NET Framework 4.8 SDK（隨 Visual Studio 安裝）
- Windows 7 SP1 或更高

### 建置配置

| 配置 | 說明 |
| --- | --- |
| `Debug` | 調試版，輸出到 `bin/Debug/` |
| `Release` | 發佈版，輸出到 `bin/Release/` |
| `Debug_MSD` | 調試 MegaSearchDesktop 整合版 |
| `Release_MSD` | 發佈 MegaSearchDesktop 整合版，輸出到 `Resources/Installer MSD/` |

### 步驟

```bash
git clone https://github.com/a1175815821/MegaDownloader-Revival.git
cd MegaDownloader-Revival
# 用 Visual Studio 打開 MegaDownloader.sln，然後 Ctrl+Shift+B
```

命令列建置：

```bash
msbuild MegaDownloader.sln /p:Configuration=Release /p:Platform=x86
```

產物位於 `bin/<Configuration>/MegaDownloader.exe`。

### 技術棧

VB.NET · .NET Framework 4.8 · WinForms · BouncyCastle（加密）· Newtonsoft.Json · ObjectListView · SharpCompress（解壓縮）· HttpServer/Fadd（Web）· F5Lib（隱寫）

---

## 專案結構

```
MegaDownloader/
├── Clases/                 # 核心類別庫（加密、下載、配置、主題、更新）
├── Controls/               # 自訂控制項
├── Forms/                  # WinForms 視窗（12 個）
├── HttpModule/             # 內建 Web 伺服器模組與 HTML 樣板
├── Stegano/                # 隱寫術視窗
├── Resources/
│   ├── DLLs/               # 第三方 DLL 相依
│   ├── Language/           # 多語言 XML（10 種）
│   └── Installer MSD/      # WiX 安裝包工程
├── docs/                   # 文件（README / 變更日誌、貢獻指南）
├── My Project/             # VS 專案中繼資料
└── MegaDownloader.sln
```

完整的目錄樹與檔案用途說明見 [CONTRIBUTING](CONTRIBUTING.zh-TW.md)。

---

## 支援的語言

軟體介面自帶 10 種語言：

| 語言 | 檔案 |
| --- | --- |
| English | `en-US-Language.xml` |
| Español | `es-ES-Language.xml` |
| 簡體中文 | `zh-CN-Language.xml` |
| 繁體中文 | `zh-TW-Language.xml` |
| Français | `fr-FR-Language.xml` |
| Deutsch | `de-DE-Language.xml` |
| Italiano | `it-IT-Language.xml` |
| Português (Brasil) | `pt-BR-Language.xml` |
| Magyar | `hu-HU-Language.xml` |
| Română | `ro-RO-Language.xml` |

想加一門語言或改進現有翻譯，見 [CONTRIBUTING](CONTRIBUTING.zh-TW.md)。

---

## 文件

| 文件 | 內容 |
| --- | --- |
| [CHANGELOG](CHANGELOG.zh-TW.md) | 完整版本歷史與每版修復明細 |
| [CONTRIBUTING](CONTRIBUTING.zh-TW.md) | 貢獻流程、程式碼風格、發佈流程 |

本文檔提供多種語言。**所有語言版本均為手動維護**，更新某一語言時請同步更新其他語言：

| 語言 | README | CHANGELOG | CONTRIBUTING | |
| --- | --- | --- | --- | --- |
| English | `docs/README.md` | `CHANGELOG.md` | `CONTRIBUTING.md` | 手動維護 |
| 簡體中文 | `docs/README.zh-CN.md` | `CHANGELOG.zh-CN.md` | `CONTRIBUTING.zh-CN.md` | **權威源** |
| 繁體中文 | `docs/README.zh-TW.md` | `CHANGELOG.zh-TW.md` | `CONTRIBUTING.zh-TW.md` | 手動維護 |
| 日本語 | `docs/README.ja-JP.md` | `CHANGELOG.ja-JP.md` | `CONTRIBUTING.ja-JP.md` | 手動維護 |
| 한국어 | `docs/README.ko-KR.md` | `CHANGELOG.ko-KR.md` | `CONTRIBUTING.ko-KR.md` | 手動維護 |

> 簡體中文是權威源，源文件本身就是簡體中文版，所以沒有單獨的 `README.zh-CN.md`。

---

## 致謝

- 原 MegaDownloader 作者 **Andres Soliño**
- 復活計畫維護者 **Yingxue**（v2.0+）
- 開源相依：[BouncyCastle](https://www.bouncycastle.org/) · [Newtonsoft.Json](https://www.newtonsoft.com/json) · [SharpCompress](https://github.com/adamhathcock/sharpcompress) · [ObjectListView](http://objectlistview.sourceforge.net/) · [7-Zip](https://www.7-zip.org/) · [mpress](https://www.matcode.com/mpress.htm)

## 許可

基於 [MIT License](LICENSE) 發佈。原始版權 © 2018 Andres Soliño，復活計畫修復版權 © 2026 MegaDownloader Revival Project 貢獻者。

> 倉庫內第三方 DLL 遵循各自原始許可證，使用者應自行確認合規性。
