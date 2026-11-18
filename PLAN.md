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
| Phase 1 | 把下载引擎与设置抽取到 `MegaDownloader.Core` | ✅ 完成（`2b4e778`，已推送） |
| Phase 2 | 在 Avalonia 中重建下载队列（虚拟化表格、实时进度） | ✅ 完成 |
| Phase 3 | 迁移剩余对话框与设置页 | 🔄 进行中（添加链接 + 设置第一块） |
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
- [x] 确定边界（见第五节）。
- [x] 用 `git mv` 迁移引擎到 `MegaDownloader.Core/Engine/`（41 个 `.cs`，含 `Cryptography/`、`StreamingLibrary/`）。
- [x] 用 `git mv` 迁移设置与本地化到 `MegaDownloader.Core/Settings/`（5 个 `.cs`）。
- [x] Core 编译通过：**0 错误**。
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

### 3.3 已完成

- [x] Full 重新编译通过（0 错误）。
- [x] 重建并运行回归测试，`PASS: 1121 assertions`。
- [x] 提交并推送 Phase 1（`2b4e778`）。
- [x] 更新 `README.md` 状态段与 `MIGRATION.md`（Phase 1 完成情况、Core 新职责）。
- [x] 将「Avalonia 可自由重写风格与排布」的偏好写入长期记忆。

---

## 四、Phase 2 详细清单

### 4.1 已完成

- [x] Core 新增公开门面 `MegaDownloader.Core/Services/DownloadEngineService.cs`：
  - [x] 加载/保存队列（复用 `Paquete.CargarStartupQueue` / `GuardarEnFichero`，原子写入 + `.bak`）。
  - [x] 500 ms 调度 tick：按 `DescargasSimultaneas` 限额启动 `EnCola` 文件，`MegaQuotaManager.IsQuarantined()` 期间不启动。
  - [x] `DownloadEngineState`（Stopped / Running / Paused）与全局 `StartAll` / `PauseAll` / `StopAll`。
  - [x] 复用 `DownloadScheduleController` 与 `DownloadSchedule`，并遵循 `ComenzarDescargando` 自动开始。
  - [x] 单文件开始/暂停（`PausaIndividual`、`DescargaIndividual`、`ThrottledStreamController`）。
  - [x] 删除条目：引用同一性查找、取消后清理、`MarcadoParaBorrarFicheroLocal`、`RemovePendingExtraction`、优先级重排。
  - [x] 每 5 秒自动保存队列；退出时保存。
- [x] `DownloadsPageViewModel` 重写：500 ms 轮询引擎、按引擎对象缓存行 ViewModel（选中项在刷新后保持）、包/文件两级行、展开/折叠、实时进度/速度/ETA、状态计数、状态筛选、工具栏开始/暂停/停止、行内开始/暂停/删除、右键「删除并删除本地文件」确认。
- [x] `DownloadsPageView.axaml` 重新设计：虚拟化 `ListBox` 队列、状态条、空状态、主题 token 样式（未照搬 WinForms 布局）。
- [x] `App.axaml.cs` 注册 `DownloadEngineService` 单例，并在应用退出时停止轮询计时器、保存并释放引擎。

### 4.2 行为说明与后续项

- 队列按包分组显示，状态「分组」以状态筛选（All / Active / Queued / Paused / Completed / Failed）+ 顶部计数实现。
- 保存沿用引擎自身的锁策略：`Save()` 只在 `Mutex.ListaDescargas` 内复制包列表，XML 序列化与落盘在锁外执行（`Paquete.GuardarXML` 内部自行短锁快照文件列表，`GuardarEnRuta` 只在 `Mutex.GuardarDownloadList` 内做标记/写入），因此 5 秒周期保存不会阻塞调度 tick。
- 删除条目在 UI 线程之外执行（`Fichero.Stop()` 最长等待 5 秒），避免界面冻结。
- 尚未暴露：拖拽调整优先级、错误重试策略（`ResetearErrores`）的界面、单文件限速设置。
- 「添加链接」入口仍是占位对话框；链接解析与预检属于 Phase 3。

### 4.3 复核修正（2026-11-18）

对照 WinForms 的调度/删除实现复核 `DownloadEngineService` 后，修正了以下行为差异：

- [x] **启动期空队列覆盖**：定时器改为在 `LoadAsync` 成功后才启动，`Save()` 在 `_loaded` 置位前直接返回。原实现构造即启动 500 ms 定时器且 `_lastSaveUtc=MinValue`，第一次 tick 可能在队列读完前用空列表覆盖 `DownloadList.xml`。
- [x] **全局恢复**：`StartAll` 清除所有 `PausaIndividual`；调度 tick 先 `Resume()` 暂停文件、再启动 `EnCola` 文件。原实现"暂停全部 → 开始全部"不会恢复已暂停下载（`FileDownloader.IsPaused` 未清除）。
- [x] **连接数**：`file.Start` 传入 `ConexionesPorFichero`，不再把"剩余同时下载名额"当作每文件连接数。
- [x] **停止语义**：`StopAll` 只置状态并中止限速流，真正的 `Fichero.Stop()` 由调度线程执行（与 `PararDescargaFicheros` 一致），避免 UI 线程被每文件最长 5 秒的等待卡住。
- [x] **暂停补漏**：暂停状态下每个 tick 重新暂停仍处于 `Descargando`/`CreandoLocal` 的文件，覆盖暂停瞬间仍在启动的任务。
- [x] **删除清理**：设置 `LimpiezaPaquete`，用 `DisposalHandled` 门闩防止取消回调重复执行，并在删除后清理空目录（含包目录），与 `Main.LimpiarDirectoriosVacios` 一致。
- [x] **tick 重入**：新增 `_tickRunning` 互斥，避免慢 `Stop()`/保存导致 500 ms 定时器回调重叠。
- [x] **并发名额**：调度计数只统计真正占用下载名额的状态（`Descargando`/`CreandoLocal`/`Verificando`），MD5 校验与解压不再阻塞后续排队文件。
- [x] **UI**：行缓存只淘汰已离开队列的对象（筛选隐藏的行保留展开状态）；退出时仅在页面已创建时 Dispose。
- [x] **回归稳定性**：`SevenZipCancelCase` 的子进程启动等待由 5 秒放宽到 60 秒；本机实测 `RegressionTests.exe` 启动并写 PID 文件需 6.1–6.3 秒，偶发更慢，原预算会导致该断言随机失败（与功能无关；取消本身的 5 秒断言未改动）。

修正后解决方案 **0 错误**；20 秒预算版本连续 5 次通过，最终 60 秒预算版本再复测 3 次均 **`PASS: 1121 assertions`**。

---

## 五、Phase 3 详细清单

### 5.1 已完成

- [x] **添加链接窗口**（`AddLinksWindow` + `AddLinksViewModel`）：
  - 链接文本、下载目录（Browse）、包名、压缩密码、创建子目录 / 自动解压 / 添加后开始下载。
  - 复用 Core 的 `PreflightSession` / `BatchLinkPreflight` / `PreflightSelection`：进度、结果列表、状态（Ready / Already queued / Duplicate / Invalid / Unavailable）、按名称过滤、全选/清空、选择计数与总大小。
  - 复用 `DownloadPackageBuilder.Build` 在后台构建包，`DownloadEngineService.AddPackage` 原子入队；入队前再次按 `BatchLinkPreflight.Identity` 去重。
  - 记住上次选项（`UltimaConfiguracionUsada`），默认目录取 `Configuracion.RutaDefecto`。
- [x] **`DownloadEngineService` 入队接口**：`GetQueuedLinkSnapshot()`、`AddPackage()`、`IsLoaded`；`AddPackage` 与调度/删除共用 `_scheduleGate`。
- [x] **设置页第一块**：默认下载目录、同时下载数、每文件连接数、创建子目录 / 自动解压 / 启动时开始下载 / 记住上次选项，保存到 `Configuration.xml`；外观主题保留。
- [x] **主工具栏**：Start / Pause / Stop 绑定到队列页命令与可用状态，不再是无动作占位按钮。
- [x] 冒烟验证：UI Automation 打开 Add links、填入无效链接并预检，结果行显示 `Unavailable`；Settings 页正常渲染。

### 5.2 待办

- [ ] 属性对话框（单任务/包：路径、限速、密码、优先级）。
- [ ] 编码链接 / ELC 对话框。
- [ ] 流媒体库管理页（依赖 Phase 4 的 HTTP 服务器替换）。
- [ ] 设置页剩余标签：代理、调度、语言、预共享密钥、ELC 账户、高级选项。
- [ ] 添加链接：剪贴板监听、`mega://` 协议入口、文件夹分享的完整路径展示。

---

## 六、架构边界（Phase 1）

| 归属 | 内容 |
|---|---|
| `MegaDownloader.Core` | 下载引擎（网络、文件、包、调度、配额、校验、解压、流媒体库）、预检模型、设置与本地化、日志、更新器、`IPlatformServices` 平台抽象、`UserNotification`、`StreamingLibraryRoutes`、`DownloadEngineService` |
| `MegaDownloader.Full`（保留） | WinForms 界面与窗体、`HttpModule`、Stegano、剪贴板/单实例管理、主题与 `UiStyles`、`PreflightTree`（视图模型，依赖 WinForms `CheckState`）、`ResourceHelper`（内嵌 DLL 加载）、`MegaURIProtocol`、`SettingsLayout`、`StartupRegistration` |
| `MegaDownloader.UI` | Avalonia 外壳；消费 Core 的公开 API（`DownloadEngineService`） |

已引入的跨平台接缝：

- `UserNotification.Sink`：引擎向上层发消息，WinForms 壳安装 `MessageBox` 实现，Avalonia 壳后续可接通知/对话框服务。
- `IPlatformServices`：开机自启、`mega://` 协议、默认程序打开、关机、查找可执行文件。
- 资源随 Core 走：语言包、`InternalConfig.xml`、`7zr.exe` 在 Core 内嵌；Full 保留自身副本以兼容既有测试。

---

## 七、当前构建状态

| 工程 | 状态 |
|---|---|
| `MegaDownloader.Core` | ✅ 0 错误（110 个既有引擎警告） |
| `MegaDownloader.Full` | ✅ 0 错误（5791 个既有 CA1416 警告） |
| `MegaDownloader.UI` | ✅ 0 错误（警告均来自 Core） |
| `MegaDownloader.Tests` | ✅ 0 错误，运行 `PASS: 1121 assertions` |

运行验证：Avalonia 壳启动后加载 `DownloadList.xml` 并按 5 秒周期回写（最后一次启动 19:33:31，队列文件 19:33:32 回写，日志无异常）；无头冒烟测试确认 `DownloadEngineService` 的加载与状态切换（Stopped → Running → Paused → Stopped）正常。以上结果均为最后一次修改（保存锁策略、删除移出 UI 线程）之后重新构建并复测。

Git 工作区：Phase 3 的修改尚未提交（分支 `refactor/avalonia`，最新提交 `74997a3`）。

---

## 八、验证命令

```bash
cd "I:/Mega Downloader"
dotnet build MegaDownloader.Core/MegaDownloader.Core.csproj -c Debug
```

```bash
dotnet build MegaDownloader.Full/MegaDownloader.Full.csproj -c Debug
```

```bash
dotnet build MegaDownloader.UI/MegaDownloader.UI.csproj -c Debug
```

```bash
dotnet build MegaDownloader.Tests/MegaDownloader.Tests.csproj
```

```bash
dotnet run --project MegaDownloader.Tests/MegaDownloader.Tests.csproj --no-build -- "I:/Mega Downloader" "I:/Mega Downloader/MegaDownloader.Full/bin/Debug/net10.0-windows"
```

期望最后一条输出 `PASS: 1121 assertions`。

---

## 九、后续阶段要点

- **Phase 3**：迁移添加链接、预检树、属性、设置各标签页、编码/ELC、流媒体库管理等对话框。添加链接后把新 `Paquete` 写入 `DownloadEngineService` 的队列。
- **Phase 4**：用 ASP.NET Core/Kestrel 替换内嵌 `HttpServer.dll`；替换 `ObjectListView`；处理 `7zr.exe` 的 RID 分发或转为纯 SharpCompress；DPAPI 调用点全部走 `IPlatformServices`。
- **Phase 5**：三平台打包、CI 发布、更新器适配。
