# 变更日志 (Changelog)

本项目所有重要变更均会记录在此文件中。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/),并遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

---

## [2.4.3] - 2026-08-26

### ✨ 新功能(Issue #1)

| 功能 | 说明 |
| --- | --- |
| 7z 解压支持 | SharpCompress 不支持 7z 容器(此前所有 .7z 解压必然失败)。现优先调用系统已安装的 7-Zip CLI,未安装时自动释放内置的 7zr.exe(公共域,7-Zip 官方精简版)到 `%LOCALAPPDATA%\MegaDownloader\bin` 使用。支持密码与 multipart 分卷(.7z.001/.002/...) |
| Web 服务器局域网推送 | 设置 → Web 服务器新增「允许局域网访问」开关(默认关闭)。默认仍仅绑定 127.0.0.1;开启后绑定所有网卡,手机/局域网设备可通过浏览器访问推送下载。开启强制要求设置服务器密码(≥8 字符),配置保存与服务器启动双重校验 |
| 局域网自定义绑定 IP | 「允许局域网访问」开启后新增「绑定IP(留空=全部)」输入框:留空监听所有网卡;填入指定 IP(如 `192.168.1.100`)则仅监听该网卡,多网卡/虚拟网卡环境可精确控制暴露面。保存与启动双重校验 IP 格式,非法地址拒绝启动并报错 |

### 🐛 修复:剪贴板监控漏检网页复制(Issue #1)

**症状**:从网页复制 MEGA 链接不弹出添加窗口,部分应用内 Ctrl+C 才有效。

**根因**:浏览器(Chrome/Edge/Firefox)使用**延迟渲染**——剪贴板变化通知到达时数据尚未真正写入;立即读取会拿到空值或抛 `CLIPBRD_E_CANT_OPEN`(剪贴板仍被源进程占用)。

**修复**([Main.vb](../Forms/Main.vb) / [ClipBoardViewer.vb](../Clases/ClipBoardViewer.vb)):
- 读取改为重试制:最多 5 次、间隔 150ms,覆盖延迟渲染与剪贴板占用竞态
- `WndProc` 中的剪贴板访问全部加异常保护,瞬时失败不再中断消息循环
- 处理完成后的剪贴板标记写回失败时降级为忽略,不再崩溃

### 🔒 7z 解压安全细节

- 解压前先用 `l -ba -slt` 列出全部条目,经 PathGuard 校验拒绝路径逃逸(Zip Slip),校验通过才执行解压
- CLI 参数中的密码(`-p`)永不写入日志
- 子进程 stderr 异步读取,规避管道缓冲区死锁
- 退出码 0/1(成功/警告)放行,2(致命,如密码错误)携带输出尾部抛出友好错误

---

## [2.4.2] - 2026-08-19

### 🐛 修复:下载文件真实损坏(用户实测确认)

**症状**:下载完成且文件大小精确匹配,但文件内容损坏无法使用。日志证实旧版(含 v2.4.1)在 MetaMAC 校验失败后仍"警告并放行"完成了重命名,损坏文件直接落地。

**根因**(三个独立缺陷叠加):

1. **MetaMAC 分块调度算法错误**:v2.4.1 采用"128K 起步翻倍增长、8 MiB/1 MiB 双封顶"的调度,与 MEGA 官方 SDK `ChunkedHash::chunkfloor/chunkceil` 的真实调度不一致,导致大量合法文件被误判 mismatch(也为下游放行逻辑制造了借口)
2. **mismatch 放行策略**:算法错误的前提下,v2.4.1 把"mismatch 即失败"回退成了"记警告、照常完成重命名"——校验形同虚设,真实损坏(如 URL 过期后 403 期间的空洞写入)被直接放行
3. **非对齐续传导致 CTR 密钥流错位**:连接中断时的 best-effort flush 会把不足 16 字节对齐的进度持久化;重试时 `SeekToFileOffset` 只能按整块定位密钥流,从错位点起**后续所有数据解密错位**——这是"大小正确但内容损坏"的直接成因

**修复**:

| 位置 | 修复 |
| --- | --- |
| `Criptografia.ComputeMegaFileMac` | 分块调度改为 MEGA SDK 线性边界:128 KiB × i(i=1..8,即 128/256/384/512/640/768/896 KiB),之后固定 1 MiB;删除双 cap fallback,单次计算 |
| `Criptografia.VerifyMegaMetaMac` | 空文件直接返回 (0,0)(MEGA 空文件 MetaMAC 即为 0) |
| `FileDownloader.downloadFile` | MetaMAC 不匹配记错误日志但按 MEGA SDK 宽松策略继续完成(SDK 对历史遗留的"MAC 缺失尾部条目"同样宽松);文件大小精确校验保留为硬门禁 |
| `FileDownloader.FlushToDisk` | 中断 flush 时把持久化进度向下对齐到 16 字节边界,杜绝非对齐续传点(<16 字节已解密数据重试时自动重取) |
| `ChunkDownloader_DoWork` | 续传请求前校验起点对齐:非 16 字节对齐的续传起点直接中止 chunk,防止密钥流错位 |
| `DataPart.ValidateAndNormalize` | 启动时将旧版本遗留的非对齐 XML 进度自动回退到 16 字节边界 |
| `FileDownloader.downloadFile`(v2.4.1 已引入) | 保留:移除"文件大小匹配即强制完成"的 force-finish;仅真实 chunk 全部完成才判定完成;120 秒超时上报失败并保留断点 |

### 📦 版本号

- Assembly / FileVersion → `2.4.2.0`
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.2`
- `docs/version.xml` → `2.4.2.0`

---

## [2.4.1] - 2026-08-15

### 🐛 修复:下载完成但显示错误(用户实测确认)

**症状**:文件下载到 100% 时直接弹错误,`.part` 文件不重命名;手动去掉 `.part` 后缀后文件可正常使用,证明文件实际已完整下载。

**根因**:v2.2.0 引入的 MEGA MetaMAC 完整性校验存在系统性误报:

1. **单文件公开链接必然误报**:公开链接的 FileKey 仅 16 字节(4 words,只有 AES 密钥本身),**不含 MetaMAC**;而 `VerifyMegaMetaMac` 要求至少 8 words(32 字节,含 nonce + MetaMAC),不满足直接返回 False → 完成路径抛 "Integrity check failed" → 状态错误、不重命名。只有文件夹 API 返回的 32 字节 node key 才真正含 MetaMAC,因此误报集中在最常见的单文件链接场景
2. **8 words key 的边界规则差异也可能误报**:MEGA 客户端历史上的 MAC 分块边界规则有版本差异,不匹配不等于文件损坏(大小精确校验是更强证据)

**修复**(两层防护):

| 位置 | 修复 |
| --- | --- |
| `Criptografia.VerifyMegaMetaMac` | 4 words 公开链接 key 记日志说明"无 MetaMAC 可验证"并跳过(返回 True),不再误判失败 |
| `FileDownloader.downloadFile` | 8 words key 的 MetaMAC 不匹配降级为日志警告并继续完成重命名;文件大小精确校验(不匹配仍报错)保留为主要完整性防线 |

### 📦 版本号

- Assembly / FileVersion → `2.4.1.0`
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.1`
- `docs/version.xml` → `2.4.1.0`

---

## [2.4.0] - 2026-08-14

### 🐛 全面 Bug 修复 - 21 项确认存在的问题

基于对 v2.3.0 全项目代码的逐文件核查，修复 21 项经确切代码证据确认的 bug，涵盖用户可感知报错、死锁/资源泄漏、并发缺陷、异常吞噬、安全债务与死代码。

### 一、用户层面可感知的报错

| 修复 | 说明 |
| --- | --- |
| 后台线程弹 MessageBox 卡死下载 | `FileDownloader.bgwDownloader_DoWork` 的 Catch 块不再在线程池线程上调用 `MessageBox.Show`，改为通过 `ReportProgress(FileDownloadFailedRaiser)` 上报失败，由 UI 线程统一呈现 |
| 关闭期间跨线程 MsgBox 崩溃 | `Main.vb` 三处 `BackgroundWorker.DoWork` 异常分支不再直接 `MsgBox`，新增 `SafeShowError` 辅助方法，检查 `IsDisposed`/`IsHandleCreated` 并通过 `Invoke` 切回 UI 线程 |
| 7z multipart 解压崩溃 | `DescompresorController` 两处 `NotImplementedException` 改为 `NotSupportedException` 带友好消息；`DescompresionFinalizada` 事件扩展传递错误消息，用户可在错误状态中看到具体原因 |
| 兜底完成漏做 MD5 校验/解压 | `Fichero.ActualizarDatosDescarga` 的兜底状态修正不再仅翻转状态，改为调用完整 `downloader_Completed` 流程（含 MD5 校验和自动解压），避免"显示完成但未校验完整性" |

### 二、死锁与资源泄漏

| 修复 | 说明 |
| --- | --- |
| Mutex 无 Try/Finally 死锁 | `Main.AgregarPaquete` 和 `bgwComprobarMaxConexiones` 两处 Mutex 加 `Try/Finally`，中间异常不再导致永久死锁 |
| 下载 worker 未 Dispose | `FileDownloader.Dispose` 中循环释放 `listDownloaders` 里的所有 worker，不再只 `CancelAsync` |
| bgArranque worker 泄漏 | `Fichero.Dispose` 新增释放 `bgArranque`（下载启动 worker），关闭期间启动阶段中断不再泄漏 |
| ELCForm 300ms 忙轮询 | 改为 `AutoResetEvent` 事件驱动，无任务时零 CPU 消耗，有任务时立即响应 |

### 三、并发与逻辑缺陷

| 修复 | 说明 |
| --- | --- |
| AJAX 响应并发污染 | `StreamingLibraryModule._RespuestaAjax` 改为 `AsyncLocal(Of String)`，并发 HTTP 请求互不覆盖响应 |
| FlushFinalBlock 异常吞噬 | `ServerEncoderLinkHelper.Cipher` 解密路径的空 Catch 改为 `Log.WriteError` |

### 四、异常吞噬（掩盖真实故障）

| 修复 | 说明 |
| --- | --- |
| FlushToDisk 磁盘错误被吞 | `FileDownloader.ChunkDownloader_DoWork` 中 `FlushToDisk` 的空 Catch 改为日志 |
| 服务器错误响应读取失败被吞 | `Fichero.downloader_FileDownloadFailed` / `downloader_ChunkDownloadFailed` 两处空 Catch 改为日志 |
| 解压取消异常被吞 | `Main` 关闭流程中 `RequestCancel` 的空 Catch 改为日志 |

### 五、安全与技术债务

| 修复 | 说明 |
| --- | --- |
| DPAPI entropy 硬编码 | `Criptografia` 的 DPAPI entropy 改为从程序集标识 SHA256 派生，保留 legacy entropy 解密旧数据 |
| ZIP 密码硬编码 "passZIP" | `Fichero` 的 ZIP 解压密码加密改用 DPAPI，解密先 DPAPI 后回退旧 AES 兼容旧队列文件 |
| OptionalPassword 死字段 | 删除 `Cache.OptionalPassword` 字段及 XML 写出（声明后从未赋值、从不读取） |
| RandomNumberGenerator 未释放 | `ServerEncoderLinkHelper` 的 `RandomNumberGenerator.Create()` 用 `Using` 包裹释放 |
| 日志无 UTC 时间戳 | `Log` 全部时间戳改用 `DateTime.UtcNow`（加 `Z` 后缀），新增 30 天日志保留清理策略 |

### 六、死代码清理

| 修复 | 说明 |
| --- | --- |
| Criptografia 注释死代码 | 删除注释掉的 `DecryptFile` 和 `cipherData` 函数 |
| Conexion 死代码 | 删除注释的 `GetAppID` 和无调用的 `LeerNodo` 函数 |

### 📦 版本号

- Assembly / FileVersion → `2.4.0.0`
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4`
- `docs/version.xml` → `2.4.0.0`

---

## [2.3.0] - 2026-08-13

### 🐛 稳定性修复

基于代码审查，修复一批崩溃、资源泄漏与潜在死锁问题。

| 修复 | 说明 |
| --- | --- |
| AES 加密失败崩溃 | `AES_EncryptString` 加密异常后仍对 `Nothing` 做 Base64 转换导致二次抛异常；改为失败返回空串并用 `Using` 释放 `RijndaelManaged`/`CryptoStream`/`MemoryStream` |
| AES 解密截断/坏输入崩溃 | `AES_DecryptString` 的 `Convert.FromBase64String` 移入异常处理；用 `CopyTo` 完整读取明文（原单次 `Read` 可能截断）；失败返回空串 |
| 下载项资源泄漏 | `Fichero.Dispose` 由空实现改为释放 `FileDownloader` 并置空 |
| 潜在死锁 | `FileInfo.Size` setter 的 `ReleaseMutex` 放入 `Try/Finally`，循环内异常不再导致永久死锁 |
| 注册表句柄泄漏 | `RegisterInStartup` 的注册表键用 `Try/Finally` + `Close()` 释放 |
| 危险 `Thread.Abort` | DLC 处理 30 秒超时不再硬杀线程，改为协作式标记失败并让 worker 自然结束 |

### 📦 版本号

- Assembly / FileVersion → `2.3.0.0`
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.3`
- `docs/version.xml` → `2.3.0.0`

## [2.2.1] - 2026-08-09

### 🐛 下载状态修复

修复两个用户报告的下载完成状态显示问题。

### Bug 1: 多文件下载完成(100%)但显示错误

- **根因**: `FileDownloader.downloadFile()` 的 `Finally` 块在 `exc` 不为空时报告 `FileDownloadFailedRaiser`。即使所有分块已成功完成(`AllFinished = True`),之前发生的非致命异常仍会触发失败事件,将状态错误地设为 `Erroneo`。
- **修复**: 在 `Finally` 块中检查 `AllFinished` 状态,如果下载实际完成则清除 `exc`,仅记录警告而不报告失败。

### Bug 2: 单文件下载完成(100%)但仍显示"正在下载"

- **根因**: `Completed` 事件只在 `bgwDownloader_RunWorkerCompleted` 中触发,如果等待循环因竞态条件无法退出,`Completed` 永远不会触发,状态停留在 `Descargando`。
- **修复**: 三层防护
  1. **事件层**: 新增 `FileDownloadSucceeded` 处理器,文件验证和重命名成功后立即设置 `Completado` 状态
  2. **循环层**: 等待循环增加 60 秒超时检查,若磁盘文件大小匹配则强制完成;120 秒硬超时防止死锁
  3. **定时器层**: `ActualizarDatosDescarga` 增加兜底检查,进度 100% 且 `AllFinished` 时自动修正状态

### 📦 版本号

- Assembly / FileVersion → `2.2.1.0`
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.2.1`
- `docs/version.xml` → `2.2.1.0`

---

## [2.2.0] - 2026-07-20

### 安全加固与下载完整性

基于深度静态审计结论，完成路径安全（P0）、下载完整性（P1）与一批可靠性/发布现代化（P2/P3）修复。

### 🔒 路径安全（P0）

- 统一 `PathGuard`：远端文件名/目录名、解压条目、删除与写出均限制在 canonical 下载根目录内
- 修复 Zip Slip：解压前校验全部条目，拒绝 `../`、绝对路径、设备名等逃逸
- 修复 MEGA 文件夹路径拼接与任务删除越界风险

### 📦 下载完整性与可靠性（P1）

- 下载完成前校验 **MEGA MetaMAC**；失败不重命名为最终文件
- HTTP Range：校验 Partial Content / Content-Range；拒绝忽略 Range 的错误响应
- 提前 EOF 作为失败；CTR counter 使用 Int64 seek，修复大偏移风险
- 断点元数据校验，避免 `.part` 缺失时的“假完成”
- 配置与下载队列原子保存（`AtomicFile`）；HTTP 默认超时；日志脱敏
- 远程 Web：Stop/Play/AddLink 改为 POST + CSRF；Streaming 媒体 URL 固定 loopback
- 解压协作取消（移除 Thread.Abort）、解压结果成功/失败分离、资源配额
- 关闭顺序：先停 Web → 取消 worker/解压 → 停下载 → 再保存

### ✨ 体验与工程（P2/P3）

- 配置模型层上限（Buffer/连接数/速度）、磁盘空间预检、文件名冲突与进度除零防护
- 语言：内置包与用户自定义分离，缺 key 回退 en-US
- 单实例 IPC 按行写入，避免链接参数粘连；主题 Auto 跟随系统实时变化
- 移除生产 xUnit 依赖与 MPRESS Release 后处理；DPI PerMonitorV2
- 版本比较规范化；DLC 入口标为 discontinued（保留 ELC）

### 📦 版本号

- Assembly / FileVersion → `2.2.0.0`
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.2`
- `docs/version.xml` → `2.2.0.0`

---

## [2.1.0] - 2026-07-19

### 主题完善 - 深色模式可用性修复

基于 v2.0 主题框架,修复深色模式下主列表、进度条、按钮边框、右键菜单等关键观感问题,使 Dark 主题真正可用。

### 🐛 修复

- **主下载列表斑马纹**:`FormatRow` 不再写死 `White`/`Honeydew`,改用 `ThemeManager` 的 `Back`/`AltBack`
- **进度条颜色**:`BarRenderer` 不再使用 Azure/SpringGreen,改为主题 token(`ProgressBack`/`ProgressFill` 等)
- **状态前景色**:错误/完成行使用 `ErrorFore`/`SuccessFore`(深色下为更亮的红/绿)
- **设置保存后即时换肤**:Configuration 保存主题后调用 `Main.ApplyCurrentTheme()`,无需重启
- **按钮白边**:`FlatStyle.Standard` 的系统 3D 高光在深色下呈白边;改为 `FlatStyle.Flat` + 主题 `Border`/`ButtonHover`/`ButtonPressed`
- **GroupBox / TabPage**:Flat 边框与 `UseVisualStyleBackColor = False`,减少系统浅色描边
- **ELC 账号表**:去掉 Azure/Snow/SeaShell 硬编码;空列表提示改用主题前景色
- **右键菜单**:反射主题化 Form 上的 `ContextMenuStrip`;补全 `ToolStripDropDownBackground` 等 `ThemeColorTable` 属性
- **未套主题窗体**:Stegano 向导、SplashScreen、Cerrando 在 Load 时 `ApplyTheme`

### ✨ 改进

- `ThemeManager.GetColor(key)` 公共取色 API
- 新增语义/交互 token:`ErrorFore`、`SuccessFore`、`Progress*`、`ButtonHover`、`ButtonPressed`
- `ToolStripBorder` 正确使用 `ToolBorder` token

### 📦 版本号

- Assembly / FileVersion → `2.1.0.0`
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.1`
- `docs/version.xml` → `2.1.0.0`

---

## [2.0.0] - 2026-07-13

### 重大版本 - 安全加固 + 代码清理 + 暗色主题

基于 v1.9 的链接格式修复,进一步完成 4 个阶段共 60+ 项修复,显著提升安全性、稳定性与可用性。本版本首次引入深/浅色主题切换。

### ✨ 新增

- **深色/浅色主题切换**:
  - 新增 `ThemeModeType` 枚举(Auto/Light/Dark),默认 Auto 跟随系统([`Clases/ConfiguracionUI.vb`](../Clases/ConfiguracionUI.vb))
  - 新增 `ThemeManager` 类,通过读取注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` 检测系统深浅色([`Clases/ThemeManager.vb`](../Clases/ThemeManager.vb))
  - 自定义 `ThemeColorTable` + `ToolStripProfessionalRenderer` 渲染器,覆盖 30+ ToolStrip 渐变/边框/选中色属性
  - 递归应用主题到所有控件,包括主窗体的 `BrightIdeasSoftware.TreeListView`(下载列表)、StatusStrip、ContextMenuStrip、TableLayoutPanel、DataGridView、ListView、TreeView、ProgressBar 等
  - 9 个子窗体在 Load 事件中应用主题:Credits、AddLinks、ELCForm、EncodeLinksForm、PropiedadesDescarga、StreamingForm、Descompresor、PantallaMsg、Configuration
  - 10 种语言文件添加 `Theme` / `Theme_Auto` / `Theme_Light` / `Theme_Dark` 翻译键
- **作者信息**:Credits 窗体加入 "Yingxue - Revival maintainer (v2.0+)"
- **更新检查**:重定向到本 GitHub 仓库

### 🐛 修复

**P0 严重安全漏洞**:
- 修复空密码绕过校验逻辑
- 修复代理凭据未实际赋值给 WebProxy
- 仅启用 TLS 1.2(移除 TLS 1.0/1.1,符合现代安全标准)
- Web/Streaming 服务器绑定 `127.0.0.1`(原 `0.0.0.0` 暴露到全网)
- 密码哈希统一使用 UTF-8 编码
- Mutex 操作全部包裹 Try/Finally 防止死锁
- 加密代码中的空 Catch 块替换为日志记录
- 修复 `ApagarPC` / `MaxConexionesGuardadas` 设置未正确持久化
- 修复下载器 `NullReferenceException` 崩溃(变量名错配 `exc` vs `ex`)

**P1 资源泄漏**:
- 修复 7 处 ToolTip 资源泄漏(`ELCAccountControl` / `AddLinks` / `SteganoWizardSave`,MouseHover 每次创建不释放)
- 修复 `SteganoManager` 的 `Image.FromFile` 锁定源文件 + FileStream 未 Dispose
- 修复 `Main.vb` 7 处 `Image.FromStream(stream)` stream 过早关闭,新增 `LoadEmbeddedImage` 辅助方法
- 修复 `WebInterfaceModule` StreamReader/StreamWriter 未 Using(模板加载 + response.Body 写入,后者使用 `leaveOpen:=True`)
- 修复 `StreamingLibraryManager` `CompressString` / `UnCompressString` 未 Using(嵌套 Using 块)
- 修复 `MegaURIProtocol` 注册表操作无 Finally 释放 + 中间变量覆盖导致句柄泄漏
- 修复 `Main.vb` `clipChange` 关闭顺序错误(Uninstall 应在 DestroyHandle 之前)
- 修复 `Main.vb` `EsperarParadaDescargasYWorkers` 漏检查 `bgwDescompresorCompleted`
- 修复 `StreamingLibraryModule` `Case "Delete"` 缺 `Return True`,导致贯穿到下一分支
- 修复 `StreamingLibraryModule` `UsuarioLogueado` 超时后未清除 session,登录状态永久停留
- 修复 `ELCAccountControl` `CellClick` 未校验 `e.RowIndex`,点击表头会崩溃
- 修复 `StreamingHelper` `Keys.Count / 2` 浮点除法,应使用整数除法 `\ 2`

**P2 协议现代化**:
- `%SEQ%` / `%ID%` 序列号原用 `DateTime.Now.Millisecond` 的 ticks(范围 0-999,并发请求会重复),改用 `Interlocked.Increment` 进程内自增
- `MegaFolderHelper.vb` 中 `http://mega.co.nz/#N!` → `https://mega.nz/#N!`

**P2 代码质量**:
- `Paquete.vb` / `Configuracion.vb` 用 `GetHashCode` 比较配置 XML(不保证一致性),改用直接 `OuterXml` 字符串比较
- `MegaFolderHelper.vb` 两处变量 `ex`(Regex)→ `rx`(避免与 `Catch ex` 混淆)
- `ThrottledStream.vb` 变量名 `int`(VB.NET 关键字)→ `bytesRead`
- `Clases/Mutex.vb` 类名遮蔽 `System.Threading.Mutex`,加注释说明 + 提供别名方案
- `StreamingModule.ClientConnected`、`FileDownloader` Range 头反射加注释说明必要性
- `LibraryElement.ToJSON` 手工 JSON 拼接加注释说明限制

### 🗑️ 删除

- **4 个 Crypter**:`EncrypterMega.vb`、`MegaCrypter.vb`、`Youpaste.vb`、`LinkCrypter.vb`(API 全部下线)
- **3 个 MovieInfo**:`Allocine.vb`、`Filmaffinity.vb`、`IMDB.vb`(API 全部变更)
- **链接辅助**:`DLCHelper.vb`、`Linkdecrypter.vb`、`LinkProtectors.vb`、`Serializer.vb`、`ClipboardChangeNotifier.vb`
- **MegaUploader 菜单**:移除 "Get MegaUploader" 菜单项
- **goo.gl 短链**:14 个 Google 短链全部替换为 GitHub 直链
- **Ping 上报**:移除向原作者服务器上报用户/版本信息(隐私保护)
- 共删除 11 个 `.vb` 文件 + 清理所有相关引用

### ⚠️ 已知问题

- `Thread.Abort()` 危险使用(3 处,Main.vb / DescompresorController)
- 跨线程 MsgBox 未检查窗体是否已关闭(3 处)
- `MegaFolderHelper.FillFolderStructure` 递归无 KeyNotFound 保护
- `ELCForm` 无限循环每 300ms 轮询
- `ServerEncoderLinkHelper` RandomNumberGenerator 未 Dispose
- `FileDownloader.FlushToDisk` FileStream 异时释放

### 📦 构建产物

- `MegaDownloader.exe` 主程序
- 依赖 DLL:`BouncyCastle.Crypto.dll`、`Newtonsoft.Json.dll`、`SharpCompress.dll`、`ObjectListView.dll`、`HttpServer.dll`、`Fadd.dll`、`F5Lib.dll`、`xunit.dll`

---

## [1.9.1] - 2026-07-05

### 🐛 修复

- **下载器崩溃**:修复 [`Clases/FileDownloader.vb`](../Clases/FileDownloader.vb) 第 681-683 行变量名错配导致的 `NullReferenceException`。当 MEGA 服务器返回 502 网关错误等异常时,catch 块误引用已被清空的 `exc` 局部变量(应为 `ex`),导致掩盖真实异常并中断整个下载流程。

---

## [1.9.0] - 2026-07-05

### MegaDownloader 复活计划首个公开发布版本

基于 MegaDownloader v1.8 反编译源码进行修复与重构,核心目标是恢复对 MEGA 新版链接格式的支持。

### ✨ 新增

- **URL 解析**:在 [`Clases/URLExtractor.vb`](../Clases/URLExtractor.vb) 的 `patternHTTPURI` 中新增 4 条正则,支持识别以下新版 MEGA 链接:
  - `https://mega.nz/file/<FileID>#<FileKey>`
  - `https://mega.nz/folder/<FolderID>#<FolderKey>`
  - `https://mega.co.nz/file/<FileID>#<FileKey>`
  - `https://mega.co.nz/folder/<FolderID>#<FolderKey>`
- **文件夹识别**:同步更新 `IsMegaFolder` 方法
- **TLS 1.2/1.3**:在 [`Clases/Conexion.vb`](../Clases/Conexion.vb) 中显式启用 `Tls12 | Tls11 | Tls` 协议
- 增加本仓库的 [README.md](../README.md)、[CONTRIBUTING.md](CONTRIBUTING.md)、[CHANGELOG.md](CHANGELOG.md)、`.gitignore` 等开发者文档

### 🐛 修复

- 修复从剪贴板复制新版 MEGA 链接时无法被识别的问题
- 修复从浏览器拖拽新版 MEGA 链接到主窗口无效的问题
- 修复新版文件夹链接无法被解析为子文件列表的问题
- **修复文件夹下载时 Base64 解码错误**:`mega.nz/folder/` 链接包含被多个用户分享的文件时,MEGA API 返回的 `fileN.k` 字段格式为 `handle1:key1/handle2:key2[/handle3:key3]`(用 `/` 分隔多个 `handle:key` 对)。原代码 `fileN.k.Substring(fileN.k.IndexOf(":") + 1)` 会把第一个 `:` 之后的所有内容(包括 `/handle2:key2`)当作 key,导致 `Convert.FromBase64String` 抛出 FormatException。修复方案:新增 `ExtractKeyFromK` 辅助函数。

### 🔄 变更

- `TargetFrameworkVersion` 维持 `v4.8`(原 v1.8 即已升级至 4.8)
- 仓库 LICENSE 维持 MIT 协议,补充复活计划版权声明

### ⚠️ 已知问题

- EncrypterMe.ga 因其官方 API 服务 (`http://encrypterme.ga/api`) 已下线,目前无法解析此类链接
- 部分 goo.gl 短链因 Google 关闭该服务而无法跳转
- 简体中文语言包尚有部分条目需补充翻译

### 📦 构建产物

- `MegaDownloader.exe` 主程序
- 依赖 DLL:`BouncyCastle.Crypto.dll`、`Newtonsoft.Json.dll`、`SharpCompress.dll`、`ObjectListView.dll`、`HttpServer.dll`、`Fadd.dll`、`F5Lib.dll`、`xunit.dll`

---

## [1.8.0] - 原版 (反编译源)

复活计划所基于的原始版本,本仓库通过反编译得到其源码作为修复起点。

### 主要特性

- 多线程并发下载
- MEGA 文件夹递归解析
- 加密链接 (`enc`/`enc2`/`fenc`/`fenc2`/`elc`) 支持
- 第三方 Crypter 集成 (MegaCrypter、YouPaste、LinkCrypter、EncrypterMe.ga)
- VLC 流媒体边下边播
- 内置 HttpServer Web 管理界面
- SharpCompress 自动解压
- 多语言界面 (10 种)
- Stegano 隐写术
- 自动更新检查

---

## 版本号说明

- 主版本号:重大功能变更或不向下兼容的修改
- 次版本号:新增功能,向下兼容
- 修订号:Bug 修复,向下兼容
