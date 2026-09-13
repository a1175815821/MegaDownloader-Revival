# MegaDownloader 复活计划

**语言**：[English](README.md) · **简体中文** · [繁體中文](README.zh-TW.md) · [日本語](README.ja-JP.md) · [한국어](README.ko-KR.md)

> 让经典 MEGA 下载器重新可用。基于 v1.8 反编译源码修复而成，已完成 60+ 项修复。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://dotnet.microsoft.com/)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![Language](https://img.shields.io/badge/Language-VB.NET-005a9c.svg)](https://docs.microsoft.com/dotnet/visual-basic/)
[![Build](https://github.com/a1175815821/MegaDownloader-Revival/actions/workflows/build.yml/badge.svg)](../../actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/a1175815821/MegaDownloader-Revival?include_prereleases)](../../releases/latest)
[![Downloads](https://img.shields.io/github/downloads/a1175815821/MegaDownloader-Revival/total)](../../releases)
[![Stars](https://img.shields.io/github/stars/a1175815821/MegaDownloader-Revival?style=social)](../../stargazers)

---

## 这是什么

MegaDownloader 是西班牙开发者 **Andres Soliño** 制作的 MEGA 网盘下载管理器，以轻量、稳定、支持多线程著称。原项目在 v1.8 后停止维护，而 MEGA 已更换链接格式（`mega.nz/file/...`、`mega.nz/folder/...`），旧版因此无法识别新链接，核心功能失效。

本仓库是它的复活计划：反编译 v1.8 拿到源码，在其基础上修复与重构。

**当前版本：v2.5.0**。完整变更历史见 [CHANGELOG](CHANGELOG.md)。

> ⚠️ **法律声明**：本项目源自对第三方已发布软件的反编译，目的仅在于修复兼容性问题以恢复其可用性。若原作者认为本仓库侵犯了其权益，请通过 Issue 联系，我们将配合处理。

---

## 快速开始

1. 从 [Releases](../../releases) 下载，二选一：
   - **`MegaDownloader-Revival-win-x86.zip`** —— 绿色版。解压到任意目录，双击 `MegaDownloader.exe`
   - **`MegaDownloader.exe`** —— 单文件版。12 个依赖 DLL 已内嵌，下载后直接双击，无需解压
2. 复制 MEGA 链接，程序自动识别剪贴板内容
3. 也可点工具栏 **添加链接** 手动粘贴，或把链接拖进主窗口
4. 在 **设置** 中配置下载目录、并发数、限速
5. 在 **设置 → 常规 → 主题** 中切换深/浅色（保存后立即生效）

### 链接示例

新版格式（v1.9 起支持）：

```
https://mega.nz/file/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/folder/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
```

旧版格式（继续支持）：

```
https://mega.nz/#!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
https://mega.co.nz/#F!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
```

加密链接：

```
mega://enc?_xlPqemSILarh5VBKbhSTFyQQQ0
mega://enc2?abcDEFgh-IjklMNop
```

---

## 功能特性

| 功能 | 说明 |
| --- | --- |
| 多线程下载 | 同一文件多路并发连接，大幅提升速度 |
| 断点续传 | 支持暂停、恢复、错误重试 |
| 速度限制 | 全局或单任务限速 |
| 剪贴板监控 | 自动识别复制到剪贴板的 MEGA 链接 |
| 拖拽支持 | 拖链接到主窗口即可加入队列 |
| MEGA 文件夹 | 递归解析并下载整个分享文件夹，支持只下载指定子文件夹/文件 |
| 加密链接 | 支持 `enc` / `enc2` / `fenc` / `fenc2` / `elc` 多种格式 |
| ELC 容器 | 加密链接容器的导入与导出 |
| 流媒体播放 | 集成 VLC，边下边播 |
| Web 界面 | 内置 HttpServer，可浏览器远程管理；支持开启局域网访问 |
| 流媒体库 | 可视化管理流媒体资源 |
| Stegano 隐写 | 图片/视频的隐写编码与解码 |
| 自动解压 | 基于 SharpCompress，支持 RAR / 7Z / ZIP |
| 配额熔断 | MEGA 配额耗尽时自动暂停并倒计时，到点自动恢复（v2.5） |
| 失败自愈 | 失败任务定时自动重试，永久失败自动排除（v2.5） |
| 多语言界面 | 支持 10 种语言，可扩展 |
| 深/浅色主题 | 跟随系统或手动切换，Auto 模式实时跟随 |

### 支持的链接格式

- `mega.nz/#...!FileID!FileKey`（旧版）
- `mega.nz/file/FileID#FileKey`（新版）
- `mega.nz/folder/FolderID#FolderKey`（新版文件夹）
- MEGA URI 协议：`mega://#!...`、`mega://enc?...`、`mega://elc?...`

> v2.0 起已移除对以下下线服务的支持：MegaCrypter、YouPaste、LinkCrypter、EncrypterMe.ga、goo.gl 短链。

---

## 构建

### 环境要求

- Visual Studio 2019 / 2022
- .NET Framework 4.8 SDK（随 Visual Studio 安装）
- Windows 7 SP1 或更高

### 构建配置

| 配置 | 说明 |
| --- | --- |
| `Debug` | 调试版，输出到 `bin/Debug/` |
| `Release` | 发布版，输出到 `bin/Release/` |
| `Debug_MSD` | 调试 MegaSearchDesktop 集成版 |
| `Release_MSD` | 发布 MegaSearchDesktop 集成版，输出到 `Resources/Installer MSD/` |

### 步骤

```bash
git clone https://github.com/a1175815821/MegaDownloader-Revival.git
cd MegaDownloader-Revival
# 用 Visual Studio 打开 MegaDownloader.sln，然后 Ctrl+Shift+B
```

命令行构建：

```bash
msbuild MegaDownloader.sln /p:Configuration=Release /p:Platform=x86
```

产物位于 `bin/<Configuration>/MegaDownloader.exe`。

### 技术栈

VB.NET · .NET Framework 4.8 · WinForms · BouncyCastle（加密）· Newtonsoft.Json · ObjectListView · SharpCompress（解压）· HttpServer/Fadd（Web）· F5Lib（隐写）

---

## 项目结构

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
├── docs/                   # 文档（README / 变更日志、贡献指南）
├── My Project/             # VS 项目元数据
└── MegaDownloader.sln
```

完整的目录树与文件用途说明见 [CONTRIBUTING](CONTRIBUTING.md)。

---

## 支持的语言

软件界面自带 10 种语言：

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

想加一门语言或改进现有翻译，见 [CONTRIBUTING](CONTRIBUTING.md)。

---

## 文档

| 文档 | 内容 |
| --- | --- |
| [CHANGELOG](CHANGELOG.md) | 完整版本历史与每版修复明细 |
| [CONTRIBUTING](CONTRIBUTING.md) | 贡献流程、代码风格、发布流程 |

本文档提供多种语言。**所有语言版本均为手动维护**，更新某一语言时请同步更新其他语言：

| 语言 | README | CHANGELOG | CONTRIBUTING | |
| --- | --- | --- | --- | --- |
| English | `docs/README.md` | `CHANGELOG.md` | `CONTRIBUTING.md` | 手动维护 |
| 简体中文 | `docs/README.zh-CN.md` | `CHANGELOG.zh-CN.md` | `CONTRIBUTING.zh-CN.md` | **权威源** |
| 繁體中文 | `docs/README.zh-TW.md` | `CHANGELOG.zh-TW.md` | `CONTRIBUTING.zh-TW.md` | 手动维护 |
| 日本語 | `docs/README.ja-JP.md` | `CHANGELOG.ja-JP.md` | `CONTRIBUTING.ja-JP.md` | 手动维护 |
| 한국어 | `docs/README.ko-KR.md` | `CHANGELOG.ko-KR.md` | `CONTRIBUTING.ko-KR.md` | 手动维护 |

> 简体中文是权威源，本文件本身就是简体中文版，所以不存在单独的 `README.zh-CN.md`。

---

## 致谢

- 原 MegaDownloader 作者 **Andres Soliño**
- 复活计划维护者 **Yingxue**（v2.0+）
- 开源依赖：[BouncyCastle](https://www.bouncycastle.org/) · [Newtonsoft.Json](https://www.newtonsoft.com/json) · [SharpCompress](https://github.com/adamhathcock/sharpcompress) · [ObjectListView](http://objectlistview.sourceforge.net/) · [7-Zip](https://www.7-zip.org/) · [mpress](https://www.matcode.com/mpress.htm)

## 许可

基于 [MIT License](LICENSE) 发布。原始版权 © 2018 Andres Soliño，复活计划修复版权 © 2026 MegaDownloader Revival Project 贡献者。

> 仓库内第三方 DLL 遵循各自原始许可证，使用者应自行确认合规性。
