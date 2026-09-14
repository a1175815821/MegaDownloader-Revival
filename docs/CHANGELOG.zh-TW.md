# 變更日誌

本專案所有重要變更均記錄於此。格式參考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本號遵循 [語義化版本](https://semver.org/lang/zh-CN/)。

*面向使用者的版本亮點摘要見 [GitHub Releases](../../../releases)。*

---

## 版本亮點速覽

| 版本 | 日期 | 主題 |
| --- | --- | --- |
| 2.5.1 | 2026-09-14 | 修補版：斷點續傳保得住、限速單位修正、兩批稽核跟進修復 |
| 2.5.0 | 2026-09-14 | 正式版：線上觀看並發修復 + 版本號轉正進更新通道 + 4 語言文件補齊 |
| 2.5 RC2 | 2026-09-13 | 生產稽核三批修復：發佈阻塞 / 可靠性 / 體驗與縱深 |
| 2.5 RC1 | 2026-09-12 | MEGA 配額熔斷 + 倒數計時橫幅；失敗自癒預設開啟 |
| 2.4.7 | 2026-09-12 | 更新提醒三選項；移除廢棄搜尋引擎整合 |
| 2.4.6 | 2026-09-04 | 假成功 / 靜默失敗專項 |
| 2.4.5 | 2026-09-02 | 全面程式碼審查後的系統性修復（16 項） |
| 2.4.4 | 2026-09-01 | 子資料夾連結下載；MetaMAC 分塊初值修復；9 項安全加固 |
| 2.4.3 | 2026-08-26 | 7z 解壓縮；Web 區域網路推送；剪貼簿漏檢修復 |
| 2.4.2 | 2026-08-19 | 修復下載檔案真實損壞（MetaMAC / 續傳對齊） |
| 2.4.1 | 2026-08-15 | 修復下載完成但顯示錯誤 |
| 2.4.0 | 2026-08-14 | 21 項 bug 修復（安全 / 洩漏 / 並發 / 死程式碼） |
| 2.3.0 | 2026-08-13 | 加密失敗崩潰、資源洩漏、移除 `Thread.Abort` |
| 2.2.x | 2026-07~08 | 路徑安全、下載完整性、原子組態儲存、Web CSRF |
| 2.1.0 | 2026-07-19 | 深色主題可用性修復 |
| 2.0.0 | 2026-07-13 | 4 階段 60+ 項修復；深/淺色主題；程式碼清理 |
| 1.9.x | 2026-07-05 | 修復新版 MEGA 連結格式識別 |
| 1.8.0 | 原版 | 反組譯原始碼，復活計畫的起點 |

---

## v2.4 系列詳細變更

下面是 v2.4 各版本的變更明細（同一版本在上方版本歷史中也有條目）。

### 變更明細

#### 更新提醒與搜尋引擎清理(v2.4.7)

| 變更                   | 說明                                                                                          |
| -------------------- | ------------------------------------------------------------------------------------------- |
| 更新提醒三選項             | 是=立即更新;否=3 小時後再提醒;取消=不再提醒目前版本(`UpdateSkipVersion` 按版本記錄,新版本發佈後自動恢復提醒) |
| 移除搜尋引擎整合           | "尋找"選單 4 個網域(megafiles.me/megafindr/megasearch.co 等)已全部下線;`mega://mega-search?` 連結解析同步移除 |

#### 假成功/靜默失敗修復(v2.4.6)

| 修復                     | 說明                                                                                             |
| ---------------------- | ---------------------------------------------------------------------------------------------- |
| 重啟後假成功(P1)          | `Verificando`/`Descomprimiendo` 被標 `Completado`,但兩者都可能一字節未落盤;統一回 `EnCola` 靠斷點續傳繼續          |
| 設定儲存假成功           | `GuardarXML` 失敗只記底層日誌,UI 照彈成功;現檢查 `ErrorConfig` 失敗則彈錯停留                                  |
| 大寫連結靜默丟棄          | 匹配 IgnoreCase 但校驗大小寫敏感,`HTTPS://MEGA.NZ/...` 無反應;6 處正則 + 前綴比較全部補齊大小寫不敏感              |
| 畸形 enc 崩潰            | base64url 長度 %4==1 拋 `ArgumentOutOfRangeException` 裸崩;提前判定彈友善錯誤                                    |
| 資料夾 API 空回應 NRE     | 畸形回應(空字串/代理 HTML)拋 NRE;加 Try/Catch + 空檢查,統一報"無效伺服器回應"                                    |
| ELC 子範圍保留           | `MegaLink` 增子範圍欄位,編碼時追加 `/folder/子ID` 或 `/file/檔案ID` 後綴;舊版解碼器忽略後綴=歷史行為,新版恢復子範圍   |
| 語言缺鍵                | en-US/zh-CN 各 +5(ELC 成功提示、URL 必填、VLC 路徑無效、開啟 ELC 選單、組態儲存失敗)                             |

#### 穩定性與安全(v2.4.5)

| 修復             | 說明                                                                                       |
| -------------- | ------------------------------------------------------------------------------------------ |
| 全域異常兜底       | 此前無任何兜底,UI 異常直接閃退;現在記日誌且不結束,背景執行緒異常留日誌                           |
| 清單重新整理閃退       | 4 個 AspectGetter 報錯時每行重繪彈一次窗再崩潰;改為記日誌返回佔位值                              |
| 缺分卷 RAR 假成功   | `IsComplete=False` 靜默跳過且上游報"解壓縮成功";現顯式拋錯                                       |
| UI 凍結         | 分塊失敗退避的忙等跑在 UI 執行緒(最長 16.5 秒);移到執行緒池                                          |
| Streaming Range | RFC 7233 合規:後綴/開放區間、416 回應、`bytes=0-0` 不再拉整個檔案、回應體不超發 Content-Length        |
| 串流函式庫 CSRF     | Delete/Save/OpenVLC/Import/Export 要求 POST + token(複用 Web 介面 EnsureCsrf 模式)              |
| 登入限速          | 並發上限 4 + 60 秒視窗失敗鎖定 10 次,防 PBKDF2 POST 轟炸打滿執行緒池                                |
| Stegano 落盤安全   | 記憶體編碼+校驗通過才寫盤(不再留寫壞的 .jpg);`WriteAllBytes` 截斷覆寫(不再拼接舊檔案尾部)             |
| 資源洩漏          | FileDownloader 句柄、`CreateDecryptor`/MD5 Using、5 處 互斥鎖 Try/Finally、5 處懸停 ToolTip       |
| 維護性           | vbproj 死引用、DPI 組態統一 PerMonitorV2、硬編碼英文訊息接入語言系統(新增 en-US/zh-CN 條目)          |

#### 新功能與完整性(v2.4.4)

| 功能/修復          | 說明                                                                                                                                                      |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 子資料夾連結下載       | `mega.nz/folder/根ID#金鑰/folder/子ID` 僅下載指定子資料夾(路徑重定基);`/file/檔案ID` 僅下載指定檔案——此前一律下載整個根資料夾                                                                  |
| MetaMAC 分塊初值修復 | 分塊 CBC-MAC 初值由零 IV 改為檔案 nonce 複製兩份 `[n0,n1,n0,n1]`(對齊 SDK `SymmCipher::ctr_crypt`),修復 8 words key 下載完成後必然誤報校驗錯誤                                         |
| MetaMAC 標準校驗   | 移除"分塊邊界前綴匹配"寬容邏輯,與 SDK 一致:讀完整個檔案後一次性完整比較                                                                                                                |
| 9 項安全加固        | StripNullCharacters 偏移修復;AES 失敗返回 Nothing 且持久化點保留舊值;密文新增隨機 IV 格式(相容舊資料);Web 密碼 PBKDF2(100k)+隨機鹽;Streaming 恆定時間密碼比較;PSK 非 ASCII 校驗;ClientConnected 反射健壯化 |

#### 新功能(v2.4.3)

| 功能        | 說明                                                                          |
| --------- | --------------------------------------------------------------------------- |
| 7z 解壓縮     | 優先系統 7-Zip,未安裝時自動釋放內建 7zr.exe(公有領域);支援密碼與 multipart 分卷;解壓縮前 PathGuard 校驗防路徑逃逸 |
| Web 區域網路推送 | 「允許區域網路存取」開關(預設關),開啟後手機/區域網路裝置可經瀏覽器推送下載;強制密碼保護;支援自訂綁定 IP(留空=全部網卡)            |
| 剪貼簿監控修復   | 瀏覽器延遲渲染 + 剪貼簿佔用競態導致網頁複製漏檢——改為重試讀取,全部存取加異常保護                                 |

#### 下載完整性(v2.4.2)

| 修復          | 說明                                                                     |
| ----------- | ---------------------------------------------------------------------- |
| MetaMAC 演算法  | 分塊排程對齊 MEGA SDK `ChunkedHash`:128 KiB × i(i=1..8)後固定 1 MiB;空檔案返回 (0,0) |
| 下載完成判定      | 移除"檔案大小匹配即強制完成";僅真實 chunk 全部完成才判定完成;120 秒逾時上報失敗並保留斷點                   |
| CTR 金鑰流錯位防護 | 中斷 flush 與續傳起點強制 16 字節對齊;啟動時回退舊版遺留的非對齊進度——杜絕"大小正確但內容損壞"                |

#### 穩定性(v2.4.0/2.4.1)

| 修復       | 說明                                                |
| -------- | ------------------------------------------------- |
| 背景執行緒彈窗卡死 | 下載失敗改經 UI 執行緒呈現;關閉期間跨執行緒 MsgBox 加 `IsDisposed` 防護   |
| 並發污染     | Streaming 模組 AJAX 回應改 `AsyncLocal`,多請求互不串擾        |
| 資源洩漏     | 互斥鎖 `Try/Finally` 釋放;`BackgroundWorker.Dispose` |
| 公開連結誤報   | 4 words key 無 MetaMAC 時跳過校驗(記日誌),不再誤判失敗           |

---

## [2.5.1] - 2026-09-14

修補版。相對 2.5.0 的兩批稽核跟進：自癒上線後的迴歸 + 其餘使用者可感知的明顯問題。一句話：**續傳保得住，限速說了算**。

### 🐛 第一批：看門狗、AddLinks、7z 配額、限速、隱身連結

([FileDownloader.vb](../Clases/FileDownloader.vb) / [Fichero.vb](../Clases/Fichero.vb) / [Main.vb](../Forms/Main.vb) / [AddLinks.vb](../Forms/AddLinks.vb) / [DescompresorController.vb](../Clases/DescompresorController.vb) / [Configuration.vb](../Forms/Configuration.vb))

- 120 秒總時長牆鐘→無進度超時：有分塊推進就重置計時，大檔案不再無條件失敗；自癒與配額喚醒保留分塊與 `.part`，重試續傳不再循環重下
- 「線上觀看」取消不再鎖死按鈕：共用解析續體同時復位兩個按鈕 + 進行中旗標（此前復位錯按鈕、旗標永不清）
- 下載目錄不存在時建完繼續新增，第一下不再靜默無操作
- 外部 7z CLI 路徑補上 50 GiB 解壓後體積上限（條目數上限原本就有）
- 0.5 MB/s 等小數限速可儲存（按 Double 解析、四捨五入回 KB）；單位標籤更正為 MB/s
- 多批隱身連結加換行分隔，不再拼成一個無法解析的大 blob 而靜默遺失

### 🐛 第二批：11 項跟進（含第一批修補引入的 2 個迴歸）

([FileDownloader.vb](../Clases/FileDownloader.vb) / [ThrottledStreamController.vb](../Clases/ThrottledStreamController.vb) / [Main.vb](../Forms/Main.vb) / [PropiedadesDescarga.vb](../Forms/PropiedadesDescarga.vb) / [DescompresorController.vb](../Clases/DescompresorController.vb))

- `.part` 尺寸不符（遠端替換/截斷/磁碟滿短檔）就地重建 + 重置分塊後繼續，不再每 15 分鐘失敗一次、永遠救不回
- 重試進度不再雙計（啟動清單次計數器；暫停/恢復不受影響；此前重試瞬間跳 ~100%）
- 限速 5 處呼叫統一 KB×1024 按位元組傳給控制器，此前設 1 MB/s 實際約 1 KB/s
- 暫停時長不再計入停滯；配額熔斷期零速 10 秒看門狗 + 5 秒排空即變紅（此前乾等 ~150 秒）
- 多個 `.elc`/`.dlc` 排隊逐個匯入（拖放與命令列），命令列支援 `.elc`（此前靜默無操作）；多 `.dlc` 不再只取第一個
- 紅字任務「強制下載」可用：全量重置後按單個強制起（永久失敗仍排除；此前零回饋）
- 包屬性只改路徑/密碼不再靜默清零各檔案限速（一致時顯示公共值，未勾選不覆寫）
- 7z 解壓發佈總量，進度顯示 已/共 而不是 `-`（逐位元組進度仍僅託管分支有）
- 限速框最多 4 位小數（`1.46484375`→`1.4648`，回乘誤差 <1KB）

### 📦 版本號

- Assembly / FileVersion → `2.5.1.0`；`docs/version.xml` → `2.5.1.0`（2.5.0 及更早版本會收到本次更新提示）
- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.5.1`（`VERSION_UPDATE` 沿用已知的 `Double` 解析限制，統計 ping 同 2.5.0 取捨；更新檢查本身用 `System.Version` 比較，不受影響）

---

## [2.5.0] - 2026-09-14

正式版。相對 RC2 的增量很小：修一個並發 bug、版本號轉正並進入更新通道、文件補齊 4 語言。核心主題:**RC 驗證通過，直接轉正**。

### 🐛 修復:線上觀看重複點擊並發解析

([AddLinks.vb](../Forms/AddLinks.vb))"線上觀看"按鈕連點/雙擊會並發跑兩次 `ResolveUrlsAsync`，同一批連結被解析兩次，嚴重時拉起兩個 VLC。現加 `_watchResolving` 進行中標記 + 解析期間停用按鈕，回呼 `Finally` 裡恢復(此前無任何防護)。

### 📦 版本號轉正，進入更新通道

- InternalConfig `VERSION_MEGADOWNLOADER` → `2.5`(標題列/關於/日誌裡的顯示版本不再帶 RC 後綴)
- `VERSION_UPDATE` 保持 `2.5`(刻意不動：`Main.CheckVersionStatistics` 用 `Double` 解析，`2.5.0` 會解析失敗導致新版統計 ping 遺失)
- `docs/version.xml` → `2.5.0.0`：2.4.7 及更早版本從此刻起會收到更新提示；2.5.0 與遠端相等，不再提示

### 🌐 文件：4 語言全量補齊(手動維護)

- 新增 `docs/README.zh-TW.md / docs/README.ko-KR.md`、`docs/CHANGELOG.{zh-TW,ja-JP,ko-KR}.md`、`docs/CONTRIBUTING.{zh-TW,ja-JP,ko-KR}.md`，英文 CHANGELOG 重寫為真正的英文
- 自動翻譯流水線已整體下線(`i18n/` 指令碼 + Docs i18n 工作流刪除)，以後所有語言版本手動同步更新，改一處請同步其他語言

---

## [2.5 RC2] - 2026-09-13

生產級破壞性稽核後的三批修復(RC1 之後的新改動全在此)。核心主題:**消滅靜默失敗與高頻卡頓**。

### 🐛 RC2 修復:發佈阻塞 5 項(Batch-1)

([Main.vb](../Forms/Main.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb) / [Fichero.vb](../Clases/Fichero.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb))

- Web 推送與手動加鏈同鏈路:`ControlRemotoAgregarLinks` 先經 `URLProcessor.ProcessURLs` 展開資料夾/ELC(此前資料夾鏈變成單個壞任務);`pckname` 經 `PathGuard` 淨化(此前字串拼接,已認證任意目錄建立+落盤越獄);子目錄結構保留
- 資料夾跳過計數:單節點解密失敗記帳,部分跳過記 Warning(含範例 handle,不記 key 材料),全部失敗拋錯(此前零檔案也報成功)
- 0 字節空檔案短路:探活後仍為 0 直接落空檔案、驗 MetaMAC(期望 `(0,0)` )、走正常重新命名與成功事件(此前 `GetDataPart` 拋錯 + 自癒空轉);重新命名衝突邏輯抽為 `RenamePartToReal` 共用
- 驗證取消旗標:`bgArranque` 不可 `CancelAsync`,加 `_StartupCancelled` 旗標,Stop/Dispose 置位後驗證完成不再建下載器(此前 Stop 後必復活)
- 壞 ELC per-URL 隔離:單條失敗只跳過該條、同條部分結果回滾,全滅時仍拋首錯保持單鏈語義(此前一條壞 ELC 團滅整批)

### 🐛 RC2 修復:可靠性 4 項(Batch-2)

([Fichero.vb](../Clases/Fichero.vb) / [Configuracion.vb](../Clases/Configuracion.vb) / [Main.vb](../Forms/Main.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb))

- 100% 回補 MD5 移出全域鎖:鎖內只預定(置 `ComprobandoMD5` 防重投),鎖外執行緒池執行(此前 GB 檔案撞線凍結 UI/排程)
- Web 密碼隨機 IV 移出組態去重比對,改明文快照比對(此前有 Web 密碼時 `Configuration.xml` 每 5 秒必重寫)
- 關閉等待補齊 `CreandoLocal/Verificando/Descomprimiendo/ComprobandoMD5`;驗證回寫與 `GuardarXML` 同持 `FicheroDownloader`(此前關機撕裂佇列)
- 120 秒看門狗判定移到 30 秒排空後,排空期間撞線完成不再誤報失敗(此前成功/失敗雙事件競態可把完好檔案釘成錯誤)

### 🐛 RC2 修復:體驗與縱深(Batch-3)

([URLExtractor.vb](../Clases/URLExtractor.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [StreamingModule.vb](../HttpModule/StreamingModule.vb) / [Main.vb](../Forms/Main.vb) / [Updater.vb](../Clases/Updater.vb) / [Criptografia.vb](../Clases/Criptografia.vb) / [SteganoManager.vb](../Stegano/SteganoManager.vb))

- 正則單例化(`Compiled` 共用執行個體):`URLExtractor` 全表掃描、`MegaFolderHelper` 逐節點、`StreamingModule` 逐請求不再 `New Regex`(此前數百連結貼上 UI 凍結)
- fragment 解碼:`UnescapeDataString` + 去空白,`%23/%3D` 轉義鏈不再永久"無法解密";刪 dead `Contains(" ")` 分支
- 更新 URL 僅 https;`version.xml` 禁外部實體(XXE);公開鏈跳校驗改 Warning 留痕;Stegano 遠端 64MB+30s 上限;streaming 畸形 mega 參數返 400 不再 500

### 🐛 RC2 修復:合流審查補丁(Pre-merge Gate)

- 左欄任務總覽+捷徑入口:總速度/計數/佇列進度/剩餘時間,複用既有 430ms 重新整理循環(無新增計時器);解壓縮佇列/串流庫/日誌提到首屏
- 串流庫 Web 匯入加按條隔離:壞資料夾/過期 ELC 只跳過該條(此前整請求無回應)
- 配額同事件去重:熔斷期內重複上報不升級檔位不延長等待(此前多連線並發命中可瞬間跳檔到 6h)
- CI 單檔校驗改 Windows PowerShell 5.1 執行(此前 `pwsh` 下 ReflectionOnlyLoad 必拋,建置必紅)

### 📦 版本號

- Assembly / FileVersion → `2.5.0.0`(RC 不動)

- InternalConfig `VERSION_MEGADOWNLOADER` → `2.5 RC2` / `VERSION_UPDATE` → `2.5`(數字不動,beta/RC 期間不提示更新;`docs/version.xml` 保持 `2.4.7.0`,正式版才抬)

***

## [2.5 RC1] - 2026-09-12

匿名下載 MEGA 配額(HTTP 509 / API -17)專項。核心主題:**配額可預期——自動暫停、誠實倒數計時、到點自動恢復**。

### ✨ 新功能:配額全域熔斷 + 倒數計時橫幅

([MegaQuotaManager.vb](../Clases/MegaQuotaManager.vb) 新增 / [Conexion.vb](../Clases/Conexion.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb) / [Main.vb](../Forms/Main.vb))

- 509 與 -17 歸一識別:判定只用 `HttpStatusCode = 509`(狀態碼)與 `-17 / EOVERQUOTA`(API 語義),不做回應體文字匹配。覆蓋檔案資訊(:429 異常路 + :364 數字路)與資料夾讀取(:47 異常路 + :51 數字路)兩個落點
- 遞進熔斷:首次命中暫停 60 分鐘,配額期內重複命中升級到 2 小時、6 小時封頂;`Retry-After` 有則取大值(大機率沒有,不依賴)
- 熔斷期間:不再開新任務、不做新檔案資訊校驗(每次校驗=一次 API 呼叫,會延長懲罰視窗)、分塊失敗不再 16 秒空轉重試,由排程器統一等待
- 主介面橫幅:`MEGA 配額已用盡(未給出確切恢復時間)。已自動暫停,將在 X 小時 Y 分後自動重試` + **[立即重試]**按鈕(換 IP / 代理 / 重啟路由後可手動解除,不被鎖死)+ 狀態列倒數計時 + 進入/解除各一次托盤氣泡

### ✨ 改進:失敗自癒預設開啟(帶一次性遷移)

([Configuracion.vb](../Clases/Configuracion.vb)) `ResetearErrores` 缺省改為開(15 分鐘);存量組態經 `ResetearErroresMigratedV25` 一次性遷移到開,後續手動選擇不再被覆寫;新裝直接為開。

### ✨ 改進:永久失敗不參與自癒 + 批次操作

([Fichero.vb](../Clases/Fichero.vb) / [Main.vb](../Forms/Main.vb)) `-9 ENOENT / -11 EACCESS / -14 EKEY / -16 EBLOCKED` 標 `EsErrorPermanente`,自癒跳過(不再每 15 分鐘詐屍刷日誌耗配額);右鍵新增**重試全部失敗**與**移除全部失敗**(確認框顯示數量,可選同時刪除本機 `.part`)。

### ✨ 改進:大資料夾讀取進度回饋

([MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb) / [AddLinks.vb](../Forms/AddLinks.vb)) 資料夾解析搬出 UI 執行緒,進度窗即時顯示`正在讀取資料夾…已解析 N 項`,支援取消(丟棄結果不加包);線上觀看同樣走非同步解析。

### 🐛 RC1 修復:配額誤報 + 重啟丟錯誤 + 進度條發灰(P1)

- 配額熔斷期內逾時改報 `MegaQuotaExceededException`(此前 120 秒兜底報通用逾時,`FailedByQuota=False`,「立即重試」撈不回);非配額 120 秒文案去「連結過期」誤導,明確為總時長看門狗(非閒置逾時),進度保留斷點續傳
- `DescripcionError / EsErrorPermanente / FailedByQuota` 持久化到佇列 XML(4000 字截斷),重啟後「檢視錯誤」不再空白,永久失敗不再被自癒反覆撈起;老佇列按描述文字回推標記相容;空描述彈可操作 fallback 不再空白窗
- 進度條自訂繪製:背景透明露出列底色(淺色整條發灰消除,0% 列不再純灰);清空漸層走 `FillColor` 實色;邊框隨主題 `Border`;條高 18;小進度保底 2px
- 下載清單列寬防 corruption:10 列加 `Minimum/MaximumWidth`(# 20–40/檔名 150–700/其餘見程式碼),`ColumnWidthChanging` 拖曳時夾取(OLV 不攔截表頭拖拽);啟動時壞狀態(`#`/檔名被壓 0、單列 >800、可見總寬 >2000)自動重置落盤;右鍵新增"恢復預設列寬"(此前 `#` 列 `Hideable=False` + 遷移已跑過,程式內無回到預設入口)

### 🐛 RC1 補丁:橫幅版面/配額語義/建置可用性

- 建置可用性:現代 MSBuild 把 resx 編為 preserialized 格式,啟動即崩(`My.Resources.icono` 處 `FileLoadException`,被全域異常兜底吞成靜默 exit 0)。`Resources\DLLs` 新增 `System.Resources.Extensions/Memory/Buffers/Unsafe/Numerics.Vectors`,vbproj 加引用,`app.config` 加版本重定向,CI 產物清單同步
- 配額橫幅改絕對版面:清單+兩側欄 Top/Height 永遠由工具列底+橫幅顯隱重算,去掉相對位移與 Bottom 錨點(此前 resize/最大化/DPI 變化後隱藏會多出一個橫幅高度壓住狀態列)
- 配額到期喚醒脫離自癒開關:熔斷解除邊沿直接調 `WakeQuotaFailedItems`(此前藏在 `If ResetearErrores` 裡,關掉自癒的使用者橫幅消失但失敗項永不恢復)
- 懲罰遞進修復:檔位 24h 衰減,到期/手動清除不再歸零(此前遞進永遠走不到,手動重試還打回 60min 又立刻發請求);`Retry-After` 支援 HTTP-date
- 倒數計時文案:120 秒內讀秒,以上向下取整(此前末 60 秒卡"1 min",1h59m30s 顯示"2 h 0 min");橫幅顏色進主題系統(`QuotaBack/QuotaFore`)
- Toast 新建/複用共用四邊夾取(此前首彈貼邊時一半在屏外);資料夾進度窗進度條去視覺樣式跟主題,取消真取消(傳 `CancellationToken` 進解析循環)
- 排程循環單點故障:迭代級 Try/Catch + `RunWorkerCompleted` 看門狗 3 秒自救重啟(此前一次異常=重新整理+橫幅+恢復全停擺);熔斷期點開始給 Toast 提示(此前點了沒反應)

### 🐛 RC1 修復:UI 可感知缺陷(12 項)

- `ThemeManager` 新增 `ListBox / CheckedListBox / ToolTip` 分支(此前深色漏刷,靠各視窗手寫補丁);頂層 `MainMenu` 仍為系統選單列為已知限制
- Toast 複用重算位置防跨屏過期座標;配額橫幅 Top 跟隨工具列+DPI 縮放;工具列圖示/導覽列高跟隨 DPI;狀態列 RAM/Proc 改自動寬度(Designer 90→150);預設窗寬 880→1024 釋放 Nombre 列
- 設定搜尋支援 `NumericUpDown / ListBox / DataGridView` 與 ComboBox 候選項,Label 不可聚焦時退化聚焦父容器,無命中蜂鳴回饋;設定 Cancel 改 `Bottom|Right` 錨點;ELC 表格換膚後重繪防首幀舊色
- AddLinks:清空文字同步清 `HiddenLinks`,佔位符列數納入計數;浮水印灰字深色可見;解壓縮密碼 `MaxLength` 6→128(兩處);ELC 兩 Label 複用已有鍵翻譯;Streaming 紅字改主題 `ErrorFore`+無效連結給提示;`btnLanzarVLC.DialogResult` 改 None 防誤關窗;Credits 去 `AcceptButton=lblTitle`+空譯文裸 key 保護

### 🌐 語言新增

`en-US` / `zh-CN` 新增 `Quota_Banner / Quota_Status / Quota_RetryNow / Quota_Recovered / Quota_Error / Retry all failed / Remove all failed(+confirm/delete part) / Folder_Reading(+Count)`;RC1 補 `es-ES` 同 11 鍵(其他語言經 en-US 回退)。

### 📦 版本號

- Assembly / FileVersion → `2.5.0.0`(RC 不動)

- InternalConfig `VERSION_MEGADOWNLOADER` → `2.5 RC1` / `VERSION_UPDATE` → `2.5`(數字不動,beta/RC 期間不提示更新;`docs/version.xml` 保持 `2.4.7.0`,正式版才抬)

***

## [2.4.7] - 2026-09-12

更新提醒升級為三選項 + 移除已廢棄的搜尋引擎整合。核心主題:**把"提醒頻率"的選擇權交給使用者,把死網域掃地出門**。

### ✨ 改進:更新提醒三選項

([Main.vb](../Forms/Main.vb) / [Configuracion.vb](../Clases/Configuracion.vb)) 新版本彈窗由"是/否"升級為三選項:**是**=立即更新;**否**=3 小時後再提醒;**取消**=不再提醒目前版本。`UpdateSkipVersion` 組態項持久化"不再提醒"的選擇(按版本記錄,只屏蔽該版本;未來更新的版本發佈後自動恢復提醒),選擇後立即寫盤。設定中的"檢查更新"勾選框仍是總開關(取消即完全停止偵測)。

### 🧹 維護性清理:移除廢棄的搜尋引擎整合

原版"選項 → 尋找"選單彙總的 4 個 MEGA 搜尋引擎網域(megafiles.me / megafindr.com / megasearch.co / megasearch.co.nz)已全部下線,整套功能不可用:

- ([URLExtractor.vb](../Clases/URLExtractor.vb)) 刪除 `mega://mega-search?...` 連結解析(`MEGASEARCHPREFIX` 常量、正則模式、`CheckFileIDAndFileKey` 的 mega-search 解析分支)
- ([Main.vb](../Forms/Main.vb)) 刪除"尋找"選單建置與 `Buscador_Click`
- ([InternalConfig.xml](../Resources/InternalConfig.xml)) 刪除 `SEARCH_LIST`(4 個死網域)與 `MEGA_SEARCH_CURL`
- ([InternalConfiguration.vb](../Clases/InternalConfiguration.vb)) 刪除無呼叫者的 `ObtenerValuesFromInternalConfig`
- 10 個語言檔案刪除 `Searc&h` 死鍵

### 🌐 語言新增

`en-US`/`zh-CN`/`zh-TW`/`es-ES` 新增 `Update prompt hint`(三選項彈窗的按鈕含義說明,其他語言經 en-US 回退)。

***

## [2.4.6] - 2026-09-04

7 項假成功/靜默失敗修復 + 1 項維護清理。核心主題:**讓失敗以失敗的樣子呈現出來**。

### 🐛 修復:重啟後假成功(P1)

([Paquete.vb](../Clases/Paquete.vb)) `MarcarFicherosComoParados` 原來把 `Verificando`/`Descomprimiendo` 狀態的檔案標成 `Completado`——但 `Verificando` 是下載前的瞬態(重啟後 `EstadoAnterior` 已遺失),`Descomprimiendo` 是下載完成但解壓縮未完成,**兩者都可能是"一個字節都沒落盤"卻報成功**。現統一回 `EnCola`:靠斷點續傳繼續,已完整的檔案只做快速校驗,不會從零重下。

### 🐛 修復:設定儲存假成功

([Configuration.vb](../Forms/Configuration.vb)) `Config.GuardarXML` 失敗時只在底層記日誌+置 `ErrorConfig`,UI 照樣彈"儲存成功"並關閉視窗。現在儲存後檢查 `ErrorConfig <> SinErrores` 則彈錯並停留,不再顯示成功。新增錯誤文案語言鍵。

### 🐛 修復:大寫連結被靜默丟棄

([URLExtractor.vb](../Clases/URLExtractor.vb)) `ExtraerURLs` 用 `IgnoreCase` 匹配到 `HTTPS://MEGA.NZ/...`,隨即呼叫大小寫敏感的 `ExtraerFileID` 校驗失敗直接丟棄——使用者貼上大寫連結毫無反應。6 處 `New Regex(pattern)` 全部補齊 `IgnoreCase`;附帶把 `#F`/`#N` 模式比較改 `ToUpperInvariant`、`fenc`/`enc`/`mega-search` 前綴匹配全部改 `OrdinalIgnoreCase`。擷取組保留原始大小寫,FileID/FileKey 的區分大小寫不受影響。

### 🐛 修復:畸形 enc 連結崩潰

([URLExtractor.vb](../Clases/URLExtractor.vb) / [ServerEncoderLinkHelper.vb](../Clases/ServerEncoderLinkHelper.vb)) base64url 長度 `%4==1` 是非法值,原 `"==".Substring(3)` 拋 `ArgumentOutOfRangeException` 裸崩。現提前判定拋友善錯誤,統一彈"連結無效"類提示。

### 🐛 修復:資料夾 API 空回應 NRE

([MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb)) `DeserializeObject` 無 Try、`FileList.f` 直接遍歷——畸形回應(空字串/代理 HTML)拋 NRE。現加 Try/Catch + 空檢查,統一拋"無效伺服器回應",記日誌不洩漏 HTML 內容。

### ✨ 改進:子資料夾連結轉 ELC 不再丟範圍

([ServerEncoderLinkHelper.vb](../Clases/ServerEncoderLinkHelper.vb)) `MegaLink` 增加 `SubFolderID`/`SubFileID`;`ServerEncode` 在 MegaFolder 時追加 `/folder/子ID` 或 `/file/檔案ID` 後綴。舊版解碼器忽略後綴(退化為整資料夾=歷史行為),新版解碼器恢復子範圍,**雙向相容**。`ExtraerSubFolderID`/`ExtraerSubFileID` 補舊式 token(`mega://#F!...!/folder/...`,ELC 解碼產物)後綴回退解析。`enc`/`enc2` 密文無處存子範圍,`EncodeLinksForm` 對含子範圍的連結跳過編碼保留原文,避免靜默擴大為整資料夾。

### 🌐 語言缺鍵補齊

`en-US`/`zh-CN` 各 +5:`ELC created successfully`、`URL is mandatory`、`VLC path is not valid`、`Open &ELC`、`Configuration could not be saved...`。此前缺鍵經 `Language.GetText` 的 en-US 回退顯示英文原文,非崩潰,但中文介面漏翻。

### 🧹 維護性清理

- 刪除 `docs/BUGFIX-CHECKLIST.md`(2026-07-13 的審查清單已嚴重過期,至少 8 處 ⬜ 實際已修)
- 刪除 `Resources\DLLs\xunit.dll`(vbproj 無引用,Fadd.dll 的 xunit 1.0.3 依賴本就無法解析,磁碟上的 1.9.1 版本從未被使用)
- vbproj 刪除死引用 `TODO\TODO.txt`
- README:Web 介面描述更正為"預設僅綁 127.0.0.1,可開區域網路存取並指定綁定 IP"(與實作一致);移除 xUnit 相關條目

***

## [2.4.5] - 2026-09-02

本版本為全面程式碼審查後的系統性修復:36 項確認問題全部處理,涵蓋崩潰修復、功能正確性、HTTP 協定合規、資源洩漏與安全加固。

### 🛡️ 全域異常兜底

([ApplicationEvents.vb](../ApplicationEvents.vb)) 此前整個應用沒有任何未處理異常兜底——任何 UI 執行緒異常直接彈 .NET 崩潰對話方塊並終止處理程序,背景執行緒異常更是讓處理程序無聲消失。現在:

- `My.UnhandledException`:異常寫入日誌後**不結束**,使用者有機會儲存狀態

- `AppDomain.UnhandledException`:背景執行緒異常至少留下日誌線索

### 🐛 修復:清單重新整理閃退(高頻崩潰源)

([Main.vb](../Forms/Main.vb)) 下載狀態/百分比/預估時間/進度文字 4 個 `AspectGetter` 的 Catch 塊會彈英文堆疊再 `Throw`——而 AspectGetter 在**每一列重繪時執行**,一列彈一次窗,關掉後崩潰。改為只記日誌並返回安全佔位值。

### 🐛 修復:其餘使用者可見錯誤

| 症狀                                    | 根因與修復                                                       |
| ------------------------------------- | ----------------------------------------------------------- |
| 開啟 ELC 點"取消"報 "The path is not valid" | 取消時仍以空字串呼叫 `AddDLC`;現在靜默結束                                   |
| 單檔點 Reset 很快又變回 Error                | Fichero 分支漏調 `ResetearDescarga()`,殘留 `.part` 與錯誤狀態;補齊與包分支一致 |
| 首次執行強制組態可被"取消"繞過                      | 密碼框被填佔位符 `*****` 恆非空,校驗永不可達;佔位符現視為"未設定"                     |
| Web 逾時儲存 61-99 重開變空再被改 5              | 載入邊界 `>60` 與儲存邊界 `0-99` 不一致;統一為 0-99                        |
| 佇列檔案損壞導致啟動即崩                          | `CDate(strFecha)` 區域性相關且無 Try;改 `Date.TryParse` 雙文化解析,壞值跳過  |
| 背景彈窗藏到主視窗後面像卡死                        | DoWork 執行緒直接 `MessageBox.Show`;改走 `SafeShowError` 編組回 UI     |
| 3 個背景 worker 報錯彈整屏英文堆疊                | 堆疊已入日誌,使用者只見 `ex.Message`                                    |
| VLC 啟動失敗無任何回饋                         | 返回值被忽略且無 Try;兩處呼叫方現檢查返回值,`WatchOnline` 內部兜底                 |
| 拖入非 ELC/DLC 檔案被靜默丟棄                   | 現在給出提示                                                      |
| 未選中條目點"開啟目錄"報空路徑錯誤                    | 空路徑直接結束                                                     |

### 🐛 修復:功能正確性缺陷

| 位置                                                               | 修復                                                                                                                                                      |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [DescompresorController.vb](../Clases/DescompresorController.vb) | **缺分卷 RAR 假成功**:`IsComplete=False` 時靜默跳過且不置異常,上游報"解壓縮成功";現顯式拋錯。**重複 Code 卡"正在解壓縮"**:靜默跳過仍返回 True;現更新既有佇列條目                                                |
| [FileDownloader.vb](../Clases/FileDownloader.vb)                 | **UI 凍結最長 16.5 秒**:分塊失敗退避的 `Thread.Sleep` 忙等跑在 UI 執行緒;移到執行緒池。**"Must specify size" 掩蓋真實錯誤**:Size=0 時跳過收尾塊。**Dispose 從不釋放 Mutex/MutexFile/trigger**:現確定性釋放 |
| [Configuracion.vb](../Clases/Configuracion.vb)                   | Web 密碼解密失敗的空 Catch:保留密文當密碼導致登入永遠失敗且無線索;現記日誌並清空                                                                                                          |
| [Conexion.vb](../Clases/Conexion.vb)                             | FileID 未轉義直接拼 JSON/URL;補 JSON 轉義 + UrlEncode                                                                                                            |
| [Main.vb](../Forms/Main.vb)                                      | 刪除"包+子檔案"時 `CancellationComplete` 雙掛導致 `Dispose` 執行兩遍;已被移除的物件直接跳過                                                                                       |

### 🌐 HTTP 模組:Range 合規 + CSRF + 限速

- **RFC 7233**([StreamingModule.vb](../HttpModule/StreamingModule.vb)):支援 `bytes=-N` 後綴區間與 `bytes=N-` 開放區間;越界返回 416 + `bytes */size`(此前靜默鉗位);`bytes=0-0` 單字節探測只取一個對齊塊(此前向 MEGA 請求整個檔案,頻寬放大);尾塊截斷到 Content-Length(回應體不再超發)

- **`?mega=`** **解析**:直接用框架解析的參數值,`?p=密碼&mega=...` 順序不再失敗

- **串流庫 CSRF**([StreamingLibraryModule.vb](../HttpModule/StreamingLibraryModule.vb)):Delete/Save/OpenVLC/ImportLinks/ExportLinks 現要求 POST + 有效 token(複用 Web 介面的 EnsureCsrf 模式);範本注入 token;**順帶修復瀏覽頁 OpenVLC 用 GET 調 POST-only 介面的既有失效**

- **登入限速**([WebInterfaceModule.vb](../HttpModule/WebInterfaceModule.vb)):PBKDF2(100k) 在請求執行緒同步執行,POST 轟炸可打滿執行緒池;現並發上限 4 + 60 秒視窗失敗鎖定 10 次

- 3 處 `StreamWriter(response.Body)` 補 `Using` + 無 BOM 編碼

### 💾 資源洩漏

- `Criptografia.decrypt_key`:循環內 `CreateDecryptor` 從不 Dispose(每次解密 key 洩漏 N 個 ICryptoTransform);改 Using

- `MD5Utils.MD5CalcString`:補 Using

- `DescompresorController` ×4 處、`ThrottledStreamController` ×1 處 互斥鎖 補 Try/Finally(符合專案約定)

- Configuration/PropiedadesDescarga 共 5 處懸停 ToolTip 洩漏;複用單一執行個體

### 🔒 Stegano(隱寫)

- **先寫壞再報錯**:容量校驗發生在寫盤之後,留下截斷的 .jpg;改為記憶體編碼+校驗全通過才落盤

- **OpenWrite 不清空尾部**:覆寫更長的舊檔案時新舊字節拼接;`WriteAllBytes` 截斷覆寫

- **Uri 校驗形同虛設**:`RelativeOrAbsolute` 對 "hello world" 返回 True;限定 Absolute + http/https/file

### 🧹 維護性清理

- vbproj 刪除死引用 `TODO\TODO.txt`、`plantilla botones.psd`;刪除孤兒檔案 `postbuildevent.xml`

- DPI 組態統一 PerMonitorV2(app.config 補 `DpiAwareness`、myapp HighDpiMode=2);移除未部署的 Unsafe 組件重定向

- README 刪除不存在的 xUnit 技術棧項

- 硬編碼英文訊息(ELC/DLC 錯誤、Invalid input data 等 10 處)接入語言系統,新增 en-US/zh-CN 條目

- BUGFIX-CHECKLIST.md 頭部加"已過時"警示(至少 8 處 ⬜ 實際已修,防止重複勞動)

***

## [2.4.4] - 2026-09-01

### ✨ 新功能:子資料夾連結下載

**此前**:`mega.nz/folder/<根ID>#<金鑰>/folder/<子ID>` 形式的連結會被當作根資料夾連結,下載整個根資料夾的全部內容。

**現在**([URLExtractor.vb](../Clases/URLExtractor.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb) / [StreamingLibraryManager.vb](../Clases/StreamingLibrary/StreamingLibraryManager.vb)):

| 連結形式                               | 行為                         |
| ---------------------------------- | -------------------------- |
| `mega.nz/folder/根ID#金鑰/folder/子ID` | 僅下載指定子資料夾的內容(路徑以子資料夾為根重定基) |
| `mega.nz/folder/根ID#金鑰/file/檔案ID`  | 僅下載指定單個檔案                  |
| `mega.nz/folder/根ID#金鑰`            | 下載整個根資料夾(不變)               |

- 正則擴展擷取 `/folder/<子ID>` 與 `/file/<檔案ID>` 後綴

- 檔案清單按父節點鏈向上遍歷過濾,只保留屬於目標子節點的檔案

- 下載路徑重定基為相對子資料夾的路徑,不再出現多餘的上層目錄層級

### 🐛 修復:子資料夾連結下載完成後誤報 MetaMAC 錯誤(使用者實測確認)

**症狀**:子資料夾連結下載到 100% 後彈 MetaMAC 校驗失敗錯誤;檔案內容實際完整。

**根因**:每個資料分塊的 CBC-MAC 計算使用**零初始 IV**。而 MEGA 真實演算法(SDK `SymmCipher::ctr_crypt`)的分塊 MAC 初值是**檔案 nonce 複製兩份**——key 第 4-5 字(word)拼接成 16 字節 `[n0, n1, n0, n1]`。零 IV 算出的 MAC 與任何真實上傳檔案的 MetaMAC 都不可能匹配,因此所有帶 8 words key(內嵌 MetaMAC)的下載必然報錯;單檔公開連結(4 words key,跳過校驗)不受影響,導致問題此前被掩蓋。

**修復**([Criptografia.vb](../Clases/Criptografia.vb)):

```vb
' 修復前:零 IV(必然校驗失敗)
Dim chunkMac As Integer() = New Integer() {0, 0, 0, 0}
' 修復後:nonce(key 第 4-5 word)複製兩份,與 SDK 一致
Dim chunkMac As Integer() = New Integer() {nonceWords(0), nonceWords(1), nonceWords(0), nonceWords(1)}
```

其餘部分(折疊零初值、`(m0^m1, m2^m3)` 最終壓縮、128 KiB × i 分塊排程)逐行比對 SDK `macsmac`/`ChunkedHash` 原始碼確認本就正確,未改動。

### 🔧 MetaMAC 校驗對齊 SDK 標準行為

刪除驗證函式中的"每個分塊邊界提前檢查 MAC、允許前綴匹配"寬容邏輯——SDK 權威實作(`generateMetaMac` + `macsmac`)是**讀完整個檔案後做一次完整比較**。前綴匹配是演算法錯誤時代的誤判產物,現一併移除;任何位置的真實損壞仍然硬失敗。

### 🔒 9 項安全加固

| 位置                                             | 修復                                                                   |
| ---------------------------------------------- | -------------------------------------------------------------------- |
| `Criptografia.StripNullCharacters`             | 重寫為 `Replace(vbNullChar, "")`,消除逐字元拼接造成的位置偏移錯誤                       |
| `Criptografia.AES_EncryptString/DecryptString` | 失敗返回 `Nothing` 而非空字串;持久化點(Configuracion/Fichero)加密失敗時跳過寫入、保留舊值,不再靜默清空 |
| AES 密文格式                                       | 新增隨機 IV 格式 `{1}\|\|IV\|\|密文`,與舊格式自動雙向相容                              |
| `FileDownloader`                               | MetaMAC 不匹配拋異常並重置分塊重試(配合本次演算法修復,不再產生誤報)                               |
| `Criptografia.GetFileKeyFromPreSharedKey`      | PSK 含非 ASCII 字元(>255)時記日誌返回 `Nothing`,杜絕金鑰流錯位                        |
| `WebInterfaceModule`                           | Web 密碼儲存改隨機鹽 + PBKDF2(100k 輪)派生,替換無鹽 MD5                             |
| `StreamingModule` / `StreamingLibraryModule`   | 密碼比較改 `Criptografia.FixedTimeEquals` 恆定時間比較,防時序側通道                   |
| `ClientConnected` 反射                           | 靜態 `MemberInfo` 快取 + null 檢查 + Try/Catch,失敗降級為"假設已連線"                |

### 📦 版本號

- Assembly / FileVersion → `2.4.4.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.4`

- `docs/version.xml` → `2.4.4.0`

***

## [2.4.3] - 2026-08-26

### ✨ 新功能(Issue #1)

| 功能           | 說明                                                                                                                                                                               |
| ------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 7z 解壓縮支援      | SharpCompress 不支援 7z 容器(此前所有 .7z 解壓縮必然失敗)。現優先呼叫系統已安裝的 7-Zip CLI,未安裝時自動釋放內建的 7zr.exe(公有領域,7-Zip 官方精簡版)到 `%LOCALAPPDATA%\MegaDownloader\bin` 使用。支援密碼與 multipart 分卷(.7z.001/.002/...) |
| Web 伺服器區域網路推送 | 設定 → Web 伺服器新增「允許區域網路存取」開關(預設關閉)。預設仍僅綁定 127.0.0.1;開啟後綁定所有網卡,手機/區域網路裝置可透過瀏覽器存取推送下載。開啟強制要求設定伺服器密碼(≥8 字元),組態儲存與伺服器啟動雙重校驗                                                              |
| 區域網路自訂綁定 IP  | 「允許區域網路存取」開啟後新增「綁定IP(留空=全部)」輸入框:留空監聽所有網卡;填入指定 IP(如 `192.168.1.100`)則僅監聽該網卡,多網卡/虛擬網卡環境可精確控制暴露面。儲存與啟動雙重校驗 IP 格式,非法位址拒絕啟動並報錯                                                         |

### 🐛 修復:剪貼簿監控漏檢網頁複製(Issue #1)

**症狀**:從網頁複製 MEGA 連結不彈出新增視窗,部分應用內 Ctrl+C 才有效。

**根因**:瀏覽器(Chrome/Edge/Firefox)使用**延遲渲染**——剪貼簿變化通知到達時資料尚未真正寫入;立即讀取會拿到空值或拋 `CLIPBRD_E_CANT_OPEN`(剪貼簿仍被來源處理程序佔用)。

**修復**([Main.vb](../Forms/Main.vb) / [ClipBoardViewer.vb](../Clases/ClipBoardViewer.vb)):

- 讀取改為重試制:最多 5 次、間隔 150ms,覆蓋延遲渲染與剪貼簿佔用競態

- `WndProc` 中的剪貼簿存取全部加異常保護,瞬時失敗不再中斷訊息循環

- 處理完成後的剪貼簿標記寫回失敗時降級為忽略,不再崩潰

### 🔒 7z 解壓縮安全細節

- 解壓縮前先用 `l -ba -slt` 列出全部條目,經 PathGuard 校驗拒絕路徑逃逸(Zip Slip),校驗通過才執行解壓縮

- CLI 參數中的密碼(`-p`)永不寫入日誌

- 子處理程序 stderr 非同步讀取,規避管道緩衝區死結

- 結束碼 0/1(成功/警告)放行,2(致命,如密碼錯誤)攜帶輸出尾部拋出友善錯誤

***

## [2.4.2] - 2026-08-19

### 🐛 修復:下載檔案真實損壞(使用者實測確認)

**症狀**:下載完成且檔案大小精確匹配,但檔案內容損壞無法使用。日誌證實舊版(含 v2.4.1)在 MetaMAC 校驗失敗後仍"警告並放行"完成了重新命名,損壞檔案直接落地。

**根因**(三個獨立缺陷疊加):

1. **MetaMAC 分塊排程演算法錯誤**:v2.4.1 採用"128K 起步翻倍增長、8 MiB/1 MiB 雙封頂"的排程,與 MEGA 官方 SDK `ChunkedHash::chunkfloor/chunkceil` 的真實排程不一致,導致大量合法檔案被誤判 mismatch(也為下游放行邏輯製造了藉口)
2. **mismatch 放行策略**:演算法錯誤的前提下,v2.4.1 把"mismatch 即失敗"回退成了"記警告、照常完成重新命名"——校驗形同虛設,真實損壞(如 URL 過期後 403 期間的空洞寫入)被直接放行
3. **非對齊續傳導致 CTR 金鑰流錯位**:連線中斷時的 best-effort flush 會把不足 16 字節對齊的進度持久化;重試時 `SeekToFileOffset` 只能按整塊定位金鑰流,從錯位點起**後續所有資料解密錯位**——這是"大小正確但內容損壞"的直接成因

**修復**:

| 位置                                        | 修復                                                                                                          |
| ----------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| `Criptografia.ComputeMegaFileMac`         | 分塊排程改為 MEGA SDK 線性邊界:128 KiB × i(i=1..8,即 128/256/384/512/640/768/896 KiB),之後固定 1 MiB;刪除雙 cap fallback,單次計算 |
| `Criptografia.VerifyMegaMetaMac`          | 空檔案直接返回 (0,0)(MEGA 空檔案 MetaMAC 即為 0)                                                                        |
| `FileDownloader.downloadFile`             | MetaMAC 不匹配記錯誤日誌但按 MEGA SDK 寬鬆策略繼續完成(SDK 對歷史遺留的"MAC 缺失尾部條目"同樣寬鬆);檔案大小精確校驗保留為硬門禁                             |
| `FileDownloader.FlushToDisk`              | 中斷 flush 時把持久化進度向下對齊到 16 字節邊界,杜絕非對齊續傳點(<16 字節已解密資料重試時自動重取)                                                  |
| `ChunkDownloader_DoWork`                  | 續傳請求前校驗起點對齊:非 16 字節對齊的續傳起點直接中止 chunk,防止金鑰流錯位                                                                |
| `DataPart.ValidateAndNormalize`           | 啟動時將舊版本遺留的非對齊 XML 進度自動回退到 16 字節邊界                                                                           |
| `FileDownloader.downloadFile`(v2.4.1 已引入) | 保留:移除"檔案大小匹配即強制完成"的 force-finish;僅真實 chunk 全部完成才判定完成;120 秒逾時上報失敗並保留斷點                                       |

### 📦 版本號

- Assembly / FileVersion → `2.4.2.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.2`

- `docs/version.xml` → `2.4.2.0`

***

## [2.4.1] - 2026-08-15

### 🐛 修復:下載完成但顯示錯誤(使用者實測確認)

**症狀**:檔案下載到 100% 時直接彈錯誤,`.part` 檔案不重新命名;手動去掉 `.part` 後綴後檔案可正常使用,證明檔案實際已完整下載。

**根因**:v2.2.0 引入的 MEGA MetaMAC 完整性校驗存在系統性誤報:

1. **單檔公開連結必然誤報**:公開連結的 FileKey 僅 16 字節(4 words,只有 AES 金鑰本身),**不含 MetaMAC**;而 `VerifyMegaMetaMac` 要求至少 8 words(32 字節,含 nonce + MetaMAC),不滿足直接返回 False → 完成路徑拋 "Integrity check failed" → 狀態錯誤、不重新命名。只有資料夾 API 返回的 32 字節 node key 才真正含 MetaMAC,因此誤報集中在最常見的單檔連結場景
2. **8 words key 的邊界規則差異也可能誤報**:MEGA 用戶端歷史上的 MAC 分塊邊界規則有版本差異,不匹配不等於檔案損壞(大小精確校驗是更強證據)

**修復**(兩層防護):

| 位置                               | 修復                                                                  |
| -------------------------------- | ------------------------------------------------------------------- |
| `Criptografia.VerifyMegaMetaMac` | 4 words 公開連結 key 記日誌說明"無 MetaMAC 可驗證"並跳過(返回 True),不再誤判失敗            |
| `FileDownloader.downloadFile`    | 8 words key 的 MetaMAC 不匹配降級為日誌警告並繼續完成重新命名;檔案大小精確校驗(不匹配仍報錯)保留為主要完整性防線 |

### 📦 版本號

- Assembly / FileVersion → `2.4.1.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.1`

- `docs/version.xml` → `2.4.1.0`

***

## [2.4.0] - 2026-08-14

### 🐛 全面 Bug 修復 - 21 項確認存在的問題

基於對 v2.3.0 全專案程式碼的逐檔核查，修復 21 項經確切程式碼證據確認的 bug，涵蓋使用者可感知報錯、死結/資源洩漏、並發缺陷、異常吞噬、安全債務與死程式碼。

### 一、使用者層面可感知的報錯

| 修復                    | 說明                                                                                                                                            |
| --------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| 背景執行緒彈 MessageBox 卡死下載 | `FileDownloader.bgwDownloader_DoWork` 的 Catch 塊不再在執行緒池執行緒上呼叫 `MessageBox.Show`，改為透過 `ReportProgress(FileDownloadFailedRaiser)` 上報失敗，由 UI 執行緒統一呈現 |
| 關閉期間跨執行緒 MsgBox 崩潰     | `Main.vb` 三處 `BackgroundWorker.DoWork` 異常分支不再直接 `MsgBox`，新增 `SafeShowError` 輔助方法，檢查 `IsDisposed`/`IsHandleCreated` 並透過 `Invoke` 切回 UI 執行緒      |
| 7z multipart 解壓縮崩潰     | `DescompresorController` 兩處 `NotImplementedException` 改為 `NotSupportedException` 帶友善訊息；`DescompresionFinalizada` 事件擴展傳遞錯誤訊息，使用者可在錯誤狀態中看到具體原因   |
| 兜底完成漏做 MD5 校驗/解壓縮      | `Fichero.ActualizarDatosDescarga` 的兜底狀態修正不再僅翻轉狀態，改為呼叫完整 `downloader_Completed` 流程（含 MD5 校驗和自動解壓縮），避免"顯示完成但未校驗完整性"                              |

### 二、死結與資源洩漏

| 修復                     | 說明                                                                                        |
| ---------------------- | ----------------------------------------------------------------------------------------- |
| 互斥鎖 無 Try/Finally 死結 | `Main.AgregarPaquete` 和 `bgwComprobarMaxConexiones` 兩處 互斥鎖 加 `Try/Finally`，中間異常不再導致永久死結 |
| 下載 worker 未 Dispose    | `FileDownloader.Dispose` 中循環釋放 `listDownloaders` 裡的所有 worker，不再只 `CancelAsync`            |
| bgArranque worker 洩漏   | `Fichero.Dispose` 新增釋放 `bgArranque`（下載啟動 worker），關閉期間啟動階段中斷不再洩漏                           |
| ELCForm 300ms 忙輪詢      | 改為 `AutoResetEvent` 事件驅動，無任務時零 CPU 消耗，有任務時立即回應                                            |

### 三、並發與邏輯缺陷

| 修復                   | 說明                                                                                  |
| -------------------- | ----------------------------------------------------------------------------------- |
| AJAX 回應並發污染          | `StreamingLibraryModule._RespuestaAjax` 改為 `AsyncLocal(Of String)`，並發 HTTP 請求互不覆寫回應 |
| FlushFinalBlock 異常吞噬 | `ServerEncoderLinkHelper.Cipher` 解密路徑的空 Catch 改為 `Log.WriteError`                   |

### 四、異常吞噬（掩蓋真實故障）

| 修復                 | 說明                                                                                        |
| ------------------ | ----------------------------------------------------------------------------------------- |
| FlushToDisk 磁碟錯誤被吞 | `FileDownloader.ChunkDownloader_DoWork` 中 `FlushToDisk` 的空 Catch 改為日誌                     |
| 伺服器錯誤回應讀取失敗被吞      | `Fichero.downloader_FileDownloadFailed` / `downloader_ChunkDownloadFailed` 兩處空 Catch 改為日誌 |
| 解壓縮取消異常被吞           | `Main` 關閉流程中 `RequestCancel` 的空 Catch 改為日誌                                                |

### 五、安全與技術債務

| 修復                        | 說明                                                                          |
| ------------------------- | --------------------------------------------------------------------------- |
| DPAPI entropy 硬編碼         | `Criptografia` 的 DPAPI entropy 改為從程式集標識 SHA256 派生，保留 legacy entropy 解密舊資料   |
| ZIP 密碼硬編碼 "passZIP"       | `Fichero` 的 ZIP 解壓縮密碼加密改用 DPAPI，解密先 DPAPI 後回退舊 AES 相容舊佇列檔案                   |
| OptionalPassword 死欄位      | 刪除 `Cache.OptionalPassword` 欄位及 XML 寫出（宣告後從未賦值、從不讀取）                        |
| RandomNumberGenerator 未釋放 | `ServerEncoderLinkHelper` 的 `RandomNumberGenerator.Create()` 用 `Using` 包裹釋放 |
| 日誌無 UTC 時間戳               | `Log` 全部時間戳改用 `DateTime.UtcNow`（加 `Z` 後綴），新增 30 天日誌保留清理策略                   |

### 六、死程式碼清理

| 修復                 | 說明                                     |
| ------------------ | -------------------------------------- |
| Criptografia 註釋死程式碼 | 刪除註釋掉的 `DecryptFile` 和 `cipherData` 函式 |
| Conexion 死程式碼       | 刪除註釋的 `GetAppID` 和無呼叫的 `LeerNodo` 函式   |

### 📦 版本號

- Assembly / FileVersion → `2.4.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4`

- `docs/version.xml` → `2.4.0.0`

***

## [2.3.0] - 2026-08-13

### 🐛 穩定性修復

基於程式碼審查，修復一批崩潰、資源洩漏與潛在死結問題。

| 修復                | 說明                                                                                                                             |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| AES 加密失敗崩潰        | `AES_EncryptString` 加密異常後仍對 `Nothing` 做 Base64 轉換導致二次拋異常；改為失敗返回空字串並用 `Using` 釋放 `RijndaelManaged`/`CryptoStream`/`MemoryStream` |
| AES 解密截斷/壞輸入崩潰    | `AES_DecryptString` 的 `Convert.FromBase64String` 移入異常處理；用 `CopyTo` 完整讀取明文（原單次 `Read` 可能截斷）；失敗返回空字串                              |
| 下載項資源洩漏           | `Fichero.Dispose` 由空實作改為釋放 `FileDownloader` 並置空                                                                                |
| 潛在死結              | `FileInfo.Size` setter 的 `ReleaseMutex` 放入 `Try/Finally`，循環內異常不再導致永久死結                                                         |
| 登錄檔句柄洩漏           | `RegisterInStartup` 的登錄檔鍵用 `Try/Finally` + `Close()` 釋放                                                                        |
| 危險 `Thread.Abort` | DLC 處理 30 秒逾時不再硬殺執行緒，改為協作式標記失敗並讓 worker 自然結束                                                                                    |

### 📦 版本號

- Assembly / FileVersion → `2.3.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.3`

- `docs/version.xml` → `2.3.0.0`

## [2.2.1] - 2026-08-09

### 🐛 下載狀態修復

修復兩個使用者報告的下載完成狀態顯示問題。

### Bug 1: 多檔案下載完成(100%)但顯示錯誤

- **根因**: `FileDownloader.downloadFile()` 的 `Finally` 塊在 `exc` 不為空時報告 `FileDownloadFailedRaiser`。即使所有分塊已成功完成(`AllFinished = True`),之前發生的非致命異常仍會觸發失敗事件,將狀態錯誤地設為 `Erroneo`。

- **修復**: 在 `Finally` 塊中檢查 `AllFinished` 狀態,如果下載實際完成則清除 `exc`,僅記錄警告而不報告失敗。

### Bug 2: 單檔下載完成(100%)但仍顯示"正在下載"

- **根因**: `Completed` 事件只在 `bgwDownloader_RunWorkerCompleted` 中觸發,如果等待循環因競態條件無法結束,`Completed` 永遠不會觸發,狀態停留在 `Descargando`。

- **修復**: 三層防護

  1. **事件層**: 新增 `FileDownloadSucceeded` 處理器,檔案驗證和重新命名成功後立即設定 `Completado` 狀態
  2. **循環層**: 等待循環增加 60 秒逾時檢查,若磁碟檔案大小匹配則強制完成;120 秒硬逾時防止死結
  3. **計時器層**: `ActualizarDatosDescarga` 增加兜底檢查,進度 100% 且 `AllFinished` 時自動修正狀態

### 📦 版本號

- Assembly / FileVersion → `2.2.1.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.2.1`

- `docs/version.xml` → `2.2.1.0`

***

## [2.2.0] - 2026-07-20

### 安全加固與下載完整性

基於深度靜態稽核結論，完成路徑安全（P0）、下載完整性（P1）與一批可靠性/發佈現代化（P2/P3）修復。

### 🔒 路徑安全（P0）

- 統一 `PathGuard`：遠端檔名/目錄名、解壓縮條目、刪除與寫出均限制在 canonical 下載根目錄內

- 修復 Zip Slip：解壓縮前校驗全部條目，拒絕 `../`、絕對路徑、裝置名等逃逸

- 修復 MEGA 資料夾路徑拼接與任務刪除越界風險

### 📦 下載完整性與可靠性（P1）

- 下載完成前校驗 **MEGA MetaMAC**；失敗不重新命名為最終檔案

- HTTP Range：校驗 Partial Content / Content-Range；拒絕忽略 Range 的錯誤回應

- 提前 EOF 作為失敗；CTR counter 使用 Int64 seek，修復大偏移風險

- 斷點中繼資料校驗，避免 `.part` 缺失時的「假完成」

- 組態與下載佇列原子儲存（`AtomicFile`）；HTTP 預設逾時；日誌脫敏

- 遠端 Web：Stop/Play/AddLink 改為 POST + CSRF；Streaming 媒體 URL 固定 loopback

- 解壓縮協作取消（移除 Thread.Abort）、解壓縮結果成功/失敗分離、資源配額

- 關閉順序：先停 Web → 取消 worker/解壓縮 → 停下載 → 再儲存

### ✨ 體驗與工程（P2/P3）

- 組態模型層上限（Buffer/連線數/速度）、磁碟空間預檢、檔名衝突與進度除零防護

- 語言：內建包與使用者自訂分離，缺 key 回退 en-US

- 單執行個體 IPC 按行寫入，避免連結參數黏連；主題 Auto 跟隨系統即時變化

- 移除生產 xUnit 依賴與 MPRESS Release 後處理；DPI PerMonitorV2

- 版本比較規範化；DLC 入口標為 discontinued（保留 ELC）

### 📦 版本號

- Assembly / FileVersion → `2.2.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.2`

- `docs/version.xml` → `2.2.0.0`

***

## [2.1.0] - 2026-07-19

### 主題完善 - 深色模式可用性修復

基於 v2.0 主題框架,修復深色模式下主清單、進度條、按鈕邊框、右鍵選單等關鍵觀感問題,使 Dark 主題真正可用。

### 🐛 修復

- **主下載清單斑馬紋**:`FormatRow` 不再寫死 `White`/`Honeydew`,改用 `ThemeManager` 的 `Back`/`AltBack`

- **進度條顏色**:`BarRenderer` 不再使用 Azure/SpringGreen,改為主題 token(`ProgressBack`/`ProgressFill` 等)

- **狀態前景色**:錯誤/完成列使用 `ErrorFore`/`SuccessFore`(深色下為更亮的紅/綠)

- **設定儲存後即時換膚**:Configuration 儲存主題後呼叫 `Main.ApplyCurrentTheme()`,無需重啟

- **按鈕白邊**:`FlatStyle.Standard` 的系統 3D 高光在深色下呈白邊;改為 `FlatStyle.Flat` + 主題 `Border`/`ButtonHover`/`ButtonPressed`

- **GroupBox / TabPage**:Flat 邊框與 `UseVisualStyleBackColor = False`,減少系統淺色描邊

- **ELC 帳號表**:去掉 Azure/Snow/SeaShell 硬編碼;空清單提示改用主題前景色

- **右鍵選單**:反射主題化 Form 上的 `ContextMenuStrip`;補全 `ToolStripDropDownBackground` 等 `ThemeColorTable` 屬性

- **未套主題視窗**:Stegano 精靈、SplashScreen、Cerrando 在 Load 時 `ApplyTheme`

### ✨ 改進

- `ThemeManager.GetColor(key)` 公共取色 API

- 新增語義/互動 token:`ErrorFore`、`SuccessFore`、`Progress*`、`ButtonHover`、`ButtonPressed`

- `ToolStripBorder` 正確使用 `ToolBorder` token

### 📦 版本號

- Assembly / FileVersion → `2.1.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.1`

- `docs/version.xml` → `2.1.0.0`

***

## [2.0.0] - 2026-07-13

### 重大版本 - 安全加固 + 程式碼清理 + 暗色主題

基於 v1.9 的連結格式修復,進一步完成 4 個階段共 60+ 項修復,顯著提升安全性、穩定性與可用性。本版本首次引入深/淺色主題切換。

### ✨ 新增

- **深色/淺色主題切換**:

  - 新增 `ThemeModeType` 列舉(Auto/Light/Dark),預設 Auto 跟隨系統([`Clases/ConfiguracionUI.vb`](../Clases/ConfiguracionUI.vb))

  - 新增 `ThemeManager` 類別,透過讀取登錄檔 `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` 偵測系統深淺色([`Clases/ThemeManager.vb`](../Clases/ThemeManager.vb))

  - 自訂 `ThemeColorTable` + `ToolStripProfessionalRenderer` 渲染器,覆寫 30+ ToolStrip 漸層/邊框/選中色屬性

  - 遞迴應用主題到所有控制項,包括主視窗的 `BrightIdeasSoftware.TreeListView`(下載清單)、StatusStrip、ContextMenuStrip、TableLayoutPanel、DataGridView、ListView、TreeView、ProgressBar 等

  - 9 個子視窗在 Load 事件中應用主題:Credits、AddLinks、ELCForm、EncodeLinksForm、PropiedadesDescarga、StreamingForm、Descompresor、PantallaMsg、Configuration

  - 10 種語言檔案新增 `Theme` / `Theme_Auto` / `Theme_Light` / `Theme_Dark` 翻譯鍵

- **作者資訊**:Credits 視窗加入 "Yingxue - Revival maintainer (v2.0+)"

- **更新檢查**:重定向到本 GitHub 倉庫

### 🐛 修復

**P0 嚴重安全漏洞**:

- 修復空密碼繞過校驗邏輯

- 修復代理憑證未實際賦值給 WebProxy

- 僅啟用 TLS 1.2(移除 TLS 1.0/1.1,符合現代安全標準)

- Web/Streaming 伺服器綁定 `127.0.0.1`(原 `0.0.0.0` 暴露到全網)

- 密碼雜湊統一使用 UTF-8 編碼

- 互斥鎖 操作全部包裹 Try/Finally 防止死結

- 加密程式碼中的空 Catch 塊替換為日誌記錄

- 修復 `ApagarPC` / `MaxConexionesGuardadas` 設定未正確持久化

- 修復下載器 `NullReferenceException` 崩潰(變數名錯配 `exc` vs `ex`)

**P1 資源洩漏**:

- 修復 7 處 ToolTip 資源洩漏(`ELCAccountControl` / `AddLinks` / `SteganoWizardSave`,MouseHover 每次建立不釋放)

- 修復 `SteganoManager` 的 `Image.FromFile` 鎖定來源檔案 + FileStream 未 Dispose

- 修復 `Main.vb` 7 處 `Image.FromStream(stream)` stream 過早關閉,新增 `LoadEmbeddedImage` 輔助方法

- 修復 `WebInterfaceModule` StreamReader/StreamWriter 未 Using(範本載入 + response.Body 寫入,後者使用 `leaveOpen:=True`)

- 修復 `StreamingLibraryManager` `CompressString` / `UnCompressString` 未 Using(巢狀 Using 塊)

- 修復 `MegaURIProtocol` 登錄檔操作無 Finally 釋放 + 中間變數覆寫導致句柄洩漏

- 修復 `Main.vb` `clipChange` 關閉順序錯誤(Uninstall 應在 DestroyHandle 之前)

- 修復 `Main.vb` `EsperarParadaDescargasYWorkers` 漏檢查 `bgwDescompresorCompleted`

- 修復 `StreamingLibraryModule` `Case "Delete"` 缺 `Return True`,導致貫穿到下一分支

- 修復 `StreamingLibraryModule` `UsuarioLogueado` 逾時後未清除 session,登入狀態永久停留

- 修復 `ELCAccountControl` `CellClick` 未校驗 `e.RowIndex`,點擊表頭會崩潰

- 修復 `StreamingHelper` `Keys.Count / 2` 浮點除法,應使用整數除法 `\ 2`

**P2 協定現代化**:

- `%SEQ%` / `%ID%` 序號原用 `DateTime.Now.Millisecond` 的 ticks(範圍 0-999,並發請求會重複),改用 `Interlocked.Increment` 處理程序內自增

- `MegaFolderHelper.vb` 中 `http://mega.co.nz/#N!` → `https://mega.nz/#N!`

**P2 程式碼品質**:

- `Paquete.vb` / `Configuracion.vb` 用 `GetHashCode` 比較組態 XML(不保證一致性),改用直接 `OuterXml` 字串比較

- `MegaFolderHelper.vb` 兩處變數 `ex`(Regex)→ `rx`(避免與 `Catch ex` 混淆)

- `ThrottledStream.vb` 變數名 `int`(VB.NET 關鍵字)→ `bytesRead`

- `Clases/Mutex.vb` 類別名遮蔽 `System.Threading.Mutex`,加註釋說明 + 提供別名方案

- `StreamingModule.ClientConnected`、`FileDownloader` Range 頭反射加註釋說明必要性

- `LibraryElement.ToJSON` 手工 JSON 拼接加註釋說明限制

### 🗑️ 刪除

- **4 個 Crypter**:`EncrypterMega.vb`、`MegaCrypter.vb`、`Youpaste.vb`、`LinkCrypter.vb`(API 全部下線)

- **3 個 MovieInfo**:`Allocine.vb`、`Filmaffinity.vb`、`IMDB.vb`(API 全部變更)

- **連結輔助**:`DLCHelper.vb`、`Linkdecrypter.vb`、`LinkProtectors.vb`、`Serializer.vb`、`ClipboardChangeNotifier.vb`

- **MegaUploader 選單**:移除 "Get MegaUploader" 選單項

- **goo.gl 短鏈**:14 個 Google 短鏈全部替換為 GitHub 直鏈

- **Ping 上報**:移除向原作者伺服器上報使用者/版本資訊(隱私保護)

- 共刪除 11 個 `.vb` 檔案 + 清理所有相關引用

### ⚠️ 已知問題

- `Thread.Abort()` 危險使用(3 處,Main.vb / DescompresorController)

- 跨執行緒 MsgBox 未檢查視窗是否已關閉(3 處)

- `MegaFolderHelper.FillFolderStructure` 遞迴無 KeyNotFound 保護

- `ELCForm` 無限循環每 300ms 輪詢

- `ServerEncoderLinkHelper` RandomNumberGenerator 未 Dispose

- `FileDownloader.FlushToDisk` FileStream 異時釋放

### 📦 建置產物

- `MegaDownloader.exe` 主程式

- 依賴 DLL:`BouncyCastle.Crypto.dll`、`Newtonsoft.Json.dll`、`SharpCompress.dll`、`ObjectListView.dll`、`HttpServer.dll`、`Fadd.dll`、`F5Lib.dll`、`xunit.dll`

***

## [1.9.1] - 2026-07-05

### 🐛 修復

- **下載器崩潰**:修復 [`Clases/FileDownloader.vb`](../Clases/FileDownloader.vb) 第 681-683 行變數名錯配導致的 `NullReferenceException`。當 MEGA 伺服器返回 502 閘道錯誤等異常時,catch 塊誤引用已被清空的 `exc` 區域變數(應為 `ex`),導致掩蓋真實異常並中斷整個下載流程。

***

## [1.9.0] - 2026-07-05

### MegaDownloader 復活計畫首個公開發佈版本

基於 MegaDownloader v1.8 反組譯原始碼進行修復與重構,核心目標是恢復對 MEGA 新版連結格式的支援。

### ✨ 新增

- **URL 解析**:在 [`Clases/URLExtractor.vb`](../Clases/URLExtractor.vb) 的 `patternHTTPURI` 中新增 4 條正則,支援識別以下新版 MEGA 連結:

  - `https://mega.nz/file/<FileID>#<FileKey>`

  - `https://mega.nz/folder/<FolderID>#<FolderKey>`

  - `https://mega.co.nz/file/<FileID>#<FileKey>`

  - `https://mega.co.nz/folder/<FolderID>#<FolderKey>`

- **資料夾識別**:同步更新 `IsMegaFolder` 方法

- **TLS 1.2/1.3**:在 [`Clases/Conexion.vb`](../Clases/Conexion.vb) 中顯式啟用 `Tls12 | Tls11 | Tls` 協定

- 增加本倉庫的 [README.md](README.md)、[CONTRIBUTING.md](CONTRIBUTING.md)、[CHANGELOG.md](CHANGELOG.md)、`.gitignore` 等開發者文件

### 🐛 修復

- 修復從剪貼簿複製新版 MEGA 連結時無法被識別的問題

- 修復從瀏覽器拖拽新版 MEGA 連結到主視窗無效的問題

- 修復新版資料夾連結無法被解析為子檔案清單的問題

- **修復資料夾下載時 Base64 解碼錯誤**:`mega.nz/folder/` 連結包含被多個使用者分享的檔案時,MEGA API 返回的 `fileN.k` 欄位格式為 `handle1:key1/handle2:key2[/handle3:key3]`(用 `/` 分隔多個 `handle:key` 對)。原程式碼 `fileN.k.Substring(fileN.k.IndexOf(":") + 1)` 會把第一個 `:` 之後的所有內容(包括 `/handle2:key2`)當作 key,導致 `Convert.FromBase64String` 拋出 FormatException。修復方案:新增 `ExtractKeyFromK` 輔助函式。

### 🔄 變更

- `TargetFrameworkVersion` 維持 `v4.8`(原 v1.8 即已升級至 4.8)

- 倉庫 LICENSE 維持 MIT 協定,補充復活計畫版權聲明

### ⚠️ 已知問題

- EncrypterMe.ga 因其官方 API 服務 (`http://encrypterme.ga/api`) 已下線,目前無法解析此類連結

- 部分 goo.gl 短鏈因 Google 關閉該服務而無法跳轉

- 簡體中文語言包尚有部分條目需補充翻譯

### 📦 建置產物

- `MegaDownloader.exe` 主程式

- 依賴 DLL:`BouncyCastle.Crypto.dll`、`Newtonsoft.Json.dll`、`SharpCompress.dll`、`ObjectListView.dll`、`HttpServer.dll`、`Fadd.dll`、`F5Lib.dll`、`xunit.dll`

***

## [1.8.0] - 原版 (反組譯源)

復活計畫所基於的原始版本,本倉庫透過反組譯得到其原始碼作為修復起點。

### 主要特性

- 多執行緒並發下載

- MEGA 資料夾遞迴解析

- 加密連結 (`enc`/`enc2`/`fenc`/`fenc2`/`elc`) 支援

- 第三方 Crypter 整合 (MegaCrypter、YouPaste、LinkCrypter、EncrypterMe.ga)

- VLC 串流媒體邊下邊播

- 內建 HttpServer Web 管理介面

- SharpCompress 自動解壓縮

- 多語言介面 (10 種)

- Stegano 隱寫術

- 自動更新檢查

***

## 版本號說明

- 主版本號:重大功能變更或不向下相容的修改

- 次版本號:新增功能,向下相容

- 修訂號:Bug 修復,向下相容
