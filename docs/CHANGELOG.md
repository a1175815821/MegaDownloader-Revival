# Changelog

All notable changes to this project are documented here. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versioning follows [Semantic Versioning](https://semver.org/).

*See [GitHub Releases](../../releases) for user-facing release highlights.*

***

## Highlights at a Glance

| Version | Date | Theme |
| --- | --- | --- |
| 2.5.0 | 2026-09-14 | Final: Watch Online concurrency fix + version finalized into update channel + 4-language docs completed |
| 2.5 RC2 | 2026-09-13 | Production audit fixes in three batches: release-blocking / reliability / experience and defense-in-depth |
| 2.5 RC1 | 2026-09-12 | MEGA quota circuit-breaker + countdown banner; self-healing for failures enabled by default |
| 2.4.7 | 2026-09-12 | Three-option update prompt; removed deprecated search-engine integration |
| 2.4.6 | 2026-09-04 | False-success / silent-failure fixes |
| 2.4.5 | 2026-09-02 | Systematic fixes after full code review (16 items) |
| 2.4.4 | 2026-09-01 | Subfolder link downloads; MetaMAC chunk IV fix; 9 security hardening items |
| 2.4.3 | 2026-08-26 | 7z extract; Web LAN push; clipboard missed-detection fix |
| 2.4.2 | 2026-08-19 | Fixed real corruption of downloaded files (MetaMAC / resume alignment) |
| 2.4.1 | 2026-08-15 | Fixed downloads completing but showing an error |
| 2.4.0 | 2026-08-14 | 21 bug fixes (security / leaks / concurrency / dead code) |
| 2.3.0 | 2026-08-13 | Encryption-failure crash, resource leak, removed `Thread.Abort` |
| 2.2.x | 2026-07~08 | Path safety, download integrity, atomic config save, Web CSRF |
| 2.1.0 | 2026-07-19 | Dark theme usability fixes |
| 2.0.0 | 2026-07-13 | 4 phases, 60+ fixes; dark/light themes; code cleanup |
| 1.9.x | 2026-07-05 | Fixed recognition of new MEGA link formats |
| 1.8.0 | Original | Decompiled source, starting point of the revival plan |

***

## v2.4 Series Details

Below are the detailed changes for each v2.4 version (each version also appears in the version history above).

### Change Details

#### Update Prompt and Search Engine Cleanup (v2.4.7)

| Change | Description |
| -------------------- | ------------------------------------------------------------------------------------------- |
| Three-option update prompt | Yes=update now; No=remind again in 3 hours; Cancel=never remind for this version (`UpdateSkipVersion` recorded per version, prompt automatically resumes when a new version is released) |
| Removed search-engine integration | 4 domains under the "Search" menu (megafiles.me/megafindr/megasearch.co and others) are all offline; `mega://mega-search?` link parsing removed together |

#### False-Success / Silent-Failure Fixes (v2.4.6)

| Fix | Description |
| ---------------------- | ---------------------------------------------------------------------------------------------- |
| Fake success after restart (P1) | `Verificando`/`Descomprimiendo` were marked `Completado`, yet both may not have written a single byte to disk; now uniformly reset to `EnCola` and continued via resume |
| Fake success when saving settings | `GuardarXML` failures only went to the low-level log while the UI still reported success; now checks `ErrorConfig` and shows an error dialog without closing on failure |
| Uppercase links silently dropped | Matched with IgnoreCase but validated case-sensitively, so `HTTPS://MEGA.NZ/...` produced no reaction; all 6 regexes plus prefix comparisons made case-insensitive |
| Malformed enc crash | base64url with length %4==1 threw a bare `ArgumentOutOfRangeException`; now detected early with a friendly error |
| Folder API empty-response NRE | Malformed responses (empty string / proxy HTML) threw NRE; added Try/Catch + null checks, now uniformly reported as "invalid server response" |
| ELC sub-scope preserved | `MegaLink` adds sub-scope fields, appending a `/folder/subID` or `/file/fileID` suffix when encoding; old decoders ignore the suffix=historic behavior, new decoders restore the sub-scope |
| Missing language keys | en-US/zh-CN +5 each (ELC success notice, URL required, VLC path invalid, Open ELC menu, config save failure) |

#### Stability and Security (v2.4.5)

| Fix | Description |
| -------------- | ------------------------------------------------------------------------------------------ |
| Global exception fallback | Previously no fallback at all, UI exceptions terminated the process immediately; now logged without exiting, background thread exceptions leave logs |
| List refresh crash | 4 AspectGetters popped a dialog per row repaint on error and then crashed; now logs and returns placeholder values |
| Missing-volume RAR fake success | `IsComplete=False` was silently skipped while upstream reported "extract succeeded"; now throws explicitly |
| UI freeze | Chunk-failure backoff busy-wait ran on the UI thread (up to 16.5 seconds); moved to the thread pool |
| Streaming Range | RFC 7233 compliance: suffix/open ranges, 416 responses, `bytes=0-0` no longer pulls the whole file, response body no longer overruns Content-Length |
| Streaming media library CSRF | Delete/Save/OpenVLC/Import/Export require POST + token (reuses the Web UI EnsureCsrf pattern) |
| Login rate limiting | Concurrency cap 4 + lockout after 10 failures in a 60-second window, prevents PBKDF2 POST floods from saturating the thread pool |
| Stegano write-to-disk safety | In-memory encode + write to disk only after checksum passes (no more broken .jpg left behind); `WriteAllBytes` truncates on overwrite (no longer appends to old file tails) |
| Resource leak | FileDownloader handles, `CreateDecryptor`/MD5 Using, 5 mutex Try/Finally sites, 5 hovering ToolTip sites |
| Maintainability | vbproj dead references, DPI config unified to PerMonitorV2, hardcoded English messages moved into the language system (new en-US/zh-CN entries) |

#### New Features and Integrity (v2.4.4)

| Feature/Fix | Description |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Subfolder link downloads | `mega.nz/folder/rootID#key/folder/subID` downloads only the specified subfolder (paths rebased); `/file/fileID` downloads only the specified file — previously the whole root folder was always downloaded |
| MetaMAC chunk IV fix | Chunk CBC-MAC IV changed from zero IV to two copies of the file nonce `[n0,n1,n0,n1]` (aligned with SDK `SymmCipher::ctr_crypt`), fixing certain checksum errors reported after downloads completed for 8 words keys |
| MetaMAC standard checksum | Removed the "prefix match at chunk boundaries" tolerance logic, aligned with the SDK: one full comparison after reading the whole file |
| 9 security hardening items | StripNullCharacters offset fix; AES failures return Nothing and persistence points keep old values; ciphertext adds random-IV format (compatible with old data); Web password PBKDF2 (100k) + random salt; Streaming constant-time password comparison; PSK non-ASCII validation; ClientConnected reflection hardening |

#### New Features (v2.4.3)

| Feature | Description |
| --------- | --------------------------------------------------------------------------- |
| 7z extract | Prefer system 7-Zip, automatically extract bundled 7zr.exe (public domain) when not installed; password and multipart volumes supported; PathGuard validation before extract prevents path escape |
| Web LAN push | New "Allow LAN access" switch (default off); when enabled, phones/LAN devices can push downloads via browser; password protection enforced; custom bind IP supported (empty=all adapters) |
| clipboard monitor fix | Browser delayed rendering + clipboard-busy race caused missed detections of web copies — changed to retry reads, all accesses wrapped with exception protection |

#### Download Integrity (v2.4.2)

| Fix | Description |
| ----------- | ---------------------------------------------------------------------- |
| MetaMAC algorithm | Chunk schedule aligned with MEGA SDK `ChunkedHash`: 128 KiB x i (i=1..8) then fixed 1 MiB; empty files return (0,0) |
| Download completion rule | Removed "force-complete when file size matches"; completion only when all real chunks finish; 120-second timeout reports failure and keeps resume points |
| CTR keystream misalignment guard | Interrupted flush and resume start forced to 16-byte alignment; legacy non-aligned progress rolled back at startup — eliminates "correct size but corrupted content" |

#### Stability (v2.4.0/2.4.1)

| Fix | Description |
| -------- | ------------------------------------------------- |
| Background thread popup deadlock | Download failures now surface via the UI thread; cross-thread MsgBox during shutdown guarded with `IsDisposed` |
| Concurrency contamination | Streaming module AJAX responses changed to `AsyncLocal`, multiple requests no longer interfere |
| Resource leak | mutex `Try/Finally` release; `BackgroundWorker.Dispose` |
| Public-link false positives | 4 words keys without MetaMAC skip verification (logged), no longer misreported as failures |

***

## \[2.5.0\] - 2026-09-14

Final release. The delta since RC2 is very small: one concurrency bug fix, version finalized into the update channel, docs completed in 4 languages. Core theme: **RC validation passed, promoted straight to final**.

### 🐛 Fix: Watch Online repeated-click concurrent resolving

([AddLinks.vb](../Forms/AddLinks.vb)) Repeated clicks / double-clicks on the "Watch Online" button ran `ResolveUrlsAsync` twice concurrently, the same batch of links was resolved twice, and in severe cases two VLC instances were launched. Now a `_watchResolving` in-progress flag + button disabled during resolving is added, restored in the callback `Finally` (previously no protection at all).

### 📦 Version finalized, entered update channel

- InternalConfig `VERSION_MEGADOWNLOADER` → `2.5` (displayed version in title bar / About / logs no longer carries an RC suffix)
- `VERSION_UPDATE` stays `2.5` (intentionally unchanged: `Main.CheckVersionStatistics` parses with `Double`, `2.5.0` would fail to parse and lose the new-version statistics ping)
- `docs/version.xml` → `2.5.0.0`: versions 2.4.7 and earlier will receive update prompts from this moment; 2.5.0 equals the remote version, so no further prompt

### 🌐 Docs: all 4 languages completed (maintained manually)

- Added `README.zh-TW.md / README.ko-KR.md`, `docs/CHANGELOG.{zh-TW,ja-JP,ko-KR}.md`, `docs/CONTRIBUTING.{zh-TW,ja-JP,ko-KR}.md`, English CHANGELOG rewritten as genuine English
- The automatic translation pipeline is fully retired (`i18n/` scripts + Docs i18n workflow deleted); from now on all language versions are synced manually, please sync the other languages when changing one

***

## \[2.5 RC2\] - 2026-09-13

Three batches of fixes after a production-grade destructive audit (all new changes since RC1 are here). Core theme: **eliminate silent failures and high-frequency stalls**.

### 🐛 RC2 Fixes: 5 release-blocking items (Batch-1)

([Main.vb](../Forms/Main.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb) / [Fichero.vb](../Clases/Fichero.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb))

- Web push and manual link-adding share the same path: `ControlRemotoAgregarLinks` first expands folders / ELC via `URLProcessor.ProcessURLs` (previously folder links became a single broken task); `pckname` sanitized via `PathGuard` (previously string concatenation, authenticated arbitrary directory creation + write-out jail escape); subdirectory structure preserved
- Folder skip accounting: single-node decryption failures counted, partial skips logged as Warning (with example handle, no key material logged), throws when all fail (previously zero files still reported success)
- 0-byte empty-file short-circuit: after probing still 0, directly materialize the empty file, verify MetaMAC (expected `(0,0)`), and go through normal rename and success events (previously `GetDataPart` threw + self-healing spun idly); rename-conflict logic extracted as shared `RenamePartToReal`
- Validation cancel flag: `bgArranque` cannot be `CancelAsync`, added a `_StartupCancelled` flag, Stop/Dispose sets it so completed validation no longer creates downloaders (previously Stop always resurrected)
- Bad-ELC per-URL isolation: a single bad entry only skips that entry, partial results of the same entry rolled back, first error still thrown when all fail to keep single-link semantics (previously one bad ELC wiped out the whole batch)

### 🐛 RC2 Fixes: 4 reliability items (Batch-2)

([Fichero.vb](../Clases/Fichero.vb) / [Configuracion.vb](../Clases/Configuracion.vb) / [Main.vb](../Forms/Main.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb))

- 100% fallback MD5 moved out of the global lock: only scheduling happens inside the lock (set `ComprobandoMD5` to prevent duplicate submission), execution runs on the thread pool outside the lock (previously GB files colliding froze the UI / scheduler)
- Web password random IV moved out of config deduplication comparison, changed to plaintext snapshot comparison (previously `Configuration.xml` was rewritten every 5 seconds whenever a Web password existed)
- Shutdown wait completed with `CreandoLocal/Verificando/Descomprimiendo/ComprobandoMD5`; validation write-back and `GuardarXML` hold `FicheroDownloader` together (previously shutdown tore the queue)
- 120-second watchdog verdict moved after 30-second drain, completions racing in during drain no longer misreported as failures (previously success/failure double-event races could pin intact files as errors)

### 🐛 RC2 Fixes: experience and defense-in-depth (Batch-3)

([URLExtractor.vb](../Clases/URLExtractor.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [StreamingModule.vb](../HttpModule/StreamingModule.vb) / [Main.vb](../Forms/Main.vb) / [Updater.vb](../Clases/Updater.vb) / [Criptografia.vb](../Clases/Criptografia.vb) / [SteganoManager.vb](../Stegano/SteganoManager.vb))

- Regex singletons (`Compiled` shared instances): `URLExtractor` full-table scans, `MegaFolderHelper` per-node scans, `StreamingModule` per-request scans no longer `New Regex` (previously pasting hundreds of links froze the UI)
- fragment decoding: `UnescapeDataString` + whitespace stripping, `%23/%3D` escaped links no longer permanently "cannot decrypt"; dead `Contains(" ")` branch removed
- Update URL https-only; `version.xml` forbids external entities (XXE); public links skip checksum with Warning trace; Stegano remote 64MB+30s limits; streaming malformed mega parameters return 400 instead of 500

### 🐛 RC2 Fixes: pre-merge gate patches

- Left task overview + shortcuts: total speed / counts / queue progress / remaining time, reuses the existing 430ms refresh loop (no new timers); extract queue / streaming media library / logs raised to first screen
- Streaming media library Web import per-entry isolation: bad folders / expired ELC only skip that entry (previously the whole request went unanswered)
- quota same-event dedup: repeated reports during a circuit-breaker window neither escalate the tier nor extend the wait (previously concurrent hits across connections could jump straight to 6h)
- CI single-file validation changed to Windows PowerShell 5.1 execution (previously `pwsh` ReflectionOnlyLoad always threw, builds always red)

### 📦 Version numbers

- Assembly / FileVersion → `2.5.0.0` (RC unchanged)

- InternalConfig `VERSION_MEGADOWNLOADER` → `2.5 RC2` / `VERSION_UPDATE` → `2.5` (numbers unchanged, no update prompts during beta/RC; `docs/version.xml` stays `2.4.7.0`, raised only for final)

***

## \[2.5 RC1\] - 2026-09-12

Anonymous-download MEGA quota (HTTP 509 / API -17) feature release. Core theme: **quota made predictable — automatic pause, honest countdown, automatic resume on time**.

### ✨ New: global quota circuit-breaker + countdown banner

([MegaQuotaManager.vb](../Clases/MegaQuotaManager.vb) new / [Conexion.vb](../Clases/Conexion.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb) / [Main.vb](../Forms/Main.vb))

- Unified 509 and -17 recognition: verdicts use only `HttpStatusCode = 509` (status code) and `-17 / EOVERQUOTA` (API semantics), no response-body text matching. Covers both file-info landing points (:429 exception path + :364 numeric path) and folder-read landing points (:47 exception path + :51 numeric path)
- Progressive circuit-breaker: first hit pauses 60 minutes, repeated hits during the quota window escalate to 2 hours, capped at 6 hours; `Retry-After` takes the larger value when present (usually absent, not relied upon)
- During circuit-breaker: no new tasks started, no new file-info checks (each check = one API call, which would extend the penalty window), chunk failures no longer spin 16 seconds in retry, the scheduler waits uniformly
- Main-window banner: `MEGA quota exhausted (no exact recovery time given). Automatically paused, will retry automatically in X hours Y minutes` + **[Retry Now]** button (manual release after changing IP / proxy / rebooting router, never locked out) + status-bar countdown + one tray balloon each on enter/exit

### ✨ Improvement: self-healing for failures enabled by default (with one-time migration)

([Configuracion.vb](../Clases/Configuracion.vb)) `ResetearErrores` default changed to on (15 minutes); existing configs migrated to on once via `ResetearErroresMigratedV25`, later manual choices no longer overwritten; fresh installs default to on.

### ✨ Improvement: permanent failures excluded from self-healing + batch operations

([Fichero.vb](../Clases/Fichero.vb) / [Main.vb](../Forms/Main.vb)) `-9 ENOENT / -11 EACCESS / -14 EKEY / -16 EBLOCKED` marked `EsErrorPermanente`, self-healing skips them (no more log-spam and quota burn every 15 minutes); context menu adds **Retry all failed** and **Remove all failed** (confirm dialog shows count, optionally deletes local `.part` files).

### ✨ Improvement: large-folder read progress feedback

([MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb) / [AddLinks.vb](../Forms/AddLinks.vb)) Folder parsing moved off the UI thread, progress dialog shows `Reading folder... N items parsed` in real time, cancellable (results discarded without adding packages); Watch Online uses async resolving as well.

### 🐛 RC1 Fixes: quota false positives + lost errors after restart + grey progress bars (P1)

- Quota timeouts during circuit-breaker now report `MegaQuotaExceededException` (previously the 120-second fallback reported generic timeout with `FailedByQuota=False`, so "Retry Now" could not recover them); non-quota 120-second wording no longer misleads with "link expired", clarified as total-duration watchdog (not idle timeout), progress kept for resume
- `DescripcionError / EsErrorPermanente / FailedByQuota` persisted to queue XML (4000-char truncation), "View error" no longer blank after restart, permanent failures no longer repeatedly revived by self-healing; old queues backfill marks from description text for compatibility; empty descriptions get an actionable fallback instead of a blank dialog
- Progress-bar custom drawing: background transparent to reveal row color (grey whole-bar in light theme eliminated, 0% rows no longer solid grey); empty gradient uses solid `FillColor`; border follows theme `Border`; bar height 18; tiny progress keeps 2px minimum
- Download-list column-width anti-corruption: 10 columns get `Minimum/MaximumWidth` (# 20-40 / filename 150-700 / see code for the rest), `ColumnWidthChanging` clamps while dragging (OLV does not intercept header drags); bad states at startup (`#`/filename squeezed to 0, single column >800, total visible width >2000) auto-reset and persist; context menu adds "Restore default column widths" (previously `#` column `Hideable=False` + migration already ran, no in-app path back to defaults)

### 🐛 RC1 Patches: banner layout / quota semantics / build availability

- Build availability: modern MSBuild compiles resx into preserialized format, startup crashed immediately (`FileLoadException` at `My.Resources.icono`, swallowed by the global exception fallback into a silent exit 0). `Resources\DLLs` adds `System.Resources.Extensions/Memory/Buffers/Unsafe/Numerics.Vectors`, vbproj adds references, `app.config` adds version redirects, CI artifact list synced
- quota banner changed to absolute layout: list + side panels Top/Height always recomputed from toolbar bottom + banner visibility, relative shifts and Bottom anchors removed (previously hide after resize/maximize/DPI change left one banner height covering the status bar)
- quota-expiry wakeup detached from the self-healing switch: circuit-breaker release edge calls `WakeQuotaFailedItems` directly (previously hidden inside `If ResetearErrores`, users with self-healing off saw the banner disappear but failed items never recovered)
- Penalty escalation fixed: tiers decay after 24h, expiry/manual clear no longer resets to zero (previously escalation never advanced, manual retry dropped back to 60min then immediately re-requested); `Retry-After` supports HTTP-date
- Countdown wording: seconds under 120 seconds, floor above that (previously last 60 seconds stuck at "1 min", 1h59m30s showed "2 h 0 min"); banner colors enter the theme system (`QuotaBack/QuotaFore`)
- Toast new/reuse shares four-edge clamping (previously first popups halfway off-screen when near edges); folder progress dialog progress bar drops visual styles to follow theme, cancel truly cancels (`CancellationToken` passed into parse loop)
- Scheduler-loop single-point failure: per-iteration Try/Catch + `RunWorkerCompleted` watchdog self-restarts in 3 seconds (previously one exception stopped refresh + banner + recovery); clicks during circuit-breaker give Toast feedback (previously clicks appeared dead)

### 🐛 RC1 Fixes: 12 user-visible UI defects

- `ThemeManager` adds `ListBox / CheckedListBox / ToolTip` branches (previously dark mode missed them, patched per-form by hand); top-level `MainMenu` remains a system menu as a known limitation
- Toast reuse recomputes position to avoid stale cross-screen coordinates; quota banner Top follows toolbar + DPI scaling; toolbar icons / navigation row height follow DPI; status-bar RAM/Proc changed to auto width (Designer 90→150); default window width 880→1024 to free the Nombre column
- Settings search supports `NumericUpDown / ListBox / DataGridView` and ComboBox candidates, unfocusable Labels degrade to focusing parent containers, beep feedback when no hits; Settings Cancel changed to `Bottom|Right` anchor; ELC table repaints after skin change to avoid first-frame old colors
- AddLinks: clearing text also clears `HiddenLinks`, placeholder line count included in count; watermark grey text visible in dark mode; extract password `MaxLength` 6→128 (both places); ELC two Labels reuse existing keys for translation; Streaming red text changed to theme `ErrorFore` + invalid links get hints; `btnLanzarVLC.DialogResult` changed to None to avoid accidental dialog close; Credits removes `AcceptButton=lblTitle` + empty-translation bare-key guard

### 🌐 New language keys

`en-US` / `zh-CN` add `Quota_Banner / Quota_Status / Quota_RetryNow / Quota_Recovered / Quota_Error / Retry all failed / Remove all failed(+confirm/delete part) / Folder_Reading(+Count)`; RC1 supplements `es-ES` with the same 11 keys (other languages fall back via en-US).

### 📦 Version numbers

- Assembly / FileVersion → `2.5.0.0` (RC unchanged)

- InternalConfig `VERSION_MEGADOWNLOADER` → `2.5 RC1` / `VERSION_UPDATE` → `2.5` (numbers unchanged, no update prompts during beta/RC; `docs/version.xml` stays `2.4.7.0`, raised only for final)

***

## \[2.4.7\] - 2026-09-12

Update prompt upgraded to three options + removal of deprecated search-engine integration. Core theme: **give the "reminder frequency" choice to users, sweep dead domains out**.

### ✨ Improvement: three-option update prompt

([Main.vb](../Forms/Main.vb) / [Configuracion.vb](../Clases/Configuracion.vb)) The new-version dialog grows from Yes/No to three options: **Yes**=update now; **No**=remind again in 3 hours; **Cancel**=never remind for this version. The `UpdateSkipVersion` setting persists the "never remind" choice (recorded per version, only that version is muted; prompts automatically resume when a future version is released), written to disk immediately after selection. The "Check for updates" checkbox in settings remains the master switch (unchecking stops all checks).

### 🧹 Maintenance: remove deprecated search-engine integration

The 4 MEGA search-engine domains aggregated under the original "Options → Search" menu (megafiles.me / megafindr.com / megasearch.co / megasearch.co.nz) are all offline, the whole feature is unusable:

- ([URLExtractor.vb](../Clases/URLExtractor.vb)) Deleted `mega://mega-search?...` link parsing (`MEGASEARCHPREFIX` constant, regex patterns, the mega-search parsing branch in `CheckFileIDAndFileKey`)
- ([Main.vb](../Forms/Main.vb)) Deleted the "Search" menu construction and `Buscador_Click`
- ([InternalConfig.xml](../Resources/InternalConfig.xml)) Deleted `SEARCH_LIST` (4 dead domains) and `MEGA_SEARCH_CURL`
- ([InternalConfiguration.vb](../Clases/InternalConfiguration.vb)) Deleted the caller-less `ObtenerValuesFromInternalConfig`
- 10 language files drop the dead `Searc&h` key

### 🌐 New language keys

`en-US`/`zh-CN`/`zh-TW`/`es-ES` add `Update prompt hint` (explains the three-option dialog button meanings, other languages fall back via en-US).

***

## \[2.4.6\] - 2026-09-04

7 false-success / silent-failure fixes + 1 maintenance cleanup. Core theme: **let failures look like failures**.

### 🐛 Fix: fake success after restart (P1)

([Paquete.vb](../Clases/Paquete.vb)) `MarcarFicherosComoParados` used to mark files in `Verificando`/`Descomprimiendo` as `Completado` — but `Verificando` is a transient pre-download state (`EstadoAnterior` already lost after restart), `Descomprimiendo` means download done but extract unfinished, **both can be "not a single byte on disk" yet reported as success**. Now uniformly reset to `EnCola`: resume continues them, already-complete files only get a fast checksum, never re-downloaded from zero.

### 🐛 Fix: fake success when saving settings

([Configuration.vb](../Forms/Configuration.vb)) When `Config.GuardarXML` failed, only the low level logged it and set `ErrorConfig`, while the UI still popped "save succeeded" and closed the dialog. Now failures check `ErrorConfig <> SinErrores`, pop an error and stay open, no longer showing success. New error-message language keys added.

### 🐛 Fix: uppercase links silently dropped

([URLExtractor.vb](../Clases/URLExtractor.vb)) `ExtraerURLs` matched `HTTPS://MEGA.NZ/...` with `IgnoreCase`, then immediately failed case-sensitive `ExtraerFileID` validation and dropped it — pasted uppercase links produced no reaction. All 6 `New Regex(pattern)` sites now add `IgnoreCase`; along the way `#F`/`#N` pattern comparisons changed to `ToUpperInvariant`, `fenc`/`enc`/mega-search prefix matches all changed to `OrdinalIgnoreCase`. Capture groups keep original case, FileID/FileKey case sensitivity unaffected.

### 🐛 Fix: malformed enc link crash

([URLExtractor.vb](../Clases/URLExtractor.vb) / [ServerEncoderLinkHelper.vb](../Clases/ServerEncoderLinkHelper.vb)) base64url length `%4==1` is illegal, the original `"==".Substring(3)` threw a bare `ArgumentOutOfRangeException`. Now detected early with a friendly error, uniformly surfacing "invalid link" style prompts.

### 🐛 Fix: folder API empty-response NRE

([MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb)) `DeserializeObject` had no Try, `FileList.f` iterated directly — malformed responses (empty string / proxy HTML) threw NRE. Now Try/Catch + null checks added, uniformly throwing "invalid server response" without leaking HTML content into logs.

### ✨ Improvement: subfolder links converted to ELC no longer lose scope

([ServerEncoderLinkHelper.vb](../Clases/ServerEncoderLinkHelper.vb)) `MegaLink` adds `SubFolderID`/`SubFileID`; `ServerEncode` appends a `/folder/subID` or `/file/fileID` suffix for MegaFolder. Old decoders ignore the suffix (degrading to whole-folder = historic behavior), new decoders restore the sub-scope, **bidirectionally compatible**. `ExtraerSubFolderID`/`ExtraerSubFileID` add fallback parsing for old-style token suffixes (`mega://#F!...!/folder/...`, ELC decode products). `enc`/`enc2` ciphertext has nowhere to store sub-scopes, `EncodeLinksForm` skips encoding for links containing sub-scopes and keeps the original text, avoiding silently expanding to the whole folder.

### 🌐 Missing language keys completed

`en-US`/`zh-CN` +5 each: `ELC created successfully`, `URL is mandatory`, `VLC path is not valid`, `Open &ELC`, `Configuration could not be saved...`. Previously missing keys fell back via `Language.GetText` to en-US English source, not a crash, but Chinese UI missed translations.

### 🧹 Maintenance cleanup

- Deleted `docs/BUGFIX-CHECKLIST.md` (the 2026-07-13 review checklist is severely outdated, at least 8 unchecked boxes already fixed)
- Deleted `Resources\DLLs\xunit.dll` (no vbproj reference, the Fadd.dll xunit 1.0.3 dependency never resolved anyway, the on-disk 1.9.1 version was never used)
- vbproj drops dead reference `TODO\TODO.txt`
- README: Web UI description corrected to "binds 127.0.0.1 by default, LAN access can be enabled with a bind IP" (matches implementation); xUnit entries removed

***

## \[2.4.5\] - 2026-09-02

Systematic fixes after a full code review in this release: all 36 confirmed issues handled, covering crash fixes, functional correctness, HTTP protocol compliance, resource leak and security hardening.

### 🛡️ Global exception fallback

([ApplicationEvents.vb](../ApplicationEvents.vb)) Previously the whole app had no unhandled-exception fallback — any UI thread exception popped the .NET crash dialog and terminated the process, background thread exceptions made the process vanish silently. Now:

- `My.UnhandledException`: exceptions written to logs and **do not exit**, users get a chance to save state

- `AppDomain.UnhandledException`: background thread exceptions at least leave log traces

### 🐛 Fix: list-refresh crash (high-frequency crash source)

([Main.vb](../Forms/Main.vb)) The Catch blocks of 4 `AspectGetter`s (download status / percent / ETA / progress text) popped English stacks then `Throw` — and AspectGetters run **on every row repaint**, one dialog per row, then crash after dismissal. Now only logs and returns safe placeholder values.

### 🐛 Fix: remaining user-visible errors

| Symptom | Cause and Fix |
| ------------------------------------- | ----------------------------------------------------------- |
| Opening ELC and clicking Cancel reports "The path is not valid" | Cancel still called `AddDLC` with an empty string; now exits silently |
| Reset on a single file flips back to Error quickly | Fichero branch missed `ResetearDescarga()`, leaving `.part` and error state; aligned with package branch |
| First-run forced config bypassed via "Cancel" | Password box filled with `*****` placeholder always non-empty, validation never reachable; placeholder now treated as "not set" |
| Web timeout save 61-99 reopens empty then changed to 5 | Load bound `>60` vs save bound `0-99` mismatch; unified to 0-99 |
| Corrupt queue file crashes at startup | `CDate(strFecha)` locale-dependent with no Try; changed to `Date.TryParse` dual-culture parsing, bad values skipped |
| Background popups hidden behind main window look like hangs | DoWork threads called `MessageBox.Show` directly; routed via `SafeShowError` back to UI |
| 3 background workers pop full-screen English stacks on error | Stacks go to logs, users only see `ex.Message` |
| VLC launch failure with no feedback | Return value ignored with no Try; both callers now check it, `WatchOnline` internally guarded |
| Dragging non-ELC/DLC files silently dropped | Now shows a hint |
| Clicking "Open directory" with no selection reports empty-path error | Empty paths exit directly |

### 🐛 Fix: functional correctness defects

| Location | Fix |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [DescompresorController.vb](../Clases/DescompresorController.vb) | **Missing-volume RAR fake success**: `IsComplete=False` silently skipped without setting an exception, upstream reported "extract succeeded"; now throws explicitly. **Duplicate Code stuck at "extracting"**: silent skip still returned True; now updates existing queue entries |
| [FileDownloader.vb](../Clases/FileDownloader.vb) | **UI freeze up to 16.5 seconds**: chunk-failure backoff `Thread.Sleep` busy-wait ran on the UI thread; moved to thread pool. **"Must specify size" hid real errors**: Size=0 skips trailing blocks. **Dispose never released Mutex/MutexFile/trigger**: now deterministically released |
| [Configuracion.vb](../Clases/Configuracion.vb) | Empty Catch on Web password decrypt failure: kept ciphertext as password so login always failed with no clue; now logs and clears |
| [Conexion.vb](../Clases/Conexion.vb) | FileID concatenated into JSON/URL unescaped; added JSON escaping + UrlEncode |
| [Main.vb](../Forms/Main.vb) | Deleting "package+child files" double-hooked `CancellationComplete` so `Dispose` ran twice; already-removed objects now skipped directly |

### 🌐 HTTP modules: Range compliance + CSRF + rate limiting

- **RFC 7233** ([StreamingModule.vb](../HttpModule/StreamingModule.vb)): supports `bytes=-N` suffix ranges and `bytes=N-` open ranges; out-of-range returns 416 + `bytes */size` (previously silently clamped); `bytes=0-0` single-byte probes only fetch one aligned block (previously requested the whole file from MEGA, bandwidth amplification); tail blocks truncated to Content-Length (response body no longer overruns)

- **`?mega=`** **parsing**: uses framework-parsed parameter values directly, `?p=password&mega=...` ordering no longer fails

- **Streaming media library CSRF** ([StreamingLibraryModule.vb](../HttpModule/StreamingLibraryModule.vb)): Delete/Save/OpenVLC/ImportLinks/ExportLinks now require POST + valid token (reuses Web UI EnsureCsrf pattern); templates inject tokens; **also fixes the existing breakage where the browse page called POST-only OpenVLC via GET**

- **Login rate limiting** ([WebInterfaceModule.vb](../HttpModule/WebInterfaceModule.vb)): PBKDF2 (100k) ran synchronously on request threads, POST floods could saturate the thread pool; now concurrency cap 4 + lockout after 10 failures in a 60-second window

- 3 `StreamWriter(response.Body)` sites add `Using` + BOM-less encoding

### 💾 Resource leak

- `Criptografia.decrypt_key`: `CreateDecryptor` inside the loop never Disposed (each key decryption leaked N ICryptoTransforms); changed to Using

- `MD5Utils.MD5CalcString`: added Using

- `DescompresorController` x4, `ThrottledStreamController` x1 mutex sites add Try/Finally (per project convention)

- Configuration/PropiedadesDescarga 5 hovering ToolTip leaks; single shared instance reused

### 🔒 Stegano (steganography)

- **Wrote broken output before erroring**: capacity checks happened after writing to disk, leaving truncated .jpg files; changed to in-memory encode and write to disk only after all checks pass

- **OpenWrite did not truncate tails**: overwriting a longer old file concatenated old and new bytes; `WriteAllBytes` truncates on overwrite

- **Uri validation was a no-op**: `RelativeOrAbsolute` returns True for "hello world"; restricted to Absolute + http/https/file

### 🧹 Maintenance cleanup

- vbproj drops dead references `TODO\TODO.txt`, `plantilla botones.psd`; deletes orphan `postbuildevent.xml`

- DPI config unified to PerMonitorV2 (app.config adds `DpiAwareness`, myapp HighDpiMode=2); removes undeployed Unsafe assembly redirect

- README drops the non-existent xUnit stack entry

- Hardcoded English messages (ELC/DLC errors, Invalid input data and 10 other sites) moved into the language system, new en-US/zh-CN entries added

- BUGFIX-CHECKLIST.md header adds "outdated" warning (at least 8 unchecked boxes already fixed, prevents duplicated work)

***

## \[2.4.4\] - 2026-09-01

### ✨ New: subfolder link downloads

**Before**: links like `mega.nz/folder/<rootID>#<key>/folder/<subID>` were treated as root-folder links, downloading the entire root folder contents.

**Now** ([URLExtractor.vb](../Clases/URLExtractor.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb) / [StreamingLibraryManager.vb](../Clases/StreamingLibrary/StreamingLibraryManager.vb)):

| Link form | Behavior |
| ---------------------------------- | -------------------------- |
| `mega.nz/folder/rootID#key/folder/subID` | Downloads only the specified subfolder contents (paths rebased to the subfolder root) |
| `mega.nz/folder/rootID#key/file/fileID` | Downloads only the specified single file |
| `mega.nz/folder/rootID#key` | Downloads the whole root folder (unchanged) |

- Regexes extended to capture `/folder/<subID>` and `/file/<fileID>` suffixes

- File lists filtered by walking parent chains upward, keeping only files under the target node

- Download paths rebased relative to the subfolder, no more redundant upper-level directories

### 🐛 Fix: subfolder links falsely reported MetaMAC errors after completing (confirmed by user testing)

**Symptom**: subfolder-link downloads popped MetaMAC checksum failures at 100%; file contents were actually intact.

**Root cause**: each data chunk CBC-MAC was computed with a **zero IV**. But the real MEGA algorithm (SDK `SymmCipher::ctr_crypt`) seeds chunk MACs with the **file nonce duplicated** — key words 4-5 joined into 16 bytes `[n0, n1, n0, n1]`. MACs computed with zero IV can never match the MetaMAC of any real upload, so every download with an 8 words key (embedded MetaMAC) necessarily errored; single-file public links (4 words keys, checksum skipped) were unaffected, which hid the problem.

**Fix** ([Criptografia.vb](../Clases/Criptografia.vb)):

```vb
' Before: zero IV (checksum always failed)
Dim chunkMac As Integer() = New Integer() {0, 0, 0, 0}
' After: nonce (key words 4-5) duplicated, aligned with SDK
Dim chunkMac As Integer() = New Integer() {nonceWords(0), nonceWords(1), nonceWords(0), nonceWords(1)}
```

The rest (folded zero IV, `(m0^m1, m2^m3)` final compression, 128 KiB x i chunk schedule) was verified line-by-line against SDK `macsmac`/`ChunkedHash` sources as already correct, unchanged.

### 🔧 MetaMAC verification aligned to SDK standard behavior

Removed the lenient "check MAC early at each chunk boundary, allow prefix match" logic from the verifier — the authoritative SDK implementation (`generateMetaMac` + `macsmac`) does **one full comparison after reading the whole file**. Prefix matching was a misjudgment from the algorithm-error era, removed together; real corruption at any position still hard-fails.

### 🔒 9 security hardening items

| Location | Fix |
| ---------------------------------------------- | -------------------------------------------------------------------- |
| `Criptografia.StripNullCharacters` | Rewritten as `Replace(vbNullChar, "")`, eliminates positional offset errors from char-by-char concatenation |
| `Criptografia.AES_EncryptString/DecryptString` | Failures return `Nothing` instead of empty string; persistence points (Configuracion/Fichero) skip writes on encryption failure and keep old values, no longer silently cleared |
| AES ciphertext format | New random-IV format `{1}||IV||ciphertext`, automatically compatible both ways with old format |
| `FileDownloader` | MetaMAC mismatch throws and resets chunk retries (with this algorithm fix, no more false positives) |
| `Criptografia.GetFileKeyFromPreSharedKey` | PSK with non-ASCII chars (>255) logged and returns `Nothing`, prevents keystream misalignment |
| `WebInterfaceModule` | Web password storage changed to random salt + PBKDF2 (100k rounds), replacing unsalted MD5 |
| `StreamingModule` / `StreamingLibraryModule` | Password comparison changed to `Criptografia.FixedTimeEquals` constant-time comparison, prevents timing side channels |
| `ClientConnected` reflection | Static `MemberInfo` cache + null checks + Try/Catch, degrades to "assume connected" on failure |

### 📦 Version numbers

- Assembly / FileVersion → `2.4.4.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.4`

- `docs/version.xml` → `2.4.4.0`

***

## \[2.4.3\] - 2026-08-26

### ✨ New (Issue #1)

| Feature | Description |
| ------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 7z extract support | SharpCompress does not support 7z containers (previously all .7z extracts necessarily failed). Now prefers the system-installed 7-Zip CLI, and when absent automatically extracts the bundled 7zr.exe (public domain, official slim 7-Zip build) to `%LOCALAPPDATA%\MegaDownloader\bin`. Passwords and multipart volumes (.7z.001/.002/...) supported |
| Web server LAN push | Settings → Web Server adds an "Allow LAN access" switch (default off). Still binds only 127.0.0.1 by default; when enabled binds all adapters so phones/LAN devices can push downloads via browser. Enabling requires a server password (≥8 chars), validated both on config save and server start |
| LAN custom bind IP | After "Allow LAN access" is enabled, a new "Bind IP (empty=all)" box appears: empty listens on all adapters; a specific IP (e.g. `192.168.1.100`) listens only on that adapter, precisely controlling exposure on multi-adapter/virtual-adapter machines. IP format double-validated on save and start, illegal addresses refuse to start with an error |

### 🐛 Fix: clipboard monitor missed web copies (Issue #1)

**Symptom**: copying MEGA links from web pages did not pop the add dialog, only Ctrl+C inside some apps worked.

**Root cause**: browsers (Chrome/Edge/Firefox) use **delayed rendering** — clipboard-change notifications arrive before data is actually written; immediate reads get empty values or throw `CLIPBRD_E_CANT_OPEN` (clipboard still held by the source process).

**Fix** ([Main.vb](../Forms/Main.vb) / [ClipBoardViewer.vb](../Clases/ClipBoardViewer.vb)):

- Reads changed to retry: up to 5 attempts, 150ms apart, covering delayed rendering and clipboard-busy races

- All clipboard accesses in `WndProc` wrapped with exception protection, transient failures no longer break the message loop

- Failed mark-back writes after handling degrade to ignored, no longer crash

### 🔒 7z extract safety details

- Before extract, `l -ba -slt` lists all entries, PathGuard validation rejects path escapes (Zip Slip), extract runs only after validation passes

- CLI password (`-p`) never written to logs

- Child-process stderr read asynchronously, avoids pipe-buffer deadlock

- Exit codes 0/1 (success/warning) pass, 2 (fatal, e.g. wrong password) throws a friendly error with output tail

***

## \[2.4.2\] - 2026-08-19

### 🐛 Fix: real corruption of downloaded files (confirmed by user testing)

**Symptom**: downloads completed with exactly matching file sizes, but contents were corrupted and unusable. Logs proved old versions (including v2.4.1) still renamed after MetaMAC checksum failures with "warn and pass", landing corrupted files directly.

**Root causes** (three independent defects combined):

1. **Wrong MetaMAC chunk schedule**: v2.4.1 used a "128K doubling with 8 MiB/1 MiB dual caps" schedule, inconsistent with the real MEGA SDK `ChunkedHash::chunkfloor/chunkceil` schedule, so many legitimate files were misjudged as mismatch (also giving the downstream pass-through logic an excuse)
2. **Mismatch pass-through policy**: on top of the wrong algorithm, v2.4.1 rolled "mismatch means failure" back to "log a warning, complete rename as usual" — verification was a no-op, real corruption (e.g. hole writes during 403 windows after URL expiry) passed straight through
3. **Non-aligned resume caused CTR keystream misalignment**: best-effort flush on connection breaks persisted progress not aligned to 16 bytes; `SeekToFileOffset` on retry can only position keystreams per whole block, so all data after the misalignment decrypted misaligned — the direct cause of "correct size but corrupted content"

**Fixes**:

| Location | Fix |
| ----------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| `Criptografia.ComputeMegaFileMac` | Chunk schedule changed to MEGA SDK linear boundaries: 128 KiB x i (i=1..8, i.e. 128/256/384/512/640/768/896 KiB), then fixed 1 MiB; dual-cap fallback removed, single computation |
| `Criptografia.VerifyMegaMetaMac` | Empty files directly return (0,0) (MEGA empty-file MetaMAC is 0) |
| `FileDownloader.downloadFile` | MetaMAC mismatch logged as error but completion continues per MEGA SDK lenient policy (SDK is equally lenient with historic "MAC missing tail entries"); exact file-size check kept as a hard gate |
| `FileDownloader.FlushToDisk` | Interrupted flush aligns persisted progress down to 16-byte boundaries, eliminating non-aligned resume points (decrypted data <16 bytes automatically refetched on retry) |
| `ChunkDownloader_DoWork` | Resume start alignment verified before requesting: non-16-byte-aligned resume starts abort the chunk, preventing keystream misalignment |
| `DataPart.ValidateAndNormalize` | Legacy non-aligned XML progress from old versions automatically rolled back to 16-byte boundaries at startup |
| `FileDownloader.downloadFile` (introduced in v2.4.1) | Kept: removed "force-finish when file sizes match"; completion only when all real chunks finish; 120-second timeout reports failure and keeps resume points |

### 📦 Version numbers

- Assembly / FileVersion → `2.4.2.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.2`

- `docs/version.xml` → `2.4.2.0`

***

## \[2.4.1\] - 2026-08-15

### 🐛 Fix: downloads completed but showed errors (confirmed by user testing)

**Symptom**: files popped errors at 100%, `.part` files were not renamed; manually stripping the `.part` suffix produced usable files, proving downloads were actually complete.

**Root causes**: the MEGA MetaMAC integrity checksum introduced in v2.2.0 systematically false-positived:

1. **Single-file public links necessarily false-positived**: public-link FileKeys are only 16 bytes (4 words, just the AES key itself), **without MetaMAC**; but `VerifyMegaMetaMac` required at least 8 words (32 bytes, with nonce + MetaMAC), returning False directly → completion path threw "Integrity check failed" → error status, no rename. Only 32-byte node keys returned by the folder API truly contain MetaMAC, so false positives concentrated in the most common single-file-link scenario
2. **Boundary-rule differences for 8 words keys could also false-positive**: MEGA clients historically varied MAC chunk-boundary rules, mismatch does not equal corruption (exact size check is stronger evidence)

**Fixes** (two layers):

| Location | Fix |
| -------------------------------- | ------------------------------------------------------------------- |
| `Criptografia.VerifyMegaMetaMac` | 4 words public-link keys log "no MetaMAC to verify" and skip (return True), no longer misjudged as failures |
| `FileDownloader.downloadFile` | MetaMAC mismatch for 8 words keys downgraded to log warnings with completion rename continued; exact file-size checksum (still errors on mismatch) kept as the primary integrity defense |

### 📦 Version numbers

- Assembly / FileVersion → `2.4.1.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.1`

- `docs/version.xml` → `2.4.1.0`

***

## \[2.4.0\] - 2026-08-14

### 🐛 Comprehensive bug fixes - 21 confirmed issues

Line-by-line review of all v2.3.0 project code, fixing 21 bugs confirmed with exact code evidence, covering user-visible errors, deadlock / resource leak, concurrency defects, swallowed exceptions, security debt and dead code.

### I. User-visible errors

| Fix | Description |
| --------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| Background thread MessageBox stuck downloads | `FileDownloader.bgwDownloader_DoWork` Catch blocks no longer call `MessageBox.Show` on thread-pool threads, now report failures via `ReportProgress(FileDownloadFailedRaiser)` for the UI thread to present uniformly |
| Cross-thread MsgBox crash during shutdown | Three `BackgroundWorker.DoWork` exception branches in `Main.vb` no longer `MsgBox` directly, new `SafeShowError` helper checks `IsDisposed`/`IsHandleCreated` and marshals back to the UI thread via `Invoke` |
| 7z multipart extract crash | Two `NotImplementedException` in `DescompresorController` changed to `NotSupportedException` with friendly messages; `DescompresionFinalizada` event extended to carry error messages, users see concrete causes in error states |
| Fallback completion skipped MD5 checksum / extract | `Fichero.ActualizarDatosDescarga` fallback status correction no longer just flips status, now calls the full `downloader_Completed` flow (with MD5 checksum and auto-extract), avoiding "shows complete but integrity never verified" |

### II. deadlock and resource leak

| Fix | Description |
| ---------------------- | ----------------------------------------------------------------------------------------- |
| mutex without Try/Finally deadlock | Two mutex sites in `Main.AgregarPaquete` and `bgwComprobarMaxConexiones` add `Try/Finally`, exceptions in between no longer cause permanent deadlock |
| Download workers never Disposed | `FileDownloader.Dispose` loops to release all workers in `listDownloaders`, no longer just `CancelAsync` |
| bgArranque worker leak | `Fichero.Dispose` newly releases `bgArranque` (download-startup worker), interrupted startups during shutdown no longer leak |
| ELCForm 300ms busy poll | Changed to `AutoResetEvent` event-driven, zero CPU when idle, immediate response when tasks arrive |

### III. Concurrency and logic defects

| Fix | Description |
| -------------------- | ----------------------------------------------------------------------------------- |
| AJAX response concurrency contamination | `StreamingLibraryModule._RespuestaAjax` changed to `AsyncLocal(Of String)`, concurrent HTTP requests no longer overwrite responses |
| FlushFinalBlock exception swallowed | Empty Catch on `ServerEncoderLinkHelper.Cipher` decrypt path changed to `Log.WriteError` |

### IV. Swallowed exceptions (hiding real failures)

| Fix | Description |
| ------------------ | ----------------------------------------------------------------------------------------- |
| FlushToDisk disk errors swallowed | Empty Catch around `FlushToDisk` in `FileDownloader.ChunkDownloader_DoWork` changed to logging |
| Server error-response read failures swallowed | Two empty Catches in `Fichero.downloader_FileDownloadFailed` / `downloader_ChunkDownloadFailed` changed to logging |
| Extract-cancel exceptions swallowed | Empty Catch around `RequestCancel` in `Main` shutdown flow changed to logging |

### V. Security and tech debt

| Fix | Description |
| ------------------------- | --------------------------------------------------------------------------- |
| DPAPI entropy hardcoded | `Criptografia` DPAPI entropy derived from assembly-identity SHA256, legacy entropy kept for decrypting old data |
| ZIP password hardcoded "passZIP" | `Fichero` ZIP extract password encryption changed to DPAPI, decryption tries DPAPI first then falls back to old AES for old queue files |
| OptionalPassword dead field | Deleted `Cache.OptionalPassword` field and XML output (declared but never assigned, never read) |
| RandomNumberGenerator never released | `ServerEncoderLinkHelper` `RandomNumberGenerator.Create()` wrapped in `Using` |
| Logs without UTC timestamps | `Log` timestamps all changed to `DateTime.UtcNow` (with `Z` suffix), new 30-day log retention cleanup policy |

### VI. Dead code cleanup

| Fix | Description |
| ------------------ | -------------------------------------- |
| Criptografia commented dead code | Deleted commented-out `DecryptFile` and `cipherData` functions |
| Conexion dead code | Deleted commented `GetAppID` and uncalled `LeerNodo` functions |

### 📦 Version numbers

- Assembly / FileVersion → `2.4.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4`

- `docs/version.xml` → `2.4.0.0`

***

## \[2.3.0\] - 2026-08-13

### 🐛 Stability fixes

Fixed a batch of crashes, resource leak and potential deadlock issues based on code review.

| Fix | Description |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| AES encryption-failure crash | `AES_EncryptString` still Base64-converted `Nothing` after encryption exceptions causing secondary throws; now returns empty string on failure and releases `RijndaelManaged`/`CryptoStream`/`MemoryStream` with `Using` |
| AES decryption truncation / bad-input crash | `AES_DecryptString` `Convert.FromBase64String` moved into exception handling; plaintext fully read with `CopyTo` (original single `Read` could truncate); returns empty string on failure |
| Download-item resource leak | `Fichero.Dispose` changed from empty implementation to releasing `FileDownloader` and nulling it |
| Potential deadlock | `FileInfo.Size` setter `ReleaseMutex` placed in `Try/Finally`, exceptions inside the loop no longer cause permanent deadlock |
| Registry handle leak | `RegisterInStartup` registry keys released with `Try/Finally` + `Close()` |
| Dangerous `Thread.Abort` | DLC handling 30-second timeout no longer hard-kills threads, changed to cooperative failure marking with workers ending naturally |

### 📦 Version numbers

- Assembly / FileVersion → `2.3.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.3`

- `docs/version.xml` → `2.3.0.0`

## \[2.2.1\] - 2026-08-09

### 🐛 Download status fixes

Fixed two user-reported download-completion status display issues.

### Bug 1: multi-file downloads complete (100%) but show errors

- **Root cause**: the `Finally` block in `FileDownloader.downloadFile()` reported `FileDownloadFailedRaiser` whenever `exc` was non-empty. Even when all chunks completed successfully (`AllFinished = True`), earlier non-fatal exceptions still triggered failure events, wrongly setting status to `Erroneo`.

- **Fix**: check `AllFinished` in the `Finally` block; when the download actually completed, clear `exc` and only log a warning instead of reporting failure.

### Bug 2: single-file downloads complete (100%) but still show "downloading"

- **Root cause**: the `Completed` event only fired in `bgwDownloader_RunWorkerCompleted`; if the wait loop could not exit due to a race, `Completed` never fired and status stayed `Descargando`.

- **Fix**: three-layer protection

  1. **Event layer**: new `FileDownloadSucceeded` handler, sets `Completado` immediately after file verification and rename succeed
  2. **Loop layer**: wait loop adds 60-second timeout check, force-completes when on-disk file size matches; 120-second hard timeout prevents deadlock
  3. **Timer layer**: `ActualizarDatosDescarga` adds fallback check, auto-corrects status when progress is 100% and `AllFinished`

### 📦 Version numbers

- Assembly / FileVersion → `2.2.1.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.2.1`

- `docs/version.xml` → `2.2.1.0`

***

## \[2.2.0\] - 2026-07-20

### Security hardening and download integrity

Completed path safety (P0), download integrity (P1) and a batch of reliability / release-modernization (P2/P3) fixes based on deep static-audit findings.

### 🔒 Path safety (P0)

- Unified `PathGuard`: remote file/dir names, extract entries, deletes and write-outs all constrained inside the canonical download root

- Fixed Zip Slip: validate all entries before extract, reject `../`, absolute paths, device names and other escapes

- Fixed MEGA folder path concatenation and task-deletion out-of-range risks

### 📦 Download integrity and reliability (P1)

- Verify **MEGA MetaMAC** before download completion; failures are not renamed to final files

- HTTP Range: validate Partial Content / Content-Range; reject erroneous responses ignoring Range

- Early EOF treated as failure; CTR counter uses Int64 seek, fixing large-offset risks

- Resume metadata validation, avoids "fake completion" when `.part` files are missing

- Atomic saves for config and download queue (`AtomicFile`); HTTP default timeouts; log redaction

- Remote Web: Stop/Play/AddLink changed to POST + CSRF; Streaming media URLs pinned to loopback

- Extract cooperative cancel (removes Thread.Abort), extract results split into success/failure, resource quotas

- Shutdown order: stop Web first → cancel workers/extract → stop downloads → then save

### ✨ Experience and engineering (P2/P3)

- Config model-layer caps (buffer / connection counts / speed), free-disk prechecks, filename-conflict and progress divide-by-zero guards

- Languages: built-in packs separated from user customizations, missing keys fall back to en-US

- Single-instance IPC writes per line, avoids link-parameter sticking; theme Auto follows system in real time

- Removed production xUnit dependency and MPRESS Release post-processing; DPI PerMonitorV2

- Normalized version comparison; DLC entry marked as discontinued (ELC kept)

### 📦 Version numbers

- Assembly / FileVersion → `2.2.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.2`

- `docs/version.xml` → `2.2.0.0`

***

## \[2.1.0\] - 2026-07-19

### Theme polish - dark-mode usability fixes

Based on the v2.0 theme framework, fixed key visuals for the main list, progress bars, button borders, context menus and more in dark mode, making the Dark theme truly usable.

### 🐛 Fixes

- **Main download list zebra stripes**: `FormatRow` no longer hardcodes `White`/`Honeydew`, now uses `ThemeManager` `Back`/`AltBack`

- **Progress-bar colors**: `BarRenderer` no longer uses Azure/SpringGreen, now uses theme tokens (`ProgressBack`/`ProgressFill` and others)

- **Status foreground colors**: error / completed rows use `ErrorFore`/`SuccessFore` (brighter red/green in dark mode)

- **Instant skin change after saving settings**: Configuration calls `Main.ApplyCurrentTheme()` after saving theme, no restart needed

- **Button white borders**: `FlatStyle.Standard` system 3D highlights render as white borders in dark mode; changed to `FlatStyle.Flat` + theme `Border`/`ButtonHover`/`ButtonPressed`

- **GroupBox / TabPage**: flat borders and `UseVisualStyleBackColor = False`, reducing system light-color outlines

- **ELC account table**: removed Azure/Snow/SeaShell hardcodes; empty-list hints use theme foreground colors

- **Context menus**: theming `ContextMenuStrip` on Forms via reflection; completed `ThemeColorTable` properties such as `ToolStripDropDownBackground`

- **Unthemed dialogs**: Stegano wizard, SplashScreen, Cerrando `ApplyTheme` on Load

### ✨ Improvements

- `ThemeManager.GetColor(key)` public color-fetch API

- New semantic / interactive tokens: `ErrorFore`, `SuccessFore`, `Progress*`, `ButtonHover`, `ButtonPressed`

- `ToolStripBorder` correctly uses the `ToolBorder` token

### 📦 Version numbers

- Assembly / FileVersion → `2.1.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.1`

- `docs/version.xml` → `2.1.0.0`

***

## \[2.0.0\] - 2026-07-13

### Major release - security hardening + code cleanup + dark theme

Based on the v1.9 link-format fixes, further completed 60+ fixes in 4 phases, significantly improving security, stability and usability. First release with dark/light theme switching.

### ✨ New

- **Dark/light theme switching**:

  - New `ThemeModeType` enum (Auto/Light/Dark), default Auto follows system ([`Clases/ConfiguracionUI.vb`](../Clases/ConfiguracionUI.vb))

  - New `ThemeManager` class, detects system light/dark via registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` ([`Clases/ThemeManager.vb`](../Clases/ThemeManager.vb))

  - Custom `ThemeColorTable` + `ToolStripProfessionalRenderer` renderer, covering 30+ ToolStrip gradient / border / selection color properties

  - Recursively applies themes to all controls, including the main window `BrightIdeasSoftware.TreeListView` (download list), StatusStrip, ContextMenuStrip, TableLayoutPanel, DataGridView, ListView, TreeView, ProgressBar and more

  - 9 child dialogs apply themes in Load events: Credits, AddLinks, ELCForm, EncodeLinksForm, PropiedadesDescarga, StreamingForm, Descompresor, PantallaMsg, Configuration

  - 10 language files add `Theme` / `Theme_Auto` / `Theme_Light` / `Theme_Dark` translation keys

- **Author info**: Credits dialog adds "Yingxue - Revival maintainer (v2.0+)"

- **Update check**: redirected to this GitHub repository

### 🐛 Fixes

**P0 critical security issues**:

- Fixed empty-password bypass of validation logic

- Fixed proxy credentials never actually assigned to WebProxy

- TLS 1.2 only (removed TLS 1.0/1.1, meets modern security standards)

- Web/Streaming servers bind `127.0.0.1` (was `0.0.0.0` exposed to the whole network)

- Password hashes uniformly use UTF-8 encoding

- All mutex operations wrapped in Try/Finally to prevent deadlock

- Empty Catch blocks in encryption code replaced with logging

- Fixed `ApagarPC` / `MaxConexionesGuardadas` settings not persisted correctly

- Fixed downloader `NullReferenceException` crash (variable-name mismatch `exc` vs `ex`)

**P1 resource leak**:

- Fixed 7 ToolTip resource leak sites (`ELCAccountControl` / `AddLinks` / `SteganoWizardSave`, created on every MouseHover and never released)

- Fixed `SteganoManager` `Image.FromFile` locking source files + FileStream not Disposed

- Fixed 7 `Image.FromStream(stream)` premature-stream-close sites in `Main.vb`, new `LoadEmbeddedImage` helper

- Fixed `WebInterfaceModule` StreamReader/StreamWriter without Using (template loading + response.Body writes, the latter with `leaveOpen:=True`)

- Fixed `StreamingLibraryManager` `CompressString` / `UnCompressString` without Using (nested Using blocks)

- Fixed `MegaURIProtocol` registry operations without Finally release + intermediate variable overwrite causing handle leaks

- Fixed `Main.vb` `clipChange` shutdown order (Uninstall before DestroyHandle)

- Fixed `Main.vb` `EsperarParadaDescargasYWorkers` missing `bgwDescompresorCompleted` check

- Fixed `StreamingLibraryModule` `Case "Delete"` missing `Return True`, causing fall-through to the next branch

- Fixed `StreamingLibraryModule` `UsuarioLogueado` never clearing session after timeout, login state stuck permanently

- Fixed `ELCAccountControl` `CellClick` without `e.RowIndex` validation, clicking headers crashed

- Fixed `StreamingHelper` `Keys.Count / 2` float division, should use integer division `\ 2`

**P2 protocol modernization**:

- `%SEQ%` / `%ID%` sequence numbers previously used `DateTime.Now.Millisecond` ticks (range 0-999, duplicated under concurrent requests), changed to `Interlocked.Increment` in-process increment

- In `MegaFolderHelper.vb`, `http://mega.co.nz/#N!` → `https://mega.nz/#N!`

**P2 code quality**:

- `Paquete.vb` / `Configuracion.vb` compared config XML with `GetHashCode` (not guaranteed consistent), changed to direct `OuterXml` string comparison

- Two variables `ex` (Regex) in `MegaFolderHelper.vb` → `rx` (avoids confusion with `Catch ex`)

- `ThrottledStream.vb` variable `int` (VB.NET keyword) → `bytesRead`

- `Clases/Mutex.vb` class name shadows `System.Threading.Mutex`, comments added + alias alternative documented

- `StreamingModule.ClientConnected`, `FileDownloader` Range-header reflection annotated for necessity

- `LibraryElement.ToJSON` hand-built JSON concatenation annotated with limitations

### 🗑️ Removed

- **4 Crypters**: `EncrypterMega.vb`, `MegaCrypter.vb`, `Youpaste.vb`, `LinkCrypter.vb` (APIs all offline)

- **3 MovieInfo**: `Allocine.vb`, `Filmaffinity.vb`, `IMDB.vb` (APIs all changed)

- **Link helpers**: `DLCHelper.vb`, `Linkdecrypter.vb`, `LinkProtectors.vb`, `Serializer.vb`, `ClipboardChangeNotifier.vb`

- **MegaUploader menu**: removed "Get MegaUploader" menu item

- **goo.gl short links**: all 14 Google short links replaced with GitHub direct links

- **Ping reporting**: removed reporting user/version info to the original author server (privacy protection)

- 11 `.vb` files deleted in total + all related references cleaned up

### ⚠️ Known issues

- `Thread.Abort()` dangerous use (3 sites, Main.vb / DescompresorController)

- Cross-thread MsgBox without checking whether forms are closed (3 sites)

- `MegaFolderHelper.FillFolderStructure` recursion without KeyNotFound protection

- `ELCForm` infinite loop polling every 300ms

- `ServerEncoderLinkHelper` RandomNumberGenerator not Disposed

- `FileDownloader.FlushToDisk` FileStream untimely release

### 📦 Build artifacts

- `MegaDownloader.exe` main program

- Dependency DLLs: `BouncyCastle.Crypto.dll`, `Newtonsoft.Json.dll`, `SharpCompress.dll`, `ObjectListView.dll`, `HttpServer.dll`, `Fadd.dll`, `F5Lib.dll`, `xunit.dll`

***

## \[1.9.1\] - 2026-07-05

### 🐛 Fixes

- **Downloader crash**: fixed `NullReferenceException` from variable-name mismatch at lines 681-683 in [`Clases/FileDownloader.vb`](../Clases/FileDownloader.vb). When MEGA servers returned 502 gateway errors and similar exceptions, the catch block wrongly referenced the cleared `exc` local (should be `ex`), hiding the real exception and aborting the whole download flow.

***

## \[1.9.0\] - 2026-07-05

### First public release of the MegaDownloader revival plan

Fixed and refactored from the MegaDownloader v1.8 decompiled source, with the core goal of restoring support for new MEGA link formats.

### ✨ New

- **URL parsing**: added 4 regexes to `patternHTTPURI` in [`Clases/URLExtractor.vb`](../Clases/URLExtractor.vb), supporting these new MEGA links:

  - `https://mega.nz/file/<FileID>#<FileKey>`

  - `https://mega.nz/folder/<FolderID>#<FolderKey>`

  - `https://mega.co.nz/file/<FileID>#<FileKey>`

  - `https://mega.co.nz/folder/<FolderID>#<FolderKey>`

- **Folder recognition**: `IsMegaFolder` method updated together

- **TLS 1.2/1.3**: explicitly enabled `Tls12 | Tls11 | Tls` protocols in [`Clases/Conexion.vb`](../Clases/Conexion.vb)

- Added developer docs for this repository: [README.md](../README.md), [CONTRIBUTING.md](CONTRIBUTING.md), [CHANGELOG.md](CHANGELOG.md), `.gitignore` and others

### 🐛 Fixes

- Fixed new MEGA links copied from clipboard not being recognized

- Fixed dragging new MEGA links from browsers to the main window not working

- Fixed new folder links failing to resolve into child file lists

- **Fixed Base64 decode errors when downloading folders**: `mega.nz/folder/` links containing files shared by multiple users get `fileN.k` fields from the MEGA API in the form `handle1:key1/handle2:key2[/handle3:key3]` (multiple `handle:key` pairs separated by `/`). The original `fileN.k.Substring(fileN.k.IndexOf(":") + 1)` treated everything after the first `:` (including `/handle2:key2`) as the key, so `Convert.FromBase64String` threw FormatException. Fix: new `ExtractKeyFromK` helper function.

### 🔄 Changed

- `TargetFrameworkVersion` stays `v4.8` (v1.8 had already upgraded to 4.8)

- Repository LICENSE stays MIT, with revival-plan copyright notices added

### ⚠️ Known issues

- EncrypterMe.ga links currently unresolvable because its official API service (`http://encrypterme.ga/api`) is offline

- Some goo.gl short links cannot redirect because Google shut the service down

- The Simplified Chinese language pack still needs translation for some entries

### 📦 Build artifacts

- `MegaDownloader.exe` main program

- Dependency DLLs: `BouncyCastle.Crypto.dll`, `Newtonsoft.Json.dll`, `SharpCompress.dll`, `ObjectListView.dll`, `HttpServer.dll`, `Fadd.dll`, `F5Lib.dll`, `xunit.dll`

***

## \[1.8.0\] - Original (decompiled source)

The original version on which the revival plan is based; its source was obtained via decompilation as the fixing starting point.

### Main features

- Multi-thread concurrent downloads

- MEGA folder recursive resolving

- Encrypted links (`enc`/`enc2`/`fenc`/`fenc2`/`elc`) support

- Third-party Crypter integrations (MegaCrypter, YouPaste, LinkCrypter, EncrypterMe.ga)

- VLC streaming media play-while-downloading

- Built-in HttpServer Web management UI

- SharpCompress auto-extract

- Multi-language UI (10 languages)

- Stegano steganography

- Automatic update checks

***

## Version Numbering Notes

- Major: major feature changes or backward-incompatible changes

- Minor: new features, backward compatible

- Patch: bug fixes, backward compatible
