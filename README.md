# MegaDownloader

> **MegaDownloader 复活计划 (Revival Project)**\
> 基于 MegaDownloader v1.8 反编译源码修复而成，完成60+ 项修复。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://dotnet.microsoft.com/)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![Language](https://img.shields.io/badge/Language-VB.NET-005a9c.svg)](https://docs.microsoft.com/dotnet/visual-basic/)
[![Build](https://github.com/a1175815821/MegaDownloader-Revival/actions/workflows/build.yml/badge.svg)](../../actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/a1175815821/MegaDownloader-Revival?include_prereleases)](../../releases/latest)
[![Downloads](https://img.shields.io/github/downloads/a1175815821/MegaDownloader-Revival/total)](../../releases)
[![Stars](https://img.shields.io/github/stars/a1175815821/MegaDownloader-Revival?style=social)](../../stargazers)
[![Issues](https://img.shields.io/github/issues/a1175815821/MegaDownloader-Revival)](../../issues)

***

## 目录

- [项目背景](#项目背景)

- [v2.4 主要变更](#v24-主要变更)

- [功能特性](#功能特性)

- [技术栈](#技术栈)

- [项目结构](#项目结构)

- [构建说明](#构建说明)

- [使用方法](#使用方法)

- [支持的语言](#支持的语言)

- [支持的链接格式](#支持的链接格式)

- [致谢与版权](#致谢与版权)

- [许可协议](#许可协议)

***

## 项目背景

MegaDownloader 是一款由西班牙开发者 **Andres Soliño \[andres\_age]** 创建的 MEGA 网盘下载管理器,因其轻量、稳定、支持多线程下载而广受用户欢迎。然而,原项目自 v1.8 后停止维护,随着 MEGA 站点链接格式的更新 (`mega.nz/file/...`、`mega.nz/folder/...`),旧版程序已无法识别新版链接,导致核心功能失效。

本项目即 **MegaDownloader 复活计划**:通过对 v1.8 进行反编译得到源码,并在其基础上进行修复与重构,让这款经典工具重新焕发生机。

- **v1.9**(2026-07-05):修复新版 MEGA 链接格式识别问题

- **v2.0**(2026-07-13):完成 4 阶段 60+ 项修复,涵盖安全、资源泄漏、代码清理,新增深/浅色主题切换

- **v2.1**(2026-07-19):深色主题可用性修复(主列表、进度条、按钮白边、右键菜单、设置即时换肤等)

- **v2.2**(2026-07-20):路径安全、MEGA MetaMAC/Range/断点完整性、原子配置保存、Web CSRF、解压与发布加固

- **v2.3**(2026-08-13):修复加密失败崩溃、资源泄漏与潜在死锁,移除 DLC 处理的 `Thread.Abort`

- **v2.4**(2026-08-14\~2026-09-01):21 项 bug 修复(2.4.0);下载完成误报错误修复(2.4.1);下载文件真实损坏修复——MetaMAC 算法对齐 MEGA SDK 线性分块调度、mismatch 宽松策略、非对齐续传 CTR 密钥流错位防护(2.4.2);7z 解压支持(内置 7zr.exe)、Web 局域网推送开关、剪贴板网页复制漏检修复(2.4.3);子文件夹链接下载、MetaMAC 分块初值 nonce 修复、9 项安全加固(2.4.4)

- **v2.4.5**(2026-09-02):全面代码审查后的系统性修复——全局异常兜底、列表刷新闪退、缺分卷 RAR 假成功、UI 冻结 16.5 秒、streaming Range RFC 7233 合规、流媒体库 CSRF、登录限速、Stegano 先写坏后校验、多项资源泄漏与维护性清理

> ⚠️ **法律声明**:本项目源自对第三方已发布软件的反编译,目的仅在于修复兼容性问题以恢复其可用性。若原作者认为本仓库侵犯了其权益,请通过 Issue 联系,我们将配合处理。

## v2.4 主要变更

### 稳定性与安全(v2.4.5)

| 修复             | 说明                                                                                       |
| -------------- | ------------------------------------------------------------------------------------------ |
| 全局异常兜底       | 此前无任何兜底,UI 异常直接闪退;现在记日志且不退出,后台线程异常留日志                           |
| 列表刷新闪退       | 4 个 AspectGetter 报错时每行重绘弹一次窗再崩溃;改为记日志返回占位值                              |
| 缺分卷 RAR 假成功   | `IsComplete=False` 静默跳过且上游报"解压成功";现显式抛错                                       |
| UI 冻结         | 分块失败退避的忙等跑在 UI 线程(最长 16.5 秒);移到线程池                                          |
| Streaming Range | RFC 7233 合规:后缀/开放区间、416 响应、`bytes=0-0` 不再拉整个文件、响应体不超发 Content-Length        |
| 流媒体库 CSRF     | Delete/Save/OpenVLC/Import/Export 要求 POST + token(复用 Web 界面 EnsureCsrf 模式)              |
| 登录限速          | 并发上限 4 + 60 秒窗口失败锁定 10 次,防 PBKDF2 POST 轰炸打满线程池                                |
| Stegano 落盘安全   | 内存编码+校验通过才写盘(不再留写坏的 .jpg);`WriteAllBytes` 截断覆盖(不再拼接旧文件尾部)             |
| 资源泄漏          | FileDownloader 句柄、`CreateDecryptor`/MD5 Using、5 处 Mutex Try/Finally、5 处悬停 ToolTip       |
| 维护性           | vbproj 死引用、DPI 配置统一 PerMonitorV2、硬编码英文消息接入语言系统(新增 en-US/zh-CN 条目)          |

### 新功能与完整性(v2.4.4)

| 功能/修复          | 说明                                                                                                                                                      |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 子文件夹链接下载       | `mega.nz/folder/根ID#密钥/folder/子ID` 仅下载指定子文件夹(路径重定基);`/file/文件ID` 仅下载指定文件——此前一律下载整个根文件夹                                                                  |
| MetaMAC 分块初值修复 | 分块 CBC-MAC 初值由零 IV 改为文件 nonce 复制两份 `[n0,n1,n0,n1]`(对齐 SDK `SymmCipher::ctr_crypt`),修复 8 words key 下载完成后必然误报校验错误                                         |
| MetaMAC 标准校验   | 移除"分块边界前缀匹配"宽容逻辑,与 SDK 一致:读完整个文件后一次性完整比较                                                                                                                |
| 9 项安全加固        | StripNullCharacters 偏移修复;AES 失败返回 Nothing 且持久化点保留旧值;密文新增随机 IV 格式(兼容旧数据);Web 密码 PBKDF2(100k)+随机盐;Streaming 恒定时间密码比较;PSK 非 ASCII 校验;ClientConnected 反射健壮化 |

### 新功能(v2.4.3)

| 功能        | 说明                                                                          |
| --------- | --------------------------------------------------------------------------- |
| 7z 解压     | 优先系统 7-Zip,未安装时自动释放内置 7zr.exe(公共域);支持密码与 multipart 分卷;解压前 PathGuard 校验防路径逃逸 |
| Web 局域网推送 | 「允许局域网访问」开关(默认关),开启后手机/局域网设备可经浏览器推送下载;强制密码保护;支持自定义绑定 IP(留空=全部网卡)            |
| 剪贴板监控修复   | 浏览器延迟渲染 + 剪贴板占用竞态导致网页复制漏检——改为重试读取,全部访问加异常保护                                 |

### 下载完整性(v2.4.2)

| 修复          | 说明                                                                     |
| ----------- | ---------------------------------------------------------------------- |
| MetaMAC 算法  | 分块调度对齐 MEGA SDK `ChunkedHash`:128 KiB × i(i=1..8)后固定 1 MiB;空文件返回 (0,0) |
| 下载完成判定      | 移除"文件大小匹配即强制完成";仅真实 chunk 全部完成才判定完成;120 秒超时上报失败并保留断点                   |
| CTR 密钥流错位防护 | 中断 flush 与续传起点强制 16 字节对齐;启动时回退旧版遗留的非对齐进度——杜绝"大小正确但内容损坏"                |

### 稳定性(v2.4.0/2.4.1)

| 修复       | 说明                                                |
| -------- | ------------------------------------------------- |
| 后台线程弹窗卡死 | 下载失败改经 UI 线程呈现;关闭期间跨线程 MsgBox 加 `IsDisposed` 防护   |
| 并发污染     | Streaming 模块 AJAX 响应改 `AsyncLocal`,多请求互不串扰        |
| 资源泄漏     | Mutex `Try/Finally` 释放;`BackgroundWorker.Dispose` |
| 公开链接误报   | 4 words key 无 MetaMAC 时跳过校验(记日志),不再误判失败           |

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

- **Web 界面**:内置 HttpServer,可通过浏览器远程管理下载任务(默认仅绑定 `127.0.0.1`;设置 → Web 服务器可开启「允许局域网访问」并指定绑定 IP)

- **流媒体库**:可视化管理流媒体资源 (StreamingLibrary)

- **Stegano 隐写**:对图片/视频进行隐写编码与解码

- **自动解压**:基于 SharpCompress 的下载后自动解压 (RAR/7Z/ZIP)

- **多语言界面**:支持 10 种语言,可扩展

- **深/浅色主题**:支持跟随系统或手动切换(Auto 可实时跟随系统)

- **下载完整性**:MEGA MetaMAC 校验、严格 Range 与断点元数据检查(v2.2)

- **路径安全**:统一 PathGuard,解压防 Zip Slip(v2.2)

- **MegaSearchDesktop**:与桌面搜索集成 (MSD 构建)

## 技术栈

| 技术                        | 用途             |
| ------------------------- | -------------- |
| VB.NET                    | 主开发语言          |
| .NET Framework 4.8        | 运行时            |
| WinForms                  | UI 框架          |
| BouncyCastle.Cryptography | 加密 (RSA/AES)   |
| Newtonsoft.Json           | JSON 解析        |
| ObjectListView            | 高级 ListView 控件 |
| SharpCompress             | 压缩包解压          |
| HttpServer (Fadd)         | 内置 Web 服务器     |
| F5Lib                     | 隐写术 (Stegano)  |

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

   | 配置            | 说明                                                       |
   | ------------- | -------------------------------------------------------- |
   | `Debug`       | 调试版本,输出到 `bin/Debug/`                                    |
   | `Release`     | 发布版本,输出到 `bin/Release/`(v2.2 起不再使用 mpress 压缩)            |
   | `Debug_MSD`   | 调试 MegaSearchDesktop 集成版本                                |
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

| 语言                 | 文件                   |
| ------------------ | -------------------- |
| English            | `en-US-Language.xml` |
| Español            | `es-ES-Language.xml` |
| 简体中文               | `zh-CN-Language.xml` |
| 繁體中文               | `zh-TW-Language.xml` |
| Français           | `fr-FR-Language.xml` |
| Deutsch            | `de-DE-Language.xml` |
| Italiano           | `it-IT-Language.xml` |
| Português (Brasil) | `pt-BR-Language.xml` |
| Magyar             | `hu-HU-Language.xml` |
| Română             | `ro-RO-Language.xml` |

## 支持的链接格式

- `mega.nz/#...!FileID!FileKey` (旧版)

- `mega.nz/file/FileID#FileKey` (新版 ★)

- `mega.nz/folder/FolderID#FolderKey` (新版文件夹 ★)

- MEGA URI 协议:`mega://#!...`、`mega://enc?...`、`mega://elc?...`

> v2.0 已移除对以下已下线服务的支持:MegaCrypter、YouPaste、LinkCrypter、EncrypterMe.ga、goo.gl 短链、IMDB/Allocine/Filmaffinity 电影信息

## 致谢与版权

- 感谢原 MegaDownloader 作者 **Andres Soliño \[andres\_age]** 的卓越工作

- 感谢复活计划维护者 **Yingxue**(v2.0+)

- 感谢以下开源库的作者:

  - [BouncyCastle.Cryptography](https://www.bouncycastle.org/)

  - [Newtonsoft.Json](https://www.newtonsoft.com/json)

  - [SharpCompress](https://github.com/adamhathcock/sharpcompress)

  - [ObjectListView](http://objectlistview.sourceforge.net/)

  - [mpress](https://www.matcode.com/mpress.htm)

  - [7-Zip](https://www.7-zip.org/)

## 许可协议

本项目基于 [MIT License](LICENSE) 发布。原始版权所有 © 2018 Andres Soliño,复活计划修复版权所有 © 2026 MegaDownloader Revival Project 贡献者。

> 本仓库中包含的第三方 DLL 文件遵循各自原始许可证。使用者应自行确认这些依赖的合规性。

