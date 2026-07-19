# MegaDownloader

> **MegaDownloader 复活计划 (Revival Project)**  
> 基于 MegaDownloader v1.8 反编译源码修复而成。v2.2 完成路径安全、下载完整性(MetaMAC/Range/断点)与一批可靠性加固;v2.1 完善深色模式;v2.0 完成 4 阶段 60+ 项修复与主题切换。

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
- [v2.2 主要变更](#v22-主要变更)
- [v2.1 主要变更](#v21-主要变更)
- [v2.0 主要变更](#v20-主要变更)
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

MegaDownloader 是一款由西班牙开发者 **Andres Soliño [andres_age]** 创建的 MEGA 网盘下载管理器,因其轻量、稳定、支持多线程下载而广受用户欢迎。然而,原项目自 v1.8 后停止维护,随着 MEGA 站点链接格式的更新 (`mega.nz/file/...`、`mega.nz/folder/...`),旧版程序已无法识别新版链接,导致核心功能失效。

本项目即 **MegaDownloader 复活计划**:通过对 v1.8 进行反编译得到源码,并在其基础上进行修复与重构,让这款经典工具重新焕发生机。

- **v1.9**(2026-07-05):修复新版 MEGA 链接格式识别问题
- **v2.0**(2026-07-13):完成 4 阶段 60+ 项修复,涵盖安全、资源泄漏、代码清理,新增深/浅色主题切换
- **v2.1**(2026-07-19):深色主题可用性修复(主列表、进度条、按钮白边、右键菜单、设置即时换肤等)
- **v2.2**(2026-07-20):路径安全、MEGA MetaMAC/Range/断点完整性、原子配置保存、Web CSRF、解压与发布加固

> ⚠️ **法律声明**:本项目源自对第三方已发布软件的反编译,目的仅在于修复兼容性问题以恢复其可用性。若原作者认为本仓库侵犯了其权益,请通过 Issue 联系,我们将配合处理。

## v2.2 主要变更

### 路径安全与下载完整性

| 项 | 说明 |
| --- | --- |
| PathGuard | 远端名/解压/删除/写出限制在下载根目录内,防目录逃逸与 Zip Slip |
| MetaMAC | 下载完成前校验 MEGA 文件完整性,失败不标记成功 |
| Range / EOF / CTR | 严格校验分块响应;提前 EOF 失败;大文件 CTR seek 使用 Int64 |
| 断点续传 | 校验分块元数据,避免 `.part` 缺失时的假完成 |
| 原子保存 | 配置与下载队列 temp + `File.Replace` |
| Web / Streaming | 状态修改 POST+CSRF;媒体 URL 固定 `127.0.0.1` |
| 其它 | 解压配额与真实结果状态、语言 en-US 回退、去 MPRESS/xUnit、DPI PerMonitorV2 |

详见 [CHANGELOG.md](docs/CHANGELOG.md)。

## v2.1 主要变更

### 深色主题可用性

| 修复 | 说明 |
| --- | --- |
| 下载列表斑马纹 | 不再写死 White/Honeydew,使用主题 Back/AltBack |
| 进度条 | 主题化 Progress 色,告别高亮青/绿 |
| 按钮白边 | Flat + 主题 Border,去掉 Visual Styles 3D 高光 |
| 设置即时换肤 | 保存主题后主窗立即刷新,无需重启 |
| 右键菜单 / ELC / Stegano | ContextMenu、账号表、隐写向导与 Splash 均套主题 |
| 语义色 | ErrorFore / SuccessFore 在深色下可读 |

详见 [CHANGELOG.md](docs/CHANGELOG.md)。

## v2.0 主要变更

### 新增功能

#### 深色/浅色主题切换

- **默认跟随系统**:Auto 模式通过读取注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` 自动匹配 Windows 深浅色设置
- **手动切换**:在 设置 → 常规 → 主题 中可选 Auto / Light / Dark
- **完整覆盖**:主窗体下载列表(TreeListView)、StatusStrip、ContextMenuStrip、所有 9 个子窗体、30+ ToolStrip 渐变属性

### 安全加固

| 修复 | 说明 |
| --- | --- |
| TLS 协议 | 仅启用 TLS 1.2,移除 TLS 1.0/1.1 |
| 服务器绑定 | Web/Streaming 服务器绑定 `127.0.0.1`(原 `0.0.0.0` 暴露到全网) |
| 密码验证 | 修复空密码绕过校验 |
| 代理凭据 | 修复代理用户名/密码未实际赋值给 WebProxy |
| 密码哈希 | 统一使用 UTF-8 编码 |
| 死锁防护 | Mutex 操作全部包裹 Try/Finally |

### 资源泄漏修复(12 项)

- 7 处 ToolTip 泄漏、Image 生命周期、StreamReader/Writer 未 Using、注册表句柄泄漏、关闭顺序错误等,详见 [CHANGELOG.md](docs/CHANGELOG.md)

### 死代码删除(11 个文件)

- 4 个 Crypter(EncrypterMega / MegaCrypter / Youpaste / LinkCrypter)
- 3 个 MovieInfo(Allocine / Filmaffinity / IMDB)
- DLCHelper / Linkdecrypter / LinkProtectors / Serializer / ClipboardChangeNotifier
- MegaUploader 菜单项、14 个 goo.gl 短链、Ping 上报

### 协议与代码质量

- `%SEQ%` / `%ID%` 序列号改用 `Interlocked.Increment`(原用毫秒 ticks,范围 0-999)
- `http://mega.co.nz/#N!` → `https://mega.nz/#N!`
- `GetHashCode` 比较改为直接 `OuterXml` 字符串比较
- 变量名冲突修复(`ex` → `rx`,`int` → `bytesRead`)

## 功能特性

- **多线程下载**:支持对同一文件建立多路并发连接,大幅提升下载速度
- **速度限制**:`ThrottledStream` 全局/单任务限速
- **断点续传**:支持任务暂停、恢复、错误重试
- **剪贴板监控**:自动识别复制到剪贴板的 MEGA 链接
- **拖拽支持**:拖拽链接到主窗口即可加入下载队列
- **MEGA 文件夹**:支持递归解析并下载整个分享文件夹
- **加密链接**:支持 `enc?` / `enc2?` / `fenc?` / `fenc2?` / `elc?` 多种加密链接格式
- **ELC 容器**:支持加密链接容器 (Encrypted Link Container) 的导入与导出
- **流媒体播放**:集成 VLC,边下边播 (Streaming)
- **Web 界面**:内置 HttpServer,可通过浏览器远程管理下载任务(仅绑定到 `127.0.0.1`)
- **流媒体库**:可视化管理流媒体资源 (StreamingLibrary)
- **Stegano 隐写**:对图片/视频进行隐写编码与解码
- **自动解压**:基于 SharpCompress 的下载后自动解压 (RAR/7Z/ZIP)
- **多语言界面**:支持 10 种语言,可扩展
- **深/浅色主题**:支持跟随系统或手动切换(Auto 可实时跟随系统)
- **下载完整性**:MEGA MetaMAC 校验、严格 Range 与断点元数据检查(v2.2)
- **路径安全**:统一 PathGuard,解压防 Zip Slip(v2.2)
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
| ~~mpress~~ | v2.2 起已从 Release 构建移除 |

## 项目结构

```
MegaDownloader/
├── Clases/                         # 核心类库
│   ├── Cryptography/AES.vb         #   AES 加密
│   ├── StreamingLibrary/           #   流媒体库管理
│   │   ├── LibraryElement.vb
│   │   ├── StreamingLibrary.vb
│   │   └── StreamingLibraryManager.vb
│   ├── ApplicationInstanceManager.vb
│   ├── Conexion.vb                 #   HTTP/网络通信
│   ├── Configuracion.vb            #   配置管理
│   ├── ConfiguracionUI.vb          #   UI 配置(主题等)★ v2.0 新增
│   ├── FileDownloader.vb           #   文件下载核心
│   ├── MegaFolderHelper.vb         #   MEGA 文件夹解析
│   ├── MegaURIProtocol.vb          #   mega:// 协议注册
│   ├── Mutex.vb                    #   互斥锁(进程单实例)
│   ├── Paquete.vb                  #   下载包数据
│   ├── ThrottledStream.vb          #   限速流
│   ├── ThemeManager.vb             #   主题管理器 ★ v2.0 新增
│   ├── URLExtractor.vb             #   URL 解析
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
├── Forms/                          # WinForms 窗体 (12 个)
│   ├── Main.vb                     #   主窗体
│   ├── AddLinks.vb                 #   添加链接窗体
│   ├── Configuration.vb            #   设置窗体(含主题切换)
│   ├── StreamingForm.vb            #   流媒体播放窗体
│   ├── Credits.vb                  #   关于/致谢
│   ├── SplashScreen.vb             #   启动画面
│   ├── Cerrando.vb                 #   关闭画面
│   ├── Descompresor.vb             #   解压窗体
│   ├── ELCForm.vb                  #   ELC 容器窗体
│   ├── EncodeLinksForm.vb          #   链接加密窗体
│   ├── PantallaMsg.vb              #   消息提示窗体
│   └── PropiedadesDescarga.vb      #   下载属性窗体
├── docs/                           # 项目文档
│   ├── CHANGELOG.md                #   变更日志
│   ├── BUGFIX-CHECKLIST.md        #   修复清单
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
    | `Release` | 发布版本,输出到 `bin/Release/`(v2.2 起不再使用 mpress 压缩) |
   | `Debug_MSD` | 调试 MegaSearchDesktop 集成版本 |
   | `Release_MSD` | 发布 MegaSearchDesktop 集成版本,输出到 `Resources/Installer MSD/` |

4. `Ctrl + Shift + B` 构建解决方案

5. 构建产物位于 `bin/<Configuration>/MegaDownloader.exe`

### 命令行构建

```bash
# 使用 MSBuild
msbuild MegaDownloader.sln /p:Configuration=Release /p:Platform=x86
```

## 使用方法

  1. 从 [Releases](../../releases) 下载最新 `MegaDownloader-Revival-win-x86.zip`（或 v2.2.0 资源）
2. 解压到任意目录(无需安装,绿色版)
3. 双击运行 `MegaDownloader.exe`
4. 复制 MEGA 链接,程序会自动识别剪贴板内容
5. 也可点击工具栏 **添加链接** 按钮手动粘贴
6. 配置下载目录、并发数、限速等选项于 **设置** 窗口
7. 在 **设置 → 常规 → 主题** 中切换深/浅色(保存后立即生效)

### 链接示例

新版格式(v1.9 起支持):
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
- MEGA URI 协议:`mega://#!...`、`mega://enc?...`、`mega://elc?...`

> v2.0 已移除对以下已下线服务的支持:MegaCrypter、YouPaste、LinkCrypter、EncrypterMe.ga、goo.gl 短链、IMDB/Allocine/Filmaffinity 电影信息

## 致谢与版权

- 感谢原 MegaDownloader 作者 **Andres Soliño [andres_age]** 的卓越工作
- 感谢复活计划维护者 **Yingxue**(v2.0+)
- 感谢以下开源库的作者:
  - [BouncyCastle.Cryptography](https://www.bouncycastle.org/)
  - [Newtonsoft.Json](https://www.newtonsoft.com/json)
  - [SharpCompress](https://github.com/adamhathcock/sharpcompress)
  - [ObjectListView](http://objectlistview.sourceforge.net/)
  - [xUnit.net](https://xunit.net/)
  - [mpress](https://www.matcode.com/mpress.htm)

## 许可协议

本项目基于 [MIT License](LICENSE) 发布。原始版权所有 © 2018 Andres Soliño,复活计划修复版权所有 © 2026 MegaDownloader Revival Project 贡献者。

> 本仓库中包含的第三方 DLL 文件遵循各自原始许可证。使用者应自行确认这些依赖的合规性。
