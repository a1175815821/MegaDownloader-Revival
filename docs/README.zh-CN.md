# MegaDownloader 复活计划

**语言**：[English](../README.md) · **简体中文** · [繁體中文](README.zh-TW.md) · [日本語](README.ja-JP.md) · [한국어](README.ko-KR.md)

> 让经典 MEGA 下载器在今天的 MEGA 上继续可用：适配新版链接格式、多线程加速、断点续传、配额自动恢复，单文件免安装。
> 非官方项目，与 MEGA（MEGA Ltd.）无关联。

**[下载 Windows 版](../../../releases/latest)** · 单文件或 ZIP 免安装包 · Windows 7 SP1+ · .NET Framework 4.8

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](../LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://dotnet.microsoft.com/)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![Language](https://img.shields.io/badge/Language-VB.NET-005a9c.svg)](https://docs.microsoft.com/dotnet/visual-basic/)
[![Build](https://github.com/a1175815821/MegaDownloader-Revival/actions/workflows/build.yml/badge.svg)](../../../actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/a1175815821/MegaDownloader-Revival?include_prereleases)](../../../releases/latest)
[![Downloads](https://img.shields.io/github/downloads/a1175815821/MegaDownloader-Revival/total)](../../../releases)
[![Stars](https://img.shields.io/github/stars/a1175815821/MegaDownloader-Revival?style=social)](../../../stargazers)

## 截图

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="screenshots/main-dark.png">
  <source media="(prefers-color-scheme: light)" srcset="screenshots/main-light.png">
  <img alt="MegaDownloader 主界面——左侧队列统计、中间任务列表、右侧选中任务的详情" src="screenshots/main-light.png">
</picture>

左侧是实时队列统计（总速度、进行中 / 等待 / 失败、队列进度、预计剩余）和快捷入口，中间是任务列表，右侧显示选中任务的详情。把 MEGA 链接拖进窗口，或者直接复制——剪贴板监控会自动识别。主题跟随系统设置，上面这张图也会跟着你的 GitHub 主题切换。

| 设置 · 一般 | 设置 · 高级 |
| :---: | :---: |
| [![设置——一般标签页](screenshots/settings-general.png)](screenshots/settings-general.png) | [![设置——高级标签页](screenshots/settings-advanced.png)](screenshots/settings-advanced.png) |
| 下载路径、剪贴板监控、开机自启、语言、主题、速度限制、出错重试 | Buffer 与日志等级、代理、内置 Web 服务器与局域网访问 |

<sub>截图取自 v2.5.9，界面为简体中文；支持 10 种界面语言。</sub>

---

## ⚡ 30 秒上手

1. 从 [Releases](../../../releases/latest) 下载，二选一：

   | 文件 | 适合你，如果…… |
   | --- | --- |
   | **`MegaDownloader.exe`**（单文件） | 想要最省事：下载后直接双击运行，15 个依赖 DLL 已内嵌 |
   | **`MegaDownloader-Revival-win-x86.zip`**（免安装包） | 喜欢压缩包：解压后运行程序 |

2. 双击运行 —— 免安装、无需管理员权限
   > 系统要求：Windows 7 SP1 及以上；.NET Framework 4.8（Windows 10 1903+ 已内置，旧系统按提示安装即可）
3. 复制一个 MEGA 链接 —— 程序自动识别剪贴板，确认后即加入下载队列

到这一步就能正常下载了。往下按需阅读：[功能](#-它能做什么) → [项目背景与对比](#-这个项目从哪来) → [常见问题](#-常见问题) → [开发者指南](#️-给开发者)。

其他加链接的方式：把链接拖进主窗口 · 工具栏「添加链接」手动粘贴 · 浏览器中点击 `mega://` 协议链接。

> 🆕 **最新版本 [v2.5.9](../../../releases/latest)**（2026-10-28）：同名并发下载互不干扰 · 解压不再覆盖原文件 · 已下完成品校验后复用 · 在线观看 Range 字节错误修复。完整历史见 [CHANGELOG](CHANGELOG.md)。

---

## 🚀 它能做什么

**下载核心**

- **多线程加速** —— 同一文件多路并发连接，吃满带宽
- **断点续传** —— 暂停 / 恢复 / 错误重试，重启程序接着下
- **文件夹下载** —— 递归解析整个分享文件夹，可只选其中部分子文件夹或文件
- **限速** —— 全局或单任务限速
- **失败自愈**（v2.5）—— 失败任务定时自动重试，永久失败自动排除
- **配额熔断**（v2.5）—— MEGA 配额耗尽时自动暂停并倒计时，到点自动恢复；也可切换回旧版「配额突破」持续重试模式（默认关闭）
- **自动解压** —— 下完自动解压 RAR / 7Z / ZIP，支持分卷与密码

**便捷与远程**

- **四种加链接方式** —— 剪贴板自动识别 / 拖拽 / 手动粘贴 / `mega://` 协议
- **Web 界面** —— 内置 HTTP 服务，浏览器远程添加任务、查看进度；可开启局域网访问，用手机推送下载
- **ELC 容器** —— 加密链接容器的导入与导出；`enc` / `enc2` / `fenc` / `fenc2` / `elc` 全支持

**界面与体验**

- **边下边播** —— 集成 VLC，流式播放尚未下完的视频
- **流媒体库** —— 可视化管理常看的流媒体资源
- **深 / 浅色主题**（v2.0）—— 跟随系统自动切换，也可手动指定
- **10 种界面语言** —— English · Español · 简体中文 · 繁體中文 · Français · Deutsch · Italiano · Português (BR) · Magyar · Română，可自行扩展
- **隐写工具** —— 用图片 / 视频藏文件（F5 隐写）

<details>
<summary><b>支持的全部链接格式（点击展开）</b></summary>

- 新版：`mega.nz/file/ID#KEY`、`mega.nz/folder/ID#KEY`（v1.9 起）
- 旧版：`mega.nz/#!ID!KEY`、`mega.co.nz/#F!ID!KEY`
- 加密：`mega://enc?…`、`mega://enc2?…`、`mega://fenc?…`、`mega://fenc2?…`、`mega://elc?…`

链接示例：

```
https://mega.nz/file/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/folder/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/#!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
mega://enc?_xlPqemSILarh5VBKbhSTFyQQQ0
```

> v2.0 起移除对已下线服务的支持：MegaCrypter、YouPaste、LinkCrypter、EncrypterMe.ga、goo.gl 短链。

</details>

---

## 📖 这个项目从哪来

MegaDownloader 是西班牙开发者 **Andres Soliño** 制作的 MEGA 下载管理器，以轻量、稳定、多线程著称。原项目在 v1.8 后停止维护，而 MEGA 更换了链接格式（`mega.nz/file/...`、`mega.nz/folder/...`），旧版无法识别新链接，核心功能失效。

本仓库把它救了回来：反编译 v1.8 拿到源码，在此基础上持续修复与重构 —— 至今累计 **200+ 项修复与加固**（崩溃、静默丢数据、安全、兼容性），并按语义化版本持续发布。

|  | 原版 v1.8（2018 年停更） | **本仓库 v2.5.x** |
| --- | --- | --- |
| 新版 `mega.nz/file` / `/folder` 链接 | ❌ 无法识别 | ✅ v1.9 起支持 |
| MEGA 配额耗尽 | 卡死、报错 | ✅ 自动熔断，倒计时恢复（v2.5） |
| 失败任务 | 只能手动重试 | ✅ 定时自愈（v2.5） |
| 界面主题 | 仅系统灰 | ✅ 深 / 浅色，跟随系统（v2.0） |
| 稳定性 | — | ✅ 200+ 项崩溃 / 丢数据 / 安全修复 |
| 维护状态 | 已停更 | ✅ 活跃维护，Issue 有响应 |

> ⚠️ **法律声明**：本项目源自对第三方已发布软件的反编译，目的仅在于修复兼容性问题以恢复其可用性。若原作者认为本仓库侵犯其权益，请通过 Issue 联系，我们将配合处理。

---

## ❓ 常见问题

**杀毒软件报毒？**
如果杀毒软件发出警告，请先查看提示并确认下载来源。你可以查看公开的 [构建流程](../../../actions/workflows/build.yml)、尝试 ZIP 包，或从源码自行构建。更换包格式不保证警告消失。

**提示配额不足 / quota exceeded？**
MEGA 对免费账户和 IP 有下载配额限制。v2.5 起程序会自动暂停并倒计时恢复；等不及的话，可在设置中开启「配额突破模式」持续重试。

**配置和下载队列存在哪？**
`%LOCALAPPDATA%\MegaDownloader\`。程序目录可以随意搬动或删除，配置不受影响；重装系统前备份这个目录即可。

**旧格式链接还能下吗？**
能。新旧格式、`mega://` 加密链接、ELC 容器都支持，完整清单见上方折叠块。

**需要管理员权限吗？**
不需要。免安装、免管理员。

**有 Mac / Linux 版吗？**
没有 —— 基于 WinForms，仅限 Windows。但可开启内置 Web 界面，用手机或其他设备的浏览器远程管理下载。

---

## 🛠️ 给开发者

<details>
<summary><b>从源码构建（点击展开）</b></summary>

**环境要求**：Visual Studio 2013 或更高（解决方案为 VS2013 格式）· .NET Framework 4.8 SDK（随 VS 安装）· Windows 7 SP1+

```bash
git clone https://github.com/a1175815821/MegaDownloader-Revival.git
cd MegaDownloader-Revival
# 用 Visual Studio 打开 MegaDownloader.sln，然后 Ctrl+Shift+B
```

命令行构建：

```bash
msbuild MegaDownloader.sln /p:Configuration=Release /p:Platform=x86
```

产物位于 `bin/<Configuration>/MegaDownloader.exe`。构建配置：`Debug` / `Release` / `Debug_MSD` / `Release_MSD`（MegaSearchDesktop 集成版，输出到 `Resources/Installer MSD/`）。

回归验证：在 PowerShell 中运行 `.\Tests\Run-RegressionTests.ps1`（需要 .NET SDK）。测试使用临时文件与本机 HTTP 服务，无需 MEGA 账号。详见 [Tests/README.md](../Tests/README.md)。

**技术栈**：VB.NET · .NET Framework 4.8 · WinForms · BouncyCastle（加密）· Newtonsoft.Json · ObjectListView · SharpCompress（解压）· HttpServer/Fadd（Web）· F5Lib（隐写）

</details>

<details>
<summary><b>项目结构（点击展开）</b></summary>

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
├── docs/                   # 文档（README / CHANGELOG / CONTRIBUTING）
├── Tests/                  # 下载、解压与流媒体回归验证
└── MegaDownloader.sln
```

完整目录树与文件说明见 [CONTRIBUTING](CONTRIBUTING.zh-CN.md)。

</details>

---

## 📚 文档

| 文档 | 内容 |
| --- | --- |
| [CHANGELOG](CHANGELOG.md) | 完整版本历史与每版修复明细（仅简体中文） |
| [CONTRIBUTING](CONTRIBUTING.zh-CN.md) | 贡献流程、代码风格、发布流程 |

README 为全部五种语言手动维护；CONTRIBUTING 仅有英文与简体中文两个版本，CHANGELOG 仅有简体中文版本。更新时请同步：

| 语言 | README | CHANGELOG | CONTRIBUTING |
| --- | --- | --- | --- |
| English | `README.md`（根目录） | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |
| 简体中文 | `docs/README.zh-CN.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.zh-CN.md` |
| 繁體中文 | `docs/README.zh-TW.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |
| 日本語 | `docs/README.ja-JP.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |
| 한국어 | `docs/README.ko-KR.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |

> 简体中文是权威源。

---

## ⭐ 支持这个项目

如果 MegaDownloader Revival 帮到了你，欢迎通过以下方式支持项目：

- ⭐ 点右上角 **Star**，支持这个项目
- 🐛 遇到问题 [提 Issue](../../../issues) —— 近期的多个修复都来自用户报告
- 🌍 帮忙改进 [翻译](CONTRIBUTING.zh-CN.md) —— 已有 10 种语言，缺母语校对
- 🔗 分享给还在找 MEGA 下载工具的朋友

<a href="https://star-history.com/#a1175815821/MegaDownloader-Revival&Date">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=a1175815821/MegaDownloader-Revival&type=Date&theme=dark" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=a1175815821/MegaDownloader-Revival&type=Date" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=a1175815821/MegaDownloader-Revival&type=Date" />
 </picture>
</a>

---

## 致谢

- 原 MegaDownloader 作者 **Andres Soliño**
- 复活计划维护者 **Yingxue**（v2.0+），以及所有提 Issue、补翻译的贡献者
- 开源依赖：[BouncyCastle](https://www.bouncycastle.org/) · [Newtonsoft.Json](https://www.newtonsoft.com/json) · [SharpCompress](https://github.com/adamhathcock/sharpcompress) · [ObjectListView](http://objectlistview.sourceforge.net/) · [7-Zip](https://www.7-zip.org/)

## 许可

基于 [MIT License](../LICENSE) 发布。原始版权 © 2018 Andres Soliño，复活计划修复版权 © 2026 MegaDownloader Revival Project 贡献者。

> 仓库内第三方 DLL 遵循各自原始许可证，使用者应自行确认合规性。
