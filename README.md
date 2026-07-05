# MegaDownloader

> **MegaDownloader 复活计划 (Revival Project)**  
> 基于 MegaDownloader v1.8 反编译源码修复而成,主要修复了对 MEGA 新版链接格式 (`mega.nz/file/`、`mega.nz/folder/`) 的不支持问题,使这款经典的 MEGA 下载工具重新可用。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://dotnet.microsoft.com/)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![Language](https://img.shields.io/badge/Language-VB.NET-005a9c.svg)](https://docs.microsoft.com/dotnet/visual-basic/)
[![Build](https://github.com/a1175815821/MegaDownloader-Revival/actions/workflows/build.yml/badge.svg)](../../actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/a1175815821/MegaDownloader-Revival?include_prereleases)](../../releases/latest)
[![Downloads](https://img.shields.io/github/downloads/a1175815821/MegaDownloader-Revival/total)](../../releases)
[![Stars](https://img.shields.io/github/stars/a1175815821/MegaDownloader-Revival?style=social)](../../stargazers)
[![Issues](https://img.shields.io/github/issues/a1175815821/MegaDownloader-Revival)](../../issues)

---

## 目录

- [项目背景](#项目背景)
- [主要修复内容](#主要修复内容)
- [功能特性](#功能特性)
- [技术栈](#技术栈)
- [项目结构](#项目结构)
- [构建说明](#构建说明)
- [使用方法](#使用方法)
- [支持的语言](#支持的语言)
- [支持的链接格式](#支持的链接格式)
- [致谢与版权](#致谢与版权)
- [许可协议](#许可协议)

---

## 项目背景

MegaDownloader 是一款由西班牙开发者 Zheng You Yun 创建的 MEGA 网盘下载管理器,因其轻量、稳定、支持多线程下载而广受用户欢迎。然而,原项目自 v1.8 后停止维护,随着 MEGA 站点链接格式的更新 (`mega.nz/file/...`、`mega.nz/folder/...`),旧版程序已无法识别新版链接,导致核心功能失效。

本项目即 **MegaDownloader 复活计划**:通过对 v1.8 进行反编译得到源码,并在其基础上修复新版链接识别问题,让这款经典工具重新焕发生机。

> ⚠️ **法律声明**:本项目源自对第三方已发布软件的反编译,目的仅在于修复兼容性问题以恢复其可用性。若原作者认为本仓库侵犯了其权益,请通过 Issue 联系,我们将配合处理。

## 主要修复内容

### v1.9 - 链接格式兼容性修复

MEGA 已迁移到新的 URL 结构,本项目在 [`Clases/URLExtractor.vb`](Clases/URLExtractor.vb) 中扩展了 URL 匹配正则,以同时支持新旧两种格式:

**新增正则匹配(支持新版链接):**

```vb
"(http://|https://|)mega.nz/file/(?<FileID>[^#/]+)(#(?<FileKey>[\w-]+))?"
"(http://|https://|)mega.nz/folder/(?<FileID>[^#/]+)(#(?<FileKey>[\w-]+))?"
"(http://|https://|)mega.co.nz/file/(?<FileID>[^#/]+)(#(?<FileKey>[\w-]+))?"
"(http://|https://|)mega.co.nz/folder/(?<FileID>[^#/]+)(#(?<FileKey>[\w-]+))?"
```

**文件夹识别同步更新:**

```vb
Result = URI.Contains("mega.co.nz/#F!") Or URI.Contains("mega.nz/#F!")
        Or URI.Contains("chrome://mega/content/secure.html#F!")
        Or URI.Contains("mega.nz/folder/") Or URI.Contains("mega.co.nz/folder/")
```

参见 [URLExtractor.vb](Clases/URLExtractor.vb) 中的 `patternHTTPURI` 与 `IsMegaFolder` 方法。

## 功能特性

- **多线程下载**:支持对同一文件建立多路并发连接,大幅提升下载速度
- **速度限制**:`ThrottledStream` 全局/单任务限速
- **断点续传**:支持任务暂停、恢复、错误重试
- **剪贴板监控**:自动识别复制到剪贴板的 MEGA 链接
- **拖拽支持**:拖拽链接到主窗口即可加入下载队列
- **MEGA 文件夹**:支持递归解析并下载整个分享文件夹
- **加密链接**:支持 `enc?` / `enc2?` / `fenc?` / `fenc2?` / `elc?` 多种加密链接格式
- **第三方 Crypter**:支持 MegaCrypter、YouPaste、LinkCrypter、EncrypterMe.ga 等链接保护服务
- **链接保护器**:支持 lix.in、j.gs、q.gs、adf.ly 等短链跳转解析
- **ELC 容器**:支持加密链接容器 (Encrypted Link Container) 的导入与导出
- **流媒体播放**:集成 VLC,边下边播 (Streaming)
- **Web 界面**:内置 HttpServer,可通过浏览器远程管理下载任务
- **流媒体库**:可视化管理流媒体资源 (StreamingLibrary)
- **Stegano 隐写**:对图片/视频进行隐写编码与解码
- **自动解压**:基于 SharpCompress 的下载后自动解压 (RAR/7Z/ZIP)
- **电影信息**:从 IMDB、Filmaffinity、Allocine 抓取电影元数据
- **多语言界面**:支持 10 种语言,可扩展
- **自动更新**:支持从远程 XML 检查并下载新版本
- **MegaSearchDesktop**:与桌面搜索集成 (MSD 构建)

## 技术栈

| 技术 | 用途 |
| --- | --- |
| VB.NET | 主开发语言 |
| .NET Framework 4.8 | 运行时 |
| WinForms | UI 框架 |
| BouncyCastle.Cryptography | 加密 (RSA/AES) |
| Newtonsoft.Json | JSON 解析 |
| ObjectListView | 高级 ListView 控件 |
| SharpCompress | 压缩包解压 |
| HttpServer (Fadd) | 内置 Web 服务器 |
| F5Lib | 隐写术 (Stegano) |
| xUnit | 单元测试 |
| mpress | EXE 压缩 (PostBuild) |

## 项目结构

```
MegaDownloader/
├── Clases/                         # 核心类库
│   ├── Crypters/                   #   链接加密/解析器
│   │   ├── EncrypterMega.vb
│   │   ├── LinkCrypter.vb
│   │   ├── MegaCrypter.vb
│   │   └── Youpaste.vb
│   ├── Cryptography/AES.vb         #   AES 加密
│   ├── MovieInfo/                  #   电影信息抓取
│   │   ├── Allocine.vb
│   │   ├── Filmaffinity.vb
│   │   └── IMDB.vb
│   ├── StreamingLibrary/           #   流媒体库管理
│   │   ├── LibraryElement.vb
│   │   ├── StreamingLibrary.vb
│   │   └── StreamingLibraryManager.vb
│   ├── ApplicationInstanceManager.vb
│   ├── Conexion.vb                 #   HTTP/网络通信
│   ├── Configuracion.vb            #   配置管理
│   ├── FileDownloader.vb           #   文件下载核心
│   ├── MegaFolderHelper.vb         #   MEGA 文件夹解析
│   ├── ThrottledStream.vb          #   限速流
│   ├── URLExtractor.vb             #   URL 解析 ★ 修复点
│   ├── URLProcessor.vb             #   URL 处理
│   └── Updater.vb                  #   自动更新
├── Controls/                       # 自定义控件
│   └── ELCAccountControl.vb
├── HttpModule/                     # 内置 Web 服务器模块
│   ├── StreamingModule.vb
│   ├── StreamingLibraryModule.vb
│   ├── WebInterfaceModule.vb
│   └── Template/                   #   HTML 模板
├── Stegano/                        # 隐写术窗体
│   ├── SteganoManager.vb
│   ├── SteganoWizardLoad.vb
│   └── SteganoWizardSave.vb
├── Resources/
│   ├── DLLs/                       # 第三方 DLL 依赖
│   ├── Language/                   # 多语言 XML (10 种)
│   ├── Tools/                      # mpress 等构建工具
│   └── Installer MSD/              # WiX 安装包工程
├── My Project/                     # VS 项目元数据
├── Forms/                          # WinForms 窗体 (12 个,各含 .vb/.Designer.vb/.resx)
│   ├── Main.vb                     #   主窗体
│   ├── AddLinks.vb                 #   添加链接窗体
│   ├── Configuration.vb            #   设置窗体
│   ├── StreamingForm.vb            #   流媒体播放窗体
│   ├── Credits.vb                  #   关于/致谢
│   ├── SplashScreen.vb             #   启动画面
│   ├── Cerrando.vb                 #   关闭画面
│   ├── Descompresor.vb             #   解压窗体
│   ├── ELCForm.vb                  #   ELC 容器窗体
│   ├── EncodeLinksForm.vb          #   链接加密窗体
│   ├── PantallaMsg.vb              #   消息提示窗体
│   └── PropiedadesDescarga.vb     #   下载属性窗体
├── docs/                           # 项目文档
│   ├── CHANGELOG.md                #   变更日志
│   └── CONTRIBUTING.md             #   贡献指南
├── MegaDownloader.sln              # VS 解决方案
├── MegaDownloader.vbproj           # VS 工程
├── app.config                      # .NET 运行时配置
├── ApplicationEvents.vb            # 应用级事件处理
├── README.md                       # 项目说明
├── LICENSE                         # MIT 许可证
└── .gitignore                      # Git 忽略规则
```

## 构建说明

### 环境要求

- Visual Studio 2019 / 2022 (推荐)
- .NET Framework 4.8 SDK (随 Visual Studio 一起安装)
- Windows 7 SP1 或更高版本

### 构建步骤

1. 克隆仓库

   ```bash
   git clone https://github.com/a1175815821/MegaDownloader-Revival.git
   cd MegaDownloader-Revival
   ```

2. 用 Visual Studio 打开 `MegaDownloader.sln`

3. 选择构建配置:

   | 配置 | 说明 |
   | --- | --- |
   | `Debug` | 调试版本,输出到 `bin/Debug/` |
   | `Release` | 发布版本,输出到 `bin/Release/`,构建后自动调用 `mpress.exe` 压缩 |
   | `Debug_MSD` | 调试 MegaSearchDesktop 集成版本 |
   | `Release_MSD` | 发布 MegaSearchDesktop 集成版本,输出到 `Resources/Installer MSD/` |

4. `Ctrl + Shift + B` 构建解决方案

5. 构建产物位于 `bin/<Configuration>/MegaDownloader.exe`

### 命令行构建

```bash
# 使用 MSBuild
msbuild MegaDownloader.sln /p:Configuration=Release /p:Platform=x86

# 或使用 dotnet CLI (需安装 .NET Framework 引用包)
dotnet build MegaDownloader.sln -c Release
```

## 使用方法

1. 从 [Releases](../../releases) 下载最新 `MegaDownloader.exe`
2. 双击运行(无需安装,绿色版)
3. 复制 MEGA 链接,程序会自动识别剪贴板内容
4. 也可点击工具栏 **添加链接** 按钮手动粘贴
5. 配置下载目录、并发数、限速等选项于 **设置** 窗口

### 链接示例

新版格式(本次修复支持):
```
https://mega.nz/file/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/folder/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
```

旧版格式(继续支持):
```
https://mega.nz/#!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
https://mega.co.nz/#F!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
```

加密链接:
```
mega://enc?_xlPqemSILarh5VBKbhSTFyQQQ0
mega://enc2?abcDEFgh-IjklMNop
```

## 支持的语言

| 语言 | 文件 |
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

## 支持的链接格式

- `mega.nz/#...!FileID!FileKey` (旧版)
- `mega.nz/file/FileID#FileKey` (新版 ★)
- `mega.nz/folder/FolderID#FolderKey` (新版文件夹 ★)
- `megacrypter.com/!...`
- `youpaste.co/!...`
- `linkcrypter.net/!...`
- `encrypterme.ga/!...`
- `megashur.se/out.php?m=...`
- 短链:`goo.gl`、`lix.in`、`j.gs`、`q.gs`、`adf.ly`
- MEGA URI 协议:`mega://#!...`、`mega://enc?...`、`mega://elc?...`

## 致谢与版权

- 感谢原 MegaDownloader 作者 **Zheng You Yun** 的卓越工作
- 感谢以下开源库的作者:
  - [BouncyCastle.Cryptography](https://www.bouncycastle.org/)
  - [Newtonsoft.Json](https://www.newtonsoft.com/json)
  - [SharpCompress](https://github.com/adamhathcock/sharpcompress)
  - [ObjectListView](http://objectlistview.sourceforge.net/)
  - [xUnit.net](https://xunit.net/)
  - [mpress](https://www.matcode.com/mpress.htm)

## 许可协议

本项目基于 [MIT License](LICENSE) 发布。原始版权所有 © 2018 Zheng You Yun,复活计划修复版权所有 © 2026 MegaDownloader Revival Project 贡献者。

> 本仓库中包含的第三方 DLL 文件遵循各自原始许可证。使用者应自行确认这些依赖的合规性。
