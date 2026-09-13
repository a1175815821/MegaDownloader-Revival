# 变更日志

本项目所有重要变更均记录于此。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

*面向用户的版本亮点摘要见 [GitHub Releases](../../releases)。*

---

## 版本亮点速览

| 版本 | 日期 | 主题 |
| --- | --- | --- |
| 2.5 RC2 | 2026-09-13 | 生产审计三批修复：发布阻塞 / 可靠性 / 体验与纵深 |
| 2.5 RC1 | 2026-09-12 | MEGA 配额熔断 + 倒计时横幅；失败自愈默认开启 |
| 2.4.7 | 2026-09-12 | 更新提醒三选项；移除废弃搜索引擎集成 |
| 2.4.6 | 2026-09-04 | 假成功 / 静默失败专项 |
| 2.4.5 | 2026-09-02 | 全面代码审查后的系统性修复（16 项） |
| 2.4.4 | 2026-09-01 | 子文件夹链接下载；MetaMAC 分块初值修复；9 项安全加固 |
| 2.4.3 | 2026-08-26 | 7z 解压；Web 局域网推送；剪贴板漏检修复 |
| 2.4.2 | 2026-08-19 | 修复下载文件真实损坏（MetaMAC / 续传对齐） |
| 2.4.1 | 2026-08-15 | 修复下载完成但显示错误 |
| 2.4.0 | 2026-08-14 | 21 项 bug 修复（安全 / 泄漏 / 并发 / 死代码） |
| 2.3.0 | 2026-08-13 | 加密失败崩溃、资源泄漏、移除 `Thread.Abort` |
| 2.2.x | 2026-07~08 | 路径安全、下载完整性、原子配置保存、Web CSRF |
| 2.1.0 | 2026-07-19 | 深色主题可用性修复 |
| 2.0.0 | 2026-07-13 | 4 阶段 60+ 项修复；深/浅色主题；代码清理 |
| 1.9.x | 2026-07-05 | 修复新版 MEGA 链接格式识别 |
| 1.8.0 | 原版 | 反编译源，复活计划的起点 |

---

## v2.4 系列详细变更

下面是 v2.4 各版本的变更明细（同一版本在上方版本历史中也有条目）。

### 变更明细

#### 更新提醒与搜索引擎清理(v2.4.7)

| 变更                   | 说明                                                                                          |
| -------------------- | ------------------------------------------------------------------------------------------- |
| 更新提醒三选项             | 是=立即更新;否=3 小时后再提醒;取消=不再提醒当前版本(`UpdateSkipVersion` 按版本记录,新版本发布后自动恢复提醒) |
| 移除搜索引擎集成           | "寻找"菜单 4 个域名(megafiles.me/megafindr/megasearch.co 等)已全部下线;`mega://mega-search?` 链接解析同步移除 |

#### 假成功/静默失败修复(v2.4.6)

| 修复                     | 说明                                                                                             |
| ---------------------- | ---------------------------------------------------------------------------------------------- |
| 重启后假成功(P1)          | `Verificando`/`Descomprimiendo` 被标 `Completado`,但两者都可能一字节未落盘;统一回 `EnCola` 靠断点续传继续          |
| 设置保存假成功           | `GuardarXML` 失败只记底层日志,UI 照弹成功;现检查 `ErrorConfig` 失败则弹错停留                                  |
| 大写链接静默丢弃          | 匹配 IgnoreCase 但校验大小写敏感,`HTTPS://MEGA.NZ/...` 无反应;6 处正则 + 前缀比较全部补齐大小写不敏感              |
| 畸形 enc 崩溃            | base64url 长度 %4==1 抛 `ArgumentOutOfRangeException` 裸崩;提前判定弹友好错误                                    |
| 文件夹 API 空响应 NRE     | 畸形响应(空串/代理 HTML)抛 NRE;加 Try/Catch + 空检查,统一报"无效服务器响应"                                    |
| ELC 子范围保留           | `MegaLink` 增子范围字段,编码时追加 `/folder/子ID` 或 `/file/文件ID` 后缀;旧版解码器忽略后缀=历史行为,新版恢复子范围   |
| 语言缺键                | en-US/zh-CN 各 +5(ELC 成功提示、URL 必填、VLC 路径无效、打开 ELC 菜单、配置保存失败)                             |

#### 稳定性与安全(v2.4.5)

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

#### 新功能与完整性(v2.4.4)

| 功能/修复          | 说明                                                                                                                                                      |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 子文件夹链接下载       | `mega.nz/folder/根ID#密钥/folder/子ID` 仅下载指定子文件夹(路径重定基);`/file/文件ID` 仅下载指定文件——此前一律下载整个根文件夹                                                                  |
| MetaMAC 分块初值修复 | 分块 CBC-MAC 初值由零 IV 改为文件 nonce 复制两份 `[n0,n1,n0,n1]`(对齐 SDK `SymmCipher::ctr_crypt`),修复 8 words key 下载完成后必然误报校验错误                                         |
| MetaMAC 标准校验   | 移除"分块边界前缀匹配"宽容逻辑,与 SDK 一致:读完整个文件后一次性完整比较                                                                                                                |
| 9 项安全加固        | StripNullCharacters 偏移修复;AES 失败返回 Nothing 且持久化点保留旧值;密文新增随机 IV 格式(兼容旧数据);Web 密码 PBKDF2(100k)+随机盐;Streaming 恒定时间密码比较;PSK 非 ASCII 校验;ClientConnected 反射健壮化 |

#### 新功能(v2.4.3)

| 功能        | 说明                                                                          |
| --------- | --------------------------------------------------------------------------- |
| 7z 解压     | 优先系统 7-Zip,未安装时自动释放内置 7zr.exe(公共域);支持密码与 multipart 分卷;解压前 PathGuard 校验防路径逃逸 |
| Web 局域网推送 | 「允许局域网访问」开关(默认关),开启后手机/局域网设备可经浏览器推送下载;强制密码保护;支持自定义绑定 IP(留空=全部网卡)            |
| 剪贴板监控修复   | 浏览器延迟渲染 + 剪贴板占用竞态导致网页复制漏检——改为重试读取,全部访问加异常保护                                 |

#### 下载完整性(v2.4.2)

| 修复          | 说明                                                                     |
| ----------- | ---------------------------------------------------------------------- |
| MetaMAC 算法  | 分块调度对齐 MEGA SDK `ChunkedHash`:128 KiB × i(i=1..8)后固定 1 MiB;空文件返回 (0,0) |
| 下载完成判定      | 移除"文件大小匹配即强制完成";仅真实 chunk 全部完成才判定完成;120 秒超时上报失败并保留断点                   |
| CTR 密钥流错位防护 | 中断 flush 与续传起点强制 16 字节对齐;启动时回退旧版遗留的非对齐进度——杜绝"大小正确但内容损坏"                |

#### 稳定性(v2.4.0/2.4.1)

| 修复       | 说明                                                |
| -------- | ------------------------------------------------- |
| 后台线程弹窗卡死 | 下载失败改经 UI 线程呈现;关闭期间跨线程 MsgBox 加 `IsDisposed` 防护   |
| 并发污染     | Streaming 模块 AJAX 响应改 `AsyncLocal`,多请求互不串扰        |
| 资源泄漏     | Mutex `Try/Finally` 释放;`BackgroundWorker.Dispose` |
| 公开链接误报   | 4 words key 无 MetaMAC 时跳过校验(记日志),不再误判失败           |

---

## [2.5 RC2] - 2026-09-13

生产级破坏性审计后的三批修复(RC1 之后的新改动全在此)。核心主题:**消灭静默失败与高频卡顿**。

### 🐛 RC2 修复:发布阻塞 5 项(Batch-1)

([Main.vb](../Forms/Main.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb) / [Fichero.vb](../Clases/Fichero.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb))

- Web 推送与手动加链同链路:`ControlRemotoAgregarLinks` 先经 `URLProcessor.ProcessURLs` 展开文件夹/ELC(此前文件夹链变成单个坏任务);`pckname` 经 `PathGuard` 净化(此前字符串拼接,已认证任意目录创建+落盘越狱);子目录结构保留
- 文件夹跳过计数:单节点解密失败记账,部分跳过记 Warning(含示例 handle,不记 key 材料),全部失败抛错(此前零文件也报成功)
- 0 字节空文件短路:探活后仍为 0 直接落空文件、验 MetaMAC(期望 `(0,0)` )、走正常重命名与成功事件(此前 `GetDataPart` 抛错 + 自愈空转);重命名冲突逻辑抽为 `RenamePartToReal` 共用
- 验证取消标志:`bgArranque` 不可 `CancelAsync`,加 `_StartupCancelled` 标志,Stop/Dispose 置位后验证完成不再建下载器(此前 Stop 后必复活)
- 坏 ELC per-URL 隔离:单条失败只跳过该条、同条部分结果回滚,全灭时仍抛首错保持单链语义(此前一条坏 ELC 团灭整批)

### 🐛 RC2 修复:可靠性 4 项(Batch-2)

([Fichero.vb](../Clases/Fichero.vb) / [Configuracion.vb](../Clases/Configuracion.vb) / [Main.vb](../Forms/Main.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb))

- 100% 回补 MD5 移出全局锁:锁内只预定(置 `ComprobandoMD5` 防重投),锁外线程池执行(此前 GB 文件撞线冻结 UI/调度)
- Web 密码随机 IV 移出配置去重比对,改明文快照比对(此前有 Web 密码时 `Configuration.xml` 每 5 秒必重写)
- 关闭等待补齐 `CreandoLocal/Verificando/Descomprimiendo/ComprobandoMD5`;验证回写与 `GuardarXML` 同持 `FicheroDownloader`(此前关机撕裂队列)
- 120 秒看门狗判定移到 30 秒排空后,排空期间撞线完成不再误报失败(此前成功/失败双事件竞态可把完好文件钉成错误)

### 🐛 RC2 修复:体验与纵深(Batch-3)

([URLExtractor.vb](../Clases/URLExtractor.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [StreamingModule.vb](../HttpModule/StreamingModule.vb) / [Main.vb](../Forms/Main.vb) / [Updater.vb](../Clases/Updater.vb) / [Criptografia.vb](../Clases/Criptografia.vb) / [SteganoManager.vb](../Stegano/SteganoManager.vb))

- 正则单例化(`Compiled` 共享实例):`URLExtractor` 全表扫描、`MegaFolderHelper` 逐节点、`StreamingModule` 逐请求不再 `New Regex`(此前数百链接粘贴 UI 冻结)
- fragment 解码:`UnescapeDataString` + 去空白,`%23/%3D` 转义链不再永久"无法解密";删 dead `Contains(" ")` 分支
- 更新 URL 仅 https;`version.xml` 禁外部实体(XXE);公开链跳校验改 Warning 留痕;Stegano 远端 64MB+30s 上限;streaming 畸形 mega 参数返 400 不再 500

### 🐛 RC2 修复:合流审查补丁(Pre-merge Gate)

- 左栏任务总览+快捷入口:总速度/计数/队列进度/剩余时间,复用既有 430ms 刷新循环(无新增计时器);解压队列/流媒体库/日志提到首屏
- 流媒体库 Web 导入加按条隔离:坏文件夹/过期 ELC 只跳过该条(此前整请求无响应)
- 配额同事件去重:熔断期内重复上报不升级档位不延长等待(此前多连接并发命中可瞬间跳档到 6h)
- CI 单文件校验改 Windows PowerShell 5.1 执行(此前 `pwsh` 下 ReflectionOnlyLoad 必抛,构建必红)

### 📦 版本号

- Assembly / FileVersion → `2.5.0.0`(RC 不动)

- InternalConfig `VERSION_MEGADOWNLOADER` → `2.5 RC2` / `VERSION_UPDATE` → `2.5`(数字不动,beta/RC 期间不提示更新;`docs/version.xml` 保持 `2.4.7.0`,正式版才抬)

***

## [2.5 RC1] - 2026-09-12

匿名下载 MEGA 配额(HTTP 509 / API -17)专项。核心主题:**配额可预期——自动暂停、诚实倒计时、到点自动恢复**。

### ✨ 新功能:配额全局熔断 + 倒计时横幅

([MegaQuotaManager.vb](../Clases/MegaQuotaManager.vb) 新增 / [Conexion.vb](../Clases/Conexion.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb) / [Main.vb](../Forms/Main.vb))

- 509 与 -17 归一识别:判定只用 `HttpStatusCode = 509`(状态码)与 `-17 / EOVERQUOTA`(API 语义),不做响应体文本匹配。覆盖文件信息(:429 异常路 + :364 数字路)与文件夹读取(:47 异常路 + :51 数字路)两个落点
- 递进熔断:首次命中暂停 60 分钟,配额期内重复命中升级到 2 小时、6 小时封顶;`Retry-After` 有则取大值(大概率没有,不依赖)
- 熔断期间:不再开新任务、不做新文件信息校验(每次校验=一次 API 调用,会延长惩罚窗口)、分块失败不再 16 秒空转重试,由调度器统一等待
- 主界面横幅:`MEGA 配额已用尽(未给出确切恢复时间)。已自动暂停,将在 X 小时 Y 分后自动重试` + **[立即重试]**按钮(换 IP / 代理 / 重启路由后可手动解除,不被锁死)+ 状态栏倒计时 + 进入/解除各一次托盘气泡

### ✨ 改进:失败自愈默认开启(带一次性迁移)

([Configuracion.vb](../Clases/Configuracion.vb)) `ResetearErrores` 缺省改为开(15 分钟);存量配置经 `ResetearErroresMigratedV25` 一次性迁移到开,后续手动选择不再被覆盖;新装直接为开。

### ✨ 改进:永久失败不参与自愈 + 批量操作

([Fichero.vb](../Clases/Fichero.vb) / [Main.vb](../Forms/Main.vb)) `-9 ENOENT / -11 EACCESS / -14 EKEY / -16 EBLOCKED` 标 `EsErrorPermanente`,自愈跳过(不再每 15 分钟诈尸刷日志耗配额);右键新增**重试全部失败**与**移除全部失败**(确认框显示数量,可选同时删除本地 `.part`)。

### ✨ 改进:大文件夹读取进度反馈

([MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb) / [AddLinks.vb](../Forms/AddLinks.vb)) 文件夹解析搬出 UI 线程,进度窗实时显示`正在读取文件夹…已解析 N 项`,支持取消(丢弃结果不加包);在线观看同样走异步解析。

### 🐛 RC1 修复:配额误报 + 重启丢错误 + 进度条发灰(P1)

- 配额熔断期内超时改报 `MegaQuotaExceededException`(此前 120 秒兜底报通用超时,`FailedByQuota=False`,「立即重试」捞不回);非配额 120 秒文案去“链接过期”误导,明确为总时长看门狗(非空闲超时),进度保留断点续传
- `DescripcionError / EsErrorPermanente / FailedByQuota` 持久化到队列 XML(4000 字截断),重启后「查看错误」不再空白,永久失败不再被自愈反复捞起;老队列按描述文本回推标记兼容;空描述弹可操作 fallback 不再空白窗
- 进度条自定义绘制:背景透明露出行底色(浅色整条发灰消除,0% 行不再纯灰);清空渐变走 `FillColor` 实色;边框随主题 `Border`;条高 18;小进度保底 2px
- 下载列表列宽防 corruption:10 列加 `Minimum/MaximumWidth`(# 20–40/文件名 150–700/其余见代码),`ColumnWidthChanging` 拖动时夹取(OLV 不拦截表头拖拽);启动时坏状态(`#`/文件名被压 0、单列 >800、可见总宽 >2000)自动重置落盘;右键新增"恢复默认列宽"(此前 `#` 列 `Hideable=False` + 迁移已跑过,程序内无回到默认入口)

### 🐛 RC1 补丁:横幅布局/配额语义/构建可用性

- 构建可用性:现代 MSBuild 把 resx 编为 preserialized 格式,启动即崩(`My.Resources.icono` 处 `FileLoadException`,被全局异常兜底吞成静默 exit 0)。`Resources\DLLs` 新增 `System.Resources.Extensions/Memory/Buffers/Unsafe/Numerics.Vectors`,vbproj 加引用,`app.config` 加版本重定向,CI 产物清单同步
- 配额横幅改绝对布局:列表+两侧栏 Top/Height 永远由工具栏底+横幅显隐重算,去掉相对位移与 Bottom 锚点(此前 resize/最大化/DPI 变化后隐藏会多出一个横幅高度压住状态栏)
- 配额到期唤醒脱离自愈开关:熔断解除边沿直接调 `WakeQuotaFailedItems`(此前藏在 `If ResetearErrores` 里,关掉自愈的用户横幅消失但失败项永不恢复)
- 惩罚递进修复:档位 24h 衰减,到期/手动清除不再归零(此前递进永远走不到,手动重试还打回 60min 又立刻发请求);`Retry-After` 支持 HTTP-date
- 倒计时文案:120 秒内读秒,以上向下取整(此前末 60 秒卡"1 min",1h59m30s 显示"2 h 0 min");横幅颜色进主题系统(`QuotaBack/QuotaFore`)
- Toast 新建/复用共用四边夹取(此前首弹贴边时一半在屏外);文件夹进度窗进度条去视觉样式跟主题,取消真取消(传 `CancellationToken` 进解析循环)
- 调度循环单点故障:迭代级 Try/Catch + `RunWorkerCompleted` 看门狗 3 秒自救重启(此前一次异常=刷新+横幅+恢复全停摆);熔断期点开始给 Toast 提示(此前点了没反应)

### 🐛 RC1 修复:UI 可感知缺陷(12 项)

- `ThemeManager` 新增 `ListBox / CheckedListBox / ToolTip` 分支(此前深色漏刷,靠各窗体手写补丁);顶层 `MainMenu` 仍为系统菜单列为已知限制
- Toast 复用重算位置防跨屏过期坐标;配额横幅 Top 跟随工具栏+DPI 缩放;工具栏图标/导航行高跟随 DPI;状态栏 RAM/Proc 改自动宽度(Designer 90→150);默认窗宽 880→1024 释放 Nombre 列
- 设置搜索支持 `NumericUpDown / ListBox / DataGridView` 与 ComboBox 候选项,Label 不可聚焦时退化聚焦父容器,无命中蜂鸣反馈;设置 Cancel 改 `Bottom|Right` 锚点;ELC 表格换肤后重绘防首帧旧色
- AddLinks:清空文本同步清 `HiddenLinks`,占位符行数纳入计数;水印灰字深色可见;解压密码 `MaxLength` 6→128(两处);ELC 两 Label 复用已有键翻译;Streaming 红字改主题 `ErrorFore`+无效链接给提示;`btnLanzarVLC.DialogResult` 改 None 防误关窗;Credits 去 `AcceptButton=lblTitle`+空译文裸 key 保护

### 🌐 语言新增

`en-US` / `zh-CN` 新增 `Quota_Banner / Quota_Status / Quota_RetryNow / Quota_Recovered / Quota_Error / Retry all failed / Remove all failed(+confirm/delete part) / Folder_Reading(+Count)`;RC1 补 `es-ES` 同 11 键(其他语言经 en-US 回退)。

### 📦 版本号

- Assembly / FileVersion → `2.5.0.0`(RC 不动)

- InternalConfig `VERSION_MEGADOWNLOADER` → `2.5 RC1` / `VERSION_UPDATE` → `2.5`(数字不动,beta/RC 期间不提示更新;`docs/version.xml` 保持 `2.4.7.0`,正式版才抬)

***

## [2.4.7] - 2026-09-12

更新提醒升级为三选项 + 移除已废弃的搜索引擎集成。核心主题:**把"提醒频率"的选择权交给用户,把死域名扫地出门**。

### ✨ 改进:更新提醒三选项

([Main.vb](../Forms/Main.vb) / [Configuracion.vb](../Clases/Configuracion.vb)) 新版本弹窗由"是/否"升级为三选项:**是**=立即更新;**否**=3 小时后再提醒;**取消**=不再提醒当前版本。`UpdateSkipVersion` 配置项持久化"不再提醒"的选择(按版本记录,只屏蔽该版本;未来更新的版本发布后自动恢复提醒),选择后立即写盘。设置中的"检查更新"勾选框仍是总开关(取消即完全停止检测)。

### 🧹 维护性清理:移除废弃的搜索引擎集成

原版"选项 → 寻找"菜单聚合的 4 个 MEGA 搜索引擎域名(megafiles.me / megafindr.com / megasearch.co / megasearch.co.nz)已全部下线,整套功能不可用:

- ([URLExtractor.vb](../Clases/URLExtractor.vb)) 删除 `mega://mega-search?...` 链接解析(`MEGASEARCHPREFIX` 常量、正则模式、`CheckFileIDAndFileKey` 的 mega-search 解析分支)
- ([Main.vb](../Forms/Main.vb)) 删除"寻找"菜单构建与 `Buscador_Click`
- ([InternalConfig.xml](../Resources/InternalConfig.xml)) 删除 `SEARCH_LIST`(4 个死域名)与 `MEGA_SEARCH_CURL`
- ([InternalConfiguration.vb](../Clases/InternalConfiguration.vb)) 删除无调用者的 `ObtenerValuesFromInternalConfig`
- 10 个语言文件删除 `Searc&h` 死键

### 🌐 语言新增

`en-US`/`zh-CN`/`zh-TW`/`es-ES` 新增 `Update prompt hint`(三选项弹窗的按钮含义说明,其他语言经 en-US 回退)。

***

## [2.4.6] - 2026-09-04

7 项假成功/静默失败修复 + 1 项维护清理。核心主题:**让失败以失败的样子呈现出来**。

### 🐛 修复:重启后假成功(P1)

([Paquete.vb](../Clases/Paquete.vb)) `MarcarFicherosComoParados` 原来把 `Verificando`/`Descomprimiendo` 状态的文件标成 `Completado`——但 `Verificando` 是下载前的瞬态(重启后 `EstadoAnterior` 已丢失),`Descomprimiendo` 是下载完成但解压未完成,**两者都可能是"一个字节都没落盘"却报成功**。现统一回 `EnCola`:靠断点续传继续,已完整的文件只做快速校验,不会从零重下。

### 🐛 修复:设置保存假成功

([Configuration.vb](../Forms/Configuration.vb)) `Config.GuardarXML` 失败时只在底层记日志+置 `ErrorConfig`,UI 照样弹"保存成功"并关闭窗口。现在保存后检查 `ErrorConfig <> SinErrores` 则弹错并停留,不再显示成功。新增错误文案语言键。

### 🐛 修复:大写链接被静默丢弃

([URLExtractor.vb](../Clases/URLExtractor.vb)) `ExtraerURLs` 用 `IgnoreCase` 匹配到 `HTTPS://MEGA.NZ/...`,随即调用大小写敏感的 `ExtraerFileID` 校验失败直接丢弃——用户粘贴大写链接毫无反应。6 处 `New Regex(pattern)` 全部补齐 `IgnoreCase`;附带把 `#F`/`#N` 模式比较改 `ToUpperInvariant`、`fenc`/`enc`/`mega-search` 前缀匹配全部改 `OrdinalIgnoreCase`。捕获组保留原始大小写,FileID/FileKey 的区分大小写不受影响。

### 🐛 修复:畸形 enc 链接崩溃

([URLExtractor.vb](../Clases/URLExtractor.vb) / [ServerEncoderLinkHelper.vb](../Clases/ServerEncoderLinkHelper.vb)) base64url 长度 `%4==1` 是非法值,原 `"==".Substring(3)` 抛 `ArgumentOutOfRangeException` 裸崩。现提前判定抛友好错误,统一弹"链接无效"类提示。

### 🐛 修复:文件夹 API 空响应 NRE

([MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb)) `DeserializeObject` 无 Try、`FileList.f` 直接遍历——畸形响应(空串/代理 HTML)抛 NRE。现加 Try/Catch + 空检查,统一抛"无效服务器响应",记日志不泄露 HTML 内容。

### ✨ 改进:子文件夹链接转 ELC 不再丢范围

([ServerEncoderLinkHelper.vb](../Clases/ServerEncoderLinkHelper.vb)) `MegaLink` 增加 `SubFolderID`/`SubFileID`;`ServerEncode` 在 MegaFolder 时追加 `/folder/子ID` 或 `/file/文件ID` 后缀。旧版解码器忽略后缀(退化为整文件夹=历史行为),新版解码器恢复子范围,**双向兼容**。`ExtraerSubFolderID`/`ExtraerSubFileID` 补旧式 token(`mega://#F!...!/folder/...`,ELC 解码产物)后缀回退解析。`enc`/`enc2` 密文无处存子范围,`EncodeLinksForm` 对含子范围的链接跳过编码保留原文,避免静默扩大为整文件夹。

### 🌐 语言缺键补齐

`en-US`/`zh-CN` 各 +5:`ELC created successfully`、`URL is mandatory`、`VLC path is not valid`、`Open &ELC`、`Configuration could not be saved...`。此前缺键经 `Language.GetText` 的 en-US 回退显示英文原文,非崩溃,但中文界面漏翻。

### 🧹 维护性清理

- 删除 `docs/BUGFIX-CHECKLIST.md`(2026-07-13 的审查清单已严重过期,至少 8 处 ⬜ 实际已修)
- 删除 `Resources\DLLs\xunit.dll`(vbproj 无引用,Fadd.dll 的 xunit 1.0.3 依赖本就无法解析,磁盘上的 1.9.1 版本从未被使用)
- vbproj 删除死引用 `TODO\TODO.txt`
- README:Web 界面描述更正为"默认仅绑 127.0.0.1,可开局域网访问并指定绑定 IP"(与实现一致);移除 xUnit 相关条目

***

## [2.4.5] - 2026-09-02

本版本为全面代码审查后的系统性修复:36 项确认问题全部处理,涵盖崩溃修复、功能正确性、HTTP 协议合规、资源泄漏与安全加固。

### 🛡️ 全局异常兜底

([ApplicationEvents.vb](../ApplicationEvents.vb)) 此前整个应用没有任何未处理异常兜底——任何 UI 线程异常直接弹 .NET 崩溃对话框并终止进程,后台线程异常更是让进程无声消失。现在:

- `My.UnhandledException`:异常写入日志后**不退出**,用户有机会保存状态

- `AppDomain.UnhandledException`:后台线程异常至少留下日志线索

### 🐛 修复:列表刷新闪退(高频崩溃源)

([Main.vb](../Forms/Main.vb)) 下载状态/百分比/预估时间/进度文本 4 个 `AspectGetter` 的 Catch 块会弹英文堆栈再 `Throw`——而 AspectGetter 在**每一行重绘时执行**,一行弹一次窗,关掉后崩溃。改为只记日志并返回安全占位值。

### 🐛 修复:其余用户可见错误

| 症状                                    | 根因与修复                                                       |
| ------------------------------------- | ----------------------------------------------------------- |
| 打开 ELC 点"取消"报 "The path is not valid" | 取消时仍以空串调用 `AddDLC`;现在静默退出                                   |
| 单文件点 Reset 很快又变回 Error                | Fichero 分支漏调 `ResetearDescarga()`,残留 `.part` 与错误状态;补齐与包分支一致 |
| 首次运行强制配置可被"取消"绕过                      | 密码框被填占位符 `*****` 恒非空,校验永不可达;占位符现视为"未设置"                     |
| Web 超时保存 61-99 重开变空再被改 5              | 载入边界 `>60` 与保存边界 `0-99` 不一致;统一为 0-99                        |
| 队列文件损坏导致启动即崩                          | `CDate(strFecha)` 区域性相关且无 Try;改 `Date.TryParse` 双文化解析,坏值跳过  |
| 后台弹窗藏到主窗体后面像卡死                        | DoWork 线程直接 `MessageBox.Show`;改走 `SafeShowError` 编组回 UI     |
| 3 个后台 worker 报错弹整屏英文堆栈                | 堆栈已入日志,用户只见 `ex.Message`                                    |
| VLC 启动失败无任何反馈                         | 返回值被忽略且无 Try;两处调用方现检查返回值,`WatchOnline` 内部兜底                 |
| 拖入非 ELC/DLC 文件被静默丢弃                   | 现在给出提示                                                      |
| 未选中条目点"打开目录"报空路径错误                    | 空路径直接退出                                                     |

### 🐛 修复:功能正确性缺陷

| 位置                                                               | 修复                                                                                                                                                      |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [DescompresorController.vb](../Clases/DescompresorController.vb) | **缺分卷 RAR 假成功**:`IsComplete=False` 时静默跳过且不置异常,上游报"解压成功";现显式抛错。**重复 Code 卡"正在解压"**:静默跳过仍返回 True;现更新既有队列条目                                                |
| [FileDownloader.vb](../Clases/FileDownloader.vb)                 | **UI 冻结最长 16.5 秒**:分块失败退避的 `Thread.Sleep` 忙等跑在 UI 线程;移到线程池。**"Must specify size" 掩盖真实错误**:Size=0 时跳过收尾块。**Dispose 从不释放 Mutex/MutexFile/trigger**:现确定性释放 |
| [Configuracion.vb](../Clases/Configuracion.vb)                   | Web 密码解密失败的空 Catch:保留密文当密码导致登录永远失败且无线索;现记日志并清空                                                                                                          |
| [Conexion.vb](../Clases/Conexion.vb)                             | FileID 未转义直接拼 JSON/URL;补 JSON 转义 + UrlEncode                                                                                                            |
| [Main.vb](../Forms/Main.vb)                                      | 删除"包+子文件"时 `CancellationComplete` 双挂导致 `Dispose` 执行两遍;已被移除的对象直接跳过                                                                                       |

### 🌐 HTTP 模块:Range 合规 + CSRF + 限速

- **RFC 7233**([StreamingModule.vb](../HttpModule/StreamingModule.vb)):支持 `bytes=-N` 后缀区间与 `bytes=N-` 开放区间;越界返回 416 + `bytes */size`(此前静默钳位);`bytes=0-0` 单字节探测只取一个对齐块(此前向 MEGA 请求整个文件,带宽放大);尾块截断到 Content-Length(响应体不再超发)

- **`?mega=`** **解析**:直接用框架解析的参数值,`?p=密码&mega=...` 顺序不再失败

- **流媒体库 CSRF**([StreamingLibraryModule.vb](../HttpModule/StreamingLibraryModule.vb)):Delete/Save/OpenVLC/ImportLinks/ExportLinks 现要求 POST + 有效 token(复用 Web 界面的 EnsureCsrf 模式);模板注入 token;**顺带修复浏览页 OpenVLC 用 GET 调 POST-only 接口的既有失效**

- **登录限速**([WebInterfaceModule.vb](../HttpModule/WebInterfaceModule.vb)):PBKDF2(100k) 在请求线程同步执行,POST 轰炸可打满线程池;现并发上限 4 + 60 秒窗口失败锁定 10 次

- 3 处 `StreamWriter(response.Body)` 补 `Using` + 无 BOM 编码

### 💾 资源泄漏

- `Criptografia.decrypt_key`:循环内 `CreateDecryptor` 从不 Dispose(每次解密 key 泄漏 N 个 ICryptoTransform);改 Using

- `MD5Utils.MD5CalcString`:补 Using

- `DescompresorController` ×4 处、`ThrottledStreamController` ×1 处 Mutex 补 Try/Finally(符合项目约定)

- Configuration/PropiedadesDescarga 共 5 处悬停 ToolTip 泄漏;复用单实例

### 🔒 Stegano(隐写)

- **先写坏再报错**:容量校验发生在写盘之后,留下截断的 .jpg;改为内存编码+校验全通过才落盘

- **OpenWrite 不清空尾部**:覆盖更长的旧文件时新旧字节拼接;`WriteAllBytes` 截断覆盖

- **Uri 校验形同虚设**:`RelativeOrAbsolute` 对 "hello world" 返回 True;限定 Absolute + http/https/file

### 🧹 维护性清理

- vbproj 删除死引用 `TODO\TODO.txt`、`plantilla botones.psd`;删除孤儿文件 `postbuildevent.xml`

- DPI 配置统一 PerMonitorV2(app.config 补 `DpiAwareness`、myapp HighDpiMode=2);移除未部署的 Unsafe 程序集重定向

- README 删除不存在的 xUnit 技术栈项

- 硬编码英文消息(ELC/DLC 错误、Invalid input data 等 10 处)接入语言系统,新增 en-US/zh-CN 条目

- BUGFIX-CHECKLIST.md 头部加"已过时"警示(至少 8 处 ⬜ 实际已修,防止重复劳动)

***

## [2.4.4] - 2026-09-01

### ✨ 新功能:子文件夹链接下载

**此前**:`mega.nz/folder/<根ID>#<密钥>/folder/<子ID>` 形式的链接会被当作根文件夹链接,下载整个根文件夹的全部内容。

**现在**([URLExtractor.vb](../Clases/URLExtractor.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb) / [StreamingLibraryManager.vb](../Clases/StreamingLibrary/StreamingLibraryManager.vb)):

| 链接形式                               | 行为                         |
| ---------------------------------- | -------------------------- |
| `mega.nz/folder/根ID#密钥/folder/子ID` | 仅下载指定子文件夹的内容(路径以子文件夹为根重定基) |
| `mega.nz/folder/根ID#密钥/file/文件ID`  | 仅下载指定单个文件                  |
| `mega.nz/folder/根ID#密钥`            | 下载整个根文件夹(不变)               |

- 正则扩展捕获 `/folder/<子ID>` 与 `/file/<文件ID>` 后缀

- 文件列表按父节点链向上遍历过滤,只保留属于目标子节点的文件

- 下载路径重定基为相对子文件夹的路径,不再出现多余的上层目录层级

### 🐛 修复:子文件夹链接下载完成后误报 MetaMAC 错误(用户实测确认)

**症状**:子文件夹链接下载到 100% 后弹 MetaMAC 校验失败错误;文件内容实际完整。

**根因**:每个数据分块的 CBC-MAC 计算使用**零初始 IV**。而 MEGA 真实算法(SDK `SymmCipher::ctr_crypt`)的分块 MAC 初值是**文件 nonce 复制两份**——key 第 4-5 字(word)拼接成 16 字节 `[n0, n1, n0, n1]`。零 IV 算出的 MAC 与任何真实上传文件的 MetaMAC 都不可能匹配,因此所有带 8 words key(内嵌 MetaMAC)的下载必然报错;单文件公开链接(4 words key,跳过校验)不受影响,导致问题此前被掩盖。

**修复**([Criptografia.vb](../Clases/Criptografia.vb)):

```vb
' 修复前:零 IV(必然校验失败)
Dim chunkMac As Integer() = New Integer() {0, 0, 0, 0}
' 修复后:nonce(key 第 4-5 word)复制两份,与 SDK 一致
Dim chunkMac As Integer() = New Integer() {nonceWords(0), nonceWords(1), nonceWords(0), nonceWords(1)}
```

其余部分(折叠零初值、`(m0^m1, m2^m3)` 最终压缩、128 KiB × i 分块调度)逐行比对 SDK `macsmac`/`ChunkedHash` 源码确认本就正确,未改动。

### 🔧 MetaMAC 校验对齐 SDK 标准行为

删除验证函数中的"每个分块边界提前检查 MAC、允许前缀匹配"宽容逻辑——SDK 权威实现(`generateMetaMac` + `macsmac`)是**读完整个文件后做一次完整比较**。前缀匹配是算法错误时代的误判产物,现一并移除;任何位置的真实损坏仍然硬失败。

### 🔒 9 项安全加固

| 位置                                             | 修复                                                                   |
| ---------------------------------------------- | -------------------------------------------------------------------- |
| `Criptografia.StripNullCharacters`             | 重写为 `Replace(vbNullChar, "")`,消除逐字符拼接造成的位置偏移错误                       |
| `Criptografia.AES_EncryptString/DecryptString` | 失败返回 `Nothing` 而非空串;持久化点(Configuracion/Fichero)加密失败时跳过写入、保留旧值,不再静默清空 |
| AES 密文格式                                       | 新增随机 IV 格式 `{1}\|\|IV\|\|密文`,与旧格式自动双向兼容                              |
| `FileDownloader`                               | MetaMAC 不匹配抛异常并重置分块重试(配合本次算法修复,不再产生误报)                               |
| `Criptografia.GetFileKeyFromPreSharedKey`      | PSK 含非 ASCII 字符(>255)时记日志返回 `Nothing`,杜绝密钥流错位                        |
| `WebInterfaceModule`                           | Web 密码存储改随机盐 + PBKDF2(100k 轮)派生,替换无盐 MD5                             |
| `StreamingModule` / `StreamingLibraryModule`   | 密码比较改 `Criptografia.FixedTimeEquals` 恒定时间比较,防时序侧信道                   |
| `ClientConnected` 反射                           | 静态 `MemberInfo` 缓存 + null 检查 + Try/Catch,失败降级为"假设已连接"                |

### 📦 版本号

- Assembly / FileVersion → `2.4.4.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.4`

- `docs/version.xml` → `2.4.4.0`

***

## [2.4.3] - 2026-08-26

### ✨ 新功能(Issue #1)

| 功能           | 说明                                                                                                                                                                               |
| ------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 7z 解压支持      | SharpCompress 不支持 7z 容器(此前所有 .7z 解压必然失败)。现优先调用系统已安装的 7-Zip CLI,未安装时自动释放内置的 7zr.exe(公共域,7-Zip 官方精简版)到 `%LOCALAPPDATA%\MegaDownloader\bin` 使用。支持密码与 multipart 分卷(.7z.001/.002/...) |
| Web 服务器局域网推送 | 设置 → Web 服务器新增「允许局域网访问」开关(默认关闭)。默认仍仅绑定 127.0.0.1;开启后绑定所有网卡,手机/局域网设备可通过浏览器访问推送下载。开启强制要求设置服务器密码(≥8 字符),配置保存与服务器启动双重校验                                                              |
| 局域网自定义绑定 IP  | 「允许局域网访问」开启后新增「绑定IP(留空=全部)」输入框:留空监听所有网卡;填入指定 IP(如 `192.168.1.100`)则仅监听该网卡,多网卡/虚拟网卡环境可精确控制暴露面。保存与启动双重校验 IP 格式,非法地址拒绝启动并报错                                                         |

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

***

## [2.4.2] - 2026-08-19

### 🐛 修复:下载文件真实损坏(用户实测确认)

**症状**:下载完成且文件大小精确匹配,但文件内容损坏无法使用。日志证实旧版(含 v2.4.1)在 MetaMAC 校验失败后仍"警告并放行"完成了重命名,损坏文件直接落地。

**根因**(三个独立缺陷叠加):

1. **MetaMAC 分块调度算法错误**:v2.4.1 采用"128K 起步翻倍增长、8 MiB/1 MiB 双封顶"的调度,与 MEGA 官方 SDK `ChunkedHash::chunkfloor/chunkceil` 的真实调度不一致,导致大量合法文件被误判 mismatch(也为下游放行逻辑制造了借口)
2. **mismatch 放行策略**:算法错误的前提下,v2.4.1 把"mismatch 即失败"回退成了"记警告、照常完成重命名"——校验形同虚设,真实损坏(如 URL 过期后 403 期间的空洞写入)被直接放行
3. **非对齐续传导致 CTR 密钥流错位**:连接中断时的 best-effort flush 会把不足 16 字节对齐的进度持久化;重试时 `SeekToFileOffset` 只能按整块定位密钥流,从错位点起**后续所有数据解密错位**——这是"大小正确但内容损坏"的直接成因

**修复**:

| 位置                                        | 修复                                                                                                          |
| ----------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| `Criptografia.ComputeMegaFileMac`         | 分块调度改为 MEGA SDK 线性边界:128 KiB × i(i=1..8,即 128/256/384/512/640/768/896 KiB),之后固定 1 MiB;删除双 cap fallback,单次计算 |
| `Criptografia.VerifyMegaMetaMac`          | 空文件直接返回 (0,0)(MEGA 空文件 MetaMAC 即为 0)                                                                        |
| `FileDownloader.downloadFile`             | MetaMAC 不匹配记错误日志但按 MEGA SDK 宽松策略继续完成(SDK 对历史遗留的"MAC 缺失尾部条目"同样宽松);文件大小精确校验保留为硬门禁                             |
| `FileDownloader.FlushToDisk`              | 中断 flush 时把持久化进度向下对齐到 16 字节边界,杜绝非对齐续传点(<16 字节已解密数据重试时自动重取)                                                  |
| `ChunkDownloader_DoWork`                  | 续传请求前校验起点对齐:非 16 字节对齐的续传起点直接中止 chunk,防止密钥流错位                                                                |
| `DataPart.ValidateAndNormalize`           | 启动时将旧版本遗留的非对齐 XML 进度自动回退到 16 字节边界                                                                           |
| `FileDownloader.downloadFile`(v2.4.1 已引入) | 保留:移除"文件大小匹配即强制完成"的 force-finish;仅真实 chunk 全部完成才判定完成;120 秒超时上报失败并保留断点                                       |

### 📦 版本号

- Assembly / FileVersion → `2.4.2.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.2`

- `docs/version.xml` → `2.4.2.0`

***

## [2.4.1] - 2026-08-15

### 🐛 修复:下载完成但显示错误(用户实测确认)

**症状**:文件下载到 100% 时直接弹错误,`.part` 文件不重命名;手动去掉 `.part` 后缀后文件可正常使用,证明文件实际已完整下载。

**根因**:v2.2.0 引入的 MEGA MetaMAC 完整性校验存在系统性误报:

1. **单文件公开链接必然误报**:公开链接的 FileKey 仅 16 字节(4 words,只有 AES 密钥本身),**不含 MetaMAC**;而 `VerifyMegaMetaMac` 要求至少 8 words(32 字节,含 nonce + MetaMAC),不满足直接返回 False → 完成路径抛 "Integrity check failed" → 状态错误、不重命名。只有文件夹 API 返回的 32 字节 node key 才真正含 MetaMAC,因此误报集中在最常见的单文件链接场景
2. **8 words key 的边界规则差异也可能误报**:MEGA 客户端历史上的 MAC 分块边界规则有版本差异,不匹配不等于文件损坏(大小精确校验是更强证据)

**修复**(两层防护):

| 位置                               | 修复                                                                  |
| -------------------------------- | ------------------------------------------------------------------- |
| `Criptografia.VerifyMegaMetaMac` | 4 words 公开链接 key 记日志说明"无 MetaMAC 可验证"并跳过(返回 True),不再误判失败            |
| `FileDownloader.downloadFile`    | 8 words key 的 MetaMAC 不匹配降级为日志警告并继续完成重命名;文件大小精确校验(不匹配仍报错)保留为主要完整性防线 |

### 📦 版本号

- Assembly / FileVersion → `2.4.1.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.1`

- `docs/version.xml` → `2.4.1.0`

***

## [2.4.0] - 2026-08-14

### 🐛 全面 Bug 修复 - 21 项确认存在的问题

基于对 v2.3.0 全项目代码的逐文件核查，修复 21 项经确切代码证据确认的 bug，涵盖用户可感知报错、死锁/资源泄漏、并发缺陷、异常吞噬、安全债务与死代码。

### 一、用户层面可感知的报错

| 修复                    | 说明                                                                                                                                            |
| --------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| 后台线程弹 MessageBox 卡死下载 | `FileDownloader.bgwDownloader_DoWork` 的 Catch 块不再在线程池线程上调用 `MessageBox.Show`，改为通过 `ReportProgress(FileDownloadFailedRaiser)` 上报失败，由 UI 线程统一呈现 |
| 关闭期间跨线程 MsgBox 崩溃     | `Main.vb` 三处 `BackgroundWorker.DoWork` 异常分支不再直接 `MsgBox`，新增 `SafeShowError` 辅助方法，检查 `IsDisposed`/`IsHandleCreated` 并通过 `Invoke` 切回 UI 线程      |
| 7z multipart 解压崩溃     | `DescompresorController` 两处 `NotImplementedException` 改为 `NotSupportedException` 带友好消息；`DescompresionFinalizada` 事件扩展传递错误消息，用户可在错误状态中看到具体原因   |
| 兜底完成漏做 MD5 校验/解压      | `Fichero.ActualizarDatosDescarga` 的兜底状态修正不再仅翻转状态，改为调用完整 `downloader_Completed` 流程（含 MD5 校验和自动解压），避免"显示完成但未校验完整性"                              |

### 二、死锁与资源泄漏

| 修复                     | 说明                                                                                        |
| ---------------------- | ----------------------------------------------------------------------------------------- |
| Mutex 无 Try/Finally 死锁 | `Main.AgregarPaquete` 和 `bgwComprobarMaxConexiones` 两处 Mutex 加 `Try/Finally`，中间异常不再导致永久死锁 |
| 下载 worker 未 Dispose    | `FileDownloader.Dispose` 中循环释放 `listDownloaders` 里的所有 worker，不再只 `CancelAsync`            |
| bgArranque worker 泄漏   | `Fichero.Dispose` 新增释放 `bgArranque`（下载启动 worker），关闭期间启动阶段中断不再泄漏                           |
| ELCForm 300ms 忙轮询      | 改为 `AutoResetEvent` 事件驱动，无任务时零 CPU 消耗，有任务时立即响应                                            |

### 三、并发与逻辑缺陷

| 修复                   | 说明                                                                                  |
| -------------------- | ----------------------------------------------------------------------------------- |
| AJAX 响应并发污染          | `StreamingLibraryModule._RespuestaAjax` 改为 `AsyncLocal(Of String)`，并发 HTTP 请求互不覆盖响应 |
| FlushFinalBlock 异常吞噬 | `ServerEncoderLinkHelper.Cipher` 解密路径的空 Catch 改为 `Log.WriteError`                   |

### 四、异常吞噬（掩盖真实故障）

| 修复                 | 说明                                                                                        |
| ------------------ | ----------------------------------------------------------------------------------------- |
| FlushToDisk 磁盘错误被吞 | `FileDownloader.ChunkDownloader_DoWork` 中 `FlushToDisk` 的空 Catch 改为日志                     |
| 服务器错误响应读取失败被吞      | `Fichero.downloader_FileDownloadFailed` / `downloader_ChunkDownloadFailed` 两处空 Catch 改为日志 |
| 解压取消异常被吞           | `Main` 关闭流程中 `RequestCancel` 的空 Catch 改为日志                                                |

### 五、安全与技术债务

| 修复                        | 说明                                                                          |
| ------------------------- | --------------------------------------------------------------------------- |
| DPAPI entropy 硬编码         | `Criptografia` 的 DPAPI entropy 改为从程序集标识 SHA256 派生，保留 legacy entropy 解密旧数据   |
| ZIP 密码硬编码 "passZIP"       | `Fichero` 的 ZIP 解压密码加密改用 DPAPI，解密先 DPAPI 后回退旧 AES 兼容旧队列文件                   |
| OptionalPassword 死字段      | 删除 `Cache.OptionalPassword` 字段及 XML 写出（声明后从未赋值、从不读取）                        |
| RandomNumberGenerator 未释放 | `ServerEncoderLinkHelper` 的 `RandomNumberGenerator.Create()` 用 `Using` 包裹释放 |
| 日志无 UTC 时间戳               | `Log` 全部时间戳改用 `DateTime.UtcNow`（加 `Z` 后缀），新增 30 天日志保留清理策略                   |

### 六、死代码清理

| 修复                 | 说明                                     |
| ------------------ | -------------------------------------- |
| Criptografia 注释死代码 | 删除注释掉的 `DecryptFile` 和 `cipherData` 函数 |
| Conexion 死代码       | 删除注释的 `GetAppID` 和无调用的 `LeerNodo` 函数   |

### 📦 版本号

- Assembly / FileVersion → `2.4.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4`

- `docs/version.xml` → `2.4.0.0`

***

## [2.3.0] - 2026-08-13

### 🐛 稳定性修复

基于代码审查，修复一批崩溃、资源泄漏与潜在死锁问题。

| 修复                | 说明                                                                                                                             |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| AES 加密失败崩溃        | `AES_EncryptString` 加密异常后仍对 `Nothing` 做 Base64 转换导致二次抛异常；改为失败返回空串并用 `Using` 释放 `RijndaelManaged`/`CryptoStream`/`MemoryStream` |
| AES 解密截断/坏输入崩溃    | `AES_DecryptString` 的 `Convert.FromBase64String` 移入异常处理；用 `CopyTo` 完整读取明文（原单次 `Read` 可能截断）；失败返回空串                              |
| 下载项资源泄漏           | `Fichero.Dispose` 由空实现改为释放 `FileDownloader` 并置空                                                                                |
| 潜在死锁              | `FileInfo.Size` setter 的 `ReleaseMutex` 放入 `Try/Finally`，循环内异常不再导致永久死锁                                                         |
| 注册表句柄泄漏           | `RegisterInStartup` 的注册表键用 `Try/Finally` + `Close()` 释放                                                                        |
| 危险 `Thread.Abort` | DLC 处理 30 秒超时不再硬杀线程，改为协作式标记失败并让 worker 自然结束                                                                                    |

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

***

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

***

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

***

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

***

## [1.9.1] - 2026-07-05

### 🐛 修复

- **下载器崩溃**:修复 [`Clases/FileDownloader.vb`](../Clases/FileDownloader.vb) 第 681-683 行变量名错配导致的 `NullReferenceException`。当 MEGA 服务器返回 502 网关错误等异常时,catch 块误引用已被清空的 `exc` 局部变量(应为 `ex`),导致掩盖真实异常并中断整个下载流程。

***

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

***

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

***

## 版本号说明

- 主版本号:重大功能变更或不向下兼容的修改

- 次版本号:新增功能,向下兼容

- 修订号:Bug 修复,向下兼容
