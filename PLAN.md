# Mega Downloader 重构计划与进度

> 最后更新：2026-11-18
> 分支：`refactor/avalonia`
> 仓库：<https://github.com/a1175815821/MegaDownloader-Revival>
> WinForms 基线标签：`winforms-baseline`

本文件记录 Avalonia 重构的路线图、当前阶段的详细清单和实时进度。README 只保留一句话状态，细节以本文件为准。

---

## 一、总体路线图

| 阶段 | 内容 | 状态 |
|---|---|---|
| Phase 0 | Git 基线 + Avalonia 骨架（DI、主题 token、导航、日志） | ✅ 完成（`f1b0aa5`，已推送） |
| Phase 1 | 把下载引擎与设置抽取到 `MegaDownloader.Core` | ✅ 完成 |
| Phase 2 | 在 Avalonia 中重建下载队列（虚拟化表格、实时进度） | ⬜ 未开始 |
| Phase 3 | 迁移剩余对话框与设置页 | ⬜ 未开始 |
| Phase 4 | 替换内嵌 HTTP 服务器和其余 Windows-only 助手 | ⬜ 未开始 |
| Phase 5 | Windows / macOS / Linux 打包发布 | ⬜ 未开始 |

说明：Avalonia UI 不受旧 WinForms 排布约束，允许重写视觉风格与整体布局，按现代桌面应用继续迭代优化。

---

## 二、Phase 0 已完成内容

- `MegaDownloader.UI`：Avalonia 12.1.6 全新应用（非 WinForms 表单移植）。
- `App.axaml.cs` 作为组合根，使用 `Microsoft.Extensions.DependencyInjection` 注册平台服务、日志、设置、主题、导航、对话框、页面与外壳 ViewModel。
- `Themes/Colors.axaml` 将 WinForms `ThemeManager` 的 30+ 个 `Md.*` token 迁移为 Light/Dark 两套 `ThemeDictionaries`；`Controls.axaml`、`Button.axaml` 提供共享窗口/文本/卡片/导航/按钮样式。
- `IThemeService` 映射 Auto/Light/Dark 到 `Application.RequestedThemeVariant`，并持久化到应用数据目录的 `ui-settings.json`。
- `INavigationService` + `ViewLocator` 承载页面；`MainWindow` 提供导航栏、工具栏与状态栏。
- `IAppLogger` / `FileLogger` 输出滚动日志，`Program` 记录未处理异常到 `crash-yyyyMMdd.log`。
- Downloads / Settings 页面为占位页，等待 Phase 2/3 接入引擎。

---

## 三、Phase 1 详细清单

### 3.1 已完成

- [x] `MegaDownloader.Core` 工程成型（`net10.0`，`RootNamespace=MegaDownloader`）。
- [x] 确定边界（见第四节）。
- [x] 用 `git mv` 迁移引擎到 `MegaDownloader.Core/Engine/`（41 个 `.cs`，含 `Cryptography/`、`StreamingLibrary/`）。
- [x] 用 `git mv` 迁移设置与本地化到 `MegaDownloader.Core/Settings/`（5 个 `.cs`）。
- [x] Core 编译通过：**0 警告 / 0 错误**（已复验）。
- [x] `MegaDownloader.Full` 添加对 Core 的 `ProjectReference`，源码通过编译通配符继续共享 `src/**`。
- [x] 资源归属：Core 内嵌语言包（`*-Language.xml`）、`InternalConfig.xml`、`7zr.exe`；`InternalConfiguration` 与 `Language` 改为相对 `typeof(...).Assembly` 读取资源。
- [x] 去除引擎中的 WinForms 依赖接缝：
  - [x] `UserNotification`（Core，静态 sink）+ `ApplicationEvents` 中安装 WinForms `MessageBox` sink。
  - [x] `StartupRegistration` 移回 Full，`Configuracion.RegisterInStartup` 删除，调用点改为 `StartupRegistration.RegisterInStartup`。
  - [x] `StreamingLibraryRoutes`（Core 常量），`StreamingHelper` 不再引用 `StreamingLibraryModule`。
  - [x] `DescompresorController` 注册表探测加 `OperatingSystem.IsWindows()` 守卫。
  - [x] `DownloadEstadoFilter` 改为 `public`。

### 3.2 已完成：让 `MegaDownloader.Full` 重新编译通过

最初 `dotnet build MegaDownloader.Full` 失败，共 **37 个错误**，全部是同一根因：被移动的成员/类型在 Core 中仍是 `internal`，而 Full（程序集名 `MegaDownloader`）现在跨程序集访问它们。C# 对跨程序集 `internal` 的表现是 `CS0117` / `CS1061` / `CS1729`（成员）与 `CS0122`（类型）。

| 符号 | 错误 | 出现次数 | 主要位置 |
|---|---|---|---|
| `URLExtractor.IsMegaFolder` | CS0117 | 5 | AddLinks, ELCForm ×2, EncodeLinksForm, PantallaMsg |
| `Utils.FormatSpeedLimit` | CS0117 | 3 | Configuration, PropiedadesDescarga ×2 |
| `Utils.TryParseSpeedLimit` | CS0117 | 2 | Configuration, PropiedadesDescarga |
| `Utils.RemoveDiacritics` | CS0117 | 1 | StreamingModule |
| `Conexion.NormalizeProxyHost` | CS0117 | 1 | Configuration |
| `Conexion.ObtenerUrlDesdeAcortador` | CS0117 | 1 | Main |
| `Paquete.CargarStartupQueue` | CS0117 | 1 | Main.Startup |
| `Paquete.DisposeLoadedQueue` | CS0117 | 2 | Main.Startup, Main |
| `Configuracion` 双参数构造函数 | CS1729 | 1 | Main |
| `Fichero.EstablecerError` | CS1061 | 1 | Main |
| `Fichero.RemovePendingExtraction` | CS1061 | 2 | Main |
| `ProcessResourceMonitor`（类型） | CS0122 | 2 | Main |
| `ProcessResourceMonitor.ReadSnapshot` | CS0122 | 1 | Main |
| `ProcessResourceMonitor.Dispose` | CS0122 | 1 | Main |
| `DescompresorController.IsExtracting` | CS1061 | 1 | Main |
| `Criptografia.Utf8BytesOrNull` | CS0117 | 4 | StreamingLibraryModule ×2, StreamingModule ×2 |
| `Criptografia.FixedTimeEquals` | CS0117 | 2 | StreamingLibraryModule, StreamingModule |
| `Criptografia.GetInstaceCipher` | CS0117 | 1 | StreamingModule |
| `LibraryElement.JsonString` | CS0117 | 4 | StreamingLibraryModule |
| `URLProcessor.FileURL.PreflightOrigin` | CS1061 | 1 | PreflightTree |

**修复结果**

1. 在 `MegaDownloader.Core.csproj` 添加 `<InternalsVisibleTo Include="MegaDownloader" />`，Full 继续使用这些内部成员，无需把引擎 API 全部公开；Avalonia UI 仍只面向公开面。
2. `InternalsVisibleTo` 一次性覆盖了全部 37 个错误，没有出现需要单独提升为 `internal` 的 `private` 成员。
3. Full **0 错误**（5791 个既有 CA1416 警告，与重构前同源）。

回归测试另需一处适配：测试通过 `typeof(Main).Assembly.GetType("MegaDownloader.AtomicFile")` / `ProcessResourceMonitor` 反射查找已迁到 Core 的类型。新增 `EngineType(...)` 解析器（先查 `typeof(Log).Assembly`，再回退 Full），替换 4 处调用后测试恢复通过。

### 3.3 待办

- [x] Full 重新编译通过（0 错误）。
- [x] 重建并运行回归测试，`PASS: 1121 assertions`。
- [ ] 提交并推送 Phase 1（提交信息带 `Co-Authored-By: Claude Code <noreply@anthropic.com>`）。
- [x] 更新 `README.md` 状态段与 `MIGRATION.md`（Phase 1 完成情况、Core 新职责）。
- [x] 将「Avalonia 可自由重写风格与排布」的偏好写入长期记忆。

---

## 四、Phase 1 架构边界

| 归属 | 内容 |
|---|---|
| `MegaDownloader.Core` | 下载引擎（网络、文件、包、调度、配额、校验、解压、流媒体库）、预检模型、设置与本地化、日志、更新器、`IPlatformServices` 平台抽象、`UserNotification`、`StreamingLibraryRoutes` |
| `MegaDownloader.Full`（保留） | WinForms 界面与窗体、`HttpModule`、Stegano、剪贴板/单实例管理、主题与 `UiStyles`、`PreflightTree`（视图模型，依赖 WinForms `CheckState`）、`ResourceHelper`（内嵌 DLL 加载）、`MegaURIProtocol`、`SettingsLayout`、`StartupRegistration` |
| `MegaDownloader.UI` | Avalonia 外壳；Phase 2 起直接消费 Core 的公开 API |

已引入的跨平台接缝：

- `UserNotification.Sink`：引擎向上层发消息，WinForms 壳安装 `MessageBox` 实现，Avalonia 壳后续可接通知/对话框服务。
- `IPlatformServices`：开机自启、`mega://` 协议、默认程序打开、关机、查找可执行文件。
- 资源随 Core 走：语言包、`InternalConfig.xml`、`7zr.exe` 在 Core 内嵌；Full 保留自身副本以兼容既有测试。

---

## 五、当前构建状态

| 工程 | 状态 |
|---|---|
| `MegaDownloader.Core` | ✅ 0 警告 / 0 错误 |
| `MegaDownloader.Full` | ✅ 0 错误（5791 个既有 CA1416 警告） |
| `MegaDownloader.UI` | ✅ 0 警告 / 0 错误 |
| `MegaDownloader.Tests` | ✅ 0 错误，运行 `PASS: 1121 assertions` |

Git 工作区：Phase 1 的 `git mv` 与修改尚未提交（分支 `refactor/avalonia`，最新提交 `f1b0aa5`）。

---

## 六、验证命令

```bash
cd "I:/Mega Downloader"
dotnet build MegaDownloader.Core/MegaDownloader.Core.csproj -c Debug
```

```bash
dotnet build MegaDownloader.Full/MegaDownloader.Full.csproj -c Debug
```

```bash
dotnet build MegaDownloader.Tests/MegaDownloader.Tests.csproj
```

```bash
dotnet run --project MegaDownloader.Tests/MegaDownloader.Tests.csproj --no-build -- "I:/Mega Downloader" "I:/Mega Downloader/MegaDownloader.Full/bin/Debug/net10.0-windows"
```

期望最后一条输出 `PASS: 1121 assertions`。

---

## 七、后续阶段要点

- **Phase 2**：在 Avalonia 中实现虚拟化下载队列（树/网格）、实时进度、暂停/继续/删除、状态分组；复用 Core 的 `Paquete` / `Fichero` / `IDescarga` / `DownloadEstadoFilter`。允许重新设计视觉与布局，不照搬 WinForms。
- **Phase 3**：迁移添加链接、预检树、属性、设置各标签页、编码/ELC、流媒体库管理等对话框。
- **Phase 4**：用 ASP.NET Core/Kestrel 替换内嵌 `HttpServer.dll`；替换 `ObjectListView`；处理 `7zr.exe` 的 RID 分发或转为纯 SharpCompress；DPAPI 调用点全部走 `IPlatformServices`。
- **Phase 5**：三平台打包、CI 发布、更新器适配。
