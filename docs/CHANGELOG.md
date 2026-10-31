# 变更日志

本项目所有重要变更均记录于此。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

*面向用户的版本亮点摘要见 [GitHub Releases](../../../releases)。*

---

## 版本亮点速览

### 最新版本：2.6.1（2026-10-31）

- 侧栏工作台视觉整理：统一中文字体、留白、线形图标与深浅色层次，进度条和百分比合并，详情按宽度调整。
- 修复首次打开菜单只显示“文件”、设置首次打开长时间卡顿、账号页生命周期与搜索滚动问题。
- “停止”恢复文字，定时下载增加时钟图标，设置按钮保持左对齐。
- 新增语言“跟随系统”：匹配 Windows 显示语言与地区变体，区分简繁中文，未支持的语言回退英文；保留已有手动选择。
- 同步五种语言 README、原生界面截图与发布说明。

### 2.6.0（2026-10-29）

- 工作台重做：浅色 / 深色统一配色、字体与间距，侧栏分组独立计数，主列表新增标题与搜索，详情栏可收起。
- 添加链接合并为同一窗口的两步流程；批量预检支持文件夹层级、多选、重复与失效识别。
- 链接提取与建包移到后台，超过 500 个文件的任务包默认折叠；添加后自动定位新任务。
- 新增任务搜索与定时下载；冷启动从 5–7 秒降到 150–200 毫秒，不再同步读取性能计数器。
- 10 个界面语言包同步新增文案。

| 版本 | 日期 | 主题 |
| --- | --- | --- |
| 2.6.1 | 2026-10-31 | 视觉层次整理；菜单与设置首开修复；语言跟随系统与英文回退 |
| 2.6.0 | 2026-10-29 | 工作台重做；两步添加与批量预检；任务搜索；定时下载 |
| 2.5.9 | 2026-10-28 | 同名并发下载互不干扰；解压保留原文件；已下完成品校验后复用；Range 字节错误、盘符根路径与空目录修复 |
| 2.5.8 | 2026-10-22 | 慢速下载不再被误判停滞；解压队列崩溃自愈；RAR 分卷与密码修复；深色菜单栏换肤；改配置静默丢失根治 |
| 2.5.7 | 2026-10-14 | 稳定性专项(36 项审计修复)；共享文件夹网络错误友好提示(issue #6)；文件夹分享包用真名 |
| 2.5.6 | 2026-10-06 | 配额突破模式回归；散装建目录；真名回写；删盘收空目录 |
| 2.5.5 | 2026-10-04 | 文件夹包命名修复(issue #4)；文件名转义解析；Dispose 清理 |
| 2.5.4 | 2026-10-01 | 流媒体死循环根治；DPAPI 熵稳定不再随升级丢数据；9 语言补译 |
| 2.5.3 | 2026-09-18 | 补丁版：下完误报失败、终结共享冲突、进度条叠字三连修 |
| 2.5.2 | 2026-09-17 | 回归风险治理三批落地：依赖升级、落盘节流、锁替换、口令加固 |
| 2.5.1 | 2026-09-14 | 补丁版：断点续传保得住、限速单位修正、两批审计跟进修复 |
| 2.5.0 | 2026-09-14 | 正式版：在线观看并发修复 + 版本号转正进更新通道 + 4 语言文档补齐 |
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

## [2.6.1] - 2026-10-31

在 2.6 工作台基础上整理视觉层次，修复首次显示与设置卡顿，并新增系统语言识别。

### 🖥️ 界面与布局

- 主窗口采用中性分层配色、统一中文字体、内容边距和列表行高；添加链接保留主操作强调，开始、暂停、停止和定时命令使用统一线形图标。
- 任务区集中展示标题、计数、工具栏和搜索；侧栏底部显示总速度与队列进度。空队列与筛选无结果使用不同提示。
- 默认显示名称、大小、状态、进度、速度、预计时间六列；百分比并入细进度条，状态颜色只用于状态文字与进度，不再整行染色。
- 名称恢复为文件树首列，隐藏的优先级列继续提供队列排序；列菜单保留可选列。
- 详情在选中任务后按需显示：宽窗口放右侧，较窄窗口放底部，布局不覆盖列表。
- 添加链接、预检、定时与设置统一字体、间距和按钮；设置改为可缩放、可滚动表单，连接与代理独立到“连接”页。
- “停止”保留两字文字，包括紧凑窗口；定时下载增加时钟图标；侧栏设置按钮及图标文字保持左对齐。
- 修正 DPI 配置与几何缩放基准；支持的系统采用原生深色标题栏接口。

### 🧩 首次显示与设置修复

- 修复主菜单初次打开只有“文件”：显式按客户区宽度布局，四个菜单无需调整窗口就能显示；重建菜单时释放旧实例。
- 减少设置页嵌套自动布局的重复尺寸计算；ELC 账号页首次进入时才初始化。隔离采样的设置首开从约 8.2 秒降至约 1.1 秒，设备与环境会影响实际耗时。
- 未打开账号页时保存继续保留原账号集合；关闭清理支持重复调用，空账号列表绘制不再释放调用者的画布。
- 设置搜索定位匹配页并滚动到对应区域；打开设置保留不可用的下载路径，避免同步探测网络路径造成阻塞。
- 修复旧 Resize 事件可能将隐藏窗口意外显示并触发业务加载的问题；启动与托盘恢复走显式显示入口。
- 进度单元格重绘先清理背景；恢复默认列时刷新缓存及原生配色，避免旧文字残留。

### 🌍 跟随系统语言

- 设置 → 一般 → 语言新增“跟随系统”，读取 Windows 当前用户的显示语言，与日期、数字格式分开。
- 新配置及缺失／无效语言选项默认自动识别；已有有效手动选择保持优先。
- 先匹配完整语言代码，再匹配同语言及同文字体系的地区版本；例如新加坡中文使用简体，香港／澳门中文使用繁体。
- 没有对应语言包时使用英文；自定义语言包可参与匹配，缺少条目时先回退到同语言内置翻译，再使用英文。损坏文件不阻止内置语言加载。
- 自动偏好以 `auto` 保存，下次启动重新识别；更改选项后保存并重启生效，不在运行中即时切换。
- 详见 [系统语言说明](SYSTEM-LANGUAGE.zh-CN.md)。

### 📖 文档与验证

- 五种语言 README 同步 2.6.1 亮点与下载入口；替换主窗口深浅色、添加／预检、设置与定时的原生截图。
- Debug / Release 构建通过；Release 完整回归通过 509 项断言，覆盖语言、布局、菜单首开、设置加载以及已有下载、解压、预检与调度场景。
- 截图使用隔离原生窗口与示例任务，验证不读取真实账号或下载队列；真实 MEGA 下载及多显示器高 DPI 场景仍以实机体验为准。

### 📦 升级说明

- 单文件版替换 EXE；ZIP 版解压并替换程序文件。设置及队列仍位于 `%LOCALAPPDATA%\MegaDownloader\`。
- 首次升级迁移到新的六列布局；此后保留列菜单中的用户调整，其他列可从列菜单恢复。
- 程序显示版本 `2.6.1`，程序集、文件、项目和更新清单版本同步为 `2.6.1.0`。

## [2.6.0] - 2026-10-29

工作台版本，一句话：**界面整体重做，添加链接从三层叠加的窗口收进一个两步流程，下载前可以先看清文件名和大小再勾选，队列能搜索，下载能定时**。

v2.5 之后改动最大的一次，新增工作台布局、两步添加、批量预检、定时下载四套模块；启动路径同时重做，冷启动实测从 5–7 秒降到 150–200 毫秒。Debug / Release 均通过编译，静态审查覆盖全部改动，实际窗口与鼠标键盘表现仍以运行实测为准。

### 🖥️ 工作台重做

([Main.Workbench.vb](../Forms/Main.Workbench.vb) / [Main.DetailPanel.vb](../Forms/Main.DetailPanel.vb) / [UiStyles.vb](../Clases/UiStyles.vb) / [ThemeManager.vb](../Clases/ThemeManager.vb))

- 浅色 / 深色统一配色、字体与间距，「添加链接」提升为主操作，工具栏新增定时入口
- 侧栏分组使用独立计数与选中标记；主列表增加标题、任务包数量与空结果提示
- 详情栏可收起，窄窗口自动让出列表空间；详情按字段分段，长名称 / 路径支持提示与滚动
- 原生列表背景、文字背景与空态提示层完整换肤；列布局恢复、窗口句柄重建后重新同步颜色，清理旧配色的虚拟行缓存
- 工具栏图标统一画布、线宽与视觉重心，设置图标改为对称齿轮；高分辨率绘制后缩小，改善缩放下边缘粗细不均
- 带文字的图标随 DPI 上提一个逻辑像素，纯图标按钮保持居中；窄 / 宽布局切换时同步更新，按钮留白随 DPI 调整
- 带格式粘贴只保留纯文本（不再带入网页字色）；下拉菜单 / 嵌入控件递归换肤；补齐链接按下与禁用配色；密码说明与空间不足警告保留语义配色

### ➕ 两步添加流程

([AddLinks.vb](../Forms/AddLinks.vb) / [AddLinks.Workflow.vb](../Forms/AddLinks.Workflow.vb) / [AddLinks.Preflight.vb](../Forms/AddLinks.Preflight.vb) / [AddLinks.Layout.vb](../Forms/AddLinks.Layout.vb))

- 添加流程合并为同一窗口内的「输入链接 → 选择文件」两步，不再叠加预检弹窗
- 返回保留链接、设置、搜索、目录展开与勾选，不需要重新输入
- 窗口可调整大小，按所在屏幕工作区限制尺寸，内容可滚动、底部按钮独立保留；构造时完成布局并统一缩放基准，跨 DPI 切换时调整边界与输入区
- 下载位置改变时单独更新空间检查；链接未变时复用已检查的元数据与预检页面

### 🗂️ 批量预检

([BatchLinkPreflight.vb](../Clases/BatchLinkPreflight.vb) / [BatchPreflightView.vb](../Forms/BatchPreflightView.vb) / [PreflightSession.vb](../Clases/PreflightSession.vb) / [PreflightTree.vb](../Clases/PreflightTree.vb))

- 添加前展示文件名、大小、状态与相对路径；支持名称搜索、类型筛选、勾选与全选当前结果，并显示选中大小与目标磁盘剩余空间
- 按共享文件夹及子目录显示可折叠层级，默认收起；支持整文件夹勾选、部分选中、全部展开 / 折叠与左右方向键
- 搜索与类型筛选自动展示匹配层级；文件夹批量操作只作用于筛选匹配项，折叠不会改变已选文件
- Ctrl / Shift 多选行，空格或右键菜单批量勾选 / 取消 / 反选，Ctrl+I 反选当前结果；重复和失效项始终跳过
- 数据改为虚拟列表、筛选防抖与增量选中统计，不再逐行创建控件、也不在每次勾选时扫描全表
- 按文件 ID 与密钥识别新旧格式的重复链接及队列重复；暂时网络故障与永久失效分开处理，无法检查的普通链接可手动勾选添加，取消则不添加任务
- 共享文件夹复用已解密元数据，避免逐文件重复请求
- 解析阶段显示已用时间并支持停止 / 重检；预检状态移到独立区域，显示解析、文件检查与链接处理进度，总量未知时使用滚动进度

### ⚙️ 后台解析与入队

([DownloadPackageBuilder.vb](../Clases/DownloadPackageBuilder.vb) / [URLExtractor.vb](../Clases/URLExtractor.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb))

- 链接提取及批量任务构建移到后台；追加链接避免重复全文解析与逐项字符串拼接，每个目标目录只创建一次
- 超过 500 个文件的任务包默认折叠，避免导入瞬间展开全部文件
- 确认期间收到的新链接先暂存，返回可合并，成功添加后留到下一批，不自动下载
- 解析阶段显示实际处理数量并可取消，取消保留选择；提交阶段明确显示「加入下载队列」并禁止重复提交
- 任务已加入队列后，刷新 / 通知异常不再被误报为提交失败：核对实际队列归属，已入队任务保留并补做后台保存，未入队对象释放
- 关闭添加窗口后自动切到全部任务，并定位最近新增的任务包；文件树更新保留仍可见的高亮行与滚动位置

### 🔎 搜索、分组与刷新

([Main.Workbench.vb](../Forms/Main.Workbench.vb))

- 任务搜索支持包名、文件名与相对路径，Ctrl+F 定位、Esc 清空；后台检索与输入防抖，组合当前状态分组过滤，清空后恢复队列
- 输入变化立即作废旧任务，返回前再比较输入；长文件列表逐项校验版本，旧结果不会回写覆盖当前结果
- 分组计数与总览复用后台快照，移除界面线程的重复全队列扫描；数值未变化时不重写导航项，详情布局只在尺寸变化时重算
- 后台快照记录名称 / 相对路径版本，元数据或状态变化后才重新计算搜索命中；普通进度刷新不重建列表

### ⏰ 定时下载

([DownloadSchedule.vb](../Clases/DownloadSchedule.vb) / [DownloadScheduleForm.vb](../Forms/DownloadScheduleForm.vb) / [Main.Scheduling.vb](../Forms/Main.Scheduling.vb))

- 「选项 → 定时下载」可配置一次性预约或每日下载时段，支持跨午夜、到点暂停、保存后重启沿用；旧配置默认关闭
- 程序须保持运行，不提供系统唤醒或后台服务
- 定时恢复保留单任务手动暂停，整队列手动暂停持续到下一时段；时段外不允许自动、手动或单任务强制开始新的下载
- 配额等待不被定时恢复绕过
- XML 同时写入每日 / 一次性设置并分别恢复，未启用模式的损坏字段不影响当前模式

### 🚀 启动与性能

([ProcessResourceMonitor.vb](../Clases/ProcessResourceMonitor.vb) / [ApplicationInstanceManager.vb](../Clases/ApplicationInstanceManager.vb) / [Language.vb](../Clases/Language.vb))

- 移除首屏同步性能计数器与 WMI 调用，窗口显示后由后台轻量采样 CPU、内存；实测冷启动从 5–7 秒降到 150–200 毫秒；实际时间因硬件和运行环境而异
- 完成条款与必要配置后才启动后台任务；静默启动需要处理时停在托盘并提示，重启保留单任务暂停
- 语言包内容不变时不再重复写盘；实例间参数投递使用跨进程互斥
- 日志增加启动阶段与首次绘制计时

### 🧩 修复

- 旧搜索结果回写：输入已改变、旧任务恰好在防抖结束前返回时，会覆盖当前结果
- 搜索未随元数据更新：文件先以链接占位、后台解析出文件名后，仍使用旧命中集合
- 状态分组成员滞后：下载完成或两个文件交换状态但总数不变时，已有节点与展开文件沿用旧成员
- 详情滚动后布局偏移：在详情已向下滚动时调整窗口，绝对坐标混入滚动偏移
- 高 DPI 文件夹展开标记重叠：图标放大但缩进 / 命中区域仍固定 20 像素，深层目录还会画到相邻单元格
- 预检失败误显示完成：已有结果后「重新检查」失败，旧选择仍在，进度被错误设为 100%
- 定时配置丢失另一模式的时间：XML 同时写入两种设置，加载时却只读取当前模式
- 剪贴板焦点判断时机错误：后台解析期间切换窗口，会漏掉外部复制，或把输入框内复制的内容重新追加
- 手动强制重试失败任务时未清除旧的单任务暂停标记
- 在线观看期间同步禁用 / 恢复下一步按钮

### 🌍 多语言与文档

- 10 个界面语言包同步新增文案（工作台、预检、定时、搜索相关）
- 五种语言 README 重写：新增 2.6 亮点、工作台说明与安装指引，统一版本说明与入口；另补 SECURITY、行为准则与 Issue / PR 模板

### 📦 版本号

- Assembly / FileVersion / 项目 ApplicationVersion / `docs/version.xml` → `2.6.0.0`
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.6.0`

---

## [2.5.9] - 2026-10-28

补丁版，一句话：**同名文件同时下载不再互相写坏；解压不再把同名、同大小的原文件直接覆盖；解压中断后重启会校验并复用已下完成品；在线观看的短 Range 请求不再返回错误字节**。

本版针对当前源码逐项复现并修复 8 项缺陷，同时补齐临时文件迁移、清理和后处理状态保护；没有把历史已修复问题重复计入。

### 📥 下载、续传与成品恢复

([FileDownloader.vb](../Clases/FileDownloader.vb) / [Fichero.vb](../Clases/Fichero.vb) / [PathGuard.vb](../Clases/PathGuard.vb))

- **同目录、同名文件同时下载失败**：此前两个任务共用 `文件名.part`，分别报“文件正由另一进程使用”和 `MEGA MetaMAC verification failed`。现在每个任务使用独立临时文件名，写入下载队列并在重启后恢复；旧版 `.part` 有断点进度时迁移到该任务名下。成品重命名统一同步，同名任务及重复链接均能分别交付
- **无扩展名文件下载完却报路径越界**：`README` 重名时，旧逻辑对完整路径找点号，无点号会丢掉基础路径，目录有点号又会被误当扩展名。改为只拆分文件名，正确生成 `README (2)`；目录占用同名位置时也会避让
- **选择盘符根目录，文件却存进程序当前目录**：路径规范化保留 `I:\` 的尾部分隔符，不再变成盘符相对路径 `I:`；UNC 根路径与目录边界校验保持有效
- **`.part` 已丢失，续传仍从 50% 开始，最后必报校验失败**：临时文件不存在时统一重置分块进度，再创建新文件，从头下载，不再把前半段留成空洞
- **解压或校验中断后重启，完整文件又下载一遍并多出 `(2)` 副本**：完整断点对应的成品先做长度与真实 MetaMAC 校验，通过后直接复用，不再发起分块下载。损坏成品不会被当作成功，也不会被覆盖
- 保留已完成文件实际使用的重名后缀，远端文件信息刷新不再将 `same (2).bin` 改回 `same.bin`；迟到的下载成功事件不再覆盖正在解压或校验的状态
- 重置与取消清理跟随任务自己的临时文件，保护其他同名任务和已有成品；队列中非法的临时文件名会被拒绝

### 🗜️ 解压：保留原文件与空目录

([DescompresorController.vb](../Clases/DescompresorController.vb))

- **同名且同字节数的原文件被静默覆盖**：不再以“长度相同”判定为同一压缩包的旧输出；已有文件一律保留到空闲的 `(n)` 后缀，新条目再使用原名。ZIP 与 7z 路径同步修正
- 托管解压关闭直接覆盖；已有文件无法移动时，ZIP 将新内容写到空闲后缀，原文件保持不动。7z 无法保留原文件时明确停止，CLI 同时使用 `-aou` 防止检查之后出现的新重名被覆盖
- **ZIP 空目录丢失，却仍报解压成功**：目录条目现在会经过路径校验并实际创建，多级空目录也能还原；目录条目同样不能越出解压根目录

### ▶️ 在线观看：Range 内容正确性

([StreamingModule.vb](../HttpModule/StreamingModule.vb))

- **短 Range 响应状态和长度都正确，内容却是错的**：例如 `bytes=15-16`，上游先送 2 字节、稍后再送剩余密文时，旧代码会提前补齐长度并解密尚未读入的缓冲区。现在将首块需要裁掉的对齐前缀纳入读取长度，读够密文后再解密
- CTR 尾块只返回实际读到、且落在请求区间内的字节；覆盖单字节探测、未对齐区间、跨多个缓冲区和非完整 AES 尾块

### 🧪 回归验证与文档

- 新增 [Tests/Run-RegressionTests.ps1](../Tests/Run-RegressionTests.ps1) 与 [复现/回归程序](../Tests/RegressionTests.cs)，无需额外测试框架；136 项断言通过
- 覆盖正常下载与续传、旧临时文件迁移、现代任务身份持久化和清理、同名并发及重复链接、成品复用与损坏成品保护、ZIP/7z 原文件保护、空目录及 Range 分段字节对照
- 验证使用合成密钥、临时文件与本机 HTTP 服务，无需真实 MEGA 账号；未将某个播放器的具体提示文字写成已实测结果
- Debug / Release 构建通过；五种语言 README（含两份英文入口）同步版本亮点、测试入口与目录说明；中英文 CONTRIBUTING 同步本地验证和发布步骤

### 📦 版本号

- Assembly / FileVersion / 项目 ApplicationVersion / `docs/version.xml` → `2.5.9.0`
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.5.9`；README 当前版本 → v2.5.9

---

## [2.5.8] - 2026-10-22

补丁版，一句话：**慢速下载不再被"停滞看门狗"误杀；解压 worker 崩了能自愈；RAR 分卷（含旧式 `.rNN` 命名）与带密码的分卷终于解得出；深色模式的菜单栏会换肤了；改完配置静默丢失那条链彻底断根**。

本版合并了 v2.5.7 之后的三批修复：`db35d8f`（已确认缺陷清单）、`1d40225`（审计第二轮）、`662dfaf`（审计第三轮，10 条确认缺陷）。

### ⏱️ 停滞误判与解压队列自愈(P1-1 / P1-2)

([FileDownloader.vb](../Clases/FileDownloader.vb) / [DescompresorController.vb](../Clases/DescompresorController.vb) / [Main.vb](../Forms/Main.vb))

- **慢速下载不再被误杀**：停滞看门狗的判据从"已落盘进度"(`GetTotalProgress()`)改为内存进度 `m_currentFileProgress`（每 ~50 KB 推进一次），落盘进度保留作兜底，两信号取"或"——任一推进即续命，只有同时静止才判真停滞。原判据粒度 = `BufferSize-PackageSize`（默认 700 KiB），总速低于该粒度/120 s 就会周期性变红：**进度条在走，却每约 2 分钟失败一次**
- **解压 worker 不再一崩即永久死亡**：单元素异常隔离在 `ProcesarElemento` 内，收尾改 `Finally` 保证任何退出路径都释放"当前元素"槽位，`elementHandled` 标记避免重复上报 `DescompresionFinalizada`。此前一次异常跳出 `While` 循环带走整个 worker，解压队列报废到重启，且 `Ocupado()` 恒 `True` → 每次关闭都弹 "Files extracting, corruption danger"
- `bgwDescompresor` 增加看门狗：3 秒后自救重启（与 `bgwActualizadorListaDescargas` 同构），关闭期不重启，带防风暴（连续 5 次崩溃即放弃），且不重置 `bgwDescompresorCompleted`（该标志被关闭等待循环读取，清 `False` 会让关闭卡住）

### 🗜️ 压缩包：RAR 分卷与 7z 误判(P2-3 / P3-12)

([DescompresorController.vb](../Clases/DescompresorController.vb))

- **按命名发现兄弟卷**：新增 `DescubrirVolumenesRar`，同时支持新式 `foo.partN.rar` 与旧式 `foo.rar` + `foo.rNN`。此前只认 `part1/part01/part001/part0001.rar`：`.r00/.r01` 旧式命名算出 `LengthPart=0` → 只读第一卷、解压结果不完整；单卷 RAR 只要碰巧叫 `xxx.part1.rar` 又被误判成多卷。现在文件名只用来"找兄弟卷"，不再用来"判定是不是多卷"
- **带密码的分卷 RAR 能解了**：多卷分支此前用 `ReaderOpts(Nothing)` 显式丢掉密码（单卷分支是 `ReaderOpts(Password)`）→ 必然解不出来；现两分支统一带密码。并删掉"多卷 + 密码即抛 `NotSupportedException`"的拦截（硬编码英文，且发生在单卷判定之前，单卷 RAR 文件名带 `part1` 就被拒）
- 打开压缩包时把整组卷交给 `RarArchive.OpenArchive(IReadOnlyList(Of FileInfo))`，`IsComplete` 反映真实完整性；不带密码的多卷 RAR 也能解出后续卷的条目
- 解压失败改为本地化提示并附原始异常，不再直接抛英文
- **7z 容器判定收紧**：改为 `Is7zContainer()`，只认结尾 `.7z` 与 `.7z.NNN`（纯数字分卷）。此前 `Contains(".7z.")` 把 `xxx.7z.zip` / `xxx.7z.rar` 也送去 7zr，退出码非 0 → 原本能解的包反而失败
- 7z 解压 Pass1(`l`) / Pass2(`x`) 统一带 `-sccUTF-8`，非 ASCII（如中文）文件名的覆写判定与路径校验不再失效

### 💾 配置不再静默丢失(P2-9 / P3-13)

([Configuration.vb](../Forms/Configuration.vb) / [Configuracion.vb](../Clases/Configuracion.vb) / [AtomicFile.vb](../Clases/AtomicFile.vb))

- **改完保存，配置全没了**：`ComboBox.SelectedValue` 在当前值无法匹配列表项时返回 `Nothing`（存量 `Configuration.xml` 的 `PrioridadDescompresion="VeryLow"` 即触发），`Enum.IsDefined(type, Nothing)` 抛 `ArgumentNullException`；异常冒出 `btnGuardar_Click`（无整体 Try/Catch），而全局兜底设 `ExitApplication=False` → 进程不崩但 `Config.GuardarXML` 被跳过，**刚改的配置全部丢失且无提示**。双管齐下：三处 `IsDefined`（comboLog / comboPrioridad / comboTheme）先判空再判是否已定义；下拉补入 `VeryLow`（枚举有该成员、`DescompresionThrottle` 有对应实现），从根上消除触发条件
- `AtomicFile.TryLoadXml` 区分 `IOException`（瞬时锁）与 `XmlException`（损坏）：锁失败时 `Configuracion` / `Paquete` 拒绝写入并保留旧文件，不再拿默认值覆盖一份好配置
- 随机 IV / 密码密文排除出 `_LastSavedXML` 去重快照（账号变更改由 SHA-256 指纹 `ELCAccountsHash()` 参与检测），周期保存不再每 5 秒重写一次磁盘；配额突破模式的续传照旧落盘
- `ApplyConfigLimits()` 移到端口 / 超时解析之后（`CargaXML` 里此前两次调用都早于端口解析），并补 `ServidorWebTimeout` 的 0..99 钳制

### 🎨 界面与主题(P2-7 / P2-8 / P2-4 / P3-10 / P3-11)

([Main.vb](../Forms/Main.vb) / [ThemeManager.vb](../Clases/ThemeManager.vb) / [Main.DetailPanel.vb](../Forms/Main.DetailPanel.vb) / [StreamingForm.vb](../Forms/StreamingForm.vb) / [PropiedadesDescarga.vb](../Forms/PropiedadesDescarga.vb))

- **深色模式下顶部菜单栏终于换肤**：经典 `MainMenu` → `MenuStrip`。实测 `MenuItem.OwnerDraw=True` 也给不了经典菜单条上色（条带由 OS 按系统菜单色绘制，仍是 `F0F0F0`），而 `MainMenu` 既不是 `ToolStrip` 也不在 `Controls` 树里，ThemeManager 的四类覆盖全都够不着；`MenuStrip` 走 `ToolStripManager.Renderer` + `ThemeColorTable`，随主题自动换肤
- **左栏 / 右栏总览说明文字可读了**：ThemeManager 新增 `Muted` 主题色。此前误用 `Border`（边框色）当前景，与 `ControlBack` 的对比度浅色 1.38:1 / 深色 1.82:1，远低于正文 4.5:1；现为浅 5.16:1 / 深 4.97:1
- `Options → Generate ELC` 打不开（查重类型 `EncodeLinksForm` → `ELCForm`，全仓 11 处 `IsFormAlreadyOpen` 里唯一错配）；已开着 ELC 再点也不再重复开第二个窗口
- 包属性里**只改解压密码**现在生效：逐文件下发的判定条件由"勾选状态变了"改为"勾选状态变了 或 密码变了"；**取消速度限制**也生效：按载入时快照判定"用户是否动过限速"，取消勾选统一清零，从没碰过则不下发（只改路径 / 密码不再静默清零文件限速）
- Streaming 窗口不再谎报 "URL is mandatory"：空 `Catch` 改为捕获并展示真实原因；解析成功后才推进 `PreviousURLMega`（失败保持原值，可重试）；后台轮询按 5 秒节流，空 URL 不重试
- `AddLinks` 勾选"立即下载"不再绕过配额熔断守卫：守卫下沉到 `StartDownload()`，工具栏 Play 与 AddLinks 行为一致

### 🔧 其他修复

- 隐写向导（[SteganoWizardLoad.vb](../Stegano/SteganoWizardLoad.vb) / [SteganoWizardSave.vb](../Stegano/SteganoWizardSave.vb) / [SteganoManager.vb](../Stegano/SteganoManager.vb)）：路径错误上报修正（此前报的是错的异常信息），移除不安全的 `FileWebRequest` 强制转换
- 版本统计上报永不触发：`Double.TryParse("2.5.7")` 必然失败 → `VersionActual` 恒为 0，与缺省的 `Config.VersionConfig="0"` 比恒假。改用 `System.Version` 比较（复用 `Updater.NormalizeVersionString`，由 `Private` 改为 `Public`）。**至此 `VERSION_UPDATE` 不再受"必须能被 `Double` 解析"的历史限制**。注：`Conexion.PingNewVersion` 目前是 no-op（goo.gl 短链已停用），本次修的是版本比较逻辑与 `VersionConfig` 的记录本身
- `Paquete.MarcarFicherosComoParados` 归一化全部瞬态状态（含 `CreandoLocal` / `ComprobandoMD5`），缺漏的状态不再让队列永久卡住
- 停滞下载的取消不再复活 worker；chunk 失败事件加重复上报守卫（含被吞掉的关闭流异常），`NumErroresChunk` 不再双计
- MetaMAC 不匹配按硬失败处理，给出对齐的提示并重置 chunk 重试；无效 base64url 密钥长度（`len mod 4 == 1`）改为跳过端到端校验，不再让每个完成的下载都判失败
- `mega://` 注册改写 `HKCU\Software\Classes`（无需管理员）；静默模式不弹模态框；失败提示带"不再询问"
- 代理主机名剥掉内嵌端口（`host:port`、`[::1]:8080`），裸 IPv6 补方括号
- Streaming：`bytes=-N` 这类非法 Range 按 200 全量处理，不再发"声称 206 却是坏区间"的响应；错误分支返回 4xx（404 无效 ID / 400 缺参数 / 502 上游 MEGA 错误），不再用 200 带错误体（播放器会当成媒体）
- StreamingForm 的 MEGA 链接解析移到线程池（带 generation 守卫防陈旧结果），不再每 150 ms 阻塞 UI 线程
- ELC 账号保存原子替换引用（竞态修复）；DLC 队列 worker 设 `IsBackground`，`StartProcessDLC` 的 `Invoke` 防已释放句柄
- 关闭期 `Cerrando` 先于 `DoEvents` 泵设置，`RestaurarVentana` / `Resize` 加守卫，托盘不再在关闭过程中把窗口唤醒

### 🌍 多语言

- 已确认缺陷批：es-ES 补 13 键；de / fr / hu / it / pt-BR / ro 各补 45 键（含完整隐写向导）；zh-TW 补 34 键；硬编码的 "Error" 标题改为本地化
- 审计第二轮：10 个语言包补 `Priority_VeryLow`
- 审计第三轮：10 个语言包各补 2 键（"Streaming link could not be resolved" / "Could not extract the RAR archive. Check that all volumes are present and that the password is correct."）
- i18n 审计：384 个字面量键 vs en-US 404 键，缺失 0；10 个语言包 XML 全部合法，保持 CRLF + no-BOM

### 📦 版本号

- Assembly / FileVersion → `2.5.8.0`；`docs/version.xml` → `2.5.8.0`；README 当前版本 → v2.5.8（5 个翻译同步）
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.5.8`

---

## [2.5.7] - 2026-10-14

补丁版，一句话：**稳定性专项（36 项审计修复：崩溃 / 静默丢数据 / 死锁）；共享文件夹读不出来时给人话不给堆栈；文件夹分享的包用 MEGA 上的真名，不再显示"封装 #N"**。

### 🐛 崩溃、静默丢数据与死锁专项

([FileDownloader.vb](../Clases/FileDownloader.vb) / [DescompresorController.vb](../Clases/DescompresorController.vb) / [Configuracion.vb](../Clases/Configuracion.vb) / [Main.vb](../Forms/Main.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [StreamingModule.vb](../HttpModule/StreamingModule.vb) / [ApplicationInstanceManager.vb](../Clases/ApplicationInstanceManager.vb))

下载与解压：

- ELC 账号导入后不再丢：导入即强制落盘 `Configuration.xml`（此前只改内存，重启就没了）
- 保存成功后清掉粘滞的 `ErrorConfig`：不再弹假"配置无法保存"，也不再丢掉刚填好的设置
- 多选删除按对象引用匹配，不再按可变的优先级序号匹配（此前会删错行 / 漏删）
- 卡死的下载会取消分块线程；连接与下载循环之间那段裸区抛出的异常会把该分块标记为失败，走指数退避与 UI 报错，不再原地紧循环重启
- 远端文件大小变了会重建分块布局（`ValidateAndNormalize` 的自愈此前是够不着的死代码）
- 解压遇到同名但大小不同的既有文件，保留成 `name (2).ext`（沿用项目既有约定）并按原名写入新文件；同名同大小幂等刷新。托管路径（RAR/ZIP/TAR）与 7z CLI 预改名路径都覆盖
- 无法解析的 `Range` 头回落到 200 全量（RFC 7233），不再发一个"声称全长 206 却只给约 16 字节"的响应；首尾包裁剪时 `DownloadIndex` 按实际发出的字节数推进
- 第二个实例不再把第一个实例的链接当新链接重放；短链更新入队的是解析后的真实 URL

运行时健壮性：

- 单实例判定改为原子（`EventWaitHandle` 的 `createdNew`），关掉原先能跑出两个"第一实例"的 TOCTOU 窗口
- MEGA 文件夹的孤儿节点挂到根之后循环继续，其后代仍能被解析（此前整棵子树被搁死在包根）
- ToolTip 的 HWND 泄漏修掉：弱引用注册表 + `ELCAccountControl` / `PropiedadesDescarga` 中确定性释放
- 日志 flush 不再抛异常；用户语言文件损坏时回落到内置语言包，不再静默杀掉启动
- ELC 解码失败（缺 `d`、结果为空）大声报错，不再静默丢链接
- 突破模式下的配额提示不再承诺该模式里并不存在的机制

### 🌐 共享文件夹网络错误友好提示(issue #6)

([NetworkErrors.vb](../Clases/NetworkErrors.vb) 新增 / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb))

- 新增传输层错误分类器 `Clases/NetworkErrors.vb`：把 TCP / DNS / 防火墙 / VPN / 代理类失败（含把 MEGA API 域名解析到非 MEGA IP 的 DNS 污染）与真正的 API 错误分开
- 传输失败给可操作的提示（查网络、防火墙 / VPN、配代理、确认浏览器能打开 `https://mega.nz`）；无效响应给一行技术线索，两者都不再甩原始堆栈
- 新增 `Folder_NetworkError` / `Folder_InvalidResponse` 两个键

### ✨ 文件夹分享的包用真名

([MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb) / [AddLinks.vb](../Forms/AddLinks.vb) / [Main.vb](../Forms/Main.vb))

- 不填包名添加文件夹分享时，主列表不再显示兜底的"封装 #N"，而是 MEGA 上那个文件夹的真名。名字一直在 `MegaFolderHelper` 里解密出来了，只是 `FillFolderStructure` 把共享根置 `""` 之后没人再读
- `URLProcessor.FileURL` 新增 `FolderName` 把真名带出来；`GetCommonFolderName` 只在整批一致时返回，混合批次（文件夹 + 散链、两个不同文件夹）继续用兜底名而不猜
- 共享根改用严格判据识别（自己的 `k` 字段里存在 handle 等于自身 `h` 的键值对）。原先那个 root 变量走 `ExtractKeyFromK` 的"找不到就回退第一个 key"语义，等价于"第一个 t=1 节点"，响应里万一不含共享根就会拿某个子文件夹名当包名
- 带子目录的文件夹分享只回写显示名，不额外套一层顶层目录——落盘形状与 issue #4 之后完全一致；扁平文件夹分享本来就建目录，现在目录名从"新建包"换成真名
- `/file/<id>` 链接刻意不带文件夹名，"单文件包按文件名命名"的既有路径不动
- Web 推送（`ControlRemotoAgregarLinks`）在 `pckname` 为空时同样使用文件夹真名，落盘形状不变

### 🩹 二次审查跟进（修上一批自己引入的回归）

([Configuracion.vb](../Clases/Configuracion.vb) / [Main.vb](../Forms/Main.vb) / [DescompresorController.vb](../Clases/DescompresorController.vb) / [StreamingLibraryManager.vb](../Clases/StreamingLibrary/StreamingLibraryManager.vb))

- **首启配置框回来了**：清 `ErrorConfig` 那条同时把"加载失败"也清了（`Fichero_No_Existe` / `Fichero_No_Valido`）。而 V26 列宽迁移在检查之前总会先存一次盘，于是全新安装永远看不到那个强制配置对话框——正是 `Configuration.vb` 专门加固过"不填账号密码不许取消"的那个框；损坏的配置也会被静默重置成默认值。现在把两件事拆开：新增 `Configuracion.LoadErrorConfig` 只记加载结果、永不被保存清掉，主窗体读它，`Configuration.vb` 继续读 `ErrorConfig` 管保存语义。第三个加载失败点（`ConexionesPorFichero = 0`）一并覆盖，它第一轮漏了
- **关闭时不再空转**：把 `Thread.Sleep(100)` 换成裸 `Application.DoEvents()` 之后，等待循环变成热自旋，实测约 19 万次/秒，每次都要抢 `Mutex.ListaDescargas` 并遍历整个下载列表——把它正在等的那些线程饿死。改成 `DoEvents` + `Sleep(50)`：消息泵约 14 Hz（`RunWorkerCompleted` 用不完），自旋没了
- **P0-1 的病根**：`ELCAccounts` 是在 `_LastSavedXML` 快照之后才追加进 XML 的（因为 `Account.User` / `Key` 是随机 IV 的 DPAPI 密文，参与比对会导致每 5 秒必写一次盘，`ServidorWebPassword` 同理）。副作用是账号变更对周期性 `GuardarXML(False)` 完全不可见，所以 P0-1 只能在每个导入点打强制保存的补丁，坑还留着。新增 `ELCAccountsHash()` 把 SHA-256 内容指纹加进变更检测条件；只留哈希，不留明文副本。已验证跨调用稳定，且对增 / 删 / 改 / 别名 / 默认项 / 顺序都敏感；也容得下 `SaveToConfig` 的重建窗口（Dispose + Clear + 重建，无锁，而周期保存正跑在 `bgwActualizadorDatosDisco` 上）——此时返回 `Nothing` 等下一轮重试，而不是在后台线程上抛异常。P0-1 那两处强制保存保留，用于即时落盘
- **N-P2-5 没修透的地方**：`StreamingLibraryManager.ImportLinks` 把 `ELC_exc` 接住就扔了，Web 界面导入坏 ELC 依旧无声消失。现在在解码循环之后重新抛出，落进既有的按条 Catch：记录真实原因、保住这一批的其余链接，已解出的链接留在列表里
- **解压取消等待上限 60 秒 → 5 秒**：这条分支只有关闭时够得着，而 Main 自己的预算是 `TIMEOUT_CLOSE`——随包发布的 InternalConfig 里是 **8 秒**（`Main.vb` 里那个 15 只是 `TryParse` 之前的兜底值，实际不生效）。60 秒必然烧满整个超时；8 秒只是打平，而且这个循环比 Main 的计时起步晚，照样输。改成 5 秒，给关闭流程的其余部分留出余量，同时仍覆盖正常停止延迟（约 1 秒）的 5 倍
- 解压失败分支里 `_codigoElementoActual` 此前留着不清（`_pathElementoActual` 已经置 `Nothing`），现在与其他退出路径一致
- `AddLinks` 里逐字重复两遍的 12 行突破模式配额提示抽成 `GetQuotaErrorMessage()`

### 🎨 界面与协议

([ThemeManager.vb](../Clases/ThemeManager.vb) / [SteganoWizardLoad.vb](../Stegano/SteganoWizardLoad.vb) / [SteganoWizardSave.vb](../Stegano/SteganoWizardSave.vb) / [ELCForm.vb](../Forms/ELCForm.vb) / [WebInterfaceModule.vb](../HttpModule/WebInterfaceModule.vb))

- 主题刷新逐个窗体加保护：一个窗体出错不再让其余窗口留在旧配色上
- 隐写向导限制本地图片上限 64 MB，输入框 / 浏览按钮支持拖放；校验加固（只收绝对的 http / https / file URI、强制 `.jpg` 输出、质量值解析防溢出）
- ELC 新建模式下丢弃已输入内容前先确认
- ELC 前缀剥离改为大小写不敏感（识别本来就是大小写不敏感的）；节流 sleep 改单调时钟；`Configuracion.ApplyConfigLimits` 与 `Conexion.SetProxy` 补端口钳制
- Web 响应不再带 UTF-8 BOM
- `mega://` 协议注册失败时给出带指引的提示，不再静默忽略

### 🌍 多语言

- 上一批新增的 5 个整句键在任何语言包里都不存在，`GetText` 只能返回原始英文键，中文界面因此显示英文。现已补进 en-US / zh-CN / zh-TW
- 本轮共新增 **11 个键**（issue #6 两个、审计批四个、本次补译五个），三语同步；其余 7 个语言包按既有三级回落机制走 en-US

### 📦 版本号

- Assembly / FileVersion → `2.5.7.0`；`docs/version.xml` → `2.5.7.0`；README 当前版本 → v2.5.7（5 个翻译同步）
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.5.7`

---

## [2.5.6] - 2026-10-06

补丁版，一句话：**配额可选突破（旧版行为回归），散装多文件自动建"新建包"目录，删盘连空目录一起收**。

### ✨ 配额突破模式(issue #5,可选,默认关)

([MegaQuotaManager.vb](../Clases/MegaQuotaManager.vb) / [Configuracion.vb](../Clases/Configuracion.vb) / [Configuration.vb](../Forms/Configuration.vb))

- 设置(连接组)新增"配额突破模式"复选框：打开后命中 509/-17 只记日志不熔断，配额失败走普通瞬时错误链（chunk 重连→停→失败自愈按周期复活），即 v1.8 的持续重试行为；横幅/倒计时不再出现
- 突破模式下配额失败项按自愈周期复活（与普通失败同节拍，禁止紧循环），且不依赖自愈开关；普通失败仍只跟自愈开关
- 熔断器其余语义不变，关闭即零行为变化；开关即时生效且持久化到配置，10 语言文案同步

### 🐛 下载列表与落盘修复

([AddLinks.vb](../Forms/AddLinks.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb) / [Fichero.vb](../Clases/Fichero.vb) / [Main.vb](../Forms/Main.vb) / [PropiedadesDescarga.vb](../Forms/PropiedadesDescarga.vb))

- 散装多文件（无相对子目录）+ 勾选创建目录 + 包名空：自动用"新建包"建子目录；文件夹分享、单文件、填名三条路径不动
- 下载完成真名回写：新增 `FileRenamed` 事件，重名 `name (2).ext` 落盘后回写队列，删盘/MD5/解压拿到的都是真名；此前删的是原名（删错或删空）
- 删除补 `CreandoLocal` 进异步分支：起步期删除不再因句柄占用失败
- 新包自动展开，列表不再只见"封装"行；验名前显示短 FileID 而非整串 URL
- 包属性框改路径时保留相对子目录（此前点确定即拍平）；无效路径拒收
- 删盘后顺手清理变空的子目录与包目录（仅空目录、封顶包根、用户自选目录永保；只删列表不动盘）

### 📦 版本号

- Assembly / FileVersion → `2.5.6.0`；`docs/version.xml` → `2.5.6.0`；README 当前版本 → v2.5.6（5 个翻译同步）
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.5.6`
- README 特性表 6 语言同步新增"配额突破模式"行

---

## [2.5.5] - 2026-10-04

补丁版，一句话：**文件夹分享不再"吃"名字，多文件包不再被首个文件名"吞"进子目录**。

### 🐛 文件夹分享与包命名修复(issue #4)

([Main.vb](../Forms/Main.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb))

- 多文件包(文件夹分享/批量添加且不填包名)不再取首个验完的文件名当包名建子目录——此前所有文件被搬进以一个文件名命名的文件夹，原相对子目录被拍平、留下空目录；只有单文件包保留按文件名命名
- 单文件分支搬移时保留 `RutaRelativa`，`/file/单文件` 这类带子路径的不再被拍平
- 文件夹首层子目录名丢失：`FillFolderStructure` 把根的直接子文件夹路径误置 `""`，文件被拍平到根；现在只有根自己置 `""`，`SubA/SubB` 完整保留（与递归分支语义对齐）

### 🔧 文件名解析与资源释放

([Conexion.vb](../Clases/Conexion.vb) / [Fichero.vb](../Clases/Fichero.vb))

- 文件名提取改走 JSON 解析（新增 `ExtractNameFromFileInfo`，失败回退历史正则）：文件名含转义引号 `\"` 不再被截断，`\uXXXX` 正常解码；常规名字与原来逐字一致
- `Fichero.Dispose` 去掉 `WorkerSupportsCancellation=False` 下必抛的 `CancelAsync` 调用（取消靠 `_StartupCancelled` 标志），关闭/释放不再刷异常日志

### 📦 版本号

- Assembly / FileVersion → `2.5.5.0`；`docs/version.xml` → `2.5.5.0`；README 当前版本 → v2.5.5（5 个翻译同步到 2.5.5）
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.5.5`

---

## [2.5.4] - 2026-10-01

两批合并发布：2.5.3 之后落地的静默修复集（升级丢口令/队列、7z 头加密、文件夹熔断失效等 P0/P1/P2）+ 流媒体在线观看根治 + 全语言翻译补齐。一句话：**升级不再丢数据，在线观看不再卡死，界面不再蹦英文**。

### 🔐 P0：升级即静默丢数据

([Criptografia.vb](../Clases/Criptografia.vb))

- DPAPI 熵原由 `AssemblyName.FullName` 派生（内含版本号）→ 每次版本 bump 熵即变，上一版保存的账号口令、下载队列链接、流媒体库链接全部解成空串且无任何提示。改为固定 `AppIdentity` 派生；解密侧轮询 12 个历史版本熵（2.4.0→2.5.3），旧值下次保存自动用新熵重加密；空值短路 + 非 base64（明文旧值）静默回退，真·解不开才记 Warning

### 🐛 P1：用户可感知的功能失效

([StreamingModule.vb](../HttpModule/StreamingModule.vb) / [DescompresorController.vb](../Clases/DescompresorController.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [Conexion.vb](../Clases/Conexion.vb) / [Configuration.vb](../Forms/Configuration.vb) / [StreamingLibraryManager.vb](../Clases/StreamingLibrary/StreamingLibraryManager.vb))

- 在线观看三处死循环/越界：上游提前 EOF 时零长转发把线程占满；EOF 落在 16 字节块中间时 `ProcessBlock` 写穿缓冲区越界崩溃；`bytes=0-0` 单字节探测被当成开区间把 content 补成整文件长度，进而触发 Read=0 死循环（2.4.5 修过一次但未修干净）。此前 QuickTime/DLNA 类播放器一探测即卡死
- 7z 头部加密档案列目录不带密码 → 口令再正确也 exit 255；口令含空格被 Windows argv 拆分（改 `-p"…"` 引号包裹，含引号/尾反斜杠显式拒绝）；子进程 stdin 未关闭 → 7-Zip 弹口令提示永久阻塞且取消检查失效
- MEGA 文件夹 API 错误码返回带括号形式 `[-9]`，`IsNumeric` 恒 False 使整段分支成死代码 → 配额熔断与错误映射全失效；先剥括号再判
- 代理地址填 `http://1.2.3.4` 会拼成 `http://http//1.2.3.4`（主机名真叫 http）→ 所有请求无提示失败；新增 `NormalizeProxyHost` 剥离 scheme/空白/尾斜杠（保留 `[::1]`）；端口校验 `=0` 改 `<1`
- 畸形 MEGA 密钥三处裸崩（base64url 长度 %4==1 越界、5..7 word 密钥索引越界、40..42 字符密钥取 intKey(7)）统一改为带说明的 FormatException；MetaMAC 校验路径记 Warning 跳过不抛
- 流媒体库 `Elements()` 返回副本，直接对返回值增删全是静默 no-op → 条目永不落盘；新增带锁 `AddElement`/`RemoveElement`，单例首次初始化加锁、加载完才发布

### 🔒 P2：并发串扰与体验修复

([WebInterfaceModule.vb](../HttpModule/WebInterfaceModule.vb) / [StreamingLibraryModule.vb](../HttpModule/StreamingLibraryModule.vb) / [ServidorWebController.vb](../Clases/ServidorWebController.vb) / [Main.vb](../Forms/Main.vb) / [Updater.vb](../Clases/Updater.vb))

- 流媒体与远程控制服务 cookie 同名 `Sd_session`，浏览器按 host 隔离导致两边互相顶掉登录态 → 流媒体改 `Sd_streaming`
- AJAX 响应与错误状态改 `AsyncLocal`：模块单实例下并发请求互相覆盖响应文本
- ToastForm 移出剪贴板监控排除列表：2 秒气泡显示期内用户复制的链接此前被静默吞掉
- Updater 注册表键补 `Using`（句柄泄漏）
- 进度列回退：条形列恢复只画条不画字，`%` 由独立数字列负责，两列并排互不覆盖；启动时把隐藏期落盘的数字列翻回可见，条形列默认宽度回 80

### 🔐 安全加固

([StreamingModule.vb](../HttpModule/StreamingModule.vb) / [StreamingLibraryModule.vb](../HttpModule/StreamingLibraryModule.vb))

- 流媒体密码错误返回 403 而非 200：密码是 query 参数非质询式认证，播放器/浏览器按真正的错误处理，不再把 "Error: Access denied" 文本当媒体数据
- 流媒体库登录加防爆破：失败锁定（60 秒窗口 10 次）+ 并发闸（4 个），与 Web 界面登录对齐

### 🌍 语言包

- 9 个语言文件补齐 567 条缺失翻译：德/意各 96 条、法/匈各 70 条、葡/罗/繁中各 69 条、西 27 条、简中 1 条——本地化界面不再混入成段英文
- 10 个文件清理 44 条历史重复死条目（运行时只读文档顺序第一条，删除的均为永不可达条目：en-US/简中各 1 条 `active downloads`，西/法/匈/葡/罗/繁中各 5 条，德/意各 12 条）；按运行时取值逐键断言零行为变化

### 📦 版本号

- Assembly / FileVersion → `2.5.4.0`；`docs/version.xml` → `2.5.4.0`；README 当前版本 → v2.5.4
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.5.4`
- 自本版起 DPAPI 熵为固定派生，后续版本 bump 不再影响存量密文（v2.4.0→2.5.3 历史熵保留在解密链中）

---

## [2.5.3] - 2026-09-18

v2.5.2 发布后用户实测抓到的三个回归，一次结清：**好文件不再误报失败，下完一次过，进度条字条同显**。

### 🐛 修复:好文件被误报 File download failed

([FileDownloader.vb](../Clases/FileDownloader.vb) / [Fichero.vb](../Clases/Fichero.vb))

- 启动探大小（HEAD）抖一下就先报失败，而 `downloadFile` 里会重探并可能成功——先到的失败把随后成功的文件钉成 `Erroneo`，好文件反复重下。现探活失败只暂存，下载阶段自有报错为准；成功则直接丢弃
- `downloader_FileDownloadFailed` 加过期免疫：已是 `Completado` 终态后到达的失败一律丢弃（成功与失败经不同 worker 编组，到达顺序不定）

### 🐛 进度列：% 实时叠在条上层

([Main.vb](../Forms/Main.vb))

- 自绘条只画条不画字，`%` 全程缺席。现文案与填充用同一 `Aspect`（同一刷新节拍，不存在谁盖谁），居中叠在条上：填充区白字、未填充区常规文字色双色裁剪，满格 100% 也清晰；渲染异常只丢文案不丢条
- 数字文本列默认隐藏（右键列菜单仍可手动恢复）；条形列默认宽度 80→110，`%` 不挤

---

## [2.5.2] - 2026-09-17

回归风险评估的三批治理一次落地。一句话：**依赖补齐欠账，落盘更快不断电丢数，锁换轻量不再泄漏**。

### 🚀 第一批：低风险性能与解耦

([FileDownloader.vb](../Clases/FileDownloader.vb) / [IDownloaderService.vb](../Clases/IDownloaderService.vb) / [WebInterfaceModule.vb](../HttpModule/WebInterfaceModule.vb) / [Criptografia.vb](../Clases/Criptografia.vb))

- 热路径 `ProgressChanged` 不再每 50KB 跨线程编组（全仓零订阅者，约 1800 次/秒消息直接省掉）；chunk 启动时的低频触发保留
- Web 层不再直接持有 `Main` 窗体：抽 `IDownloaderService`（9 方法 + 1 枚举），实现仍在 `Main`，行为不变
- `.part` 写流长生命周期复用，去掉每次 open/close；`Flush(True)` 语义保留
- 冷路径吞异常补日志：DPAPI 解密失败记 Warning（跨机恢复可查），VLC 路径校验失败记 Warning

### 🔐 第二批：口令加固、测试安全网、详情面板拆分

([Configuracion.vb](../Clases/Configuracion.vb) / [Configuration.vb](../Forms/Configuration.vb) / [DescompresorController.vb](../Clases/DescompresorController.vb) / [Main.vb](../Forms/Main.vb) / [Main.DetailPanel.vb](../Forms/Main.DetailPanel.vb))

- 代理/流媒体口令改 DPAPI 加密存储（`DPAPI:` 前缀区分密文与历史明文，老配置首次保存自动迁移）；换机/换账户恢复导致解密失败时记日志 + 打开设置弹窗提示重填，不再静默丢失
- 新建零 NuGet 回归测试工程（`Tests/`，控制台断言集，CI 自动执行）：`URLExtractor` / `PathGuard` / `Criptografia` 29 项 + 解压 13 项（ZIP 中文名、Zip Slip 拦截、7z 单卷/分卷/带密码/错密码）；RAR 真实样本缺失，合入前需手工覆盖
- 右侧栏结构化详情 Region 整块平移至 `Main.DetailPanel.vb`（Partial 同类，无逻辑改动、无事件绑定、Designer 不动）

### 📦 第三批：依赖升级、落盘节流、锁替换

([DescompresorController.vb](../Clases/DescompresorController.vb) / [Criptografia.vb](../Clases/Criptografia.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb) / [Fichero.vb](../Clases/Fichero.vb))

- SharpCompress 0.10.3 → 0.49.1：命名空间复数化（`Archives`/`Readers`）、`ExtractOptions` → `ExtractionOptions`、进度事件改按条目记账；自研 `DescompresionPriority` 替代已移除的 `PriorityExtension`（成员名不变，存量配置直兼容；限流粒度由按次 Read 放宽为按条目）
- BouncyCastle 1.7 → 2.6.2：`IBlockCipher` 精简（`Reset`/`IsPartialBlockOkay` 移除）对应去掉两个 `Implements` 子句；命名空间不变。Newtonsoft.Json 4.5 → 13.0.4；随包 DLL 12 → 15（含 SharpCompress 新增传递依赖），单文件版内嵌校验同步到 15 个
- `Flush(True)` 降频：新增 `SyncedIndex` 记账，8MB/5 秒才 fsync 一次，取消/失败/收尾强制同步；`GuardarXML` 只持久化已 fsync 边界（红线"持久化进度 ≤ 已 fsync 数据"），断电重启最多重下窗口内数据；旧版 XML 无记账字段，读盘时继承 `Index`（旧版每次都 fsync，无重下惩罚）
- 全仓 117 处内核 `Mutex` 换 `SyncLock` 轻量锁（可重入、编译器自带释放）；顺手修掉 `Main.Eliminar` 的加锁不释放与一处双 `Release`；`Dispose` 里对锁对象的 `Close()` 删除（锁对象无需释放，句柄泄漏源头消失）

### 🔔 体验：完成通知、错误人话、列表卡顿

([Main.vb](../Forms/Main.vb) / [Paquete.vb](../Clases/Paquete.vb) / [DownloadAdvice.vb](../Clases/DownloadAdvice.vb))

- 任务包完成弹托盘气泡，点击回到主窗口；文件失败弹气泡（同 tick 多个合并一条，60 秒 ≥3 条只记日志防刷屏）；存量红字启动时不补弹
- 失败原因说人话：配额/永久失效/连接重试三档 + 原文首行兜底（永不空），气泡里直接给动作（换 IP 重试 / 删除任务 / 无需操作）；`DownloadAdvice` 纯函数，10 项单测覆盖
- 大文件夹卡顿：430ms 循环改快照模式——短锁拷贝包与文件引用，重活（Downloader 属性读取、ETA 格式化）在锁外做，聚合只读快照；全局锁持有从"几百个文件全程"降为两次浅拷贝；列表刷新加 `BeginUpdate/EndUpdate` 批量重绘

### 🐛 导入卡死：文件夹解析取消穿透与验信息并行

([Conexion.vb](../Clases/Conexion.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb) / [ServerEncoderLinkHelper.vb](../Clases/ServerEncoderLinkHelper.vb) / [AddLinks.vb](../Forms/AddLinks.vb) / [Fichero.vb](../Clases/Fichero.vb))

- `SendJSON`/`SendPOST` 接受 `CancellationToken`，用 `Abort()` 把阻塞的同步 HTTP 炸出来转取消；此前取消键在 60s+120s 超时打满前无效，解析窗"一直在转"
- 文件夹解析与 ELC 解码链路透传取消；验信息（此前单 worker 串行逐个调 API）改跨包 4 并发，同包靠投递序号保包命名确定性；退出时打断在途请求最多等 5 秒
- 解析进度窗加已用秒数心跳（纯数字后缀，无需进语言系统）；取消穿透有 HttpListener 黑洞单测覆盖

### 📦 版本号

- Assembly / FileVersion → `2.5.2.0`；`docs/version.xml` → `2.5.2.0`
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.5.2`

---

## [2.5.1] - 2026-09-14

补丁版。相对 2.5.0 的两批审计跟进：自愈上线后的回归 + 其余用户可感知的明显问题。一句话：**续传保得住，限速说了算**。

### 🐛 第一批：看门狗、AddLinks、7z 配额、限速、隐身链接

([FileDownloader.vb](../Clases/FileDownloader.vb) / [Fichero.vb](../Clases/Fichero.vb) / [Main.vb](../Forms/Main.vb) / [AddLinks.vb](../Forms/AddLinks.vb) / [DescompresorController.vb](../Clases/DescompresorController.vb) / [Configuration.vb](../Forms/Configuration.vb))

- 120 秒总时长墙钟→无进度超时：有分块推进就重置计时，大文件不再无条件失败；自愈与配额唤醒保留分块与 `.part`，重试续传不再循环重下
- 「在线观看」取消不再锁死按钮：共享解析续体同时复位两个按钮 + 进行中标志（此前复位错按钮、标志永不清）
- 下载目录不存在时建完继续添加，第一下不再静默无操作
- 外部 7z CLI 路径补上 50 GiB 解压后体积上限（条目数上限原本就有）
- 0.5 MB/s 等小数限速可保存（按 Double 解析、四舍五入回 KB）；单位标签更正为 MB/s
- 多批隐身链接加换行分隔，不再拼成一个无法解析的大 blob 而静默丢失

### 🐛 第二批：11 项跟进（含第一批补丁引入的 2 个回归）

([FileDownloader.vb](../Clases/FileDownloader.vb) / [ThrottledStreamController.vb](../Clases/ThrottledStreamController.vb) / [Main.vb](../Forms/Main.vb) / [PropiedadesDescarga.vb](../Forms/PropiedadesDescarga.vb) / [DescompresorController.vb](../Clases/DescompresorController.vb))

- `.part` 尺寸不符（远端替换/截断/磁盘满短文件）就地重建 + 重置分块后继续，不再每 15 分钟失败一次、永远救不回
- 重试进度不再双计（启动清单次计数器；暂停/恢复不受影响；此前重试瞬间跳 ~100%）
- 限速 5 处调用统一 KB×1024 按字节传给控制器，此前设 1 MB/s 实际约 1 KB/s
- 暂停时长不再计入停滞；配额熔断期零速 10 秒看门狗 + 5 秒排空即变红（此前干等 ~150 秒）
- 多个 `.elc`/`.dlc` 排队逐个导入（拖放与命令行），命令行支持 `.elc`（此前静默无操作）；多 `.dlc` 不再只取第一个
- 红字任务「强制下载」可用：全量重置后按单个强制起（永久失败仍排除；此前零反馈）
- 包属性只改路径/密码不再静默清零各文件限速（一致时显示公共值，未勾选不覆写）
- 7z 解压发布总量，进度显示 已/共 而不是 `-`（逐字节进度仍仅托管分支有）
- 限速框最多 4 位小数（`1.46484375`→`1.4648`，回乘误差 <1KB）

### 📦 版本号

- Assembly / FileVersion → `2.5.1.0`；`docs/version.xml` → `2.5.1.0`（2.5.0 及更早版本会收到本次更新提示）
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.5.1`（`VERSION_UPDATE` 沿用已知的 `Double` 解析限制，统计 ping 同 2.5.0 取舍；更新检查本身用 `System.Version` 比较，不受影响）

---

## [2.5.0] - 2026-09-14

正式版。相对 RC2 的增量很小：修一个并发 bug、版本号转正并进入更新通道、文档补齐 4 语言。核心主题:**RC 验证通过，直接转正**。

### 🐛 修复:在线观看重复点击并发解析

([AddLinks.vb](../Forms/AddLinks.vb))"在线观看"按钮连点/双击会并发跑两次 `ResolveUrlsAsync`，同一批链接被解析两次，严重时拉起两个 VLC。现加 `_watchResolving` 进行中标记 + 解析期间禁用按钮，回调 `Finally` 里恢复(此前无任何防护)。

### 📦 版本号转正，进入更新通道

- InternalConfig `VERSION_MEGADOWNLOADER` → `2.5`(标题栏/关于/日志里的显示版本不再带 RC 后缀)
- `VERSION_UPDATE` 保持 `2.5`(刻意不动：`Main.CheckVersionStatistics` 用 `Double` 解析，`2.5.0` 会解析失败导致新版统计 ping 丢失)
- `docs/version.xml` → `2.5.0.0`：2.4.7 及更早版本从此刻起会收到更新提示；2.5.0 与远端相等，不再提示

### 🌐 文档：4 语言全量补齐(手动维护)

- 新增 `docs/README.zh-TW.md / docs/README.ko-KR.md`、`docs/CHANGELOG.{zh-TW,ja-JP,ko-KR}.md`、`docs/CONTRIBUTING.{zh-TW,ja-JP,ko-KR}.md`，英文 CHANGELOG 重写为真正的英文
- 自动翻译流水线已整体下线(`i18n/` 脚本 + Docs i18n 工作流删除)，以后所有语言版本手动同步更新，改一处请同步其他语言

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
