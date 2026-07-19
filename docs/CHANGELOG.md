# 变更日志 (Changelog)

本项目所有重要变更均会记录在此文件中。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/),并遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

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
