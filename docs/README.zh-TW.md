# MegaDownloader 復活計畫

**語言**：[English](../README.md) · [简体中文](README.zh-CN.md) · **繁體中文** · [日本語](README.ja-JP.md) · [한국어](README.ko-KR.md)

> 讓經典 MEGA 下載器在今天的 MEGA 上繼續可用：支援新版連結格式、多執行緒加速、中斷點續傳、配額自動恢復，單一檔案免安裝。
> 非官方專案，與 MEGA（MEGA Ltd.）無關聯。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](../LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://dotnet.microsoft.com/)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![Language](https://img.shields.io/badge/Language-VB.NET-005a9c.svg)](https://docs.microsoft.com/dotnet/visual-basic/)
[![Build](https://github.com/a1175815821/MegaDownloader-Revival/actions/workflows/build.yml/badge.svg)](../../../actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/a1175815821/MegaDownloader-Revival?include_prereleases)](../../../releases/latest)
[![Downloads](https://img.shields.io/github/downloads/a1175815821/MegaDownloader-Revival/total)](../../../releases)
[![Stars](https://img.shields.io/github/stars/a1175815821/MegaDownloader-Revival?style=social)](../../../stargazers)

> ⭐ 如果這個專案幫你省了時間，點一下右上角 Star —— 這是獨立開源專案被更多人看到的方式。

## 截圖

| 淺色主題 | 深色主題 |
| --- | --- |
| ![主介面（淺色主題）](screenshots/main-light.png) | ![主介面（深色主題）](screenshots/main-dark.png) |

---

## ⚡ 30 秒上手

1. 從 [Releases](../../../releases/latest) 下載，二選一：

   | 檔案 | 適合你，如果…… |
   | --- | --- |
   | **`MegaDownloader.exe`**（單一檔案） | 想要最省事：下載後直接雙擊執行，15 個依賴 DLL 已內嵌 |
   | **`MegaDownloader-Revival-win-x86.zip`**（免安裝包） | 求穩妥：解壓縮到任意目錄執行。若防毒軟體對單一檔案版誤報，請選這個 |

2. 雙擊執行 —— 免安裝、無需系統管理員權限
   > 系統需求：Windows 7 SP1 以上；.NET Framework 4.8（Windows 10 1903+ 已內建，舊系統依提示安裝即可）
3. 複製一個 MEGA 連結 —— 程式會自動辨識剪貼簿，確認後即加入下載佇列

到這一步就能正常下載了。往下依需求閱讀：[功能](#-它能做什麼) → [專案背景與對比](#-這個專案從哪來) → [常見問題](#-常見問題) → [開發者指南](#️-給開發者)。

其他加入連結的方式：把連結拖進主視窗 · 工具列「加入連結」手動貼上 · 瀏覽器中點擊 `mega://` 協定連結。

> 🆕 **最新版本 [v2.5.9](../../../releases/latest)**（2026-10-28）：同名並行下載互不干擾 · 解壓縮不再覆蓋原檔 · 已下載成品驗證後重用 · 線上播放 Range 位元組錯誤修復。完整歷史見 [CHANGELOG](CHANGELOG.md)。

---

## 🚀 它能做什麼

**下載核心**

- **多執行緒加速** —— 同一檔案多路並發連線，吃滿頻寬
- **中斷點續傳** —— 暫停 / 恢復 / 錯誤重試，重啟程式接著下
- **資料夾下載** —— 遞迴解析整個分享資料夾，可只選其中部分子資料夾或檔案
- **限速** —— 全域或單一任務限速
- **失敗自癒**（v2.5）—— 失敗任務定時自動重試，永久失敗自動排除
- **配額熔斷**（v2.5）—— MEGA 配額耗盡時自動暫停並倒數，時間到自動恢復；也可切換回舊版「配額突破」持續重試模式（預設關閉）
- **自動解壓縮** —— 下完自動解壓 RAR / 7Z / ZIP，支援分卷與密碼

**便捷與遠端**

- **四種加入連結方式** —— 剪貼簿自動辨識 / 拖曳 / 手動貼上 / `mega://` 協定
- **Web 介面** —— 內建 HTTP 服務，瀏覽器遠端加入任務、檢視進度；可開啟區域網路存取，用手機推送下載
- **ELC 容器** —— 加密連結容器的匯入與匯出；`enc` / `enc2` / `fenc` / `fenc2` / `elc` 全支援

**介面與體驗**

- **邊下邊播** —— 整合 VLC，串流播放尚未下完的影片
- **串流媒體庫** —— 視覺化管理常看的串流媒體資源
- **深 / 淺色主題**（v2.0）—— 跟隨系統自動切換，也可手動指定
- **10 種介面語言** —— English · Español · 简体中文 · 繁體中文 · Français · Deutsch · Italiano · Português (BR) · Magyar · Română，可自行擴充
- **隱寫工具** —— 用圖片 / 影片藏檔案（F5 隱寫）

<details>
<summary><b>支援的全部連結格式（點擊展開）</b></summary>

- 新版：`mega.nz/file/ID#KEY`、`mega.nz/folder/ID#KEY`（v1.9 起）
- 舊版：`mega.nz/#!ID!KEY`、`mega.co.nz/#F!ID!KEY`
- 加密：`mega://enc?…`、`mega://enc2?…`、`mega://fenc?…`、`mega://fenc2?…`、`mega://elc?…`

連結範例：

```
https://mega.nz/file/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/folder/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/#!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
mega://enc?_xlPqemSILarh5VBKbhSTFyQQQ0
```

> v2.0 起移除對已下線服務的支援：MegaCrypter、YouPaste、LinkCrypter、EncrypterMe.ga、goo.gl 短連結。

</details>

---

## 📖 這個專案從哪來

MegaDownloader 是西班牙開發者 **Andres Soliño** 製作的 MEGA 下載管理器，以輕量、穩定、多執行緒著稱。原專案在 v1.8 後停止維護，而 MEGA 更換了連結格式（`mega.nz/file/...`、`mega.nz/folder/...`），舊版無法辨識新連結，核心功能失效。

本儲存庫把它救了回來：反編譯 v1.8 取得原始碼，在此基礎上持續修復與重構 —— 至今累計 **200+ 項修復與強化**（當機、靜默丟資料、安全、相容性），並按語意化版本持續發布。

|  | 原版 v1.8（2018 年停更） | **本儲存庫 v2.5.x** |
| --- | --- | --- |
| 新版 `mega.nz/file` / `/folder` 連結 | ❌ 無法辨識 | ✅ v1.9 起支援 |
| MEGA 配額耗盡 | 當死、報錯 | ✅ 自動熔斷，倒數恢復（v2.5） |
| 失敗任務 | 只能手動重試 | ✅ 定時自癒（v2.5） |
| 介面主題 | 僅系統灰 | ✅ 深 / 淺色，跟隨系統（v2.0） |
| 穩定性 | — | ✅ 200+ 項當機 / 丟資料 / 安全修復 |
| 維護狀態 | 已停更 | ✅ 積極維護，Issue 有回應 |

> ⚠️ **法律聲明**：本專案源自對第三方已發佈軟體的反編譯，目的僅在於修復相容性問題以恢復其可用性。若原作者認為本儲存庫侵犯其權益，請透過 Issue 聯繫，我們將配合處理。

---

## ❓ 常見問題

**防毒軟體誤報？**
單一檔案版使用 mpress 加殼，少數防毒軟體會誤報。可改用 zip 版，或從原始碼自行建置 —— 所有官方 Release 均由 GitHub Actions 從原始碼自動建置，過程公開可查。

**提示配額不足 / quota exceeded？**
MEGA 對免費帳戶和 IP 有下載配額限制。v2.5 起程式會自動暫停並倒數恢復；等不及的話，可在設定中開啟「配額突破模式」持續重試。

**設定與下載佇列存在哪裡？**
`%LOCALAPPDATA%\MegaDownloader\`。程式目錄可以隨意搬動或刪除，設定不受影響；重灌系統前備份這個目錄即可。

**舊格式連結還能下嗎？**
能。新舊格式、`mega://` 加密連結、ELC 容器都支援，完整清單見上方摺疊區塊。

**需要系統管理員權限嗎？**
不需要。免安裝、免系統管理員權限。

**有 Mac / Linux 版嗎？**
沒有 —— 基於 WinForms，僅限 Windows。但可開啟內建 Web 介面，用手機或其他裝置的瀏覽器遠端管理下載。

---

## 🛠️ 給開發者

<details>
<summary><b>從原始碼建置（點擊展開）</b></summary>

**環境需求**：Visual Studio 2013 以上（解決方案為 VS2013 格式）· .NET Framework 4.8 SDK（隨 VS 安裝）· Windows 7 SP1+

```bash
git clone https://github.com/a1175815821/MegaDownloader-Revival.git
cd MegaDownloader-Revival
# 用 Visual Studio 開啟 MegaDownloader.sln，然後 Ctrl+Shift+B
```

命令列建置：

```bash
msbuild MegaDownloader.sln /p:Configuration=Release /p:Platform=x86
```

產物位於 `bin/<Configuration>/MegaDownloader.exe`。建置組態：`Debug` / `Release` / `Debug_MSD` / `Release_MSD`（MegaSearchDesktop 整合版，輸出到 `Resources/Installer MSD/`）。

回歸驗證：在 PowerShell 中執行 `.\Tests\Run-RegressionTests.ps1`（需要 .NET SDK）。測試使用暫存檔與本機 HTTP 服務，無需 MEGA 帳號。詳見 [Tests/README.md](../Tests/README.md)。

**技術堆疊**：VB.NET · .NET Framework 4.8 · WinForms · BouncyCastle（加密）· Newtonsoft.Json · ObjectListView · SharpCompress（解壓縮）· HttpServer/Fadd（Web）· F5Lib（隱寫）

</details>

<details>
<summary><b>專案結構（點擊展開）</b></summary>

```
MegaDownloader/
├── Clases/                 # 核心類別庫（加密、下載、設定、主題、更新）
├── Controls/               # 自訂控制項
├── Forms/                  # WinForms 表單（12 個）
├── HttpModule/             # 內建 Web 伺服器模組與 HTML 範本
├── Stegano/                # 隱寫術表單
├── Resources/
│   ├── DLLs/               # 第三方 DLL 依賴
│   ├── Language/           # 多語系 XML（10 種）
│   └── Installer MSD/      # WiX 安裝包專案
├── docs/                   # 文件（README / CHANGELOG / CONTRIBUTING）
├── Tests/                  # 下載、解壓縮與串流回歸驗證
└── MegaDownloader.sln
```

完整目錄樹與檔案說明見 [CONTRIBUTING](CONTRIBUTING.zh-CN.md)。

</details>

---

## 📚 文件

| 文件 | 內容 |
| --- | --- |
| [CHANGELOG](CHANGELOG.md) | 完整版本歷史與每版修復明細（僅簡體中文） |
| [CONTRIBUTING](CONTRIBUTING.zh-CN.md) | 貢獻流程、程式碼風格、發布流程 |

README 為全部五種語言手動維護；CONTRIBUTING 僅有英文與簡體中文兩個版本，CHANGELOG 僅有簡體中文版本。更新時請同步：

| 語言 | README | CHANGELOG | CONTRIBUTING |
| --- | --- | --- | --- |
| English | `README.md`（根目錄） | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |
| 简体中文 | `docs/README.zh-CN.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.zh-CN.md` |
| 繁體中文 | `docs/README.zh-TW.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |
| 日本語 | `docs/README.ja-JP.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |
| 한국어 | `docs/README.ko-KR.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |

> 簡體中文是權威源。

---

## ⭐ 支持這個專案

這個工具沒有官網、不做推廣，你的 Star 就是它被更多人看到的方式：

- ⭐ 點右上角 **Star** —— 讓搜尋「MEGA 下載器」的人找得到它
- 🐛 遇到問題[提 Issue](../../../issues) —— 近期的多個修復都來自使用者回報
- 🌍 幫忙改進[翻譯](CONTRIBUTING.zh-CN.md) —— 已有 10 種語言，缺母語校對
- 🔗 分享給還在找 MEGA 下載工具的朋友

<a href="https://star-history.com/#a1175815821/MegaDownloader-Revival&Date">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=a1175815821/MegaDownloader-Revival&type=Date&theme=dark" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=a1175815821/MegaDownloader-Revival&type=Date" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=a1175815821/MegaDownloader-Revival&type=Date" />
 </picture>
</a>

---

## 銘謝

- 原 MegaDownloader 作者 **Andres Soliño**
- 復活計畫維護者 **Yingxue**（v2.0+），以及所有提 Issue、補翻譯的貢獻者
- 開源依賴：[BouncyCastle](https://www.bouncycastle.org/) · [Newtonsoft.Json](https://www.newtonsoft.com/json) · [SharpCompress](https://github.com/adamhathcock/sharpcompress) · [ObjectListView](http://objectlistview.sourceforge.net/) · [7-Zip](https://www.7-zip.org/) · [mpress](https://www.matcode.com/mpress.htm)

## 授權

基於 [MIT License](../LICENSE) 發布。原始版權 © 2018 Andres Soliño，復活計畫修復版權 © 2026 MegaDownloader Revival Project 貢獻者。

> 儲存庫內第三方 DLL 遵循各自原始授權條款，使用者應自行確認合規性。
