<p align="center">
  <img src="screenshots/banner.png" alt="MegaDownloader 復活計畫 —— 經典 MEGA 下載器，為今天的連結重建" width="100%">
</p>

<h1 align="center">MegaDownloader 復活計畫</h1>

<p align="center">
  <b>經典 MEGA 下載器，為今天的連結重建。</b><br>
  多執行緒加速 · 真斷點續傳 · 配額自動恢復 · 下載前批次預檢
</p>

<p align="center">
  <a href="../../../releases/latest"><img alt="下載最新發行版" src="https://img.shields.io/badge/Download-Latest%20Release-FF6B1F?style=for-the-badge&logo=windows&logoColor=white"></a>
  <a href="CHANGELOG.md"><img alt="更新日誌" src="https://img.shields.io/badge/Changelog-2.6-2D333B?style=for-the-badge&logo=gitbook&logoColor=white"></a>
</p>

<p align="center"><sub>如果這個專案幫到了你，歡迎<a href="https://github.com/a1175815821/MegaDownloader-Revival">點個 ⭐ Star</a>，讓更多人發現它。</sub></p>

<p align="center">
  <a href="../../../releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/a1175815821/MegaDownloader-Revival?style=flat-square&label=release&color=FF6B1F"></a>
  <a href="../../../releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/a1175815821/MegaDownloader-Revival/total?style=flat-square&label=downloads&color=4E8BFF"></a>
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows%207%20SP1%2B-0078D4?style=flat-square">
  <a href="../LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-3DA639?style=flat-square"></a>
  <a href="../../../stargazers"><img alt="Stars" src="https://img.shields.io/github/stars/a1175815821/MegaDownloader-Revival?style=flat-square&label=stars&color=FFC107"></a>
</p>

<p align="center">
  <sub>單檔 EXE 或 ZIP · 免安裝 · 免管理員 · .NET Framework 4.8 · 10 種介面語言</sub><br>
  <sub><a href="../README.md">English</a> · <a href="README.zh-CN.md">简体中文</a> · <b>繁體中文</b> · <a href="README.ja-JP.md">日本語</a> · <a href="README.ko-KR.md">한국어</a></sub>
</p>

> **認識 v2.6「工作台」。** 介面整體重做，新增連結收進兩步流程並支援批次預檢，新增任務搜尋與排程下載 —— 復活計畫啟動以來改動最大的一版，冷啟動同時降到 **150–200 毫秒**。[看看新在哪 ↓](#-26-新在哪)
>
> 非官方專案，與 MEGA（MEGA Ltd.）無關聯。

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="screenshots/v2.6-workbench-dark.png">
    <source media="(prefers-color-scheme: light)" srcset="screenshots/v2.6-workbench-light.png">
    <img alt="MegaDownloader v2.6 workbench" src="screenshots/v2.6-workbench-light.png" width="100%">
  </picture>
</p>
<p align="center"><sub>v2.6 工作台 · 截圖隨 GitHub 主題切換。介面顯示為簡體中文。</sub></p>

---

## ✨ 2.6 新在哪

> **一個視窗，兩步操作，不用猜。**
>
> ⚡ **冷啟動實測 5–7 秒 → 150–200 毫秒** · 500+ 個檔案匯入不卡介面
>
> 啟動時間因硬體和執行環境而異。

<table>
<tr>
<td width="50%" valign="top">

<b>🖥️ 工作台重做</b>

深淺色共用一套間距、字階與圖示網格。「新增連結」終於成了主操作，側欄分組各自計數，詳情欄可以收起，把寬度讓給清單。

</td>
<td width="50%" valign="top">

<b>➕ 兩步新增</b>

先在同一個視窗裡貼連結，再挑檔案，不再層層疊彈窗。按返回，連結、搜尋字、展開的目錄與勾選全都還在。

</td>
</tr>
<tr>
<td valign="top">

<b>🗂️ 批次預檢</b>

下載之前先看清檔名、大小、狀態與相對路徑。資料夾樹可折疊，支援名稱搜尋、類型篩選、Ctrl/Shift 多選，並顯示選取總大小與目標磁碟剩餘空間。

</td>
<td valign="top">

<b>🔎 任務搜尋</b>

<kbd>Ctrl</kbd>+<kbd>F</kbd> 搜尋任務包名、檔名或相對路徑，並與目前狀態分組疊加過濾；<kbd>Esc</kbd> 清空後恢復整個佇列。

</td>
</tr>
<tr>
<td valign="top">

<b>⏰ 排程下載</b>

一次預約或每日下載時段，支援跨午夜。時段開始自動恢復，結束時自動暫停，設定重啟後沿用。

</td>
<td valign="top">

<b>⚡ 啟動 200 毫秒</b>

冷啟動從 5–7 秒降到 150–200 毫秒：首屏不再同步讀效能計數器與 WMI。連結解析、建包與中繼資料檢查也移出了介面執行緒，預檢改為虛擬清單。

</td>
</tr>
</table>

<details>
<summary><b>2.6 的其餘改動與背後的修復</b></summary>

<br>

- **重複識別**：依檔案 ID + 金鑰識別新舊格式的重複連結，並檢查現有佇列；重複與失效項目一律跳過。
- **暫時故障與永久失效分開處理**：網路不穩導致檢查不了的普通連結，仍可手動勾選新增。
- **確認期間的新連結**先暫存到下一批，不會被靜默下載。
- **大型任務包**（超過 500 個檔案）預設折疊，匯入瞬間不會卡住清單。
- **新增完成後**自動切到全部任務並定位最新任務包；檔案樹保留仍可見列的高亮與捲動位置。
- **過程可見可停**：解析階段顯示已用時間、可停止 / 重檢；提交階段單獨顯示「加入下載佇列」。
- **DPI 與主題修復**：高 DPI 資料夾展開標記、詳情捲動後的版面偏移、原生清單背景與空態換膚、帶格式貼上不再夾帶網頁字色。
- **排程控制佇列**：在設定時段內恢復下載，時段結束後暫停；手動暫停整個佇列會持續到下一時段。

</details>

---

## ⚡ 30 秒上手

1. 從 [Releases](../../../releases/latest) 下載，二選一：

   | 檔案 | 適合你，如果…… |
   | --- | --- |
   | **`MegaDownloader.exe`**（單檔） | 想要最省事：下載後直接雙擊執行，15 個相依 DLL 已內嵌 |
   | **`MegaDownloader-Revival-win-x86.zip`**（免安裝包） | 喜歡壓縮包：解壓後執行 |

2. **雙擊執行** —— 免安裝、免管理員權限
   > Windows 7 SP1 以上 · .NET Framework 4.8（Windows 10 1903+ 已內建，舊系統依提示安裝）

3. **複製一個 MEGA 連結** —— 剪貼簿監控會自動識別，確認後即加入下載佇列

到這裡就能正常下載了。其他新增連結的方式：把連結拖進視窗 · 工具列「新增連結」 · 瀏覽器中點擊 `mega://` 協定連結。

---

## 🚀 它能做什麼

**下載核心**

- **多執行緒加速** —— 同一檔案多路並行連線，吃滿頻寬
- **真斷點續傳** —— 暫停 / 恢復 / 出錯重試，重啟程式接著下，中斷的半成品會被重用
- **資料夾下載** —— 遞迴解析整個分享資料夾，可只選其中部分子資料夾或檔案
- **限速** —— 全域或單任務限速
- **失敗自癒** —— 失敗任務定時自動重試，永久失敗自動排除（v2.5）
- **配額熔斷** —— MEGA 配額耗盡時自動暫停並倒數，到點自動恢復；舊版「配額突破」持續重試模式仍可開啟，預設關閉（v2.5）
- **自動解壓** —— 下完自動解壓 RAR / 7Z / ZIP，支援分卷與密碼，且不會覆蓋同名檔案
- **成品校驗** —— 連結包含 MetaMAC 時，下載檔案會先經過校驗，再標記為完成（v2.5.9）

**連結接入**

- **四種方式** —— 剪貼簿自動識別 · 拖放 · 手動貼上 · `mega://` 協定
- **批次預檢** —— 大型分享先看清再勾選，不再盲下（v2.6）
- **ELC 容器** —— 加密連結容器的匯入與匯出；`enc` / `enc2` / `fenc` / `fenc2` / `elc` 全支援

**工作台**

- **秒開** —— 冷啟動 150–200 毫秒，不再阻塞讀 WMI 與效能計數器（v2.6）
- **任務搜尋** —— 包名、檔名、路徑，`Ctrl+F` 直達（v2.6）
- **排程下載** —— 每日時段或一次預約（v2.6）
- **詳情欄可收起** —— 佇列統計、任務清單、詳情三段式，窄視窗自動讓出空間
- **深 / 淺色主題** —— 跟隨系統，也可手動指定
- **10 種介面語言** —— English · Español · 简体中文 · 繁體中文 · Français · Deutsch · Italiano · Português (BR) · Magyar · Română
- **Web 介面** —— 內建 HTTP 服務，瀏覽器遠端新增任務、查看進度；可開啟區域網路存取，用手機推送下載

**附加能力**

- **邊下邊播** —— 整合 VLC，串流播放尚未下完的影片
- **串流媒體庫** —— 視覺化管理常看的串流來源
- **隱寫工具** —— 用圖片 / 影片藏檔案（F5 隱寫）

<details>
<summary><b>支援的全部連結格式</b></summary>

<br>

- 新版：`mega.nz/file/ID#KEY`、`mega.nz/folder/ID#KEY`（v1.9 起），含分享內的子資料夾與單檔連結
- 舊版：`mega.nz/#!ID!KEY`、`mega.co.nz/#F!ID!KEY`
- 加密：`mega://enc?…`、`mega://enc2?…`、`mega://fenc?…`、`mega://fenc2?…`、`mega://elc?…`

```
https://mega.nz/file/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/folder/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/#!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
mega://enc?_xlPqemSILarh5VBKbhSTFyQQQ0
```

> v2.0 起移除對已下線服務的支援：MegaCrypter、YouPaste、LinkCrypter、EncrypterMe.ga、goo.gl 短連結。

</details>

---

## 🖼️ 介面

**兩步新增，下載前先看清**

<table>
<tr>
  <td align="center" width="50%"><b>1. 貼上連結</b><br><img src="screenshots/v2.6-add-links.png" alt="新增連結視窗" width="100%"></td>
  <td align="center" width="50%"><b>2. 預檢並選擇檔案</b><br><img src="screenshots/v2.6-preflight.png" alt="檔案預檢與選擇視窗" width="100%"></td>
</tr>
</table>

---

## 📖 這個專案從哪來

MegaDownloader 是西班牙開發者 **Andres Soliño** 製作的 MEGA 下載管理器，以輕量、穩定、多執行緒著稱。原專案在 v1.8 後停止維護，而 MEGA 更換了連結格式（`mega.nz/file/...`、`mega.nz/folder/...`），舊版無法識別新連結，核心功能失效。

本倉庫把它救了回來：反編譯 v1.8 取得原始碼，在此基礎上持續修復與重構 —— 至今累計 **200+ 項修復與加固**（崩潰、靜默遺失資料、安全、相容性），並依語意化版本持續發布。

|  | 原版 v1.8（2018 年停更） | **本倉庫 v2.6** |
| --- | --- | --- |
| 新版 `mega.nz/file` / `/folder` 連結 | ❌ 無法識別 | ✅ v1.9 起支援 |
| MEGA 配額耗盡 | 卡死、報錯 | ✅ 自動熔斷，倒數恢復（v2.5） |
| 失敗任務 | 只能手動重試 | ✅ 定時自癒（v2.5） |
| 新增大型分享 | 貼完就下，全憑運氣 | ✅ 批次預檢 + 資料夾樹（v2.6） |
| 找任務 | 整個佇列翻找 | ✅ 依包名 / 檔案 / 路徑搜尋（v2.6） |
| 無人值守下載 | — | ✅ 每日時段或一次預約（v2.6） |
| 啟動速度 | 首屏同步阻塞 | ✅ 冷啟動 150–200 毫秒，此前 5–7 秒（v2.6） |
| 介面主題 | 僅系統灰 | ✅ 深 / 淺色，跟隨系統（v2.0） |
| 穩定性 | — | ✅ 200+ 項崩潰 / 遺失資料 / 安全修復 |
| 維護狀態 | 已停更 | ✅ 活躍維護，Issue 有回應 |

> ⚠️ **法律聲明**：本專案源自對第三方已發布軟體的反編譯，目的僅在於修復相容性問題以恢復其可用性。若原作者認為本倉庫侵犯其權益，請透過 Issue 聯絡，我們將配合處理。

---

## ❓ 常見問題

**提示配額不足 / quota exceeded？**
MEGA 對免費帳戶與 IP 有下載配額限制。v2.5 起程式會自動暫停並倒數恢復；等不及的話，可在設定中開啟「配額突破模式」持續重試。

**設定與下載佇列存在哪？**
`%LOCALAPPDATA%\MegaDownloader\`。程式目錄可以隨意搬動或刪除，設定不受影響；重裝系統前備份這個目錄即可。

**舊格式連結還能下嗎？**
能。新舊格式、`mega://` 加密連結、ELC 容器都支援，完整清單見上方折疊區塊。

**需要管理員權限嗎？**
不需要。免安裝、免管理員。

**有 Mac / Linux 版嗎？**
沒有 —— 基於 WinForms，僅限 Windows。但可開啟內建 Web 介面，用手機或其他裝置的瀏覽器遠端管理下載。

**排程下載會喚醒電腦嗎？**
不會。程式必須保持執行，不註冊喚醒計時器、也不作為背景服務執行。

**發現安全性問題？**
請不要公開提 Issue，依 [SECURITY](../SECURITY.md) 的方式回報。

---

## 🛠️ 給開發者

<details>
<summary><b>從原始碼建置</b></summary>

<br>

**環境需求**：Visual Studio 2019 或 2022 · .NET Framework 4.8 開發人員套件· Windows 7 SP1+

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

回歸驗證：在 PowerShell 中執行 `.\Tests\Run-RegressionTests.ps1`（需要 .NET SDK）。測試使用暫存檔案與本機 HTTP 服務，無需 MEGA 帳號。詳見 [Tests/README.md](../Tests/README.md)。

**技術堆疊**：VB.NET · .NET Framework 4.8 · WinForms · BouncyCastle（加密）· Newtonsoft.Json · ObjectListView · SharpCompress（解壓）· HttpServer/Fadd（Web）· F5Lib（隱寫）

</details>

<details>
<summary><b>專案結構</b></summary>

<br>

```
MegaDownloader/
├── Clases/                 # 核心類別庫（加密、下載、設定、主題、預檢、排程）
├── Controls/               # 自訂控制項
├── Forms/                  # WinForms 視窗（工作台、兩步新增流程等）
├── HttpModule/             # 內建 Web 伺服器模組與 HTML 範本
├── Stegano/                # 隱寫術視窗
├── Resources/
│   ├── DLLs/               # 第三方 DLL 相依
│   ├── Language/           # 多語系 XML（10 種）
│   └── Installer MSD/      # WiX 安裝包專案
├── docs/                   # 文件、截圖、發行說明
├── Tests/                  # 下載、解壓與串流回歸驗證
└── MegaDownloader.sln
```

完整目錄樹與檔案說明見 [CONTRIBUTING](CONTRIBUTING.zh-CN.md)。

</details>

---

## 📚 文件

| 文件 | 內容 |
| --- | --- |
| [CHANGELOG](CHANGELOG.md) | 完整版本歷史與每版修復明細（簡體中文，權威來源） |
| [CONTRIBUTING](CONTRIBUTING.zh-CN.md) | 貢獻流程、程式風格、發布流程 |
| [SECURITY](../SECURITY.md) | 安全性問題回報方式 |
| [CODE OF CONDUCT](../CODE_OF_CONDUCT.md) | 社群行為準則 |
| [2.6 發行說明](release-notes/v2.6.0.md) | 用作 GitHub Release 正文的文案 |

README 為五種語言手動維護；CONTRIBUTING 有英文與簡體中文兩個版本，CHANGELOG 為簡體中文。更新時請同步：

| 語言 | README | CHANGELOG | CONTRIBUTING |
| --- | --- | --- | --- |
| English | `README.md`（根目錄） | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |
| 简体中文 | `docs/README.zh-CN.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.zh-CN.md` |
| 繁體中文 | `docs/README.zh-TW.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |
| 日本語 | `docs/README.ja-JP.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |
| 한국어 | `docs/README.ko-KR.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |

---

## ⭐ 支持這個專案

如果 MegaDownloader Revival 幫到了你，這幾件事最有幫助：

- ⭐ **點個 Star** —— 這是讓專案被更多人看到的最直接訊號
- 🐛 [提 Issue](../../../issues) —— 近期的多個修復都來自使用者回報
- 🌍 幫忙改進 [翻譯](CONTRIBUTING.zh-CN.md) —— 已有 10 種語言，缺母語校對
- 🔗 分享給還在找 MEGA 下載工具的朋友

<a href="https://star-history.com/#a1175815821/MegaDownloader-Revival&Date">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=a1175815821/MegaDownloader-Revival&type=Date&theme=dark" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=a1175815821/MegaDownloader-Revival&type=Date" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=a1175815821/MegaDownloader-Revival&type=Date" />
 </picture>
</a>

---

## 致謝

- 原 MegaDownloader 作者 **Andres Soliño**
- 復活計畫維護者 **Yingxue**（v2.0+），以及所有提 Issue、補翻譯的貢獻者
- 開源相依：[BouncyCastle](https://www.bouncycastle.org/) · [Newtonsoft.Json](https://www.newtonsoft.com/json) · [SharpCompress](https://github.com/adamhathcock/sharpcompress) · [ObjectListView](http://objectlistview.sourceforge.net/) · [7-Zip](https://www.7-zip.org/)

## 授權

基於 [MIT License](../LICENSE) 發布。原始版權 © 2018 Andres Soliño，復活計畫修復版權 © 2026 MegaDownloader Revival Project 貢獻者。

> 倉庫內第三方 DLL 遵循各自原始授權，使用者應自行確認合規性。
