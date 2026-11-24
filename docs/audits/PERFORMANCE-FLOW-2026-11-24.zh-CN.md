# 性能与流畅度分析 · 2026-11-24

分析基线：`2a41b42`（`fix/audited-download-defects`）。分析范围：匿名下载吞吐、MEGA 账号/SDK 传输、合法限制与错误恢复、界面流畅度。本文是只读分析，不包含产品源码改动。

用户提出的“绕过限制”在本报告中仅按**合法韧性**理解：代理配置、超时与重试、配额提示与恢复、会话恢复、避免误判和不必要的全量重下。不实现规避 MEGA 配额、风控、访问控制、账号限制或身份伪装。

## 本机基线

- `Tests/Run-RegressionTests.ps1 -NoBuild -Cases batch2_perf,startup_performance,queue_view_snapshot,preflight_selection,single_task_resume,download_scheduling`：6 个用例、214 项断言全部通过。16 MiB MetaMAC 验证约 126 ms，10 万条预检选择初始化约 7.08 ms。
- `Tests/Stage-ReleaseAssets.ps1`：19 个内嵌 DLL、MEGA SDK 初始化和 ZIP 内容校验通过。
- 单文件 `artifacts/MegaDownloader.exe` 冷启动至主窗口标题出现，5 次测量为 906、880、1017、849、858 ms；最小 849 ms、中位 880 ms、最大 1017 ms、均值 902 ms。这是进程到窗口出现的时间，不等于队列就绪、首次下载启动或真实网络吞吐。
- 当前默认配置见 `Clases/Configuracion.vb`：缓冲 750 KiB、包 50 KiB、每文件 3 连接、3 个并发任务。
- 未取得真实 MEGA 账号会话，也没有外部网络吞吐基准；涉及真实吞吐的建议必须先建立对照测量。

## 总体结论

最值得打磨的不是再改一次启动流程，而是“下载热路径减少同步开销 + SDK 等待/进度节拍降噪 + UI 周期刷新按变化驱动”。三者分别对应下载速度、账号传输稳定性和大队列流畅度。

建议按以下顺序做，且每一步单独提交、单独 A/B 测量：

1. 匿名下载：降低热路径的锁竞争、分配和重复日志开销。
2. MEGA 账号：把原生传输等待从固定轮询改为可唤醒等待，并减少进度回调到 UI 的无效刷新。
3. UI：保持 430 ms 业务节拍，但把列表刷新、详情、配额文字改为变化驱动，避免固定 tick 全量刷新。
4. 限制恢复：完善代理、超时、会话失效和错误分类的可观测性，避免误判造成长时间停顿。
5. 建立性能回归基线：把吞吐、尾延迟、UI 帧间隔、等待唤醒延迟加入测试和文档。

## 方向一：匿名下载吞吐

代码事实：

- `Clases/Fichero.vb:1272` 把配置的 750 KiB 缓冲与 50 KiB 包传给下载器；`Clases/FileDownloader.vb:1350` 为每个分块分配 `BufferMem` 和 `BufferDisk`。在默认 3 连接下仅这两类缓冲就约 4.5 MiB/任务，3 个并发任务约 13.5 MiB，另有 UI、队列和 SDK 开销。
- `Clases/FileDownloader.vb:1542` 每次以 50 KiB 为单位填满包，再在 `Clases/FileDownloader.vb:1568` 逐 16 字节调用 `ProcessBlock` 解密。`Clases/FileDownloader.vb:1517` 的落盘条件是累计接近 750 KiB。包越小，循环、节流计数和锁交接越频繁；包越大，取消与暂停的响应粒度越粗。
- `Clases/FileDownloader.vb:1595` 每 50 KiB 对共享 `Mutex` 取锁累加进度；`Clases/FileDownloader.vb:1608` 每 175 ms 计算一次速度并再次取锁。
- `Clases/ThrottledStream.vb:261` 的限速逻辑每次读取都做浮点计算，超限 slept 段按 20 ms 轮询；`Clases/ThrottledStreamController.vb:54` 的重算会遍历流集合并构造日志字符串，`Clases/ThrottledStreamController.vb:147` 无论是否调试都写入日志字符串。
- `Clases/Conexion.vb:142` 每次创建请求都重写全局 `ServicePointManager.SecurityProtocol` 与 `DefaultConnectionLimit`。这些是全局状态赋值，不必每请求重复；但改动时必须确认代理场景与既有兼容行为。

建议优化：

1. **先做微基准，再改默认值。** 用本机 HTTP 夹具测 50/128/256 KiB 包与 256/512/750 KiB 缓冲的组合，记录 MB/s、p95 读取延迟、取消到停止的延迟和峰值内存。不要在未测前直接上调默认值。
2. **进度累加改为分块本地累加。** 每个 chunk worker 先在自己的局部变量累计，按 175–250 ms 或FlushToDisk 边界一次性合并到共享进度，减少每 50 KiB 的全局锁交接。必须保留断电恢复只持久化已 fsync 边界的约束。
3. **限速重算只在限速变化时产生日志。** 保留 `_RecalcularVelocidad` 的计算语义，但把字符串构造放入实际写日志前；热路径上的 AddStream/RemoveStream 不应为调试文本付固定成本。
4. **消除重复全局网络设置。** 在一次性初始化中设置 `ServicePointManager`，请求构造只设置代理、超时和认证。以回归测试验证代理、IPv6、HTTPS 和 2 GB+ Range 行为不变。
5. **不要把 3 连接误当成普适最优。** 连接数应可配置并说明代价：连接越多越容易触发上游限流、重试和磁盘随机写。优化目标是稳定吞吐，不是最大并发数字。

验收要点：同一本机文件、同一缓冲/连接组合、至少 5 轮交替测试；对比下载完成总时长、有效吞吐、p95 单读耗时、CPU、工作集、取消响应时间和失败重试次数。所有断点、MetaMAC、同名冲突、配额 509 用例必须全绿。

## 方向二：MEGA 账号与 SDK 传输

代码事实：

- `Native/MegaAccount/MegaAccountBridge.cpp:223` 的传输等待循环使用 `transfer.trywait(100)`，每 100 ms 检查取消和限速；即使没有状态变化也会返回。相比事件驱动等待，这会引入固定唤醒成本和取消延迟上限。
- `Native/MegaAccount/MegaAccountBridge.cpp:195` 的 `DownloadRate::update()` 每次调用都可能再次读取委托并比较；限速变更在同一 SDK client 上调用 `setMaxDownloadSpeed`。同一账号并发传输共享预算，这一点必须保留。
- `Native/MegaAccount/MegaAccountBridge.cpp:46` 的进度回调把 SDK 的 transferred/total/speed 直接交给托管层。若回调频率高而 UI 每层都重绘，会造成账号下载比匿名下载更容易“卡顿但不慢”。
- `Clases/Fichero.vb:774` 用 `Task.Run` 启动账号下载；`Clases/Fichero.vb:1062` 的进度回调进入 `Mutex.FicheroDownloader`，更新字节、百分比和速度。签名、恢复和配额分支都在同一把锁附近汇合，锁竞争会表现为进度跳变和取消迟滞。
- 私有下载使用任务专属 `.mega.part` 和 `.owner`，交付前做整文件 MetaMAC；这部分是正确性边界，不应为速度牺牲。

建议优化：

1. **把固定 `trywait(100)` 改为可唤醒等待。** 保留超时轮询作为兜底，但让取消信号与限速变化能立即唤醒等待。目标是降低取消尾延迟和每秒固定唤醒次数；真实吞吐增益需实测，不能预设。
2. **进度向上传递做节流与合并。** SDK 回调只更新原子/轻量状态，UI 读快照时按固定节拍取一次；“速度”“字节”“状态变化”分开，避免每个回调触发列表重建。
3. **限速更新采用版本号而非每次重读。** `Clases/ThrottledStreamController.AccountDownloadBudget` 已有线程安全速度值；可增加单调版本，使 SDK 只在版本变化时 `setMaxDownloadSpeed`。
4. **保持每账号 SDK client 与预算隔离。** 多账号轮换、私有任务绑定原账号、公开链接轮换后的身份标记均是现有正确性要求，不应为共享吞吐而合并身份。
5. **补齐真实账号 A/B。** 只有真实账号和真实文件才能验证 SDK 进度频率、配额归属、会话恢复与私有断点收益；本机离线夹具只能验证节奏和状态机。

验收要点：至少一个小文件、一个大文件、暂停/取消、会话失效、配额等待、私有断点续传六类场景；记录完成时长、平均速度、进度回调次数、UI 刷新次数、取消到状态落地耗时和 SDK 错误码。任何规避配额的实现均不纳入。

## 方向三：软件流畅度与 UI 刷新

代码事实：

- `Forms/Main.vb:3352` 的周期 worker 每 430 ms 执行调度、快照、逐文件状态更新、聚合、列表刷新、状态栏、资源监控、配额横幅和失败横幅。
- `Forms/Main.vb:3375` 已经把全局锁内的重活移到锁外，这是正确方向；但 `Forms/Main.vb:3430` 仍每 tick 调用 `RefreshListaDescargas(False)`，`Forms/Main.vb:4872` 会对所有根对象执行 `RefreshObjects`。
- `Forms/Main.vb:4832` 的 `QueuePeriodicListRefresh` 已把最多一个待处理刷新合并到 UI 线程，避免无限排队；但尚未按“数据是否变化”跳过刷新。
- `Forms/Main.vb:2190` 的配额横幅已改为显隐边沿重排，但倒计时文字仍每 430 ms 走一次 `BeginInvoke`。
- `Forms/Main.Workbench.vb` 的搜索有 220 ms 防抖；`Forms/AddLinks.Preflight.vb` 与 `Forms/BatchPreflightView.vb` 的进度/筛选定时器分别约 150/180 ms。这些是交互响应底线，不应简单延长。

建议优化：

1. **给周期刷新加结构版本。** 由 `PublishQueueOverview` 或独立快照维护版本：任务状态、进度桶、名称/路径、错误计数、配额倒计时秒数任一变化才请求对应 UI 更新。进度桶可按整百分比或视觉阈值合并，避免每个字节刷新。
2. **拆分“轻刷新”和“重刷新”。** 仅速度/百分比变化时用 ObjectListView 的轻量单元格刷新或只更新可见行；结构性变化（增删包、筛选、排序、改名、状态跨组）才 `BuildList()`/`SetObjects()`。
3. **详情面板改为选中项版本驱动。** 当前刷新随全局 tick 进入锁；可只在选中对象或其字段版本变化时重算布局，避免选中大任务详情时重复排版。
4. **窗口不可见时降低 UI 成本，但不降业务节拍。** 现有 `QueuePeriodicListRefresh` 已跳过最小化/不可见。配额文字、失败横幅和资源监控也应遵循同一规则；下载调度和停止/恢复仍保持 430 ms。
5. **测量交互尾延迟。** 用 UI 专项夹具记录队列 679/6790 行时点击开始/暂停/选中、搜索输入到结果、滚动帧间隔、每 tick `RefreshObjects` 次数和 UI 线程被占用最长片段。

验收要点：结构变化必须立即反映，不能因节流漏掉失败、完成、错误、配额到期或手动暂停；低负载与高负载都要测，不能只看静态队列。

## 方向四：合法限制、代理与恢复体验

代码事实：

- `Clases/Conexion.vb:142` 的代理设置支持 host:port 与凭据，`SendJSON` 在传入 CancellationToken 时通过 `Abort()` 解开 .NET Framework 阻塞请求；这是合理的恢复能力。
- `Clases/MegaQuotaManager.vb:91` 对 509/-17 采用 60 分钟、2 小时、6 小时递进等待，并对同一熔断期内的重复上报去重；`TryGetRetryAfterSeconds` 支持 `Retry-After` 秒数和 HTTP-date。
- `Clases/FileDownloader.vb:1404` 会按状态码识别 509 并进入配额路径；`Clases/FileDownloader.vb:1445` 会校验 Content-Range 并对齐续传起点，避免错位解密。
- `Clases/Fichero.vb:1719` 附近的错误分类将永久错误、配额错误和会话错误分开，手动/自动重试路径依赖这些标记。

建议优化：

1. **代理与超时集中配置。** 把请求超时、读写超时、连接限制与代理构造从每次请求移到可测试的一次性配置入口，但保留对不同 URL/代理凭证的隔离，避免全局状态误用。
2. **配额等待展示服务端提示。** 已经有 `Retry-After` 解析；把它与 SDK 的 `bandwidthOverquotaDelay`、账号状态中的 `QuotaRetryUtc` 统一到同一个“最早可恢复时间”，减少用户误以为软件卡死。
3. **错误分类加可观测性。** 对重复 509、会话失效、过期直链、Range 被忽略分别记录轻量计数和最近一次时间，只在用户可见横幅/详情显示摘要，避免日志风暴本身拖慢热路径。
4. **避免误判导致全量重下。** 任何恢复优化都必须先验证已落盘成品的身份、长度和 MetaMAC；本次已修复的删除归属问题也说明“同名即同物”的假设不成立。

明确不做：自动轮换 IP/代理来规避配额、绕过账号登录、并发轰炸突破限速、伪装客户端身份或规避风控。

## 建议的实施顺序与验收矩阵

| 阶段 | 改动 | 主要收益 | 主要风险 | 必须先做的测量 |
| --- | --- | --- | --- | --- |
| P0 | 性能测量夹具：本机 HTTP 吞吐、UI 刷新计数、SDK 状态机计时 | 让后续优化有可比基线 | 夹具与真实网络不等价 | 固定文件大小、并发、缓冲/连接组合 |
| P1 | 匿名分块本地累计进度 + 限速日志降噪 + 请求配置收敛 | 减少锁竞争与分配，提高稳定吞吐 | 进度/取消与限速语义回归 | MB/s、p95 读取、取消延迟、CPU/内存 |
| P2 | SDK 可唤醒等待 + 进度节流 + 限速版本化 | 降低取消/暂停尾延迟与 UI 抖动 | 原生桥接生命周期竞态 | 真实账号大小文件、错误码、刷新次数 |
| P3 | UI 版本驱动刷新、可见行轻刷新、详情边沿刷新 | 大队列拖动/搜索/点击更顺 | 漏刷新结构变化 | 679/6790 行、帧间隔、UI 占用片段 |
| P4 | 配额/代理/错误提示统一 | 减少长等待中的“像卡死”体验 | 错误分类误导 | 509、会话失效、过期直链、Range 忽略 |

每个阶段完成后运行完整回归和发布包校验，并更新 `Tests/README.md`、`docs/CHANGELOG.md`。真实 MEGA 吞吐与配额归属只能通过用户授权的真实测试账号验证，不能由离线夹具代替。

## 当前工作区注意

当前分支还有两个未提交文件：`Clases/UiStyles.vb`、`Forms/AddLinks.vb` 的图标赋值；`artifacts/` 是本次发布与启动测量生成的未跟踪目录。建议先确认图标改动是否要独立提交，再开始性能改造，避免基线混淆。
