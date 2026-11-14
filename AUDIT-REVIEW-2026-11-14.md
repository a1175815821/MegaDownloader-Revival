# 最近修改与修复审查报告（修复后状态）

日期：2026-11-14
分支：`docs/readme-first-screen`
结论：**阻断级问题已经修复并通过 Debug/Release 全量回归；但当前工作树仍未提交，也未推送，未做真实 MEGA 下载与发布验证。** 本轮包含本地工作树调整及后续验证，没有上传或发布。

## 1. 最终验证基线

- Debug/x86 构建通过，无编译警告。
- Release/x86 构建通过，无编译警告。
- Debug 全量回归：`PASS: 936 assertions.`
- Release 全量回归：`PASS: 936 assertions.`
- 原始 924 条断言已增加至 936 条。
- 新增的 `ConfirmedAuditFixes()` 覆盖 split-key、长名哈希唯一化、非首分卷等待与 StreamingModule 的原始 TCP 502 响应。
- 未在真实 MEGA 网络环境中下载并验证，也未完成实际安装/发布验证。

## 2. 本轮已修复的高风险问题

### 2.1 Split-key 回退已恢复

- 恢复了因聊天软件插入空格或换行而被截断的 43 字符 file key 与 22 字符 folder key 的修复。
- 只有在去空白结果恰好等于标准 key 长度时才采用结果，因此后续正文不会被错误并入 key。
- 链接类型重新用清洗后的文本识别，避免 folder 链接被按 file key 长度误判。

### 2.2 多卷等待状态机已闭合

- `EsperandoVolumenes` 已加入状态机；同一归档的多个 code 可一起登记、等待、完成或失败。
- `DescompresionDiferida` 事件会把任务送入“等待分卷”状态，而非长期停留在“解压中”。
- 后到分卷会加入同一等待记录并刷新等待窗口；首卷缺失时仍可先登记等待，不会被误报为无需解压或已完成。
- “等分卷”记录以多个 code 聚合，不再只对应单个 GUI 任务。
- 目录枚举移出全局控制器锁；已有等待记录会按新卷数刷新窗口。
- `Estado.EsperandoVolumenes` 已放到枚举末尾，以保持既有数值兼容；队列 XML 以符号名称持久化，避免因新值插入而变更旧数值。

### 2.3 已解压台账已加强

- 台账由单一 `LastWriteTimeUtc` 改为长度、修改时间和前 8 KB 的 SHA256 摘要组合。
- 台账计算移出全局队列锁，保持磁盘 I/O 不 блоки短锁临界区。
- 分卷仍会回到正常解压流程；只要首卷或后续卷仍在变化就不会用旧完成台账误判。

### 2.4 7z 预改名与回滚已收敛

- 7z 解压改为先写入一次性 staging 目录，再按“原始相对名 → `PathGuard` 安全目标”映射提交。
- 删除了旧的预测式预改名和 `UndoPreservingRenames`；实际改名结果统一写入 `PreservedCopies`，失败时统一恢复。
- ZIP、RAR、TAR 等托管解压同样遵循“提交成功才清保留表，失败则恢复原文件”的规则。
- `Extract()` 的外层 `Try/Catch` 与内层事务均保持完整，异常仍会落入 `Exception`。

### 2.5 长归档条目名与 MAX_PATH 已处理

- 归档条目名超过 240 字符时，使用 SHA256 前六字节生成稳定哈希后缀；两个仅在末尾不同的长名不会碰撞。
- 7z staging 现在对每个归档条目做真实目标映射和重复目标检测。
- 应用配置增加 .NET Framework 4.8 现代路径归一开关，使 `Path.GetFullPath()` 不再因传统 260 字符 MAX_PATH 限制截断合法的 255 字符单段名。

### 2.6 StreamingModule 502 响应已修复

- 错误时先清除可能残留的 `Content-Length`、`Content-Range`、`Accept-Ranges`、`Content-Disposition`、`Content-Encoding` 和 `Content-Type` 媒体头。
- 错误正文经 `ContentLength` 精确声明；gzip 请求会得到 gzip 后的实际长度。
- 新增原始 TCP 回归测试覆盖普通请求和带 `Range` 的请求，并确认 502 响应没有残留媒体头、声明长度与实际 body 长度一致。

### 2.7 下载退避代际与启动恢复已修复

- 下载退避 Task 现在记录 generation，在等待、重新排队和新建 worker 前均检查代际，Stop→Start 的旧任务不会复活。
- 启动失败的通用 Catch 只使用瞬时写保护；真正损坏由加载器分类后处理，避免瞬时文件锁把旧 `.bak` 恢复成当前队列。
- `ArmTransientWriteProtection()` 让启动性能回归测试通过，并保持写保护路径明确。

### 2.8 更新提醒和取消状态已修复

- `Updater` 每次检查前清空 ByRef URL 与版本，网络或 XML 失败不会沿用旧结果。
- 后台或最小化时记录“已排队/抑制”的当前版本，避免每秒重入弹窗逻辑；新版本变化会重新排期。
- `DescompresorController` 在 worker 重新启动且无当前任务或排队任务时，清除一次性的关闭取消标记。

### 2.9 其他风险处理

- 分块错误自愈时同步清除 `FechaUltimoErrorChunk`。
- `IsIncompleteVolumeError()` 不再把所有 `Cannot open...` 都归为缺卷；必须有缺卷诊断证据并满足分卷环境条件。
- `.rar`/`.r00` 旧式命名在分卷计数与等待判断中已经支持；常规 `.rar` 仍只有在存在 `.r00` 兄弟卷时才视为潜在多卷。
- 修复了此前出现的重复 XML `<summary>` 警告，当前 Debug/Release 构建干净。

## 3. 明确有意未继续改动或保留的边界

- `StreamingLibraryModule` 对已写入部分 body 后的失败兜底未改；Headers 已发送时无法安全回滚已发 body。
- Web/Streaming 的同端口冲突没有主动回滚已启动的第一项；重复端口仍应由配置避免。
- 恢复计时器与正常启动的竞态没有进一步重构；在当前代码中 `CompleteStartup()` 有 `_startupReady` 守卫而非原子完成状态。
- 验证失败后的自愈重试未额外分类；现有优先级保留已有的错误分类。

## 4. 当前改动文件

- `Clases/DescompresorController.vb`
- `Clases/DownloadEstadoFilter.vb`
- `Clases/Fichero.vb`
- `Clases/FileDownloader.vb`
- `Clases/IDescarga.vb`
- `Clases/Paquete.vb`
- `Clases/PathGuard.vb`
- `Clases/URLExtractor.vb`
- `Clases/Updater.vb`
- `Forms/Main.Startup.vb`
- `Forms/Main.vb`
- `HttpModule/StreamingModule.vb`
- 11 个 `Resources/Language/*-Language.xml`
- `Tests/RegressionTests.cs`
- `app.config`
- `AUDIT-REVIEW-2026-11-14.md`（本报告）

## 5. 最终建议

当前代码可以作为下一阶段的本地验收基线，但不等于已正式发布：请在修复后基线之上做一次真实环境烟测（实际下载、真实多卷归档、7z 解压、安装包）再决定是否提交、推送和发布。
