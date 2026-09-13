# v2.5.0（正式版） / Final

> 绿色免安装，解压即用；从 2.4.x 升级前建议备份 `%LOCALAPPDATA%\MegaDownloader\Config\Configuration.xml`。2.4.7 及更早版本会收到本次更新提示。
> Portable, no install; back up `%LOCALAPPDATA%\MegaDownloader\Config\Configuration.xml` before upgrading from 2.4.x. Versions 2.4.7 and older will be offered this update.

## What's Changed / 本次变更

匿名下载 MEGA 配额专项 + 生产审计三批修复，一句话：**配额可预期，失败不静默，卡顿不再来**。
Quota handling for anonymous downloads plus three batches of production-audit fixes. In one line: **predictable quota, no silent failures, no more UI freezes**.

- **配额全局熔断 + 倒计时横幅 / Quota circuit-breaker + countdown banner**：509 / -17 归一识别；60 分钟 → 2 小时 → 6 小时递进等待；横幅 + **[立即重试]** + 状态栏倒计时 + 托盘气泡。Identified by status code 509 / API -17; escalating 60min → 2h → 6h backoff; banner + **Retry Now** + status-bar countdown + tray notifications.
- **失败自愈默认开启 / Self-healing on by default**：15 分钟，存量配置一次性迁移；`-9 / -11 / -14 / -16` 永久失败自动排除；右键「重试/移除全部失败」。Every 15 min with one-time migration; permanent errors excluded; "Retry/Remove all failed" context actions.
- **发布阻塞修复 / Release blockers**：文件夹/ELC 展开、跳过计数、0 字节文件、验证取消标志、坏 ELC 按条隔离。Folder/ELC expansion, skip accounting, 0-byte files, verify-cancel flag, per-URL bad-ELC isolation.
- **可靠性 / Reliability**：MD5 移出全局锁、配置不再每 5 秒重写、关机等验证落盘、看门狗竞态消除。MD5 off the global lock, no more 5-second config rewrites, shutdown waits for verify, watchdog race removed.
- **体验 / Experience**：正则单例化（数百链接不卡）、转义链接可解、更新只走 https、XXE 封堵。Singleton regexes, escaped-link decryption, https-only updates, XXE blocked.
- **在线观看防连点 / Watch Online double-click guard**：解析期间禁用按钮，杜绝并发双解析。Button disabled while resolving; no more concurrent double resolves.

详见 `docs/CHANGELOG.md`（含 4 语言：English / 繁體中文 / 日本語 / 한국어）。
See `docs/CHANGELOG.md` (available in 4 languages: English / 繁體中文 / 日本語 / 한국어).

## 📦 包内容 / Package

`MegaDownloader.exe` ＋ 12 个 DLL ＋ `MegaDownloader.exe.config`，须放同一目录。
`MegaDownloader.exe` + 12 DLLs + `MegaDownloader.exe.config`, keep in the same folder.

---

**构建 / Build**：.NET Framework 4.8 · x86 · Win7 SP1+
**版本 / Version**：Assembly `2.5.0.0` / InternalConfig `2.5`
**已知限制 / Known limitations**：公开链接本质无端到端校验（仅长度）；配额倒计时为启发式估算；顶层系统菜单深色下仍浅色。Public links have no end-to-end checksum (length only); quota countdown is heuristic; top-level system menu stays light in dark mode.
