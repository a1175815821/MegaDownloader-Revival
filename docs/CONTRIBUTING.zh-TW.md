# 貢獻指南

首先，感謝你願意為 MegaDownloader 復活計畫貢獻程式碼！本文件將指引你完成貢獻流程。

## 行為準則

請保持友善、尊重所有參與者。我們歡迎任何與專案目標（讓 MegaDownloader 重新可用）相關的貢獻，無論是修復 Bug、新增功能、完善翻譯還是改進文件。

## 我能貢獻什麼？

| 類型 | 說明 |
| --- | --- |
| 🐛 Bug 修復 | 修復連結解析、下載失敗、介面錯誤等問題 |
| ✨ 新功能 | 支援新的 Crypter、新的連結保護器、新的協定等 |
| 🌐 翻譯 | 在 `Resources/Language/` 中改進現有翻譯或新增語言 |
| 📚 文件 | 改進 README、CHANGELOG、程式碼註解 |
| 🎨 UI/UX | 改進 WinForms 介面佈局、圖示、可用性 |
| 🔧 重構 | 在不影響功能的前提下提升程式碼品質 |

## 開發環境

- Visual Studio 2019 / 2022
- .NET Framework 4.8 SDK
- Git

## 專案結構

```
MegaDownloader/
├── Clases/                         # 核心類別庫
│   ├── Cryptography/AES.vb         #   AES 加密
│   ├── StreamingLibrary/           #   串流媒體庫管理
│   │   ├── LibraryElement.vb
│   │   ├── StreamingLibrary.vb
│   │   └── StreamingLibraryManager.vb
│   ├── ApplicationInstanceManager.vb
│   ├── Conexion.vb                 #   HTTP/網路通訊
│   ├── Configuracion.vb            #   組態管理
│   ├── ConfiguracionUI.vb          #   UI 組態（佈景主題等）★ v2.0 新增
│   ├── FileDownloader.vb           #   檔案下載核心
│   ├── MegaFolderHelper.vb         #   MEGA 資料夾解析
│   ├── MegaURIProtocol.vb          #   mega:// 協定註冊
│   ├── Mutex.vb                    #   互斥鎖（處理程序單一執行個體）
│   ├── Paquete.vb                  #   下載封包資料
│   ├── ThrottledStream.vb          #   限速串流
│   ├── ThemeManager.vb             #   佈景主題管理員 ★ v2.0 新增
│   ├── URLExtractor.vb             #   URL 解析
│   ├── URLProcessor.vb             #   URL 處理
│   └── Updater.vb                  #   自動更新
├── Controls/                       # 自訂控制項
│   └── ELCAccountControl.vb
├── HttpModule/                     # 內建 Web 伺服器模組
│   ├── StreamingModule.vb
│   ├── StreamingLibraryModule.vb
│   ├── WebInterfaceModule.vb
│   └── Template/                   #   HTML 樣板
├── Stegano/                        # 隱寫術窗體
│   ├── SteganoManager.vb
│   ├── SteganoWizardLoad.vb
│   └── SteganoWizardSave.vb
├── Resources/
│   ├── DLLs/                       # 第三方 DLL 相依
│   ├── Language/                   # 多語言 XML（10 種）
│   └── Installer MSD/              # WiX 安裝包工程
├── My Project/                     # VS 專案中繼資料
├── Forms/                          # WinForms 窗體（12 個）
│   ├── Main.vb                     #   主窗體
│   ├── AddLinks.vb                 #   新增連結窗體
│   ├── Configuration.vb            #   設定窗體（含佈景主題切換）
│   ├── StreamingForm.vb            #   串流播放窗體
│   ├── Credits.vb                  #   關於/致謝
│   ├── SplashScreen.vb             #   啟動畫面
│   ├── Cerrando.vb                 #   關閉畫面
│   ├── Descompresor.vb             #   解壓縮窗體
│   ├── ELCForm.vb                  #   ELC 容器窗體
│   ├── EncodeLinksForm.vb          #   連結加密窗體
│   ├── PantallaMsg.vb              #   訊息提示窗體
│   └── PropiedadesDescarga.vb      #   下載屬性窗體
├── docs/                           # 專案文件
│   ├── README*.md                   #   專案說明（5 種語言）
│   ├── CHANGELOG*.md                #   變更日誌（5 種語言）
│   └── CONTRIBUTING*.md             #   貢獻指南（5 種語言）
├── MegaDownloader.sln              # VS 解決方案
├── MegaDownloader.vbproj           # VS 工程
├── app.config                      # .NET 執行階段組態
├── ApplicationEvents.vb            # 應用層級事件處理
├── README.md                       # 文件首頁索引（正文在 docs/）
├── LICENSE                         # MIT 授權條款
└── .gitignore                      # Git 忽略規則
```

### 關鍵目錄說明

| 目錄 | 內容 |
| --- | --- |
| `Clases/` | 核心類別庫：加密（`Cryptography/`）、串流媒體庫（`StreamingLibrary/`）、HTTP 通訊（`Conexion.vb`）、組態（`Configuracion.vb`）、下載核心（`FileDownloader.vb`）、資料夾解析（`MegaFolderHelper.vb`）、佈景主題（`ThemeManager.vb`）、更新（`Updater.vb`） |
| `Forms/` | WinForms 窗體。主窗體是 `Main.vb`，含大量下載清單與佈景主題邏輯 |
| `HttpModule/` | 內建 Web 伺服器模組，`Template/` 下是 HTML 樣板 |
| `Stegano/` | 隱寫術相關窗體與邏輯 |
| `Resources/Language/` | 介面多語言 XML，每種語言一個檔案 |
| `Resources/DLLs/` | 第三方相依 DLL，隨倉庫提交 |
| `docs/` | 本文件與變更日誌 |

## 貢獻流程

### 1. Fork 並複製倉庫

```bash
# Fork 倉庫到自己的 GitHub 帳戶後：
git clone https://github.com/<你的使用者名稱>/MegaDownloader-Revival.git
cd MegaDownloader
git remote add upstream https://github.com/a1175815821/MegaDownloader-Revival.git
```

### 2. 建立功能分支

```bash
# 從最新的 main 分支建立
git checkout main
git pull upstream main
git checkout -b feature/你的功能名稱
# 或： fix/bug-描述， docs/文件主題， i18n/語言-改進
```

### 3. 開發與本機測試

- 在 Visual Studio 中開啟 `MegaDownloader.sln`
- 選擇 `Debug` 組態建置
- 執行 `bin/Debug/MegaDownloader.exe`，驗證你的修改

**測試案例（請務必涵蓋）：**

- 舊版連結：`https://mega.nz/#!abcDEF!ghijklmnop`
- 新版連結：`https://mega.nz/file/abcDEF#ghijklmnop`
- 資料夾連結：`https://mega.nz/folder/abcDEF#ghijklmnop`
- 加密連結：`mega://enc?...`
- 剪貼簿自動辨識
- 拖曳連結

### 4. 提交程式碼

遵循 [Conventional Commits](https://www.conventionalcommits.org/zh-hans/v1.0.0/) 規範：

```
<type>(<scope>): <subject>

<body可選>

<footer可選>
```

常用類型：

- `feat`： 新功能，如 `feat(url): 支持 mega.nz/embed/ 链接格式`
- `fix`： Bug 修復，如 `fix(clipboard): 修复剪贴板监听失效问题`
- `docs`： 文件，如 `docs: 补充 zh-CN 翻译`
- `refactor`： 重構，如 `refactor(conexion): 简化代理设置逻辑`
- `i18n`： 翻譯，如 `i18n(zh-CN): 补全未翻译条目`

```bash
git add .
git commit -m "feat(url): 支持 mega.nz/embed/ 链接格式"
```

### 5. 推送並發起 PR

```bash
git push origin feature/你的功能名稱
```

到 GitHub 上發起 Pull Request 到 `main` 分支，在 PR 描述中說明：

- 這個 PR 修改了什麼？
- 為什麼需要修改？（關聯 Issue 編號）
- 如何測試？
- 是否影響現有功能？

### 6. 程式碼審查與合併

維護者會審查你的 PR，可能會要求修改。請耐心配合，所有修改都是為了專案的長期可維護性。

## 程式碼風格約定

- VB.NET 專案已啟用 `Option Strict On`、`Option Explicit Off`、`Option Infer On`，**新增程式碼必須符合這些約束**
- 檔案編碼：**UTF-8 with BOM**
- 縮排：**4 個空格**
- 命名：
  - 類別、方法： PascalCase，如 `ExtraerFileID`
  - 私有欄位： camelCase 或帶底線前綴，如 `_ProxyIP`
  - 區域變數： camelCase，如 `fileInfo`
- 註解：
  - 複雜邏輯需用 `'` 單行註解說明
  - 公開 API 用 `''' <summary>` XML 文件註解
- 原專案使用西班牙語命名，如 `Clases`、`Configuracion`、`Fichero`。**為保持一致性，新增程式碼可使用英語命名**，但不要批次重新命名現有識別符

## 新增 Crypter / Link Protector

參考 [`Clases/Crypters/EncrypterMega.vb`](../Clases/Crypters/EncrypterMega.vb) 的實作模式：

1. 在 `Clases/Crypters/` 下新增 `<Name>.vb`
2. 實作 `ObtenerInformacionFichero` 方法，傳回 `Conexion.InformacionFichero`
3. 在 [`Clases/URLExtractor.vb`](../Clases/URLExtractor.vb) 中：
   - 新增 `<NAME>TOKEN` 常數
   - 在 `patternOthers` 中新增比對正則式
   - 在 `ExtraerFileID` 中新增分支
4. 必要時在 `Forms/Main.vb` 中接取 UI

## 文件與翻譯

本專案的**文件**與**軟體介面**是兩套獨立的多語言體系，都歡迎貢獻。

### 文件多語言（README / CHANGELOG / CONTRIBUTING）

文件採手動翻譯模式，無自動翻譯流程：

- 每種語言的文件都是人工維護的：英文（`docs/README.md`、`docs/CHANGELOG.md`、`docs/CONTRIBUTING.md`）、簡體中文（`docs/*.zh-CN.md`，權威來源）、繁中/日/韓對應檔案
- 修改文件時請同步更新所有語言版本，保持內容一致
- 想改進文件用詞 → 直接修改對應語言的檔案，或提交 PR
- 想新增一種語言 → 複製英文（或中文）文件為新語言檔案，並更新各文件頂部的語言導覽連結與語言對照表

### 軟體介面多語言

介面文案在 `Resources/Language/<locale>-Language.xml`，每種語言一個檔案。

改進步驟：

1. 找到對應語言的 XML（如 `ja-JP-Language.xml`）
2. 修改 `<Text>` 節點的 CDATA 內容，**不要變更 `key` 屬性**
3. 若新增鍵，需同時確保 `en-US-Language.xml` 中有同名 key（作為退回基準）

### 新增介面語言

1. 複製 `Resources/Language/en-US-Language.xml` 為 `<locale>-Language.xml`
2. 翻譯所有 `<Text>` 節點的 CDATA 內容
3. 在 `MegaDownloader.vbproj` 中新增內嵌資源：

```xml
<EmbeddedResource Include="Resources\Language\ja-JP-Language.xml" />
```

4. 執行程式，在 **設定 → 語言** 中能看到新語言

> 語言鍵查詢有三級退回：磁碟檔案 → 內建資源 → `en-US` → 傳回 key 本身。所以即使某些鍵漏譯，程式也不會當掉，只是會顯示英文或原始 key。

## 回報 Bug

提交 Bug 時請在 Issue 中包含以下資訊：

- **MegaDownloader 版本**（查看 關於 → 版本）
- **Windows 版本**
- **連結類型**（完整複製一個範例連結，敏感部分可去敏）
- **重現步驟**
- **預期行為** vs **實際行為**
- **錯誤日誌**（如有，位於程式目錄下的日誌檔）

## 發布流程（僅維護者）

1. 確認所有測試通過，`Debug` 與 `Release` 組態都能建置
2. 更新 `docs/CHANGELOG.zh-CN.md`，追加新版本章節，並同步更新其他語言的 CHANGELOG
3. 更新 `My Project/AssemblyInfo.vb` 中的 `AssemblyVersion` 與 `AssemblyFileVersion`
4. 在 `Resources/InternalConfig.xml`（Base64 編碼）中更新 `VERSION_MEGADOWNLOADER` 與 `VERSION_UPDATE`
5. 更新 `docs/version.xml` 中的 `<Version>`
6. 撰寫中英雙語的發布說明（中英對照，先寫中文再寫英文，參考往期 `release-notes-*.md` 的結構）
7. 建立 Git Tag 並推送，CI 會自動建置產物並建立 GitHub Release：

```bash
git tag -a v2.5.0 -m "Release v2.5.0"
git push origin main
git push origin v2.5.0
```

CI 在 tag `v*` 上會自動：
- 建置 Release 組態
- 校驗單檔版的 12 個內嵌 DLL 是否齊全（缺失則建置失敗，避免發出「雙擊沒反應」的包）
- 打包為 zip 並上傳 GitHub Release

> 測試預發布（RC 版）建議先以 `prerelease` 形式發布，驗證通過後再轉為正式版。

## 聯絡方式

- 提交 Issue：GitHub Issues
- 安全相關問題：請勿在公開 Issue 中討論，透過郵件聯絡維護者

---

再次感謝你的貢獻！讓我們一起讓 MegaDownloader 煥發新生。 🚀
