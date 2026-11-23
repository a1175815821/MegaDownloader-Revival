# MegaDownloader 跨平台整改清单

以 `refactor/avalonia` 的 `327014d` 为实施基线，以 Windows 2.6.5 为操作体验参照。分支已有 .NET 10 下载核心、Avalonia 队列、链接预检和远程服务。整改从这些实现继续做。本文的性能数值是验收目标，Mac 和 Linux 的运行表现待按清单实测。 [Avalonia 分支](https://github.com/a1175815821/MegaDownloader-Revival/tree/327014d) · [Windows 2.6.5 变更](https://github.com/a1175815821/MegaDownloader-Revival/blob/main/docs/CHANGELOG.md#最新版本2652026-11-22)

**首发预览版目标**　用户在 Apple Silicon Mac 或 Ubuntu 24.04 x64 上装好应用，粘贴一个公开分享，选中想要的子目录，在后台下载，退出并重新打开后继续，最终打开已校验的成品。首发同时支持公开单文件和公开文件夹。账号、云盘和目录订阅排在后续版本。

| 阶段 | 交付内容 | 完成条件 |
| --- | --- | --- |
| P0 · 首发预览 | 公开链接、文件夹选择、队列与断点、两平台安装包 | 跑完文末的 P0 验收矩阵 |
| P1 · 日常使用 | 下载优先级、计划与批处理、系统入口、已有附加功能的体验对齐 | 各功能通过真实操作和重启验证 |
| P2 · 账号与自动下载 | 多账号、私有续传、云盘、目录订阅 | 真实账号下载、增量入队和跨重启校验通过 |
| P3 · 桌面 UI 重做 | 主工作台、添加链接、设置及账号相关页面的新布局和控件体系 | 用真实队列验收深浅色、信息密度和整条下载流程 |

在 `refactor/avalonia` 上继续开发。跨平台 Core 和 Avalonia 各自已有入口，Windows 2.6.5 的行为按场景移植。首发公共下载用一套流程覆盖 Mac、Linux 和 Windows 对照构建。

## 实施进度（2026-11-23）

第二轮（P1）代码已完成并本地构建通过。验证情况：解决方案 `Debug` 构建 0 错误；`MegaDownloader.Core.Tests` 跨平台回归 57 项断言通过（含真实双进程单实例用例）；Windows 完整回归 1143 项断言通过。Mac 和 Linux 的实机安装与运行尚未执行。

| 条目 | 状态 | 落地内容 |
| --- | --- | --- |
| P0-02 两步添加与入队反馈 | 已完成 | 添加窗口拆成「链接 → 文件」两步，返回时保留结果；剪贴板和协议激活统一走确认窗口，窗口打开期间到达的链接留给下一批；提交后汇报实际添加、重复和跳过数量 |
| P0-03 目录树预检 | 已完成 | WinForms 的目录投影迁入 Core（`PreflightTree`，三态枚举，去掉 WinForms 依赖）；勾选目录可选中全部后代，折叠与筛选不改变已选统计；显示相对路径、类型筛选、目标目录预览与可用空间 |
| P0-04 队列刷新 | 已完成 | 引擎在自身 500 ms 循环里生成并发布 `QueueSummary`；界面读取该摘要，仅在队列或汇总变化时重建行；`Reconcile` 改为移动、插入、移除，保留选择与滚动 |
| P0-05 日常队列操作 | 已完成 | 任务搜索（Ctrl/Cmd+F，Esc 清空）、多选批量暂停/重试/移除、重试失败（保留配额断点）、打开文件与所在目录、详情栏与状态/路径/错误 |
| P0-06 断点与失败反馈 | 部分完成 | 退出顺序改为先停止派发、再让在途下载提交进度、最后保存队列；失败重试遵循永久失败不重试的规则。真实下载的 Range 与重启续传仍需实机验证 |
| P0-08 构建与回归 | 部分完成 | 新增 `net10.0` 的 Core 回归工程并接入解决方案；CI 改为三平台运行 Core 回归并按 `win-x64`、`osx-arm64`、`linux-x64` 发布。安装包、签名与实机验收未做 |
| P1-01 批量选择与优先级 | 已完成 | 队列沿用 Avalonia 原生 Shift 范围选择与 Cmd/Ctrl 增删，Ctrl/Cmd+A 全选；预检树的 Shift 连选按复选框语义扩展；引擎新增 `MoveTo`（置顶/上移/下移）与 `MoveBeside`（拖拽），直接调整包与包内文件的实际列表顺序并重新编号，随队列保存，重启后顺序一致 |
| P1-02 定时、限速与设置页 | 已完成 | 设置页给出当前生效值（目录、并行数、连接数、限速、代理）；下载计划支持一次性日期时段与每日时段，摘要实时显示「正在允许/下次开始/已结束」，跨午夜保持同一窗口；全局限速保存即生效，单任务限速在任务属性中设置 |
| P1-03 链接入口与单实例 | 部分完成 | 工具栏粘贴、剪贴板、拖放、`mega://` 激活和命令行统一到确认窗口；`.elc` 解包为服务端编码链接，`.dlc` 给出下线说明；macOS `IActivatableLifetime` 接入已运行实例的协议/文件事件；双进程与参数转发用例纳入 Core 回归。安装包的协议声明（`packaging/Info.plist`、`packaging/MegaDownloader.desktop`）已提供，需在 P0-08 打包时验证 |
| P1-04 解压、Web 与播放 | 部分完成 | macOS/Linux 查找 `7zz`/`7z`（PATH 与常见安装路径），无 CLI 时单卷走 SharpCompress，内置 `7zr.exe` 只在 Windows 部署，`\\?\` 前缀只在 Windows 添加；Web 远程添加复用同一目标目录与查重；VLC 解析支持 macOS 应用包、Linux PATH 与配置路径，添加窗口新增「边下边播」。真实分卷、VLC 与三平台实测待执行 |

尚未完成：P0-01 的三平台基线记录、P0-07 的桌面适配实机检查、P0-08 的 macOS/Linux 安装包与实机验证，以及 P1-03/P1-04 需要在 Mac、Linux 和 VLC 上完成的实机验收。拖拽排序与右键命令共用同一引擎移动路径，取消拖拽不会改变队列。

## P0 · 首发预览

### P0-01 · 建立可重复的三平台对照

- [ ] 建三组固定测试数据。空配置验证首开，合成的 679 任务队列验证恢复与工作台，一个有多层目录和约 500 个文件的测试分享验证选择。准备一个带已知 SHA-256 的公开单文件，用作真实下载。各平台用独立 `MEGADOWNLOADER_DATA_DIR`，用同一份任务定义在各自环境生成本地队列和加密字段。
- [ ] 记录 Windows 2.6.5 和 Avalonia 基线。每次记录构建版本、机器、系统、缩放和测试数据版本。在同一台 Windows 机器上对照两个 Release 构建。在 Mac、Linux 上记录各自的发布包实测值。
- [ ] 建一条最早就能在两个目标系统执行的发布包路径。首次只走「启动 → 粘贴一个文件链接 → 完成」，先确认能交付，再用同一打包流程生成后续验收版本。
- **验收**　两台目标环境都能从独立配置启动，测试链接落到指定目录，文件 SHA-256 一致。留一张基线表，分别记录首屏可操作、队列就绪、添加完成和下载完成。

参考 [现有隔离配置入口](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.Core/Platform/ApplicationDataDirectory.cs) · [现有发布包下载脚本](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/Tests/Test-PublishedTransfer.ps1) · [679 任务队列基准](https://github.com/a1175815821/MegaDownloader-Revival/blob/main/docs/audits/PERFORMANCE_ANALYSIS_2026-11-05.zh-CN.md#L10)

### P0-02 · 恢复两步添加和统一的入队反馈

- [ ] 第一步只放链接输入、粘贴和「解析并选文件」。支持粘贴多行链接和从主窗口拖入链接文本。第二步显示文件选择及保存位置，包名、解压和密码留在下载选项里。
- [ ] 把剪贴板监听当前的直接入队改为打开确认窗。输入页接到新链接时追加，预检与提交期间接到的新链接暂存。返回输入页时合并，成功提交后作为下一批显示。监听关闭后仍可手动粘贴或拖入文本。
- [ ] 回到第一步时保留原文本、搜索、展开、勾选与滚动位置。同一批重复进入时复用 `PreflightSession.Prepare` 的来源缓存。提供原地取消和重新检查，显示已用时间或已发现的文件数。
- [ ] 沿用 `BatchLinkPreflight`、`DownloadPackageBuilder` 和 `AddUniquePackage`。就绪项默认勾选，临时不可用且有有效来源的项目允许手选，重复、已排队和失效项标明原因。提交时使用本次勾选和选项，在现有锁内复查重复。
- [ ] 提交按钮显示已选数量。提交结束后汇报实际添加、重复和失败数量，定位到新任务包。用户在下载选项里选择「加入队列」或「立即下载」。
- **验收**　预检混合文件链接、子目录链接、重复项和无效链接。预检链接 A 时收到 B，提交时又收到 C，第一批只入队所选 A，下一批各显示一次 B、C。返回再进入能够恢复选择。重复提交只入队一次，取消建包时入队数为零。

改动入口 [AddLinksWindow](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.UI/Views/AddLinksWindow.axaml) · [AddLinksViewModel](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.UI/ViewModels/AddLinksViewModel.cs) · [LinkIntakeService](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.Core/Services/LinkIntakeService.cs)

### P0-03 · 把文件夹预检恢复成树形选择

- [ ] 按分享来源区分同名根目录，再按相对路径分层。提供文件夹节点、折叠展开、完整路径和三态勾选。勾选目录能选中其有效后代。取消一个子文件，祖先目录变为部分选中。
- [ ] 复用 `PreflightSelection` 的勾选状态和 `src/Clases/PreflightTree.cs` 的目录投影逻辑。界面虚拟化当前展开且符合筛选条件的行。已选文件的数量和大小始终包含折叠或筛选隐藏的勾选项。
- [ ] 保留 Core 已有的文件名、共享目录名和相对路径匹配，启用类型筛选。明确区分「选中当前结果」和「清空全部选择」。空格勾选，左右键展开和折叠。文件行显示名称、大小、状态和相对路径。
- [ ] 从 `DownloadPackageBuilder` 复用目录计算，预览目标根目录、包名目录和文件相对路径。验名后才能确定的目录标为待定。查询当前目标卷可用空间，显示已选总大小及大小未知项，空间已知且不足时停止提交。
- [ ] 使用子文件夹或指定单文件的分享链接时，按当前链接指向的范围预检和入队。子目录结构、同名不同目录及 Unicode 文件名在本地都能核对。
- **验收**　约 500 个文件的分享中，勾选一个二级子目录，再取消其中一项。改变筛选和折叠后，预检统计与最终入队的文件一致。再加入一个同名分享，能看到两个独立的根目录。实际保存路径与预览一致，改选到可用空间以内可继续提交。

改动入口 [Core 预检选择](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.Core/Engine/PreflightSelection.cs) · [已有目录投影](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/src/Clases/PreflightTree.cs) · [当前预检列表](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.UI/Views/AddLinksWindow.axaml#L31-L65) · [Windows 版预检](https://github.com/a1175815821/MegaDownloader-Revival/blob/main/docs/README.zh-CN.md#26-引入的工作台功能)

### P0-04 · 改掉工作台的全量刷新

- [ ] 让 `DownloadEngineService` 在已有的调度和统计循环中生成队列摘要，界面读取这一份结果。队列结构或状态改变时更新相关投影。进度、速度和剩余时间按界面的显示节奏合并更新。
- [ ] 把每 500 毫秒的界面全队列扫描拆开。折叠包显示包摘要，展开时再建立必要的子行。完成、排队且属性没变的行直接保留。只在状态切换时更新状态分组数量。
- [ ] 将 `Reconcile` 的整表清空替换成按已有任务身份插入、移动、移除。切分组、添加包、展开和状态变化后保留仍可见的焦点、选择和滚动锚点。
- [ ] 从同一份数据取总速度、活动数和配额信息。速度图独立更新。下载调度、显示统计和保存队列继续各自按业务需求运行。
- **验收**　用 679 项队列分别测全折叠、展开、滚动和有活动下载的四种状态。交互确认目标为 P95 小于 100 ms，单位为输入到搜索结果或操作状态首次变化。静止时同一任务的行实例和选择保持稳定。每种状态记录样本数与中位数。

改动入口 [Core 调度和汇总](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.Core/Services/DownloadEngineService.cs#L472-L522) · [界面刷新](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.UI/ViewModels/Pages/DownloadsPageViewModel.cs#L349-L398) · [列表更新](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.UI/ViewModels/Pages/DownloadsPageViewModel.cs#L580-L604)

### P0-05 · 补齐队列里每天要用的操作

- [ ] 为任务名、包名和相对路径增加搜索，并与当前状态分组叠加。Mac 用 `Cmd+F`，Linux/Windows 用 `Ctrl+F`。`Esc` 清空搜索。过滤关闭后回到原选择或最近的可见行。
- [ ] 提供开始、暂停、停止、重试、查看属性、打开已完成文件和打开所在目录。主区域突出「添加链接」和队列控制。编码与 ELC 收到工具菜单。批量操作完成后显示实际影响的任务数量。
- [ ] 下载行保留名称、状态、进度、大小、速度和剩余时间。详情区放来源、目标目录、错误原因等次要字段。状态文字跟随切换，并通过文字区分排队、下载、暂停、校验、完成与失败。
- [ ] 把首开引导放进空态或帮助入口。首次启动可以立即添加链接。恢复大队列时显示「正在恢复任务」。`LoadAsync` 完成后就发布队列就绪，再独立启动可选 Web/流媒体服务。
- **验收**　从空队列和大队列恢复两种起点操作。能用键盘搜索并定位任务。下载中搜索、展开、暂停和切页保持响应。完成后可从列表直接打开本地文件。

改动入口 [队列页面](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.UI/Views/DownloadsPageView.axaml) · [首开引导](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.UI/Views/MainWindow.axaml.cs#L11-L18) · [当前启动顺序](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.UI/ViewModels/Pages/DownloadsPageViewModel.cs#L112-L145)

### P0-06 · 完成断点、校验、失败反馈和下一步操作

- [ ] 沿用现有 `.part`、持久化分块位置、文件校验和最终交付机制。把界面暂停、手动停止、正常退出和再次启动分别接到已有的业务语义上。仅从剩余区间请求数据，用有 MetaMAC 的测试链接检查整文件结果。
- [ ] 调整 `DownloadEngineService` 的退出顺序。先停止派发新任务，再让活动下载完成当前写入并提交可恢复的分块进度，最后保存队列。沿用已有停止和落盘逻辑，让保存记录和实际文件处于同一进度。
- [ ] 在工作台显示可重试失败、永久失败、配额等待和空间不足。配额显示剩余时间，恢复时使用已有断点。已失效链接给出明确原因。失败区允许重试可重试任务，并显示本次恢复到队列的数量。
- [ ] 检查队列保存和重启间的一致性。加入后退出、下载中退出、下载完成后退出，各自验证任务状态和文件是否对应。移除任务与删除文件继续做成两个用户动作，保留现有后台移除流程。
- [ ] 配额和失败横幅的显示变化只影响各自区域。保持选中任务、滚动位置和下载状态的可见性。
- **验收**　下载到 30%–50% 后正常退出并重新打开，测试服务记录的 Range 只覆盖尚未持久化的区间，成品 SHA-256 与预期一致。分别触发 509、失效链接和空间不足，界面显示对应原因与可用动作。校验完成后状态和磁盘成品一致。

改动入口 [断点任务模型](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.Core/Engine/Fichero.cs) · [配额状态](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.Core/Engine/MegaQuotaManager.cs) · [Windows 失败重试语义](https://github.com/a1175815821/MegaDownloader-Revival/blob/main/src/Forms/Main.Failures.shared.cs)

### P0-07 · 把桌面体验做实

- [ ] 用分栏和紧凑状态行把空间给文件列表。按窗口实际可用宽度收起详情或改为抽屉，主操作保持可见。降低当前 `930 × 625` 最小尺寸对小屏的限制，并在真实工作区尺寸下确定新值。
- [ ] 处理 Tab 顺序、键盘焦点、菜单可达性和按钮文本。Mac 用系统主修饰键，Linux/Windows 用 Ctrl。名称过长时可看完整名称和路径。完成、失败和配额状态都有文字。
- [ ] 验证浅色、深色、缩放、中文和英文。翻译长文本的按钮保持可读。切换主题和语言后更新状态标签、菜单、预检过滤与空态。使用分支已有主题和本地化资源。
- **验收**　在 1280×800 工作区、macOS Retina、Linux 100%/200% 缩放和一个窄窗口上跑完添加及移除流程。不存在被裁掉的提交按钮。只用键盘可完成添加、勾选和入队。

改动入口 [主窗口](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.UI/Views/MainWindow.axaml) · [主题资源](https://github.com/a1175815821/MegaDownloader-Revival/tree/327014d/MegaDownloader.UI/Themes) · [语言资源](https://github.com/a1175815821/MegaDownloader-Revival/tree/327014d/MegaDownloader.UI/Localization)

### P0-08 · 发布原生安装包和三平台验证

- [ ] 新建 `net10.0` 的 `MegaDownloader.Core.Tests`，只引用 Core。先迁移公开链接范围、分块续传、MetaMAC、队列恢复和文件落盘用例，沿用本地 Range 测试服务和固定样本。Windows CI 同时执行现有的 WinForms 回归入口。
- [ ] 在 Windows、macOS、Linux runner 上运行 Core 用例。直接发布 `MegaDownloader.UI.csproj`，用 `Release`、`--self-contained true` 和对应的 `osx-arm64`、`linux-x64` RID。每个 RID 单独暂存，发布取验收过的同一份产物。
- [ ] macOS　把发布文件放入 `.app/Contents/MacOS/`，设置 `Info.plist` 的入口、包标识和版本，图标放入 `Contents/Resources/`。对外包走 Developer ID 签名和公证，以 ZIP 分发。用 Finder 解压和启动，确认 Dock 显示和可写的下载位置。
- [ ] Linux　以 Ubuntu 24.04 x64 为首发安装目标，提供 `.deb`。文件装到 `/usr/lib/megadownloader/`，入口使用 `/usr/bin/megadownloader`，随包放置 `.desktop` 文件和图标。验证从应用菜单启动、选择目录和粘贴。发布说明写明已验证的发行版。
- [ ] 把 Core 配置、队列、日志和单实例的应用数据根合到一个解析入口。Windows 读取现有目录，macOS 使用 `~/Library/Application Support/MegaDownloader`，Linux 使用 `$XDG_DATA_HOME/MegaDownloader`，未设置时取 `~/.local/share/MegaDownloader`。保留绝对路径的 `MEGADOWNLOADER_DATA_DIR` 作为测试入口。
- [ ] 把 `Test-PublishedTransfer.ps1` 的验证规则用于两平台最终包。检查队列为完成、进度 100%、大小与预期一致、目标目录出现单一成品且 SHA-256 正确。
- **验收**　两个安装包可从普通用户的操作路径打开。记录系统及架构、包版本、下载结果和实测截图。干净环境运行无需手动安装 .NET。重复下载、卸载并重装或升级后可验证相应的队列行为。

改动入口 [现有工作流](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/.github/workflows/build.yml) · [Windows 回归工程](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.Tests/MegaDownloader.Tests.csproj) · [Avalonia 项目](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.UI/MegaDownloader.UI.csproj)

## P1 · 日常使用体验

### P1-01 · 批量选择和任务优先级

- [ ] 补上按 Shift 扩展、按 Cmd/Ctrl 增删选择，以及对多个任务同时暂停、重试和移除。队列和预检两处采用相同的选择习惯。
- [ ] 给任务包和可调整的包内任务提供上移、下移和置顶。顺序影响后续排队调度，保存后重启复现。拖拽排序沿用相同命令。
- **验收**　把三个排队任务中的第三个置顶，完成当前下载后优先启动它。重启后队列顺序与关闭前一致。连续选中 20 项暂停时，正在下载项进入暂停状态，其他任务保持原状态。

### P1-02 · 定时、限速、代理和设置页

- [ ] 单独整理下载设置页，覆盖目标目录、并行文件数、单文件连接数、全局及单任务限速、代理和启动后继续下载。设置保存应反映真实结果，并给出当前生效值。
- [ ] 添加定时下载的可见摘要。支持一次性与每日时段、跨午夜、到点暂停和下个时段恢复。复用 `DownloadSchedule` 和现有调度判断，打开计划时能看到下次触发。
- **验收**　计划进入与离开时段能从界面和任务状态观察到。切换限速后测速有对应变化。重启后计划、代理和路径读回正确。

### P1-03 · 剪贴板监听、协议链接和单实例

- [ ] 让工具栏粘贴、剪贴板监听、拖放、浏览器 `mega://` 激活和命令行参数复用同一套链接规范化和查重。浏览器链接走 P0 的确认流程。把拖入的 `.dlc` 和 `.elc` 容器逐个解包，再送入同一确认入口。
- [ ] macOS 在 `.app` 的 `CFBundleURLTypes` 声明协议，把系统 URL 事件送入 `ActivationService`。Linux `.desktop` 使用 `/usr/bin/megadownloader %u` 和 `x-scheme-handler/mega`，由安装包和桌面系统管理关联。
- [ ] 首开尚在恢复队列时暂存激活事件，等队列就绪后按顺序交给确认入口。已有窗口时，第二次启动仅转发参数并激活主窗。用双进程用例覆盖带链接和空参数两种路径。
- [ ] 登录自启使用安装后的稳定入口。macOS 用 `LaunchAgent` 的 `ProgramArguments`，Linux 用 XDG 自启目录里的桌面入口。开启与关闭都用重新登录验证。
- **验收**　在应用未启动、正在恢复队列和已打开三种状态，分别打开同一个测试协议链接。链接片段完整，只有一个主窗，每次操作只提交一次。关闭剪贴板监听后，手动粘贴和拖放仍可使用。

改动入口 [剪贴板](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.UI/Services/ClipboardMonitor.cs) · [激活服务](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.UI/Services/ActivationService.cs) · [Unix 平台服务](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.Core/Platform/UnixPlatformServices.cs)

### P1-04 · 对齐解压、Web 和播放的已有功能

- [ ] Windows 使用内嵌 `7zr.exe` 的逻辑留在 Windows。macOS/Linux 查找原生 `7zz` 或 `7z`，无命令行工具时单卷走现有 SharpCompress 解码。归档长路径只在 Windows 加 `\\?\` 前缀。验证 ZIP、RAR、7z 和多卷样本，分卷齐全后统一解压，已下载分卷可跨重启复用。
- [ ] 把已有 Kestrel Web 远程的添加、开始、暂停和进度与桌面队列打通。远程提交的任务有相同的重复判定和状态反馈。保存端口设置后，新监听和桌面下载都能使用。
- [ ] 验证边下边播、流媒体库和 ELC 的现有入口。网页端 `OpenVLC` 接入 Mac 的 VLC.app 和 Linux 的原生 `vlc`。完成文件从队列打开，ELC 从工具菜单打开。
- **验收**　每个平台完成一次归档下载和解压。分卷在等待时退出再重启，已下完的分卷可继续使用。用手机浏览器添加合法链接，桌面端可见并正常完成。

改动入口 [归档工具选择](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.Core/Engine/DescompresorController.cs#L978) · [现有 VLC 查找](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.Core/Engine/StreamingHelper.cs#L19) · [Windows 分卷修复记录](https://github.com/a1175815821/MegaDownloader-Revival/blob/main/docs/CHANGELOG.md#最新版本2652026-11-22)

## P2 · 账号、云盘和目录订阅

### P2-01 · 把 MEGA 账号桥接移到两平台

- [ ] 以 `main` 的 `Native/MegaAccount` 和 SDK `v10.21.1` 为来源构建 macOS arm64 的 `.dylib` 与 Linux x64 的 `.so`。修改 CMake triplet、导出和原生库加载，按目标平台适配 `LimitSdkReceive.cmake` 选用的 SDK 源文件。保持 UTF-8、64 位句柄、C ABI 和 `md_free_string` 的释放契约。库和 SDK 许可证一起随包发布。
- [ ] 将账号服务接入 Core 的平台加载与凭据保护，沿用 `ISecretProtector` 的 Keychain 和 libsecret 实现。账号缓存按账号隔离。Windows 版保留读取已有 DPAPI 会话的能力。统一登录、退出、记住登录、账号别名与默认下载身份。
- [ ] 移植公开单文件的身份选择和合法配额状态，私有任务由原账号恢复。验收使用可控账号与公开测试文件，记录服务端提供的配额结果。公开文件夹账号下载以实测能力决定接入行为。
- **验收**　安装包可直接加载对应 SDK。两个平台分别登录、重启并恢复记住的会话，再注销。给公开单文件选定账号后，队列显示该身份并完整下载。

参考 [原生桥接构建](https://github.com/a1175815821/MegaDownloader-Revival/blob/main/Native/MegaAccount/CMakeLists.txt#L1) · [2.6.5 账号行为](https://github.com/a1175815821/MegaDownloader-Revival/blob/main/Native/MegaAccount/README.md#try-it-locally) · [现有平台凭据保护](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.Core/Platform/SecretProtector.cs)

### P2-02 · 云盘和私有文件断点续传

- [ ] 在工作台加入「云盘」，支持「我的云盘」「分享给我的」、搜索、勾选目录和下载预览。复用 P0 的文件树与路径展示。导入任务保留原账号、云端节点与相对路径。
- [ ] 从 Windows 2.6.5 移植 `.mega.part`、缺失区间续传和整文件 MetaMAC 交付。对应移植队列中途退出、已交付重启和批量暂停的行为。
- [ ] 移植收藏、绑定本地目录、检查新增与更新和交付记录。先在人机确认的流程中跑通归档，再接入调度。
- **验收**　从「分享给我的」下载一个自有测试私有文件，中途退出，重新开启后只取缺失区间并完成整文件校验。更改一个云端文件后再次检查，本地已有、已排队、新增和更新各自可辨认。

参考 [Windows 云盘入队](https://github.com/a1175815821/MegaDownloader-Revival/blob/main/Forms/MegaDriveForm.vb#L706) · [目录增量比较](https://github.com/a1175815821/MegaDownloader-Revival/blob/main/Clases/MegaDriveHistory.vb#L118) · [2.6.5 私有断点](https://github.com/a1175815821/MegaDownloader-Revival/blob/main/docs/CHANGELOG.md#最新版本2652026-11-22)

### P2-03 · 目录订阅

- [ ] 在已绑定目录上提供订阅模式，保存来源、目标路径、检查时间和类型规则。支持「自动入队」和「只列出变化」两种动作。复用云盘增量比较与现有计划下载。
- [ ] 工作台列出上次检查、下次检查、发现的变化和实际入队结果。支持立即检查和暂停订阅。先接自有云盘及分享目录，再接匿名公开文件夹作为订阅来源。
- **验收**　同一测试目录先增加文件，再更新文件，然后重启应用。三次检查的结果分别对应实际变化。重启后的下一次检查沿用同一条订阅和交付记录。

## P3 · 桌面 UI 重做

### P3-01 · 重做整个下载工作台的视觉和布局

**设计方向**　做成一眼就能读懂的桌面下载工作台。打开有任务的窗口，视线先落在文件名、下载进度和当前可用的操作上。任务包通过缩进和展开关系组织内容，包和文件共用对齐的状态、进度与速度列。P3 的主窗口和添加链接样板在 P0 前段就开做，定下布局后用于 P0 的功能改动。

```text
[添加链接] [搜索任务                  ]        [开始] [暂停] [停止]
┌─────────┬─────────────────────────────────┬────────────────┐
│ 全部    │ 名称          状态    进度   速度 │ 所选任务       │
│ 下载中  │ ▼ 项目资料    下载中   42%        │ 来源           │
│ 已完成  │   文件 A      下载中   68%   8 MB/s│ 保存位置       │
│ 失败    │   文件 B      排队中    —         │ 已下载 / 总大小│
└─────────┴─────────────────────────────────┴────────────────┘
  2 个下载中                                  总速度 8 MB/s
```

- [ ] **主窗口和队列。** 重排 `MainWindow` 与 `DownloadsPageView`。顶栏合并搜索、添加和全局控制。左侧只放队列分类和已启用的工作区，列表占据中间主体，右侧详情按需打开。把目前的品牌横幅缩为标题栏中的应用名称，把速度趋势放进可展开的传输详情，底栏集中显示当前吞吐量和等待状态。
- [ ] **信息密度。** 样板以约 36 DIP 的文件行起步。文件名左对齐，进度、大小和速度按列对齐，数字使用等宽字形。状态和进度固定在容易扫视的位置。包行用展开标记、字重和缩进表现父子关系。窄窗先收起详情，再收起低频列，保持文件名、状态和进度可见。
- [ ] **颜色和排版。** 样板以 `#F1F5F8` 作浅色工作区底色、`#FFFFFF` 作列表表面、`#17283B` 作正文、`#17628B` 作操作强调色。深色以 `#192532` 作底色，浅灰作正文，`#8CD4F0` 用于主操作和活动进度。按钮、选择态和进度沿用这套颜色；失败、等待、完成分别用图标和文字区分。拉丁字形采用 Source Sans 3，中文采用系统字体。正文、区块标题和数字用同一套字号与字重体系。
- [ ] **重新设计添加链接。** 第一步是一块宽敞的多行输入区，底部只有当前步骤的主按钮。第二步把空间交给文件树，底部固定显示选中文件数、大小、保存位置和入队按钮。高级选项进入可展开区。输入、返回、重检和确认的操作位置在窗口缩放时保持稳定。
- [ ] **设置、云盘和账号。** 设置按「下载」「行为」「网络」「外观」组织，当前值和修改入口同行。云盘沿用文件树和目的地栏，账号信息放在独立窗口或对应详情区。未接入的功能在对应版本交付时再进入导航。
- [ ] **统一组件后再铺开。** 从可运行的工作台、添加链接两张样板提取按钮、表格、输入框、状态、详情、空态和错误提示的 XAML 样式。先用同一批真实内容检查空队列和忙碌队列的视觉重心，再把定下来的版式应用到设置、属性、云盘和账号。展开、收起、入队等动作只在需要交代变化时使用短动画。
- [ ] **以运行中的界面验收。** 样板用 679 项队列和约 500 文件的预检，分别展示空态、传输中、配额等待和失败。交付 macOS、Linux 实际窗口的深浅色截图及一段从粘贴到入队的录屏。查看窄窗、满窗和中英文状态，再用同一套样式检查其它页面。
- **验收**　首次看到有任务的窗口，就能找到添加入口、辨认正在下载的文件并看清进度。预检文件树在窄窗仍有可用空间，确认按钮固定可见。系统缩放下列头和数值对齐，切换主题后选中行、进度和失败状态清楚。工作台、添加链接和设置使用同一套视觉语言。

改动入口 [当前主窗口](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.UI/Views/MainWindow.axaml) · [当前队列页面](https://github.com/a1175815821/MegaDownloader-Revival/blob/327014d/MegaDownloader.UI/Views/DownloadsPageView.axaml) · [主题资源](https://github.com/a1175815821/MegaDownloader-Revival/tree/327014d/MegaDownloader.UI/Themes)

## 首发验收矩阵

同一套预览包分别在 macOS arm64 和 Ubuntu 24.04 x64 执行。Windows 上用同一组样本对照 2.6.5。实机记录包含系统、构建号、操作结果和失败时的可复现输入。

| 场景 | 操作 | 通过条件 |
| --- | --- | --- |
| 干净安装 | 安装、首次打开、选本地目录并重启 | 从系统入口启动，目录设置读回，空态能立刻添加 |
| 单文件 | 粘贴公开链接，下载并打开 | 正确文件名、大小与 SHA-256，成品可打开 |
| 批量预检 | 混合单文件、子目录、重复和无效链接 | 每项状态可辨认，有效项只入队一次 |
| 文件夹选择 | 展开目录、三态勾选、筛选、返回再进入 | 选择可恢复，计划和实交付文件清单一致 |
| 本地路径 | 下载同名文件与包含中英文的目录树 | 展示目标路径与实际写入一致 |
| 大队列 | 恢复 679 项，边下边搜索、滚动、切筛选 | 无整表闪动，选择保留，输入到反馈按 P0-04 记录 |
| 暂停与重启 | 部分传输后暂停，再停止或退出并重启 | Range 只请求剩余区间，恢复后成品校验相同 |
| 状态与错误 | 各触发一次临时错误、509、失效链接和空间不足 | 原因、下一步动作、剩余时间与真实状态一致 |
| 任务移除 | 分别选移除任务和删除本地文件 | 两种操作的队列和文件结果各自正确 |
| 屏幕与输入 | 窄窗口、深浅色、中英文、只用键盘 | 提交操作可见，焦点顺序明确，任务可完成 |
| 升级 | 带任务和设置安装下一构建 | 恢复已有设置与队列，未完成任务可续传 |

## 建议开工顺序

1. **接通两平台构建，同时做 UI 样板。** 做 P0-01 和 P0-08 的打包基础，另一条线先做 P3-01 的主窗口和添加链接样板。用真实文件名、进度与错误状态定下版式。
2. **用新版界面交付预检和队列。** 先交付 P0-02、P0-03，再交付 P0-04、P0-05。前者用大文件夹验收，后者用 679 项队列验收。P3 的设置、属性、云盘与账号界面随后按同一套组件铺开。
3. **带着同一批安装包跑完完整流程。** 完成 P0-06、P0-07 和 P0-08 的分发步骤，再按矩阵出首发预览。P1 按使用频率铺开，P2 在公共下载体验通过后接上账号和订阅。
