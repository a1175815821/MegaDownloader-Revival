# v2.5.1（补丁版） / Patch

> 绿色免安装，解压即用；从 2.4.x/2.5.0 升级前建议备份 `%LOCALAPPDATA%\MegaDownloader\Config\Configuration.xml`。2.5.0 及更早版本会收到本次更新提示。
> Portable, no install; back up `%LOCALAPPDATA%\MegaDownloader\Config\Configuration.xml` before upgrading from 2.4.x/2.5.0. Version 2.5.0 and older will be offered this update.

## What's Changed / 本次变更

两批审计跟进：一批是 2.5.0 的 P0/P1（看门狗、AddLinks、7z 配额、限速小数、隐身链接），一批是自愈上线后暴露的回归 + 其余用户可感知的明显问题，一句话：**续传保得住，限速说了算**。
Two audit rounds: round 1 covers the 2.5.0 P0/P1 items (watchdog, AddLinks, 7z quota, fractional limits, hidden links); round 2 covers post-self-heal regressions plus the remaining user-visible issues. In one line: **resume keeps working, limits mean what they say**.

- **看门狗改无进度超时 / Idle watchdog**：有推进就顺延，大文件不再误杀；自愈/配额唤醒保留断点。Progress resets the timer, large files no longer fail; self-heal and quota wake-ups preserve resume points.
- **限速真实生效 / Honest speed limits**：5 处调用统一按字节传递，设多少跑多少；0.5 MB/s 可存，框内最多 4 位小数。All call sites pass bytes; fractional limits save; 4-decimal display.
- **交互不再吞操作 / No more swallowed actions**：多 `.elc`/`.dlc` 排队导入、命令行认 `.elc`、红字可强制下载、包属性不清空限速、7z 显示总量。Queued multi-file import, `.elc` on the command line, force-download on failed tasks, package dialog preserves limits, 7z totals shown.

详见 `docs/CHANGELOG.md`（5 语言：English / 简体中文 / 繁體中文 / 日本語 / 한국어）。
See `docs/CHANGELOG.md` (5 languages: English / Simplified / Traditional Chinese / 日本語 / 한국어).

## 📦 包内容 / Package

`MegaDownloader.exe` ＋ 12 个 DLL ＋ `MegaDownloader.exe.config`，须放同一目录。
`MegaDownloader.exe` + 12 DLLs + `MegaDownloader.exe.config`, keep in the same folder.

---

**构建 / Build**：.NET Framework 4.8 · x86 · Win7 SP1+
**版本 / Version**：Assembly `2.5.1.0` / InternalConfig `2.5.1`
**已知限制 / Known limitations**：公开链接本质无端到端校验（仅长度）；配额倒计时为启发式估算；7z 解压仅总量进度（无逐字节）；顶层系统菜单深色下仍浅色。Public links have no end-to-end checksum (length only); quota countdown is heuristic; 7z shows total-only progress; top-level system menu stays light in dark mode.
