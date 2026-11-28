# MegaDownloader 性能优化清单（最终复核版）· 2026-11-28

分析基线：`7491d74`（main）。本文是优化计划与验收清单，不是实现记录；除标注“实测”外，“效果”均为目标值，必须按本文流程做 A/B 后才能写进发布说明。

本轮复核：逐条对照代码与现有性能用例，补充了 4 个之前未列出的热点，澄清了 2 处容易误判的开销，排除了 1 个不成立的候选。未改代码。

2026-11-28 二次核对：17 个优化项逐条对照代码，全部属实，引用行号已核对（个别 ±1 行）；重跑 4 个性能用例（4/4 通过、180 断言；MetaMAC 132ms、10 万条预检 6.89ms、快照 6ms），基线数字同量级。已按核对结果修正本文：P0-2 改为双缓冲优先，P1-1 明确收益范围并改为池化优先，P2-2 改为整 chunk CBC，P2-3 补边界/续传设计，新增 P0-8。未改代码。

相关文档：

- [性能与流畅度分析 · 2026-11-24](PERFORMANCE-FLOW-2026-11-24.zh-CN.md)
- [性能与启动速度分析 · 2026-11-05](PERFORMANCE_ANALYSIS_2026-11-05.zh-CN.md)

## 0. 当前基线

| 项目 | 当前值 | 来源 |
| --- | --- | --- |
| 冷启动到主窗口标题 | 0.85–1.02s，中位 0.88s | 2026-11-24 隔离测量（基线 `2a41b42`）；当前提交未重测 |
| 16MiB MetaMAC 校验 | 126ms | 2026-11-27 本机运行 |
| 10 万条预检初始化 | 5.78ms | 2026-11-27 本机运行 |
| 队列快照用例 | 5ms | 2026-11-27 本机运行；不等同 6790 行真实刷新 |
| 设置首开 / 重开 | 约 0.67s / 0.2s | 代码注释中的隔离实测 |
| 同提交全量回归 | 63/63 用例、1427 断言 | 此前已运行 |
| 本次性能专项 | 4/4 用例、180 断言 | `batch2_perf`、`startup_performance`、`queue_view_snapshot`、`preflight_selection` |
| 2026-11-28 二次核对 | 4/4 用例、180 断言；MetaMAC 132ms、预检 6.89ms、快照 6ms | 本机重跑，与上表同量级 |

结论：启动已经不错，先不动；优先处理每 tick 的大对象分配与锁扫描、下载热路径的 CPU、大文件完成后的校验等待。

### 复核结论

**已确认：**

- 每 60 秒一次强制 GC + 清工作集，worker 启动后的第一轮就会执行。
- 430ms tick 内存在多趟全表扫描、多次 UI 编组和同步 `Invoke`。
- 大文件 MetaMAC 在完成后单线程整文件重读。
- 账号 SDK 传输使用固定 100ms 轮询等待。

**本轮补充：**

1. **每 tick 的 LOH 分配**：`PublishQueueOverview` 的 `ViewItems`、`CheckDownloadNotifications` 的 `newStates` 字典，在数千文件规模下底层数组超过 85KB 的 LOH 阈值；旧数组每 tick 变成垃圾，带来 Gen2 GC 压力。选中大包时 `ReadQueueSelection` 的 `HashSet` 也会接近或超过阈值。注意本程序是 AnyCPU + `Prefer32Bit=false`（x64）：`QueueViewItem` 实际 32 字节，6790 项约 217KB；字典条目 24 字节且增长过程每 tick 还会留下多个 LOH 数组。原文按 16 字节/109KB 估算，结论不变但数量级偏小。
2. **包聚合仍在全局锁内**：`Paquete.ActualizarDatosDescarga` 仍取 `Mutex.ListaDescargas` 并遍历包内全部文件；`Main` 里的计数循环也在同一把锁内遍历快照。代码注释称“包聚合不需要锁”，与实现不一致，以代码为准。
3. **计数锁没有保护对象**：计数循环只写 `Me.NumDescargas...` 字段，读方（网页远控）并不取同一把锁；这把全局锁可以去掉，改用局部计数或 `Volatile`/原子字段。
4. **小文件缓冲过大**：每个 chunk worker 固定分配 750KiB `BufferDisk` + 50KiB `BufferMem`；分块数等于连接数（默认 3），所以 10KB 文件也会分配 800KiB，1MB 文件约 2.4MiB；大量小文件会制造 LOH 垃圾。
5. **状态机与验证泵的锁内全表扫描**（二次核对补充）：430ms tick 每轮必进状态机分支——下载态 `PonerFicherosADescargar` 在 `Mutex.ListaDescargas` 内两趟全表，暂停态 `PonerFicherosEnPausa`、停止态 `PararDescargaFicheros` 各一趟；磁盘 worker 每 250ms 的 `PumpVerifyQueue` 在无待验证文件时也走完全部文件。仅改 P0-3 的两处达不到其验收目标（详见 P0-8）。

**澄清：**

- 50KiB 进度锁是**每个下载器一把**（[FileDownloader.vb:439](../../Clases/FileDownloader.vb:439)），不是全局锁；只在本文件的 3 个 worker 之间竞争，优先级低于 UI 全局锁。
- 分块数等于 `PartsPerFile` → `ConexionesPorFichero`（默认 3），不是 1MiB 块数；每 chunk 的日志/限速/分配开销是“每文件 3 次量级”，不是“每 MiB 一次”。

**复核排除：**

- “分块簿记 O(n²)”不成立：`NextAvailablePartIndex`/`SetProgress` 的扫描长度是连接数（默认 3），不是大文件块数；不列入优化。

### 一页速览

| 优先级 | 优化点 | 用户能感知的效果 | 估算 |
| --- | --- | --- | --- |
| P0 | 去掉周期强制 GC；一次快照 + 双缓冲复用；包聚合/计数/失败计数/详情/状态机/验证泵出锁；合并 UI 帧 | 长时间挂机不再周期卡顿；万级队列选中/滚动/搜索更顺；空闲 CPU/GC 下降 | 2–3 天 |
| P1 | 小文件缓冲池化/按需分配、参数 A/B、批量 AES、日志/限速快路径 | 小文件队列 GC 和 CPU 下降；下载更稳；网络受限时不盲目加连接 | 3–5 天 |
| P2 | 分块 MAC 并行、硬件 AES、随下载计算 | 10GiB 从约 80s 降到 15–30s，最终接近 0 等待 | 5–8 天 |
| P3 | SDK 可唤醒等待、启动/设置微调 | 暂停/取消更跟手；启动/设置不回退 | 2–3 天 |

使用方式：按 P0 → P1 → P2 → P3 勾选；每个方框对应一次独立提交和一次 A/B，不要把多项合成一次“感觉快了”。

## 1. 优化点清单

### P0 — 界面与后台节拍（最先做，风险最低）

> 状态（2026-11-28）：P0-1 至 P0-8 已在工作区实现（未提交）。Release 构建通过；全量回归 63/63、1427 断言通过。专项 A/B、1 小时 working set 曲线未做，因此下方方框暂不勾选（按本文约定，方框 = 独立提交 + A/B 完成）。

- [ ] **P0-1 去掉/收紧周期性强制 GC**
  - 现状：更新检查 worker 默认每 60 秒执行 `GC.Collect()` + `WaitForPendingFinalizers()` + `SetProcessWorkingSetSize(-1,-1)`，worker 启动后的第一轮就会执行一次（[Main.vb:2998](../../Forms/Main.vb:2998)、[Main.vb:4679](../../Forms/Main.vb:4679)）。
  - 改法：删除周期调用；或仅当窗口最小化且无活动下载、间隔 ≥30 分钟时执行。先用 1 小时 working set 曲线确认没有真实泄漏，再决定是否保留兜底。
  - 验收：强制 GC 次数为 0；1 小时内 working set 平稳，无 OOM、无明显持续增长；不再出现每 60 秒一次的卡顿。
  - 预期：消除周期性卡顿和缺页，置信度高。

- [ ] **P0-2 一次快照 + 双缓冲复用，消除每 tick 的 LOH 分配**
  - 现状：`PublishQueueOverview` 每 tick 为每个包/文件重建 `ViewItems`，实际只用于比较出 Scope/Search 版本（[Main.Workbench.vb:886](../../Forms/Main.Workbench.vb:886)）；`CheckDownloadNotifications` 每 tick 重建整张文件的 `Dictionary(Of Fichero, Estado)` 和 `stillComplete` 字典（[Main.vb:3472](../../Forms/Main.vb:3472)）。x64 下 `QueueViewItem` 为 32 字节，6790 项约 217KB；字典条目 24 字节，且增长过程每 tick 还会留下多个 LOH 数组，全部变成垃圾。
  - 改法（优先双缓冲）：一次 snapshot 同时算计数、失败跃迁、包聚合；保留逐项比较，但把 `ViewItems` 与两个字典做成两份预分配缓冲轮换（字典用 `Clear()` 保留容量后重填、再交换引用），消除每 tick 新分配。**版本计数器为可选项**：`Fichero.EstadoDescarga` 是公开字段（[Fichero.vb:169](../../Clases/Fichero.vb:169)），全文件约 30 处直接写，改成“每次状态变化递增版本”要么把字段改属性、要么逐处挂钩，漏一处即漏刷新；只有 profile 证明逐项比较是 CPU 热点时才做。若做，SearchVersion 只由名称/结构变化递增，不能被进度变化触发。
  - 验收：每 tick 不再产生 O(文件数) 的 LOH 分配；679/6790/20k 队列无漏刷新（状态跨组、失败、完成、配额到期）；Gen2/LOH 计数下降。
  - 预期：显著降低 GC 暂停和 UI tick 工作，置信度高。

- [ ] **P0-3 包聚合和计数移出 `Mutex.ListaDescargas`**
  - 现状：`Paquete.ActualizarDatosDescarga` 先 `HayFicheros()` 取一次锁，再在 `SyncLock Mutex.ListaDescargas` 里遍历包内全部文件（[Paquete.vb:39](../../Clases/Paquete.vb:39)、[Paquete.vb:89](../../Clases/Paquete.vb:89)）；`Main` 的计数循环也在 `Mutex.ListaDescargas` 内遍历快照（[Main.vb:3328](../../Forms/Main.vb:3328)），但只写 `Me.NumDescargas...` 字段，读方并不取同一把锁。
  - 改法：把 worker 已经拿到的 `kv.Value` 快照传给包聚合，遍历快照、不取全局锁；计数用局部变量算完再一次性写入，网页远控读取的字段改为原子/`Volatile` 或从快照读取（注意字段现为 `Integer?`，[Main.vb:207](../../Forms/Main.vb:207)，`Volatile`/`Interlocked` 不能直接用于 Nullable，需改成普通 `Integer` 或发布单个计数器快照对象）。修正 [Main.vb:3323](../../Forms/Main.vb:3323) 与实现不一致的注释。
  - 验收：每 tick 的 `ListaDescargas` 锁只用于短拷贝/增删，不再出现 O(包×文件) 的锁内遍历；包状态、百分比、计数与网页远控读数不变。**该验收需与 P0-8 一起完成**，否则状态机分支仍会在锁内全表遍历。
  - 预期：再减少一趟每 tick 全表全局锁，置信度高。

- [ ] **P0-4 只更新下载中的文件，ETA 移出 `Mutex.FicheroDownloader`**
  - 现状：每个文件都调用 `ActualizarDatosDescarga`，整段包在全局 `Mutex.FicheroDownloader`（[Mutex.vb:10](../../Clases/Mutex.vb:10)）里，连排队、暂停、已完成文件也照锁；ETA 字符串格式化也在锁内（[Fichero.vb:1782](../../Clases/Fichero.vb:1782)）。6790 个文件就是每 tick 6790 次全局锁。
  - 改法：只对 `Descargando` / `CreandoLocal` / `Verificando` 等活跃态调用；ETA 格式化放到锁外；状态离开活跃态时补一次速度/ETA 清零。账号任务的进度由 `SetMegaAccountProgress` 维护，跳过非活跃文件不影响。
  - 验收：锁获取次数约等于活跃文件数；暂停/完成/失败后速度、ETA 仍正确清零；账号任务进度不丢。
  - 预期：每 tick 少 6790 次全局锁与 ETA 格式化；全局锁本身基本无竞争（下载进度用每下载器锁，[FileDownloader.vb:439](../../Clases/FileDownloader.vb:439)），收益以清理为主，置信度高。

- [ ] **P0-5 失败计数和选择摘要移出锁内全表扫描**
  - 现状：`CountBannerFailures` 每 tick 在 `Mutex.ListaDescargas` 内扫全表（[Main.Failures.shared.vb:46](../../Forms/Main.Failures.shared.vb:46)）；选中大包时 `ReadQueueSelection` 每 tick 在锁内扫该包全部文件、构建 `HashSet`，再遍历一遍算等待原因（[Main.Workbench.vb:594](../../Forms/Main.Workbench.vb:594)）。已知 2.6.4 修复过 `RefreshObjects` 触发 SelectionChanged 的闪烁，但 `SelectionChanged` 仍会再次调用 `UpdateDetailPanel`（[Main.vb:2735](../../Forms/Main.vb:2735)、[Main.vb:2744](../../Forms/Main.vb:2744)），同一 tick 可能重复计算。
  - 改法：失败横幅从快照取“失败文件身份 + `FechaUltimoError`”列表，而不是纯计数（dismiss 语义按文件比对，[Main.Failures.shared.vb:43](../../Forms/Main.Failures.shared.vb:43)）；详情摘要优先做同 tick 去重（SelectionChanged 与周期刷新只算一次），跨 tick 缓存需把调度窗口开启、配额重试倒计时等时间边界也作为失效条件，不能只看 ScopeVersion；状态/账号/配额变化时重算。
  - 验收：tick 内不再出现 O(选中文件数) 的全局锁扫描；失败横幅计数与 dismiss 行为不变；等待原因仍准确；无重复刷新。
  - 预期：选中万级包时 UI 和锁竞争明显下降，置信度高。

- [ ] **P0-6 合并 UI 帧，去掉同步 `Invoke`**
  - 现状：状态栏、托盘文字用同步 `Invoke` 回 UI（[Main.vb:4710](../../Forms/Main.vb:4710)、[Main.vb:4800](../../Forms/Main.vb:4800)）；列表、导航、配额、失败横幅各自 `BeginInvoke`，UI 忙时会排队。
  - 改法：一个 UI 帧（一次 `BeginInvoke`）承载状态栏/托盘/导航/横幅；全部异步；窗口不可见时跳过窗口绘制，但托盘文字仍要更新（最小化后它是唯一显示）；只刷新变化值。
  - 验收：每 tick ≤1 次 UI 编组；UI 忙时 430ms worker 不被阻塞；最小化时 UI 工作≈0（业务节拍保留）。
  - 预期：拖动、选中、打开设置时更跟手，置信度高。

- [ ] **P0-7 列表/详情只在可见变化时刷新**
  - 现状：每 tick `RefreshObjects(Roots)` + `UpdateDetailPanel`，即使数值未变也执行。
  - 改法：结构变化立即刷新；进度按 1% 桶或可见行轻刷新；详情值未变不重排；与 P0-5 的去重共用同一个版本号。
  - 验收：开始/暂停/完成/失败/配额/账号等待仍即时；滚动和选中不闪。
  - 预期：降低绘制占用，置信度中高。

- [ ] **P0-8 状态机与验证泵的全表锁内扫描**
  - 现状：430ms tick 每轮必进状态机分支：下载态 `PonerFicherosADescargar` 在 `Mutex.ListaDescargas` 内两趟全表（失败重试扫描 [Main.vb:3652](../../Forms/Main.vb:3652)、连接数统计 [Main.vb:3674](../../Forms/Main.vb:3674)）；暂停态 `PonerFicherosEnPausa`（[Main.vb:3772](../../Forms/Main.vb:3772)）、停止态 `PararDescargaFicheros`（[Main.vb:3804](../../Forms/Main.vb:3804)）各一趟。磁盘 worker 每 250ms 调 `PumpVerifyQueue`，无待验证文件时也走完全部 package/file（[Main.vb:3145](../../Forms/Main.vb:3145)）。
  - 改法：状态机分支维护“活跃/排队/待重试”集合或增量计数，每 tick 只处理这些对象；`PumpVerifyQueue` 用待验证计数或游标，空队列 O(1) 返回；遍历基于 tick 快照，不持全局锁。
  - 验收：每 tick 的 `ListaDescargas` 锁内不再出现 O(全部文件) 遍历；开始/暂停/停止、失败自愈、新链接入库仍即时。
  - 预期：万级队列 tick 锁持有时间再降一档，置信度高。

### P1 — 下载热路径（先做确定收益，再谈算法）

> 状态（2026-11-28）：P1-1、P1-3、P1-4、P1-5、P1-6 已在工作区实现（未提交）；P1-2 未做（测量任务，且应在 P1-1/P1-3 落地后重新取基线）。全量回归 63/63、1431 断言通过（新增 3 条缓冲池断言 + 1 条批量 CTR 等价断言）。P1-3 微基准：BouncyCastle 逐块约 139MB/s，系统 AES 批量 `ProcessBlocks` 约 553MB/s（4×），已满足“先确认 CPU 是瓶颈”的前置条件；P1-1 模拟 500 文件×3 chunk 的缓冲分配由 1172MB 降到 1.22MB。

- [ ] **P1-1 小文件/小块缓冲池化与按需分配**
  - 现状：每个 chunk worker 固定分配 750KiB `BufferDisk` 和 50KiB `BufferMem`（[FileDownloader.vb:1350](../../Clases/FileDownloader.vb:1350)）；分块数等于 `PartsPerFile` → `ConexionesPorFichero`（默认 3，[Fichero.vb:1357](../../Clases/Fichero.vb:1357)）。10KB 文件只有 1 个 chunk，仍分配约 800KiB；1MB 文件约 3×800KiB = 2.4MiB；`BufferDisk` 超过 85KB，进入 LOH。
  - 改法（池化优先）：按连接槽复用/池化 `BufferDisk`/`BufferMem`，池内数组按最大需求一次分配、反复使用，这是让 1MB–2MB 文件也摆脱每文件 LOH 的唯一办法。按需分配作为附带：`BufferDisk = min(BufferSize, 实际 chunk 大小 + 一个 package 的余量)`，`BufferMem = min(PackageSize, chunk 大小)`。无论哪种，读循环的 `Me.PackageSize`（[FileDownloader.vb:1550](../../Clases/FileDownloader.vb:1550)）和 flush 条件的 `Me.BufferSize`（[FileDownloader.vb:1525](../../Clases/FileDownloader.vb:1525)）都必须改为按实际数组长度，不能只改分配处。
  - 验收：10KB、1MB、1GB、10GB 文件都能完成、续传、MetaMAC 校验；大文件吞吐不回退。LOH 分档记录：仅按需分配只能让约 <100KB 的文件脱离 LOH（chunk+50KB ≤ 85KB），1MB–2MB 文件仍需池化；池化后整个小文件样本的每文件 LOH 分配应接近 0。
  - 预期：池化后大量小文件队列的 GC 和 CPU 明显下降；只做按需分配时收益仅覆盖 <100KB 文件，大文件无感，置信度高。

- [ ] **P1-2 下载参数 A/B（不改默认值，先拿数据）**
  - 现状：默认 3 并发 × 3 连接、750KiB 缓冲、50KiB 包；每 50KiB 取一次每下载器进度锁（[FileDownloader.vb:1603](../../Clases/FileDownloader.vb:1603)）。
  - 改法：固定本机 HTTP 夹具，分别跑“1 个 10GiB 大文件”和“2000 个 10KB–2MB 小文件”两组；连接 3/4/6、缓冲 750KiB/1.5MiB、包 50/128/256KiB；记录 MB/s、p95 单读、CPU、取消延迟、重试次数、LOH/GC。真实 MEGA 只在有授权测试账号时补测。
  - 验收：形成对照表；只有稳定胜出的组合才考虑改默认值；小文件场景不被大文件调优牺牲。
  - 预期：可能提升稳定吞吐，也可能证明当前默认已合适；置信度取决于实测。

- [ ] **P1-3 批量 CTR / 硬件 AES（仅当 CPU 是瓶颈）**
  - 现状：解密是每 16 字节一次虚调用 + BouncyCastle AES + 16 字节 XOR（[Criptografia.vb:717](../../Clases/Criptografia.vb:717)、[FileDownloader.vb:1576](../../Clases/FileDownloader.vb:1576)）。按 126ms/16MiB 估算，单核约 130MB/s。注意 126ms 来自 MetaMAC 校验路径（同样是每 16 字节一次托管 AES），决定做之前应直接微基准 `FileDownloader` 的解密循环。
  - 改法：先做微基准确认 BouncyCastle 是否已用硬件 AES；若 CPU-bound，一次生成多块 keystream（系统 AES/AES-NI），再批量 XOR；保留原有 CTR 计数器和 seek 语义。
  - 验收：下载字节与断点续传逐字节一致；CPU/MB 下降；网络受限场景 MB/s 不下降。
  - 预期：低端 CPU/多连接场景提升明显，高端机器可能无感；置信度中。

- [ ] **P1-4 日志级别判断前置**
  - 现状：`Log.WriteDebug/Info` 先做 3 条正则脱敏，再判断级别（[Log.vb:60](../../Clases/Log.vb:60)）；默认 Normal 下日志被丢弃，但正则照跑。每个文件约 3 个 chunk，每个 chunk 有若干调试日志。
  - 改法：`WriteDebug/WriteInfo` 先检查级别，再调用 `Redact`。
  - 验收：日志内容不变；全量回归通过。
  - 预期：低风险小幅收益，置信度高。

- [ ] **P1-5 无限速时跳过限速重算与日志**
  - 现状：`ThrottledStreamController` 每次 `AddStream`/`RemoveStream`（每个 chunk/连接）都重算并构造日志字符串，即使没有设置任何限速（[ThrottledStreamController.vb:54](../../Clases/ThrottledStreamController.vb:54)、[:147](../../Clases/ThrottledStreamController.vb:147)）。每文件约 3–6 次，量级不大，但属于纯浪费。
  - 改法：全局和单 ID 限速都为 0 时走快速路径；日志字符串按级别守卫；从“有限速”切到“无限速”时仍做一次清零重算。
  - 验收：无限速下载行为不变；限速用例全绿。
  - 预期：低风险小幅收益，置信度中高。

- [ ] **P1-6 全局网络设置只初始化一次**
  - 现状：每个请求都重设 `ServicePointManager.SecurityProtocol` 和 `DefaultConnectionLimit`（[Conexion.vb:142](../../Clases/Conexion.vb:142)）。
  - 改法：启动时设置一次；请求构造只设置代理、超时和认证。
  - 验收：代理、TLS、IPv6、2GB+ Range 行为不变；回归通过。
  - 预期：低风险小幅收益，置信度高。

### P2 — 大文件完成校验（用户体感最强的一段等待）

> 状态（2026-11-28）：P2-2 与 P2-1 已实现（未提交）——MetaMAC 改为整 chunk 系统 AES-CBC，大文件按“顺序读 + 并行算”分组处理；16MiB 校验 131ms → 32ms，512MiB 3.64s → 0.20s（冷）/0.15s（热），约 20×。P2-3（下载时顺带算 MAC、持久化断点状态）未做：需 sidecar 持久化、part 边界跨 chunk 协调与 SyncedIndex 回退，按清单约定单独立项。全量回归 63/63、1431 断言通过。

- [ ] **P2-1 分块 MAC 并行**
  - 现状：下载完成后整文件单线程重读算 MetaMAC（[FileDownloader.vb:1114](../../Clases/FileDownloader.vb:1114)、[Criptografia.vb:832](../../Clases/Criptografia.vb:832)）；16MiB 实测 126ms，10GiB 线性约 80 秒。MetaMAC 的 chunk 是 1MiB 级、数量多，和下载分块不是一回事。
  - 改法：分块 MAC 彼此独立，用 2–4 个 worker 各读各的 chunk，最后按原顺序合并；每 worker 独立缓冲和 FileStream。HDD 上可采用“顺序读 + 并行算”，避免随机读。
  - 验收：与串行结果逐字节一致（空文件、4 词跳过、8 词、跨块、残缺尾块、断点续传）；10GiB 校验时间显著下降。
  - 预期：SSD/多核上数倍收益，置信度中高。若 P2-2 的整 chunk CBC 已把校验降到磁盘带宽以下，并行收益会受限，可先做 P2-2 再决定是否并行。

- [ ] **P2-2 MAC 使用系统 AES（CBC 整块，吃 AES-NI）**
  - 现状：MAC 链使用 BouncyCastle 托管 `AesEngine` 逐块加密（[Criptografia.vb:845](../../Clases/Criptografia.vb:845)）。
  - 改法：CBC-MAC 链在数学上就是 CBC 加密（IV = 文件 nonce 重复，尾块零填充），用系统 `Aes` 的 **CBC 模式 + `PaddingMode.None` 对整个 MetaMAC chunk 一次 `TransformBlock`，取最后一块密文**；每 chunk 一次调用才能吃到 AES-NI（优先 `AesCng`）。**不要按字面用 ECB 每 16 字节调一次 `TransformBlock`**——调用开销会抵消硬件收益，可能比现在还慢。
  - 验收：与现有实现逐字节一致（空文件、4 词跳过、8 词、跨块、残缺尾块、断点续传）；同文件校验时间下降。
  - 预期：整 chunk 批量后可达 GB/s 级，即使不并行也能快数倍；单块 ECB 写法不在此预期内，置信度中。

- [ ] **P2-3 下载时顺带计算 chunk MAC（长期方案）**
  - 现状：完成后再读一遍文件，是“100% 后还要等”的根因。
  - 改法：解密/落盘时同步计算每个 chunk 的 MAC，和断点状态一起保存；完成时只做合并和比较；续传时复用已保存的 chunk MAC，缺失的才重算。**两个必须处理的设计点**：
    - MetaMAC 的 chunk（128KiB×i，之后 1MiB，[Criptografia.vb:875](../../Clases/Criptografia.vb:875)）与下载分块（默认 3 个 16KiB 对齐 part，[FileDownloader.vb:55](../../Clases/FileDownloader.vb:55)）边界不重合；每个 part 边界最多有 1 个 MetaMAC chunk 横跨两个 worker，全文件最多 2 个。这两个 chunk 必须推迟到前一个 part 完成后从磁盘补算（或做跨 worker 协调），不能按 part 各自算完就合并。
    - MAC 状态必须和 `SyncedIndex` 一起持久化；flush 时进度会向下取整到 16 字节（[FileDownloader.vb:1718](../../Clases/FileDownloader.vb:1718)），MAC 链也必须回退到同一边界，否则续传后结果不一致。
  - 验收：新下载与旧实现结果一致；暂停/重启/续传/取消全部覆盖；**跨 part 边界的 chunk、断点落在 MetaMAC chunk 中间**两种情况必须单列用例；完成后额外等待接近 0。
  - 预期：彻底消除二次全文件读；改动最大，单独立项，置信度中。

### P3 — 账号 SDK、启动与设置（收益较低，最后做）

- [ ] **P3-1 SDK 可唤醒等待**
  - 现状：原生桥固定 `trywait(100)`，每秒 10 次唤醒，取消/暂停最多多等 100ms。传输路径在 [MegaAccountBridge.cpp:370](../../Native/MegaAccount/MegaAccountBridge.cpp:370) 和 [:537](../../Native/MegaAccount/MegaAccountBridge.cpp:537)；[:111](../../Native/MegaAccount/MegaAccountBridge.cpp:111) 是 API 请求路径。
  - 改法：取消信号和限速变化可立即唤醒；保留超时轮询兜底。需要重编原生桥。
  - 验收：真实账号的小文件/大文件、暂停/取消/会话失效；取消到尾延迟下降；吞吐不回退。
  - 预期：手感改善，吞吐基本不变，置信度中。

- [ ] **P3-2 启动/设置只做低风险微调**
  - 现状：冷启动中位 0.88s，设置首开约 0.67s、重开约 0.2s；已有后台队列、懒建页、窗口复用。
  - 改法（可选）：空闲时预热设置窗口；`Buffer.dat` 参数从 430ms 轮询改为事件唤醒；启动队列就绪前允许先解析链接。
  - 验收：首屏/设置不回退；启动专项全绿。
  - 预期：小幅体验改善，置信度中低；不建议为此重写启动流程。

## 2. 优化流程

### Step 0 — 建立基线（必须先做）

- [ ] 固定测量环境：同一台机器、同一网络/代理、同一份 679/6790/20k 队列夹具。
- [ ] 准备两类下载夹具：1 个 10GiB 大文件；2000 个 10KB–2MB 小文件。真实 MEGA 只在有授权测试账号时补测。
- [ ] 记录启动：进程到主窗口、队列就绪、允许下载三个时间点。
- [ ] 记录 UI：每 tick worker/UI 耗时、UI 线程占用最长片段、每 tick 的 `BeginInvoke`/`Invoke` 次数、列表刷新次数、全局锁获取次数。
- [ ] 记录 GC/内存：强制 GC 次数、Gen0/Gen2 次数与暂停、**每 tick 的 LOH 分配次数/字节**、1 小时 working set 曲线。
- [ ] 记录下载：MB/s、p95 单读、CPU、取消到尾延迟、重试次数、每文件缓冲分配字节。
- [ ] 记录校验：1GiB/10GiB 文件的 MetaMAC 耗时。
- [ ] 把基线写进本文件或独立 CSV，后续每项优化都与它对比。

### Step 1 — P0 界面与后台

- [ ] 按 P0-1 → P0-5 顺序实现，P0-6/P0-7/P0-8 随后；每项单独提交、单独 A/B。
- [ ] 每项完成后跑：目标 UI 用例、完整回归、最小化/恢复、开始/暂停/停止、配额、失败横幅。
- [ ] 退出条件：状态变化无漏刷新；每 tick 指标下降；1 小时内存平稳。

### Step 2 — P1 下载快路径

- [ ] 先做 P1-1（小文件缓冲池化），再用 Step 0 夹具跑 P1-2 参数矩阵。
- [ ] 根据 CPU 占比决定是否做 P1-3；再做 P1-4/5/6 的低风险清理。
- [ ] 退出条件：小文件 LOH 下降、大文件 MB/s 不回退、断点/MetaMAC/限速用例全绿。

### Step 3 — P2 校验

- [ ] 先做 P2-2（整 chunk CBC）微基准与实现，与串行结果逐字节对照；若已降到磁盘带宽以下，再决定 P2-1 并行是否值得。
- [ ] 两项都稳定后再立项 P2-3，先补齐边界 chunk 与续传 MAC 状态设计。
- [ ] 退出条件：结果一致；10GiB 时间下降；续传/暂停/取消全部覆盖。

### Step 4 — P3 账号与启动

- [ ] P3-1 重编原生桥，用真实测试账号验证暂停/取消/会话失效。
- [ ] P3-2 只在 Step 1–3 完成后做，避免混淆基线。

### Step 5 — 发布验收

- [ ] 完整回归（当前基线 63/63、1427 断言）。
- [ ] `Tests\Stage-ReleaseAssets.ps1` 通过（内嵌 DLL、MEGA SDK、ZIP）。
- [ ] 更新 CHANGELOG、性能文档和本清单的执行记录。
- [ ] 发布说明只写实测收益；目标值不能当结果。

## 3. 优化后的效果体现

### 3.1 用户视角

| 场景 | 现在 | 优化后 | 对应项 |
| --- | --- | --- | --- |
| 长时间挂机 | 每 60 秒可能卡一下 | 无周期强制 GC/清工作集 | P0-1 |
| 万级队列选中、滚动、搜索 | 每 430ms 多趟全表扫描、LOH 分配和锁竞争 | 更顺、CPU/GC 更低 | P0-2 ~ P0-8 |
| 大量小文件下载 | 每个 chunk 固定 800KiB 缓冲，LOH 垃圾多 | 缓冲池化（必要时按 chunk 大小分配），GC 明显减少 | P1-1 |
| 下载时 CPU/风扇 | 逐块 AES + 热路径日志/限速开销 | CPU 降低；若 CPU 是瓶颈则 MB/s 提升 | P1-3 ~ P1-6 |
| 10GiB 文件下完 | 100% 后约 80 秒校验 | 整块 CBC 后接近磁盘带宽；并行后约 15–30 秒；随下载计算后接近 0 | P2 |
| 暂停/取消账号任务 | 最多 100ms 尾延迟 | 可唤醒等待，更跟手 | P3-1 |
| 启动/设置 | 0.88s / 0.67s | 维持现状，不回退 | P3-2 |

### 3.2 工程验收指标

| 指标 | 基线 | 目标 | 验证方式 | 置信度 |
| --- | --- | --- | --- | --- |
| 强制 GC 次数 | 约 1 次/分钟 | 0 | 日志/ETW | 高 |
| 每 tick LOH 分配 | Step 0 测量（6790 项时约 0.2–0.8MB，随字典增长浮动） | 0（不再 O(文件数)） | 分配计数器/ETW | 高 |
| 每 tick 全局锁获取 | O(全部文件) | O(活跃文件 + 包)；快照拷贝为 O(全部文件) 指针拷贝 | 计数器 | 高 |
| 每 tick UI 编组 | 4+ BeginInvoke + 2 同步 Invoke | ≤1 BeginInvoke，0 同步 Invoke | 计数器 | 高 |
| 小文件缓冲分配/文件 | 约 3 × 800KiB | 池化复用；或 ≤ 3 × min(750KiB, chunk 大小) | 分配计数器 | 高 |
| UI tick p95（6790 行） | Step 0 测量（参考 21ms 静态全展开刷新） | 下降 ≥50%，目标 ≤10ms | UI 夹具 | 中高 |
| 下载 CPU/MB | Step 0 测量 | 下降；MB/s 不回退 | 本机 HTTP A/B | 中 |
| 10GiB MetaMAC | 约 80s（线性估算） | 15–30s | 1/10GiB 夹具 | 中高 |
| 取消到尾延迟 | 最多约 100ms | <20ms | SDK/真实账号 | 中 |
| 1 小时 working set | Step 0 测量 | 平稳，无持续增长 | 进程监控 | 中 |
| 冷启动到主窗口 | 0.88s | 不回退 | 进程到窗口计时 | 高 |

说明：目标值是工程目标，不是发布承诺；每项以 A/B 实测为准，未达标就回退该项，不叠加宣称收益。

## 4. 最终验收清单

- [ ] Step 0 基线已记录（含 LOH/GC、锁、UI 编组、大小文件两组下载），后续每项都有对照数据。
- [ ] P0 全部完成（含 P0-8 状态机/验证泵），状态变化（开始/暂停/停止/完成/失败/配额/账号等待）无漏刷新。
- [ ] 每 tick 的 LOH 分配、锁获取、UI 编组、全表扫描达到目标。
- [ ] 1 小时挂机无周期卡顿，working set 平稳。
- [ ] P1 小文件缓冲完成（池化优先），小文件 LOH 下降；参数 A/B 有结论；MB/s 不回退。
- [ ] P2 校验结果逐字节一致，10GiB 时间达到目标或明确记录未达标原因。
- [ ] P3 账号暂停/取消在真实账号上验证；启动/设置不回退。
- [ ] 完整回归通过（63/63、1427 断言），性能专项通过。
- [ ] 发布包校验通过，CHANGELOG/文档已更新。
- [ ] 每项收益来自实测，没有把目标值写进发布说明。

## 5. 风险与不做

### 风险

| 风险 | 缓解 |
| --- | --- |
| 去掉强制 GC 后内存增长 | 先跑 1 小时 working set；若是真泄漏，修泄漏而不是恢复周期 GC |
| 快照/版本驱动漏刷新 | 覆盖所有状态跃迁和配额/账号变化；结构变化立即刷新 |
| 包聚合改读快照后短暂陈旧 | 只影响计数类 UI，最多差一个 430ms 节拍；状态切换用例覆盖 |
| 只更新活跃文件后残留旧速度/ETA | 状态离开活跃态时补一次清零；暂停/完成/失败用例覆盖 |
| 缓冲按 chunk 缩小后语义变化 | 覆盖 10KB/1MB/1GB/10GB、续传、MetaMAC；保留原默认上限；池化方案要覆盖租借/归还 |
| AES 改动破坏 CTR/MetaMAC | 逐字节对照 + 断点续传用例；P2-2 用 CBC 整块而非 ECB 单块；原型稳定前不合并 |
| 随下载算 MAC 在 part 边界/续传点不一致 | 边界 chunk 推迟补算；MAC 状态随 SyncedIndex 持久化并覆盖断点用例 |
| 并行校验增加磁盘竞争 | 2–4 worker，HDD/SSD 都测；收益不足就只做硬件 AES |
| 参数调优触发上游限流 | 只改有实测胜出的组合；保留现有默认兜底 |

### 不做

- 不盲目增加连接数/缓冲/并发；没有 A/B 不改默认值。
- 不关闭 MetaMAC 校验来换速度。
- 不为“感觉快”重写 UI 框架或更换存储。
- 不增加更多轮询线程来“优化”响应。
- 不在没有基线的情况下做微优化。

## 6. 建议排期

| 阶段 | 内容 | 估算 | 退出条件 |
| --- | --- | --- | --- |
| Step 0 | 基线 + 夹具 | 1 天 | 数据可复现 |
| P0 | 界面/GC/锁/LOH | 2–3 天 | tick 指标达标，回归绿 |
| P1 | 小文件缓冲 + A/B + 快路径 | 3–5 天 | LOH 降、CPU 降、MB/s 不回退 |
| P2 | 校验 | 5–8 天 | 逐字节一致，时间达标 |
| P3 | SDK/启动 | 2–3 天 | 真实账号验证，不回退 |

估算只用于排期，不构成交付承诺。

## 7. 执行记录

| 日期 | 项 | 提交 | 基线 → 结果 | 回归 | 备注 |
| --- | --- | --- | --- | --- | --- |
| 2026-11-28 | 清单二次核对 | 未改代码 | 17 项逐条属实；4 个性能用例重跑 4/4、180 断言；MetaMAC 132ms、预检 6.89ms、快照 6ms | 性能专项通过（未跑全量） | 修正 P2-2 改法、P2-3 设计、P1-1 收益范围、P0-2 双缓冲优先，新增 P0-8 |
| 2026-11-28 | P0-1 ~ P0-8 | 工作区（未提交） | 移除周期 GC/清工作集；tick 双缓冲；包聚合/计数/失败计数/状态机/验证泵出锁；单 UI 帧；空闲轻刷新 | 63/63、1427 断言 | P0-7 为混合刷新（有活动下载仍全量）；P0-5 选中集合仍做一次短锁收集 |
| 2026-11-28 | P1-1、P1-3 ~ P1-6 | 工作区（未提交） | `DownloadBufferPool` 池化 + 按需分配；批量 CTR（系统 AES，微基准 139→553MB/s）；日志级别前置；无限速快速路径；网络全局设置一次化 | 63/63、1431 断言 | 新增缓冲池/批量 CTR 断言；500 文件×3 chunk 模拟分配 1172MB→1.22MB；P1-2 参数 A/B 未做 |
| 2026-11-28 | P2-1、P2-2 | 工作区（未提交） | MetaMAC 改为整 chunk 系统 AES-CBC（AES-NI）；大文件顺序读 + 最多 4 路并行算，保留 BouncyCastle 回退 | 63/63、1431 断言；16MiB 131→32ms；512MiB 3.64s→0.20s（冷）/0.15s（热） | P2-3 未做（需持久化 chunk MAC、跨 part 边界与断点回退，单独立项） |
|  |  |  |  |  |  |
