# 変更履歴

本プロジェクトの重要な変更はすべてここに記録されます。形式は [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/) を参照し、バージョン番号は [セマンティック バージョニング](https://semver.org/lang/zh-CN/) に従います。

*ユーザー向けのバージョン概要は [GitHub Releases](../../../releases) をご覧ください。*

---

## バージョン概要

| バージョン | 日付 | テーマ |
| --- | --- | --- |
| 2.5.0 | 2026-09-14 | 正式版：オンライン視聴の並行処理修正 + バージョン番号の正式化および更新チャネルへの登録 + 4言語ドキュメントの補完 |
| 2.5 RC2 | 2026-09-13 | 製品監査後の3バッチ修正：リリースブロッカー / 信頼性 / 体験と深層防御 |
| 2.5 RC1 | 2026-09-12 | MEGA クォータのサーキットブレーカー + カウントダウン横断幕；失敗時の自動復旧を既定で有効化 |
| 2.4.7 | 2026-09-12 | 更新通知の3択化；廃止された検索エンジン統合の削除 |
| 2.4.6 | 2026-09-04 | 見せかけの成功 / サイレント失敗の特集修正 |
| 2.4.5 | 2026-09-02 | 全面コードレビュー後の体系的修正（16件） |
| 2.4.4 | 2026-09-01 | サブフォルダーリンクのダウンロード；MetaMAC チャンク初期値の修正；9件のセキュリティ強化 |
| 2.4.3 | 2026-08-26 | 7z 解凍；Web LAN プッシュ；クリップボード検出漏れの修正 |
| 2.4.2 | 2026-08-19 | ダウンロードファイルの実破損の修正（MetaMAC / レジューム位置合わせ） |
| 2.4.1 | 2026-08-15 | ダウンロード完了時にエラー表示となる問題の修正 |
| 2.4.0 | 2026-08-14 | 21件のバグ修正（セキュリティ / リーク / 並行処理 / デッドコード） |
| 2.3.0 | 2026-08-13 | 暗号化失敗時のクラッシュ、リソースリーク、`Thread.Abort` の削除 |
| 2.2.x | 2026-07~08 | パス安全性、ダウンロード完全性、設定の原子保存、Web CSRF |
| 2.1.0 | 2026-07-19 | ダークテーマのユーザビリティ修正 |
| 2.0.0 | 2026-07-13 | 4段階60件超の修正；ダーク／ライトテーマ；コード整理 |
| 1.9.x | 2026-07-05 | 新版 MEGA リンク形式の認識修正 |
| 1.8.0 | オリジナル | 逆コンパイルソース、復活計画の起点 |

---

## v2.4 シリーズの詳細な変更

以下は v2.4 各バージョンの変更詳細です（同一バージョンは上記のバージョン履歴にも項目があります）。

### 変更詳細

#### 更新通知と検索エンジンの整理(v2.4.7)

| 変更 | 説明 |
| -------------------- | ------------------------------------------------------------------------------------------- |
| 更新通知の3択化 | はい=今すぐ更新；いいえ=3時間後に再通知；キャンセル=このバージョンでは再通知しない（`UpdateSkipVersion` はバージョンごとに記録され、新バージョン公開後に自動で通知を再開） |
| 検索エンジン統合の削除 | 「探す」メニューの4ドメイン（megafiles.me/megafindr/megasearch.co など）はすべてサービス終了；`mega://mega-search?` リンク解析も同時に削除 |

#### 見せかけの成功／サイレント失敗の修正(v2.4.6)

| 修正 | 説明 |
| ---------------------- | ---------------------------------------------------------------------------------------------- |
| 再起動後の見せかけの成功(P1) | `Verificando`／`Descomprimiendo` が `Completado` と表示されるが、どちらも1バイトも保存されていない可能性がある；一律で `EnCola` に戻しレジュームで継続 |
| 設定保存の見せかけの成功 | `GuardarXML` の失敗が下層ログにのみ記録され、UI は成功と表示していた；`ErrorConfig` の失敗時はエラー表示のまま留まるよう修正 |
| 大文字リンクのサイレント破棄 | IgnoreCase で一致するのに検証が大文字小文字を区別し、`HTTPS://MEGA.NZ/...` が無反応となる；6か所の正規表現 + プレフィックス比較をすべて大文字小文字不問に修正 |
| 不正な enc によるクラッシュ | base64url 長さ %4==1 で `ArgumentOutOfRangeException` がそのままクラッシュ；事前判定で分かりやすいエラーを表示 |
| フォルダー API 空応答の NRE | 不正な応答（空文字／プロキシ HTML）で NRE が発生；Try/Catch + 空チェックを追加し、一律で「無効なサーバー応答」を報告 |
| ELC サブ範囲の保持 | `MegaLink` にサブ範囲フィールドを追加し、符号化時に `/folder/子ID` または `/file/ファイルID` サフィックスを付加；旧版デコーダーはサフィックスを無視＝従来動作、新版ではサブ範囲を復元 |
| 言語キー不足 | en-US/zh-CN に各+5（ELC 成功通知、URL 必須、VLC パス無効、ELC を開くメニュー、設定保存失敗） |

#### 安定性とセキュリティ(v2.4.5)

| 修正 | 説明 |
| -------------- | ------------------------------------------------------------------------------------------ |
| グローバル例外の受け皿 | 従来は受け皿がなく、UI 例外で即終了していた；現在はログ記録後に終了せず、バックグラウンドスレッド例外もログに残す |
| リスト更新時のクラッシュ | 4つの AspectGetter が失敗時に行再描画のたびにダイアログを出してクラッシュしていた；ログ記録＋プレースホルダー値を返す方式に変更 |
| 分割不足 RAR の見せかけの成功 | `IsComplete=False` を黙ってスキップし上流に「解凍成功」と報告していた；明示的にエラーを送出 |
| UI 固まり | チャンク失敗時の待避 BusyWait が UI スレッド上で実行されていた（最大16.5秒）；スレッドプールへ移動 |
| Streaming Range | RFC 7233 準拠：サフィックス／オープン区間、416応答、`bytes=0-0` でファイル全体を取得しない、応答本文が Content-Length を超えて送出されない |
| ストリーミングライブラリ CSRF | Delete/Save/OpenVLC/Import/Export に POST + token を要求（Web 画面の EnsureCsrf 方式を再利用） |
| ログイン速度制限 | 同時実行上限4 + 60秒ウィンドウで失敗10回時にロックし、PBKDF2 POST 集中によるスレッドプール枯渇を防止 |
| Stegano の保存安全性 | メモリ上で符号化＋検証通過後のみ保存（壊れた .jpg を残さない）；`WriteAllBytes` で切り詰め上書き（旧ファイル末尾との連結を廃止） |
| リソースリーク | FileDownloader ハンドル、`CreateDecryptor`／MD5 の Using 化、5か所のミューテックス Try/Finally 化、5か所のホバー ToolTip |
| 保守性 | vbproj のデッド参照、DPI 設定を PerMonitorV2 に統一、ハードコード英文メッセージを言語システムへ移行（en-US/zh-CN 項目を追加） |

#### 新機能と完全性(v2.4.4)

| 機能／修正 | 説明 |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| サブフォルダーリンクのダウンロード | `mega.nz/folder/ルートID#キー/folder/子ID` は指定サブフォルダーのみダウンロード（パス基準付け替え）；`/file/ファイルID` は指定ファイルのみダウンロード——従来は一律でルートフォルダー全体をダウンロードしていた |
| MetaMAC チャンク初期値の修正 | チャンク CBC-MAC 初期値をゼロ IV からファイル nonce 複製 `[n0,n1,n0,n1]` に変更（SDK `SymmCipher::ctr_crypt` に整合）、8 words key ダウンロード完了時の誤ったチェックサムエラーを修正 |
| MetaMAC 標準チェックサム | 「チャンク境界でのプレフィックス一致」寛容ロジックを削除し SDK と一致：ファイル全体読了後に一度だけ完全比較 |
| 9件のセキュリティ強化 | StripNullCharacters オフセット修正；AES 失敗時は Nothing を返し永続化時は旧値を保持；暗号文にランダム IV 形式を追加（旧データ互換）；Web パスワードは PBKDF2（100k）＋ランダムソルト；Streaming は定数時間パスワード比較；PSK 非 ASCII 検証；ClientConnected リフレクション堅牢化 |

#### 新機能(v2.4.3)

| 機能 | 説明 |
| --------- | --------------------------------------------------------------------------- |
| 7z 解凍 | システムの 7-Zip を優先し、未導入時は内蔵 7zr.exe（パブリックドメイン）を自動展開；パスワードと multipart 分割に対応；解凍前に PathGuard 検証でパス逃避を防止 |
| Web LAN プッシュ | 「LAN アクセスを許可」スイッチ（既定オフ）、有効化後は携帯／LAN 機器からブラウザ経由でダウンロードをプッシュ可能；パスワード保護を強制；カスタムバインド IP 対応（空欄＝全 NIC） |
| クリップボード監視の修正 | ブラウザ遅延レンダリング + クリップボード占有競合による Web コピー検出漏れ——再試行読み取りに変更し、すべてのアクセスに例外保護を追加 |

#### ダウンロード完全性(v2.4.2)

| 修正 | 説明 |
| ----------- | ---------------------------------------------------------------------- |
| MetaMAC アルゴリズム | チャンクスケジュールを MEGA SDK `ChunkedHash` に整合：128 KiB × i（i=1..8）後は固定1 MiB；空ファイルは (0,0) を返す |
| ダウンロード完了判定 | 「ファイルサイズ一致で強制完了」を削除；真の chunk がすべて完了した場合のみ完了と判定；120秒タイムアウトは失敗を報告しレジューム点を保持 |
| CTR キーストリームずれ防止 | 中断 flush とレジューム開始点を16バイト整列に強制；起動時に旧版由来の非整列進捗を切り戻し——「サイズ一致だが内容破損」を根絶 |

#### 安定性(v2.4.0/2.4.1)

| 修正 | 説明 |
| -------- | ------------------------------------------------- |
| バックグラウンドスレッドのダイアログ固まり | ダウンロード失敗は UI スレッド経由で表示；終了時のクロススレッド MsgBox に `IsDisposed` 保護を追加 |
| 並行処理汚染 | Streaming モジュール AJAX 応答を `AsyncLocal` 化し、複数要求が混線しない |
| リソースリーク | ミューテックス `Try/Finally` 解放；`BackgroundWorker.Dispose` |
| 公開リンク誤報 | 4 words key で MetaMAC がない場合はチェックサムをスキップ（ログ記録）、失敗誤判定を廃止 |

---

## [2.5.0] - 2026-09-14

正式版です。RC2 からの差分はごくわずかです：並行処理バグ1件の修正、バージョン番号の正式化および更新チャネルへの登録、4言語ドキュメントの補完。中心テーマ：**RC 検証を通過し、そのまま正式化**。

### 🐛 修正:オンライン視聴の連続クリックによる並行解析

([AddLinks.vb](../Forms/AddLinks.vb))「オンライン視聴」ボタンの連打／ダブルクリックで `ResolveUrlsAsync` が並行して2回実行され、同一リンク群が2重に解析され、深刻な場合は2つの VLC が起動していました。`_watchResolving` 進行中フラグ + 解析中のボタン無効化を追加し、コールバック `Finally` で復旧します（従来は保護なし）。

### 📦 バージョン番号の正式化、更新チャネルへの登録

- InternalConfig `VERSION_MEGADOWNLOADER` → `2.5`（タイトルバー／バージョン情報／ログの表示バージョンから RC サフィックスを除去）
- `VERSION_UPDATE` は `2.5` のまま（意図的に不変：`Main.CheckVersionStatistics` は `Double` 解析を使用し、`2.5.0` では解析失敗で新版統計 ping が失われるため）
- `docs/version.xml` → `2.5.0.0`：2.4.7 以前のバージョンはこの時点から更新通知を受信します；2.5.0 はリモートと等しいため通知されません

### 🌐 ドキュメント：4言語の完全補完（手動メンテナンス）

- `docs/README.zh-TW.md / docs/README.ko-KR.md`、`docs/CHANGELOG.{zh-TW,ja-JP,ko-KR}.md`、`docs/CONTRIBUTING.{zh-TW,ja-JP,ko-KR}.md` を新規追加し、英文 CHANGELOG を真の英語に書き直し
- 自動翻訳パイプラインは全体を廃止（`i18n/` スクリプト + Docs i18n ワークフローを削除）、今後は全言語版を手動で同期更新します。1か所を変更したら他言語も同期してください

---

## [2.5 RC2] - 2026-09-13

製品レベルの破壊的監査後の3バッチ修正です（RC1 以降の新規変更はすべてここに含まれます）。中心テーマ：**サイレント失敗と高頻度のもたつきを根絶**。

### 🐛 RC2 修正:リリースブロッカー5件(Batch-1)

([Main.vb](../Forms/Main.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb) / [Fichero.vb](../Clases/Fichero.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb))

- Web プッシュと手動リンク追加の同一経路化：`ControlRemotoAgregarLinks` は先に `URLProcessor.ProcessURLs` でフォルダー／ELC を展開（従来はフォルダーリンクが単一の壊れたタスク化）；`pckname` は `PathGuard` で無害化（従来は文字列連結で、認証済み任意ディレクトリ作成＋保存逃避が可能）；サブディレクトリ構造を保持
- フォルダースキップ計数：単一ノード復号失敗を計上し、部分スキップは Warning 記録（サンプル handle を含み、key 材料は記録しない）、全失敗時はエラーを送出（従来はゼロファイルでも成功報告）
- 0バイト空ファイルの短絡処理：存在確認後も0の場合は直接空ファイルを作成し MetaMAC を検証（期待値 `(0,0)`）、通常のリネームと成功イベント経路へ（従来は `GetDataPart` エラー + 自動復旧の空転）；リネーム競合ロジックを `RenamePartToReal` に抽出して共用
- 検証キャンセルフラグ：`bgArranque` は `CancelAsync` 不可のため `_StartupCancelled` フラグを追加し、Stop/Dispose 後に検証完了してもダウンローダーを再生成しない（従来は Stop 後に必ず復活）
- 不良 ELC の URL 単位分離：単条の失敗は当該条のみスキップし、同条の部分結果はロールバック、全滅時のみ先頭エラーを送出して単鎖セマンティクスを保持（従来は1件の不良 ELC で全バッチが全滅）

### 🐛 RC2 修正:信頼性4件(Batch-2)

([Fichero.vb](../Clases/Fichero.vb) / [Configuracion.vb](../Clases/Configuracion.vb) / [Main.vb](../Forms/Main.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb))

- 100% 補完 MD5 をグローバルロック外へ：ロック内では予約のみ（`ComprobandoMD5` 設定で重投防止）、ロック外でスレッドプール実行（従来は GB ファイル衝突で UI／调度が凍結）
- Web パスワードのランダム IV を設定重複比較から除外し、平文スナップショット比較に変更（従来は Web パスワードあり時に `Configuration.xml` が5秒ごとに必ず再書き込み）
- 終了待機に `CreandoLocal/Verificando/Descomprimiendo/ComprobandoMD5` を補完；検証書き戻しと `GuardarXML` は共に `FicheroDownloader` を保持（従来は終了時にキューが破断）
- 120秒ウォッチドッグ判定を30秒排出後に移動し、排出中に完了線を越えても失敗誤報としない（従来は成功／失敗の二重イベント競合で健全ファイルをエラーに固定し得た）

### 🐛 RC2 修正:体験と深層防御(Batch-3)

([URLExtractor.vb](../Clases/URLExtractor.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [StreamingModule.vb](../HttpModule/StreamingModule.vb) / [Main.vb](../Forms/Main.vb) / [Updater.vb](../Clases/Updater.vb) / [Criptografia.vb](../Clases/Criptografia.vb) / [SteganoManager.vb](../Stegano/SteganoManager.vb))

- 正規表現のシングルトン化（`Compiled` 共有インスタンス）：`URLExtractor` 全表走査、`MegaFolderHelper` ノード単位、`StreamingModule` 要求単位で `New Regex` しない（従来は数百リンク貼付で UI 凍結）
- fragment 復号：`UnescapeDataString` + 空白除去、`%23/%3D` 逃避鎖は恒久的「復号不可」とならない；デッドな `Contains(" ")` 分岐を削除
- 更新 URL は https のみ；`version.xml` は外部実体を禁止（XXE）；公開鎖の検証スキップは Warning 記録付きに変更；Stegano リモートは64MB＋30s上限；streaming 不正 mega 引数は400を返し500としない

### 🐛 RC2 修正:合流レビュー補修(Pre-merge Gate)

- 左欄タスク概観＋快捷入口：総速度／件数／キュー進捗／残り時間、既存430ms更新循環を再利用（新規タイマーなし）；解凍キュー／ストリーミングライブラリ／ログを首屏へ
- ストリーミングライブラリ Web インポートに条単位分離を追加：不良フォルダー／期限切れ ELC は当該条のみスキップ（従来は要求全体が無応答）
- クォータ同一イベント重複排除：サーキットブレーカー期間内の重複報告は段階引き上げも待機延長もしない（従来は多接続の並行命中で瞬時に6h段階へ跳躍）
- CI 単一ファイル検証を Windows PowerShell 5.1 実行に変更（従来は `pwsh` 下で ReflectionOnlyLoad が必ず例外となりビルドが紅灯）

### 📦 バージョン番号

- Assembly / FileVersion → `2.5.0.0`（RC は不変）

- InternalConfig `VERSION_MEGADOWNLOADER` → `2.5 RC2` / `VERSION_UPDATE` → `2.5`（数値は不変、beta/RC 期間は更新を通知しない；`docs/version.xml` は `2.4.7.0` のまま、正式版でのみ引き上げ）

***

## [2.5 RC1] - 2026-09-12

匿名ダウンロード MEGA クォータ（HTTP 509 / API -17）特集です。中心テーマ：**クォータを予測可能に——自動一時停止、正直なカウントダウン、時刻通りの自動復旧**。

### ✨ 新機能:クォータのグローバルサーキットブレーカー + カウントダウン横断幕

([MegaQuotaManager.vb](../Clases/MegaQuotaManager.vb) 新規 / [Conexion.vb](../Clases/Conexion.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [FileDownloader.vb](../Clases/FileDownloader.vb) / [Main.vb](../Forms/Main.vb))

- 509 と -17 の正規化識別：判定は `HttpStatusCode = 509`（状態コード）と `-17 / EOVERQUOTA`（API 意味論）のみ使用し、応答本文の文字照合は行わない。ファイル情報（:429 例外経路 + :364 数値経路）とフォルダー読み取り（:47 例外経路 + :51 数値経路）の2落点を網羅
- 段階的サーキットブレーカー：初回命中で60分一時停止、クォータ期間内の重複命中で2時間へ引き上げ、6時間上限；`Retry-After` があれば大きい値を採用（大半はないため依存しない）
- サーキットブレーカー期間中：新規タスクを開始せず、新規ファイル情報検証を行わず（検証1回＝API 呼び出し1回で、処罰窗口を延長させる）、チャンク失敗は16秒空転再試行とせず调度器が一律待機
- 主画面の横断幕：`MEGA クォータを使い切りました（正確な回復時刻は提示されていません）。自動的に一時停止し、X 時間 Y 分後に自動再試行します` + **[今すぐ再試行]**ボタン（IP 変更／プロキシ／ルーター再起動後に手動解除可能、締め出し防止）＋ ステータスバー countdown + 進入／解除時に各1回のトレイ吹き出し

### ✨ 改善:失敗時の自動復旧を既定で有効化（一次性移行付き）

([Configuracion.vb](../Clases/Configuracion.vb)) `ResetearErrores` 既定値を有効（15分）に変更；既存設定は `ResetearErroresMigratedV25` で一次性移行により有効化し、以降の手動選択は上書きされない；新規導入では直接有効。

### ✨ 改善:恒久失敗は自動復旧対象外 + 一括操作

([Fichero.vb](../Clases/Fichero.vb) / [Main.vb](../Forms/Main.vb)) `-9 ENOENT / -11 EACCESS / -14 EKEY / -16 EBLOCKED` に `EsErrorPermanente` 標識を付け、自動復旧はスキップ（15分ごとの幽霊復活でログ消費・クォータ消費を防止）；右クリックに**失敗をすべて再試行**と**失敗をすべて削除**を追加（確認框に件数表示、ローカル `.part` 同時削除を選択可能）。

### ✨ 改善:大フォルダー読み取りの進捗反馈

([MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb) / [AddLinks.vb](../Forms/AddLinks.vb)) フォルダー解析を UI スレッド外へ移し、進捗框に`フォルダーを読み取り中…N 件を解析済み` を即時表示し、取消に対応（結果を破棄し追加しない）；オンライン視聴も同様に非同期解析。

### 🐛 RC1 修正:クォータ誤報 + 再起動後のエラー消失 + 進捗バー灰色化(P1)

- クォータのサーキットブレーカー期間内のタイムアウトは `MegaQuotaExceededException` に変更（従来は120秒包括で汎用タイムアウトとなり、`FailedByQuota=False` で「今すぐ再試行」でも救済不可）；非クォータ120秒文言から「リンク期限切れ」誤導を除去し、総時間ウォッチドッグ（アイドル timeout ではない）であることを明記、進捗はレジューム点を保持
- `DescripcionError / EsErrorPermanente / FailedByQuota` をキュー XML に永続化（4000字切り詰め）、再起動後の「エラー表示」空白を解消、恒久失敗は自動復旧で繰り返し掘り起こされない；旧キューは記述文から逆推標識で互換；空記述では操作可能な fallback を表示し空白框としない
- 進捗バーのカスタム描画：背景を透明化して行底色を露出（ライト系全体灰色化を解消、0% 行の純灰を解消）；グラデーション除去は `FillColor` 単色；境界はテーマ `Border` 追従；バー高18；小進捗は最低2px保証
- ダウンロード一覧の列幅破損防止：10列に `Minimum/MaximumWidth` 追加（# 20–40／ファイル名150–700／他はコード参照）、`ColumnWidthChanging` 引き摺り時に挟み取り（OLV は表頭引き摺りを遮断しない）；起動時に破損状態（`#`／ファイル名が0圧縮、単列>800、可視総幅>2000）を自動初期化して保存；右クリックに「既定列幅に戻す」を追加（従来は `#` 列 `Hideable=False` ＋ 移行済みで、程序内に戻す入口がなかった）

### 🐛 RC1 補修:横断幕配置／クォータ意味論／ビルド可用性

- ビルド可用性：現代 MSBuild は resx を preserialized 形式に編成し、起動即崩壊（`My.Resources.icono` 处 `FileLoadException`、グローバル例外受け皿に呑まれ静默 exit 0）。`Resources\DLLs` に `System.Resources.Extensions/Memory/Buffers/Unsafe/Numerics.Vectors` を追加し、vbproj に参照を追加、`app.config` に版本リダイレクトを追加、CI 成果物目録を同期
- クォータ横断幕を絶対配置に変更：一覧＋両側欄 Top/Height は常に道具欄底＋横断幕表示有無から再計算し、相対変位と Bottom 錨を除去（従来は resize／最大化／DPI 変化後に隠蔽すると横断幕1個分の高さが余り状態欄を圧迫）
- クォータ期限切れ起動は自動復旧开关から分離：サーキットブレーカー解除縁で直接 `WakeQuotaFailedItems` を呼ぶ（従来は `If ResetearErrores` 内に隠れ、自動復旧を切った利用者は横断幕消失後も失敗項が恒久に復旧しない）
- 処罰漸進の修正：段階は24h減衰、期限切れ／手動除去でゼロ化しない（従来は漸進が到達不能で、手動再試行は60minに打ち戻して直ちに要求発射）；`Retry-After` は HTTP-date 対応
- countdown 文言：120秒以内は秒読み、以上は切り捨て（従来は末60秒が「1 min」に固着、1h59m30s が「2 h 0 min」表示）；横断幕色は主題系統へ（`QuotaBack/QuotaFore`）
- Toast 新建／再利用は四辺挟み取りを共用（従来は初弾が辺貼り時に半分屏外）；フォルダー進捗框の進捗バーは視覚様式を除去し主題追従、取消は真取消（`CancellationToken` を解析循環へ伝達）
- 调度循環の単点故障：反復級 Try/Catch + `RunWorkerCompleted` ウォッチドッグ3秒自救再起動（従来は1回例外で更新＋横断幕＋復旧が全停摆）；サーキットブレーカー期間中の開始点に Toast 提示（従来は押下無応答）

### 🐛 RC1 修正:UI 可感知欠陥(12件)

- `ThemeManager` に `ListBox / CheckedListBox / ToolTip` 分岐を追加（従来はダーク漏塗り、各窗体手書補修に依存）；頂層 `MainMenu` は依然系統菜単で既知制限として記録
- Toast 再利用は位置を再計算し屏跨ぎ过期座標を防止；クォータ横断幕 Top は道具欄＋DPI 縮放に追従；道具欄図標／案内行高は DPI 追従；状態欄 RAM/Proc は自動幅に変更（Designer 90→150）；既定框幅880→1024で Nombre 列を解放
- 設定検索は `NumericUpDown / ListBox / DataGridView` と ComboBox 候補に対応し、Label 非焦点時は親容器へ焦点退化、無命中時は蜂鳴反馈；設定 Cancel は `Bottom|Right` 錨に変更；ELC 表は肌替え後再描画で初幀旧色を防止
- AddLinks：文本除去は `HiddenLinks` 同期除去、占位符行数を計数に編入；透かし灰字はダーク可視；解凍密碼 `MaxLength` 6→128（2か所）；ELC 2 Label は既有鍵翻訳を再利用；Streaming 赤字は主題 `ErrorFore`＋無効鏈接に提示；`btnLanzarVLC.DialogResult` を None に変更し誤閉框を防止；Credits は `AcceptButton=lblTitle` 除去＋空訳文裸 key 保護

### 🌐 言語追加

`en-US` / `zh-CN` に `Quota_Banner / Quota_Status / Quota_RetryNow / Quota_Recovered / Quota_Error / Retry all failed / Remove all failed(+confirm/delete part) / Folder_Reading(+Count)` を追加；RC1 で `es-ES` に同11鍵を補完（他言語は en-US fallback 経由）。

### 📦 バージョン番号

- Assembly / FileVersion → `2.5.0.0`（RC は不変）

- InternalConfig `VERSION_MEGADOWNLOADER` → `2.5 RC1` / `VERSION_UPDATE` → `2.5`（数値は不変、beta/RC 期間は更新を通知しない；`docs/version.xml` は `2.4.7.0` のまま、正式版でのみ引き上げ）

***

## [2.4.7] - 2026-09-12

更新通知を3択に格上げ + 廃止された検索エンジン統合を削除。中心テーマ：**「通知頻度」の選択権を利用者に返し、死んだドメインを一掃**。

### ✨ 改善:更新通知の3択化

([Main.vb](../Forms/Main.vb) / [Configuracion.vb](../Clases/Configuracion.vb)) 新版弹框は「はい／いいえ」の2択から3択へ格上げ：**はい**=今すぐ更新；**いいえ**=3時間後に再通知；**キャンセル**=このバージョンでは再通知しない。`UpdateSkipVersion` 設定項が「再通知しない」選択を永続化（バージョンごとに記録、当該版本のみ遮蔽；将来の新版公開後は自動で通知再開）、選択後は直ちに保存。設定中の「更新確認」勾選框は依然総开关（解除で検出完全停止）。

### 🧹 保守整理:廃止された検索エンジン統合の削除

オリジナル「選択肢 → 探す」菜単が集成した4つの MEGA 検索エンジンドメイン（megafiles.me / megafindr.com / megasearch.co / megasearch.co.nz）はすべてサービス終了し、機能一式が利用不可：

- ([URLExtractor.vb](../Clases/URLExtractor.vb)) `mega://mega-search?...` 鏈接解析を削除（`MEGASEARCHPREFIX` 定数、正規表現様式、`CheckFileIDAndFileKey` の mega-search 解析分岐）
- ([Main.vb](../Forms/Main.vb)) 「探す」菜単構築と `Buscador_Click` を削除
- ([InternalConfig.xml](../Resources/InternalConfig.xml)) `SEARCH_LIST`（4死域名）と `MEGA_SEARCH_CURL` を削除
- ([InternalConfiguration.vb](../Clases/InternalConfiguration.vb)) 呼び出しのない `ObtenerValuesFromInternalConfig` を削除
- 10言語ファイルから `Searc&h` 死鍵を削除

### 🌐 言語追加

`en-US`／`zh-CN`／`zh-TW`／`es-ES` に `Update prompt hint` を追加（3択弹框の釦意味説明、他言語は en-US fallback 経由）。

***

## [2.4.6] - 2026-09-04

7件の見せかけの成功／サイレント失敗修正 + 1件の保守整理。中心テーマ：**失敗を失敗らしく見せる**。

### 🐛 修正:再起動後の見せかけの成功(P1)

([Paquete.vb](../Clases/Paquete.vb)) `MarcarFicherosComoParados` は従来 `Verificando`／`Descomprimiendo` 状態のファイルを `Completado` と標識していた——しかし `Verificando` は事前ダウンロードの瞬態（再起動後 `EstadoAnterior` は既に消失）、`Descomprimiendo` は事後ダウンロード完了だが解凍未了で、**どちらも「1バイトも保存されていないのに成功報告」となり得る**。現在は一律で `EnCola` に戻す：レジュームで継続し、完全なファイルは高速チェックサムのみで、ゼロから再取得しない。

### 🐛 修正:設定保存の見せかけの成功

([Configuration.vb](../Forms/Configuration.vb)) `Config.GuardarXML` 失敗時は下層でログ記録＋`ErrorConfig` 設定のみで、UI は「保存成功」と表示して框を閉じていた。現在は保存後に `ErrorConfig <> SinErrores` を検査し、失敗時はエラー表示のまま留まり、成功表示としない。新規エラー文言の言語鍵を追加。

### 🐛 修正:大文字リンクのサイレント破棄

([URLExtractor.vb](../Clases/URLExtractor.vb)) `ExtraerURLs` は `IgnoreCase` で `HTTPS://MEGA.NZ/...` に一致させた直後、大文字小文字区別ありの `ExtraerFileID` 検証失敗で直接破棄——利用者が大文字リンクを貼付しても無反応。6か所の `New Regex(pattern)` にすべて `IgnoreCase` を補完；付帯的に `#F`／`#N` 様式比較を `ToUpperInvariant` 化、`fenc`／`enc`／mega-search 前缀照合をすべて `OrdinalIgnoreCase` 化。捕獲組は元大文字小文字を保持し、FileID/FileKey の区別は影響されない。

### 🐛 修正:不正な enc リンクのクラッシュ

([URLExtractor.vb](../Clases/URLExtractor.vb) / [ServerEncoderLinkHelper.vb](../Clases/ServerEncoderLinkHelper.vb)) base64url 長さ `%4==1` は違法値で、元 `"==".Substring(3)` は `ArgumentOutOfRangeException` 裸崩壊。現在は事前判定で分かりやすいエラーを送出し、一律で「リンク無効」系提示を弹出。

### 🐛 修正:フォルダー API 空応答の NRE

([MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb)) `DeserializeObject` に Try なし、`FileList.f` 直接遍歴——不正応答（空串／代理 HTML）で NRE を送出。現在は Try/Catch + 空検査を追加し、一律で「無効なサーバー応答」を送出し、HTML 内容を漏らさずログ記録。

### ✨ 改善:サブフォルダーリンクの ELC 化で範囲消失としない

([ServerEncoderLinkHelper.vb](../Clases/ServerEncoderLinkHelper.vb)) `MegaLink` に `SubFolderID`／`SubFileID` を追加；`ServerEncode` は MegaFolder 時に `/folder/子ID` または `/file/ファイルID` サフィックスを付加。旧版デコーダーはサフィックスを無視（整フォルダーへ退化＝履歴動作）、新版デコーダーはサブ範囲を復元、**双方向互換**。`ExtraerSubFolderID`／`ExtraerSubFileID` は旧式 token（`mega://#F!...!/folder/...`、ELC 復号産物）サフィックス回退解析を補完。`enc`／`enc2` 暗号文は子範囲の格納処がなく、`EncodeLinksForm` は子範囲含み鏈接の符号化を跳過して原文保持し、整フォルダーへの黙示拡大を回避。

### 🌐 言語不足鍵の補完

`en-US`／`zh-CN` に各+5：`ELC created successfully`、`URL is mandatory`、`VLC path is not valid`、`Open &ELC`、`Configuration could not be saved...`。従来は不足鍵が `Language.GetText` の en-US fallback で英文原文表示となり、非崩壊だが中文界面で翻訳漏れ。

### 🧹 保守整理

- `docs/BUGFIX-CHECKLIST.md` を削除（2026-07-13 の審査目録は深刻に过期、少なくとも8か所 ⬜ は実際修了済み）
- `Resources\DLLs\xunit.dll` を削除（vbproj 参照なし、Fadd.dll の xunit 1.0.3 依存は元来解析不可、磁盤上1.9.1版本は未使用）
- vbproj からデッド参照 `TODO\TODO.txt` を削除
- README：Web 画面記述を「既定は127.0.0.1のみ束縛、LAN アクセス開放および束縛 IP 指定可」に訂正（実装と一致）；xUnit 関連条目を除去

***

## [2.4.5] - 2026-09-02

本版は全面コード審査後の体系的修正です：36件確認問題をすべて処理し、崩壊修正、機能正確性、HTTP 規約準拠、リソースリークと安全強化を網羅。

### 🛡️ グローバル例外の受け皿

([ApplicationEvents.vb](../ApplicationEvents.vb)) 従来は応用全体に未処理例外の受け皿が皆無——任意 UI スレッド例外で直接 .NET 崩壊対話框を弹出して進行を終止し、後台スレッド例外では進行が無声消失。現在：

- `My.UnhandledException`：例外を日誌書き込み後に**終了しない**、利用者は状態保存の機会あり

- `AppDomain.UnhandledException`：後台スレッド例外も少なくとも日誌手掛かりを残す

### 🐛 修正:一覧更新のクラッシュ（高頻度崩壊源）

([Main.vb](../Forms/Main.vb)) 事後ダウンロード状態／百分率／予測時間／進捗文本4つの `AspectGetter` の Catch 塊は英文堆棧を弹出して再 `Throw`——しかも AspectGetter は**毎行再描画時に実行**され、1行ごとに1框、閉じてから崩壊。日誌記録のみ＋安全占位値返却に変更。

### 🐛 修正:その他利用者可視エラー

| 症状 | 根因と修正 |
| ------------------------------------- | ----------------------------------------------------------- |
| ELC を開いて「取消」で "The path is not valid" | 取消時も空串で `AddDLC` 呼び出し；現在は静默退出 |
| 単一ファイル Reset 後すぐ Error に戻る | Fichero 分岐が `ResetearDescarga()` 呼び漏れ、残留 `.part` と錯誤状態；包分岐と一致するよう補完 |
| 初回実行の強制設定が「取消」で迂回可能 | 密碼框に占位符 `*****` が恒非空で、検証が恒到達不可；占位符は現在「未設定」と看做す |
| Web timeout 保存61-99が再開後空→5に改変 | 載入境界 `>60` と保存境界 `0-99` 不一致；0-99に統一 |
| キューファイル破損で起動即崩壊 | `CDate(strFecha)` 地域性関連かつ Try なし；`Date.TryParse` 双文化解析に変更、不良値跳過 |
| 後台弹框が主窗体背後に隠れ固まり様相 | DoWork スレッド直接 `MessageBox.Show`；`SafeShowError` 経由で UI へ編組し直し |
| 3後台 worker 報錯で整屏英文堆棧 | 堆棧は既入日誌、利用者は `ex.Message` のみ閲覧 |
| VLC 起動失敗で何ら反馈なし | 戻り値無視かつ Try なし；2呼出方で現在戻り値検査、`WatchOnline` 内部兜底 |
| 非 ELC/DLC 拖入が静默破棄 | 現在は提示あり |
| 未選択条目で「目録を開く」と空経路報錯 | 空経路は直接退出 |

### 🐛 修正:機能正確性欠陥

| 位置 | 修正 |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [DescompresorController.vb](../Clases/DescompresorController.vb) | **分割不足 RAR の見せかけの成功**：`IsComplete=False` 時に静默跳過かつ異常不置で、上流は「解凍成功」報告；現在は明示送錯。**重複 Code で「解凍中」固着**：静默跳過も True 返却；現在は既有隊列条目を更新 |
| [FileDownloader.vb](../Clases/FileDownloader.vb) | **UI 凍結最大16.5秒**：分塊失敗待避の `Thread.Sleep` 忙等が UI スレッド上で実行；スレッドプールへ移動。**"Must specify size" が真實錯誤を掩蔽**：Size=0 時に終尾塊を跳過。**Dispose は Mutex/MutexFile/trigger を解放せず**：現在は確定性解放 |
| [Configuracion.vb](../Clases/Configuracion.vb) | Web 密碼復号失敗の空 Catch：密文を密碼として保留し登入が恒失敗かつ手掛かりなし；現在は日誌記録＋除去 |
| [Conexion.vb](../Clases/Conexion.vb) | FileID 未逃避で直接 JSON/URL 連接；JSON 逃避 + UrlEncode を補完 |
| [Main.vb](../Forms/Main.vb) | 「包＋子文件」削除時に `CancellationComplete` 二重掛けで `Dispose` 2遍実行；既除去対象は直接跳過 |

### 🌐 HTTP モジュール:Range 準拠 + CSRF + 速度制限

- **RFC 7233**([StreamingModule.vb](../HttpModule/StreamingModule.vb))：`bytes=-N` サフィックス区間と `bytes=N-` 開放区間に対応；範囲外は416 + `bytes */size`（従来は静默挟み取り）；`bytes=0-0` 単バイト探測は1整列塊のみ取得（従来は MEGA へ文件全体を要求し帯域増幅）；尾塊は Content-Length まで切り詰め（応答本文の超発を廃止）

- **`?mega=`** **解析**：直接枠組解析の引数值を使用し、`?p=密碼&mega=...` 順序でも失敗しない

- **ストリーミングライブラリ CSRF**([StreamingLibraryModule.vb](../HttpModule/StreamingLibraryModule.vb))：Delete/Save/OpenVLC/ImportLinks/ExportLinks は現在 POST + 有効 token を要求（Web 画面の EnsureCsrf 様式を再利用）；模板に token 注入；**付帯的に閲覧頁 OpenVLC の GET による POST-only 接口呼び出し既存失効を修正**

- **ログイン速度制限**([WebInterfaceModule.vb](../HttpModule/WebInterfaceModule.vb))：PBKDF2（100k）は要求スレッド同期実行で、POST 集中はスレッドプールを打ち尽くし得る；現在は同時実行上限4 + 60秒窗口失敗鎖定10回

- 3か所の `StreamWriter(response.Body)` に `Using` + BOM なし符号化を補完

### 💾 リソースリーク

- `Criptografia.decrypt_key`：循環内 `CreateDecryptor` が Dispose されず（key 復号ごとに N 個の ICryptoTransform 漏洩）；Using 化

- `MD5Utils.MD5CalcString`：Using 補完

- `DescompresorController` ×4か所、`ThrottledStreamController` ×1か所のミューテックスに Try/Finally 補完（項目約定に適合）

- Configuration/PropiedadesDescarga 計5か所の懸停 ToolTip 漏洩；単一実例を再利用

### 🔒 Stegano(ステガノグラフィ)

- **先に壊してから報錯**：容量検証が書盤後に発生し、切り詰め .jpg を残留；メモリ符号化＋検証全通過後のみ落盤に変更

- **OpenWrite は尾部を除去せず**：より長い旧文件への上書き時に新旧バイト接合；`WriteAllBytes` 切り詰め上書き

- **Uri 検証が形骸化**：`RelativeOrAbsolute` は "hello world" に True 返却；Absolute + http/https/file に限定

### 🧹 保守整理

- vbproj からデッド参照 `TODO\TODO.txt`、`plantilla botones.psd` を削除；孤児文件 `postbuildevent.xml` を削除

- DPI 設定を PerMonitorV2 に統一（app.config に `DpiAwareness` 補完、myapp HighDpiMode=2）；未配備 Unsafe 程序集リダイレクトを除去

- README から存在しない xUnit 技術棧項を削除

- ハードコード英文消息（ELC/DLC 錯誤、Invalid input data など10か所）を言語系統へ接入し、en-US/zh-CN 条目を追加

- BUGFIX-CHECKLIST.md 頭部に「过期」警示を追加（少なくとも8か所 ⬜ は実際修了済み、重複労働防止）

***

## [2.4.4] - 2026-09-01

### ✨ 新機能:サブフォルダーリンクのダウンロード

**従来**：`mega.nz/folder/<ルートID>#<キー>/folder/<子ID>` 形式のリンクはルートフォルダーリンクと看做され、ルートフォルダー全体の全内容を事後ダウンロード。

**現在**([URLExtractor.vb](../Clases/URLExtractor.vb) / [MegaFolderHelper.vb](../Clases/MegaFolderHelper.vb) / [URLProcessor.vb](../Clases/URLProcessor.vb) / [StreamingLibraryManager.vb](../Clases/StreamingLibrary/StreamingLibraryManager.vb))：

| リンク形式 | 動作 |
| ---------------------------------- | -------------------------- |
| `mega.nz/folder/ルートID#キー/folder/子ID` | 指定サブフォルダーの内容のみダウンロード（経路はサブフォルダーを根に再基準付け） |
| `mega.nz/folder/ルートID#キー/file/ファイルID` | 指定単一ファイルのみダウンロード |
| `mega.nz/folder/ルートID#キー` | ルートフォルダー全体をダウンロード（不変） |

- 正規表現を拡張し `/folder/<子ID>` と `/file/<ファイルID>` サフィックスを捕獲

- 文件一覧は親節点鎖を上方遍歴して濾過し、目標子節点所属文件のみ保留

- 事後ダウンロード経路はサブフォルダー相対経路へ再基準付けし、余剰上層目録階層は出現しない

### 🐛 修正:サブフォルダーリンク完了後の MetaMAC 誤報（利用者実測確認）

**症状**：サブフォルダーリンクが100%まで事後ダウンロード後に MetaMAC チェックサム失敗エラーを弹出；文件内容は実際完全。

**根因**：各数据分塊の CBC-MAC 計算が**ゼロ初期 IV** を使用。一方 MEGA 真實算法（SDK `SymmCipher::ctr_crypt`）の分塊 MAC 初値は**文件 nonce 複製2份**——key 第4-5字（word）接合の16バイト `[n0, n1, n0, n1]`。ゼロ IV 算出 MAC は任意真實事後上传文件の MetaMAC と一致不可能で、8 words key（内嵌 MetaMAC）付き事後ダウンロードは必然報錯；単文件公開鏈接（4 words key、検証跳過）は影響されず、問題は従来掩蔽されていた。

**修正**([Criptografia.vb](../Clases/Criptografia.vb))：

```vb
' 修复前:零 IV(必然校验失败)
Dim chunkMac As Integer() = New Integer() {0, 0, 0, 0}
' 修复后:nonce(key 第 4-5 word)复制两份,与 SDK 一致
Dim chunkMac As Integer() = New Integer() {nonceWords(0), nonceWords(1), nonceWords(0), nonceWords(1)}
```

残りの部分（ゼロ初期値の畳み込み、`(m0^m1, m2^m3)` 最終圧縮、128 KiB × i チャンクスケジュール）は SDK `macsmac`／`ChunkedHash` ソースと行単位で照合して元来正確であることを確認済みのため、変更していません。

### 🔧 MetaMAC 検証の SDK 標準動作への整合

検証関数中の「各分塊境界で事前 MAC 検査し、前缀照合を許容」寛容逻辑を削除——SDK 権威実装（`generateMetaMac` + `macsmac`）は**文件全体読了後に一度だけ完全比較**。前缀照合は算法錯誤時代の誤判産物で、今回一併除去；任意位置の真實破損は依然硬失敗。

### 🔒 9件のセキュリティ強化

| 位置 | 修正 |
| ---------------------------------------------- | -------------------------------------------------------------------- |
| `Criptografia.StripNullCharacters` | `Replace(vbNullChar, "")` に書き直し、逐文字接合による位置偏移錯誤を除去 |
| `Criptografia.AES_EncryptString/DecryptString` | 失敗時は `Nothing` 返却で空串とせず；永続化点（Configuracion/Fichero）暗号化失敗時は書き込み跳過・旧値保持で、静默除去としない |
| AES 暗号文形式 | ランダム IV 形式 `{1}\|\|IV\|\|密文` を追加、旧形式と自動双方向互換 |
| `FileDownloader` | MetaMAC 不一致は異常送出＋分塊再試行初期化（本次算法修正と配合し、誤報を生じない） |
| `Criptografia.GetFileKeyFromPreSharedKey` | PSK 非 ASCII 文字（>255）含有時は日誌記録＋`Nothing` 返却で、鍵流錯位を杜絶 |
| `WebInterfaceModule` | Web 密碼保存はランダムソルト + PBKDF2（100k 輪）派生に変更し、無塩 MD5 を置換 |
| `StreamingModule` / `StreamingLibraryModule` | 密碼比較は `Criptografia.FixedTimeEquals` 定数時間比較に変更し、時序側信道を防止 |
| `ClientConnected` リフレクション | 静態 `MemberInfo` 緩存 + null 検査 + Try/Catch、失敗時は「接続済みと仮定」へ降級 |

### 📦 バージョン番号

- Assembly / FileVersion → `2.4.4.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.4`

- `docs/version.xml` → `2.4.4.0`

***

## [2.4.3] - 2026-08-26

### ✨ 新機能(Issue #1)

| 機能 | 説明 |
| ------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 7z 解凍対応 | SharpCompress は 7z 容器に未対応（従来は全 .7z 解凍が必然失敗）。現在は系統已導入の 7-Zip CLI を優先呼び出し、未導入時は内蔵 7zr.exe（パブリックドメイン、7-Zip 公式精簡版）を `%LOCALAPPDATA%\MegaDownloader\bin` へ自動放出して使用。密碼と multipart 分割（.7z.001/.002/...）に対応 |
| Web サーバー LAN プッシュ | 設定 → Web サーバーに「LAN アクセスを許可」开关を追加（既定閉鎖）。既定は依然127.0.0.1のみ束縛；開放後は全 NIC 束縛で、携帯／LAN 機器から瀏覧器経由で推送事後ダウンロード。開放は服務器密碼設定（≥8文字）を強制し、配置保存と服務器起動の二重検証 |
| LAN カスタム束縛 IP | 「LAN アクセスを許可」開放後に「束縛IP（空欄＝全部）」入力框を追加：空欄は全 NIC 待聴；指定 IP 記入（例 `192.168.1.100`）時は当該 NIC のみ待聴し、多 NIC／仮想 NIC 環境で露出面を精確制御。保存と起動の二重検証で IP 格式を検証し、非法地址は起動拒否＋報錯 |

### 🐛 修正:クリップボード監視の Web コピー検出漏れ(Issue #1)

**症状**：Web頁から MEGA リンクを複写しても追加框が弹出しなくなり、部分応用内 Ctrl+C のみ有効。

**根因**：瀏覧器（Chrome/Edge/Firefox）は**遅延描画**を使用——クリップボード変化通知到達時に数据が未だ真に書き込まれていない；即時読取は空値を得るか `CLIPBRD_E_CANT_OPEN` を送出（クリップボードは依然源進行に占有）。

**修正**([Main.vb](../Forms/Main.vb) / [ClipBoardViewer.vb](../Clases/ClipBoardViewer.vb))：

- 読取は再試行制に変更：最多5回、間隔150ms、遅延描画とクリップボード占有競合を網羅

- `WndProc` 中のクリップボード接近は全部に異常保護を追加し、瞬時失敗は消息循環を中断しない

- 処理完了後のクリップボード標識書き戻し失敗時は無視へ降級し、崩壊しない

### 🔒 7z 解凍の安全詳細

- 解凍前に `l -ba -slt` で全部条目を列挙し、PathGuard 検証で経路逃避（Zip Slip）を拒否、検証通過後のみ解凍実行

- CLI 引数中の密碼（`-p`）は恒久的に日誌書き込みしない

- 子進行 stderr 非同期読取で、管道緩衝区デッドロックを回避

- 終了符号 0/1（成功／警告）は放行、2（致命、密碼錯誤など）は出力尾部付きで分かりやすいエラーを送出

***

## [2.4.2] - 2026-08-19

### 🐛 修正:ダウンロードファイルの実破損（利用者実測確認）

**症状**：事後ダウンロード完了かつ文件尺度が精確一致するも、文件内容が破損して使用不可。日誌は旧版（含 v2.4.1）が MetaMAC チェックサム失敗後に依然「警告＋放行」してリネーム完了し、破損文件が直接落盤したことを証実。

**根因**（3独立欠陥の重畳）：

1. **MetaMAC 分塊调度算法錯誤**：v2.4.1 は「128K 起歩倍増成長、8 MiB/1 MiB 双封頂」调度を採用し、MEGA 公式 SDK `ChunkedHash::chunkfloor/chunkceil` の真實调度と不一致、大量合法文件が誤判 mismatch（下流放行逻辑の口実も製造）
2. **mismatch 放行策略**：算法錯誤の前提下、v2.4.1 は「mismatch 即失敗」を「警告記録、照常完了リネーム」へ回退——検証は形骸化し、真實破損（URL 期限切れ後403期間の空洞書き込みなど）は直接放行
3. **非整列レジュームによる CTR 鍵流錯位**：接続中断時の best-effort flush は16バイト整列不足の進捗を永続化；再試行時 `SeekToFileOffset` は整塊定位のみで鍵流を定位し、錯位点以降**後続全数据が復号錯位**——「尺度正確だが内容破損」の直接成因

**修正**：

| 位置 | 修正 |
| ----------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| `Criptografia.ComputeMegaFileMac` | 分塊调度を MEGA SDK 線形境界へ変更：128 KiB × i（i=1..8、即128/256/384/512/640/768/896 KiB）、以後固定1 MiB；双 cap fallback を削除し単次計算 |
| `Criptografia.VerifyMegaMetaMac` | 空文件は直接 (0,0) 返却（MEGA 空文件 MetaMAC 即0） |
| `FileDownloader.downloadFile` | MetaMAC 不一致は錯誤日誌記録だが MEGA SDK 寛容策略で継続完了（SDK は履歴遺留「MAC 欠失尾部条目」にも同様寛容）；文件尺度精確検証は硬門禁として保留 |
| `FileDownloader.FlushToDisk` | 中断 flush 時は永続化進捗を16バイト境界へ下方整列し、非整列レジューム点を杜絶（<16バイト已復号数据は再試行時自動再取得） |
| `ChunkDownloader_DoWork` | レジューム要求前に起点整列を検証：非16バイト整列のレジューム起点は直接 chunk 中止で、鍵流錯位を防止 |
| `DataPart.ValidateAndNormalize` | 起動時に旧版遺留の非整列 XML 進捗を自動で16バイト境界へ切り戻し |
| `FileDownloader.downloadFile`（v2.4.1 已導入） | 保留：「文件尺度照合即強制完了」force-finish を除去；真實 chunk 全完了時のみ完了判定；120秒 timeout は失敗報告＋断点保持 |

### 📦 バージョン番号

- Assembly / FileVersion → `2.4.2.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.2`

- `docs/version.xml` → `2.4.2.0`

***

## [2.4.1] - 2026-08-15

### 🐛 修正:ダウンロード完了だがエラー表示（利用者実測確認）

**症状**：文件が100%まで事後ダウンロード時に直接エラーを弹出し、`.part` 文件はリネームされない；手動で `.part` 接尾辞を除去後は文件が正常使用可能で、文件実際已完全取得を証明。

**根因**：v2.2.0 導入の MEGA MetaMAC 完全性チェックサムに系統的誤報あり：

1. **単文件公開鏈接は必然誤報**：公開鏈接の FileKey は僅か16バイト（4 words、AES 鍵本体のみ）、**MetaMAC を含まない**；一方 `VerifyMegaMetaMac` は少なくとも8 words（32バイト、nonce + MetaMAC 含む）を要求し、不満足で直接 False 返却 → 完了経路は "Integrity check failed" 送出 → 状態錯誤、リネームせず。フォルダー API 返却の32バイト node key のみ真に MetaMAC を含むため、誤報は最多見の単文件鏈接場面に集中
2. **8 words key の境界規則差異も誤報し得る**：MEGA 客戸端履歴上の MAC 分塊境界規則に版差異あり、不一致≠文件破損（尺度精確検証はより強証拠）

**修正**（2層防護）：

| 位置 | 修正 |
| -------------------------------- | ------------------------------------------------------------------- |
| `Criptografia.VerifyMegaMetaMac` | 4 words 公開鏈接 key は日誌説明「検証可能 MetaMAC なし」で跳過（True 返却）、失敗誤判としない |
| `FileDownloader.downloadFile` | 8 words key の MetaMAC 不一致は日誌警告へ降級し継続完了リネーム；文件尺度精確検証（不一致は依然報錯）は主要完全性防線として保留 |

### 📦 バージョン番号

- Assembly / FileVersion → `2.4.1.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4.1`

- `docs/version.xml` → `2.4.1.0`

***

## [2.4.0] - 2026-08-14

### 🐛 全面バグ修正 - 21件確認済み問題

v2.3.0 全項目 code の逐文件審査に基づき、確切 code 証拠で確認された21件のバグを修正し、利用者可感知報錯、デッドロック／リソースリーク、並行欠陥、異常呑込、安全債務とデッド code を網羅。

### 一、利用者層で感知可能な報錯

| 修正 | 説明 |
| --------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| 後台スレッド MessageBox で事後ダウンロード固着 | `FileDownloader.bgwDownloader_DoWork` の Catch 塊はスレッドプールスレッド上で `MessageBox.Show` を呼ばず、`ReportProgress(FileDownloadFailedRaiser)` 経由で失敗を報告し UI スレッドが一律呈示に変更 |
| 終了期間クロススレッド MsgBox 崩壊 | `Main.vb` 3か所の `BackgroundWorker.DoWork` 異常分岐は直接 `MsgBox` とせず、`SafeShowError` 補助方法を新規追加し `IsDisposed`／`IsHandleCreated` を検査して `Invoke` で UI スレッドへ切戻し |
| 7z multipart 解凍崩壊 | `DescompresorController` 2か所の `NotImplementedException` を分かりやすい消息付き `NotSupportedException` に変更；`DescompresionFinalizada` イベントは錯誤消息伝達に拡張し、利用者は錯誤状態で具体原因を閲覧可能 |
| 兜底完了で MD5 チェックサム／解凍漏れ | `Fichero.ActualizarDatosDescarga` の兜底状態修正は状態反転のみとせず、完全 `downloader_Completed` 流程呼び出し（MD5 チェックサムと自動解凍含む）に変更し、「完了表示だが完全性未検証」を回避 |

### 二、デッドロックとリソースリーク

| 修正 | 説明 |
| ---------------------- | ----------------------------------------------------------------------------------------- |
| ミューテックス Try/Finally なしでデッドロック | `Main.AgregarPaquete` と `bgwComprobarMaxConexiones` 2か所のミューテックスに `Try/Finally` を追加し、中間異常で恒久デッドロックとしない |
| 事後ダウンロード worker 未 Dispose | `FileDownloader.Dispose` で `listDownloaders` 内の全 worker を循環放出し、`CancelAsync` のみとしない |
| bgArranque worker リーク | `Fichero.Dispose` に `bgArranque` 放出を追加（事後ダウンロード起動 worker）、閉鎖期間の起動段階中断は漏洩しない |
| ELCForm 300ms 忙輪詢 | `AutoResetEvent` イベント駆動に変更し、無任務時はゼロ CPU 消費、有任務時は即時応答 |

### 三、並行処理と逻辑欠陥

| 修正 | 説明 |
| -------------------- | ----------------------------------------------------------------------------------- |
| AJAX 応答の並行汚染 | `StreamingLibraryModule._RespuestaAjax` を `AsyncLocal(Of String)` に変更し、並行 HTTP 要求は応答を上書きしない |
| FlushFinalBlock 異常呑込 | `ServerEncoderLinkHelper.Cipher` 復号経路の空 Catch を `Log.WriteError` に変更 |

### 四、異常呑込（真實故障の掩蔽）

| 修正 | 説明 |
| ------------------ | ----------------------------------------------------------------------------------------- |
| FlushToDisk 磁盤錯誤被呑 | `FileDownloader.ChunkDownloader_DoWork` 中 `FlushToDisk` の空 Catch を日誌化に変更 |
| 服務器錯誤応答読取失敗被呑 | `Fichero.downloader_FileDownloadFailed` ／ `downloader_ChunkDownloadFailed` 2か所の空 Catch を日誌化に変更 |
| 解凍取消異常被呑 | `Main` 閉鎖流程中 `RequestCancel` の空 Catch を日誌化に変更 |

### 五、安全と技術債務

| 修正 | 説明 |
| ------------------------- | --------------------------------------------------------------------------- |
| DPAPI entropy ハードコード | `Criptografia` の DPAPI entropy を程序集標識 SHA256 派生に変更し、legacy entropy による旧数据復号を保留 |
| ZIP 密碼ハードコード "passZIP" | `Fichero` の ZIP 解凍密碼暗号化は DPAPI 使用に変更し、復号は DPAPI 先行＋旧 AES 回退で旧隊列文件に互換 |
| OptionalPassword デッド字段 | `Cache.OptionalPassword` 字段および XML 書き出しを削除（宣言後未代入・未読取） |
| RandomNumberGenerator 未放出 | `ServerEncoderLinkHelper` の `RandomNumberGenerator.Create()` を `Using` 包裹放出 |
| 日誌 UTC 時刻戳なし | `Log` 全時刻戳を `DateTime.UtcNow` 化（`Z` 接尾辞付加）、30日日誌保留除去策略を新規追加 |

### 六、デッドコード整理

| 修正 | 説明 |
| ------------------ | -------------------------------------- |
| Criptografia 注釈デッドコード | 注釈化 `DecryptFile` と `cipherData` 関数を削除 |
| Conexion デッドコード | 注釈 `GetAppID` と無呼び出し `LeerNodo` 関数を削除 |

### 📦 バージョン番号

- Assembly / FileVersion → `2.4.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.4`

- `docs/version.xml` → `2.4.0.0`

***

## [2.3.0] - 2026-08-13

### 🐛 安定性修正

code 審査に基づき、崩壊、リソースリークと潜在デッドロック問題を一括修正。

| 修正 | 説明 |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| AES 暗号化失敗のクラッシュ | `AES_EncryptString` 暗号化異常後も `Nothing` に対し Base64 変換して二次例外送出；失敗時は空串返却＋`Using` で `RijndaelManaged`／`CryptoStream`／`MemoryStream` を放出に変更 |
| AES 復号切断／不良入力のクラッシュ | `AES_DecryptString` の `Convert.FromBase64String` を異常処理へ移入；`CopyTo` で平文を完全読取（元単次 `Read` は切断し得る）；失敗時は空串返却 |
| ダウンロード項のリソースリーク | `Fichero.Dispose` を空実装から `FileDownloader` 放出＋空置きに変更 |
| 潜在デッドロック | `FileInfo.Size` setter の `ReleaseMutex` を `Try/Finally` 化し、循環内異常で恒久デッドロックとしない |
| レジストリハンドル漏洩 | `RegisterInStartup` のレジストリ鍵を `Try/Finally` + `Close()` 放出 |
| 危険な `Thread.Abort` | DLC 処理30秒 timeout はスレッド硬殺を廃止し、協調式標識失敗＋ worker 自然終了に変更 |

### 📦 バージョン番号

- Assembly / FileVersion → `2.3.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.3`

- `docs/version.xml` → `2.3.0.0`

## [2.2.1] - 2026-08-09

### 🐛 ダウンロード状態の修正

利用者報告の2件のダウンロード完了状態表示問題を修正。

### Bug 1: 複数ファイル完了(100%)だがエラー表示

- **根因**: `FileDownloader.downloadFile()` の `Finally` 塊は `exc` 非空時に `FileDownloadFailedRaiser` を報告。全部塊已成功完了（`AllFinished = True`）でも、既往発生の非致命異常が依然失敗イベントを触発し、状態を錯誤的に `Erroneo` 化。

- **修正**: `Finally` 塊で `AllFinished` 状態を検査し、事後ダウンロード実際完了時は `exc` を除去し、警告記録のみで失敗報告としない。

### Bug 2: 単一ファイル完了(100%)だが依然「ダウンロード中」表示

- **根因**: `Completed` イベントは `bgwDownloader_RunWorkerCompleted` でのみ触発され、待機循環が競合条件で退出不可時は、`Completed` は恒久に触発されず、状態は `Descargando` に停留。

- **修正**: 3層防護

  1. **イベント層**: `FileDownloadSucceeded` 処理器を新規追加し、文件検証とリネーム成功後に直ちに `Completado` 状態を設定
  2. **循環層**: 待機循環に60秒 timeout 検査を追加し、磁盤文件尺度照合時は強制完了；120秒硬 timeout でデッドロック防止
  3. **計時器層**: `ActualizarDatosDescarga` に兜底検査を追加し、進捗100%かつ `AllFinished` 時に自動状態修正

### 📦 バージョン番号

- Assembly / FileVersion → `2.2.1.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.2.1`

- `docs/version.xml` → `2.2.1.0`

***

## [2.2.0] - 2026-07-20

### 安全強化とダウンロード完全性

深度靜態監査結論に基づき、経路安全（P0）、ダウンロード完全性（P1）と一括信頼性／公開現代化（P2/P3）修正を完了。

### 🔒 経路安全（P0）

- 統一 `PathGuard`：遠端文件名／目録名、解凍条目、削除と書出はすべて canonical 事後ダウンロード根目録内に制限

- Zip Slip 修正：解凍前に全部条目を検証し、`../`、絶対経路、設備名など逃避を拒否

- MEGA フォルダー経路接合と任務削除の範囲外危険を修正

### 📦 ダウンロード完全性と信頼性（P1）

- 事後ダウンロード完了前に **MEGA MetaMAC** を検証；失敗時は最終文件へリネームしない

- HTTP Range：Partial Content / Content-Range を検証；Range 無視の錯誤応答を拒否

- 早期 EOF は失敗扱い；CTR counter は Int64 seek を使用し、大偏移危険を修正

- 断点元数据を検証し、`.part` 欠失時の「偽完了」を回避

- 配置とダウンロードキューは原子保存（`AtomicFile`）；HTTP 既定 timeout；日誌脱敏

- 遠隔 Web：Stop/Play/AddLink を POST + CSRF 化；Streaming 媒体 URL は loopback 固定

- 解凍は協調取消（Thread.Abort 除去）、解凍結果の成功／失敗分離、資源配額

- 閉鎖順序：先に Web 停止 → worker／解凍取消 → 事後ダウンロード停止 → 保存

### ✨ 体験と工程（P2/P3）

- 配置模型層上限（Buffer／接続数／速度）、磁盤空間予検、文件名衝突と進捗除零防護

- 言語：内蔵包と利用者自定義を分離し、欠 key は en-US へ fallback

- 単一実例 IPC は行単位書き込みで、鏈接引数粘連を回避；主題 Auto は系統追従で即時変化

- 製品 xUnit 依存と MPRESS Release 後処理を除去；DPI PerMonitorV2

- 版本比較を規範化；DLC 入口は discontinued 標識（ELC 保留）

### 📦 バージョン番号

- Assembly / FileVersion → `2.2.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.2`

- `docs/version.xml` → `2.2.0.0`

***

## [2.1.0] - 2026-07-19

### 主題整備 - ダークモード可用性修正

v2.0 主題枠組に基づき、深色様式下の主一覧、進捗バー、釦辺框、右鍵菜単など核心観感問題を修正し、Dark 主題を真に可用化。

### 🐛 修正

- **主ダウンロード一覧縞模様**：`FormatRow` は `White`／`Honeydew` 直書を廃止し、`ThemeManager` の `Back`／`AltBack` 使用に変更

- **進捗バー色**：`BarRenderer` は Azure/SpringGreen 使用を廃止し、主題 token（`ProgressBack`／`ProgressFill` など）に変更

- **状態前景色**：錯誤／完了行は `ErrorFore`／`SuccessFore` 使用（深色下でより明るい赤／緑）

- **設定保存後の即時肌替え**：Configuration 保存主題後に `Main.ApplyCurrentTheme()` を呼び出し、再起動不要

- **釦白辺**：`FlatStyle.Standard` の系統3D 高光は深色下で白辺化；`FlatStyle.Flat` + 主題 `Border`／`ButtonHover`／`ButtonPressed` に変更

- **GroupBox / TabPage**：Flat 辺框と `UseVisualStyleBackColor = False` で、系統浅色描辺を削減

- **ELC 账号表**：Azure/Snow/SeaShell ハードコードを除去；空一覧提示は主題前景色に変更

- **右鍵菜単**：反射主題化 Form 上の `ContextMenuStrip`；`ToolStripDropDownBackground` など `ThemeColorTable` 属性を補完

- **未適用主題窗体**：Stegano 導向、SplashScreen、Cerrando は Load 時に `ApplyTheme`

### ✨ 改善

- `ThemeManager.GetColor(key)` 公共取色 API

- 新規語義／交互 token：`ErrorFore`、`SuccessFore`、`Progress*`、`ButtonHover`、`ButtonPressed`

- `ToolStripBorder` は `ToolBorder` token を正確使用

### 📦 バージョン番号

- Assembly / FileVersion → `2.1.0.0`

- InternalConfig `VERSION_MEGADOWNLOADER` / `VERSION_UPDATE` → `2.1`

- `docs/version.xml` → `2.1.0.0`

***

## [2.0.0] - 2026-07-13

### 重大版 - 安全強化 + コード整理 + 暗色主題

v1.9 の鏈接格式修正に基づき、4段階計60件超の修正を完了し、安全性、安定性と可用性を顕著向上。本版で初めて深／浅色主題切替を導入。

### ✨ 新規

- **ダーク／ライト主題切替**：

  - `ThemeModeType` 列挙を新規追加（Auto/Light/Dark）、既定 Auto 追従系統（[`Clases/ConfiguracionUI.vb`](../Clases/ConfiguracionUI.vb)）

  - `ThemeManager` 類を新規追加し、注冊表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` 読取で系統深浅色を検出（[`Clases/ThemeManager.vb`](../Clases/ThemeManager.vb)）

  - 自定義 `ThemeColorTable` + `ToolStripProfessionalRenderer` 描画器で、30超 ToolStrip 漸変／辺框／選択色属性を上書き

  - 遞帰的に主題を全制御へ適用し、主窗体の `BrightIdeasSoftware.TreeListView`（ダウンロード一覧）、StatusStrip、ContextMenuStrip、TableLayoutPanel、DataGridView、ListView、TreeView、ProgressBar などを含む

  - 9子窗体は Load イベントで主題適用：Credits、AddLinks、ELCForm、EncodeLinksForm、PropiedadesDescarga、StreamingForm、Descompresor、PantallaMsg、Configuration

  - 10種言語文件に `Theme` / `Theme_Auto` / `Theme_Light` / `Theme_Dark` 翻訳鍵を追加

- **作者情報**：Credits 窗体に "Yingxue - Revival maintainer (v2.0+)" を加入

- **更新確認**：本 GitHub 倉庫へリダイレクト

### 🐛 修正

**P0 深刻安全漏洞**：

- 空密碼迂回検証逻辑を修正

- 代理資格情報が WebProxy へ実際代入されない問題を修正

- TLS 1.2 のみ有効化（TLS 1.0/1.1 除去、現代安全標準に適合）

- Web／Streaming 服務器は `127.0.0.1` に束縛（元 `0.0.0.0` は全網露出）

- 密碼哈希は UTF-8 符号化に統一

- ミューテックス操作は全部 Try/Finally 包裹でデッドロック防止

- 暗号 code 中の空 Catch 塊は日誌記録に置換

- `ApagarPC` ／ `MaxConexionesGuardadas` 設定の未正確永続化を修正

- ダウンローダー `NullReferenceException` 崩壊を修正（変数名錯誤 `exc` vs `ex`）

**P1 リソースリーク**：

- 7か所 ToolTip リソースリークを修正（`ELCAccountControl` ／ `AddLinks` ／ `SteganoWizardSave`、MouseHover ごとに生成して放出せず）

- `SteganoManager` の `Image.FromFile` 源文件鎖定 + FileStream 未 Dispose を修正

- `Main.vb` 7か所の `Image.FromStream(stream)` stream 早期閉鎖を修正し、`LoadEmbeddedImage` 補助方法を新規追加

- `WebInterfaceModule` StreamReader/StreamWriter 未 Using を修正（模板載入 + response.Body 書き込み、後者は `leaveOpen:=True` 使用）

- `StreamingLibraryManager` `CompressString` ／ `UnCompressString` 未 Using を修正（入子 Using 塊）

- `MegaURIProtocol` 注冊表操作の Finally 放出なし + 中間変数上書きによるハンドル漏洩を修正

- `Main.vb` `clipChange` 閉鎖順序錯誤を修正（Uninstall は DestroyHandle 以前とすべき）

- `Main.vb` `EsperarParadaDescargasYWorkers` の `bgwDescompresorCompleted` 検査漏れを修正

- `StreamingLibraryModule` `Case "Delete"` の `Return True` 欠落を修正し、次分岐への貫通を防止

- `StreamingLibraryModule` `UsuarioLogueado` timeout 後 session 未除去を修正し、登入状態の恒久停留を解消

- `ELCAccountControl` `CellClick` の `e.RowIndex` 未検証を修正し、表頭押下の崩壊を防止

- `StreamingHelper` `Keys.Count / 2` 浮動除法を修正し、整数除法 `\ 2` を使用

**P2 規約現代化**：

- `%SEQ%` ／ `%ID%` 序列号は元 `DateTime.Now.Millisecond` の ticks 使用（範囲0-999、並行要求で重複）で、`Interlocked.Increment` 進行内自増に変更

- `MegaFolderHelper.vb` 中 `http://mega.co.nz/#N!` → `https://mega.nz/#N!`

**P2 code 品質**：

- `Paquete.vb` ／ `Configuracion.vb` は `GetHashCode` で配置 XML 比較（一致性不保証）で、直接 `OuterXml` 文字串比較に変更

- `MegaFolderHelper.vb` 2か所の変数 `ex`（Regex）→ `rx`（`Catch ex` 混淆回避）

- `ThrottledStream.vb` 変数名 `int`（VB.NET 关键字）→ `bytesRead`

- `Clases/Mutex.vb` 類名が `System.Threading.Mutex` を遮蔽、注釈説明＋別名方案を提供

- `StreamingModule.ClientConnected`、`FileDownloader` Range 頭反射に注釈説明の必要性を追加

- `LibraryElement.ToJSON` 手工 JSON 接合に注釈制限説明を追加

### 🗑️ 削除

- **4 Crypter**：`EncrypterMega.vb`、`MegaCrypter.vb`、`Youpaste.vb`、`LinkCrypter.vb`（API 全部終了）

- **3 MovieInfo**：`Allocine.vb`、`Filmaffinity.vb`、`IMDB.vb`（API 全部変更）

- **鏈接補助**：`DLCHelper.vb`、`Linkdecrypter.vb`、`LinkProtectors.vb`、`Serializer.vb`、`ClipboardChangeNotifier.vb`

- **MegaUploader 菜単**：「Get MegaUploader」菜単項を除去

- **goo.gl 短鎖**：14 Google 短鎖を全部 GitHub 直鎖に置換

- **Ping 報告**：元作者服務器への利用者／版本情報報告を除去（隠私保護）

- 計11 `.vb` 文件削除＋関連引用整理

### ⚠️ 既知問題

- `Thread.Abort()` 危険使用（3か所、Main.vb / DescompresorController）

- クロススレッド MsgBox は窗体已閉鎖検査なし（3か所）

- `MegaFolderHelper.FillFolderStructure` 遞帰は KeyNotFound 保護なし

- `ELCForm` 無限循環は300ms 輪詢

- `ServerEncoderLinkHelper` RandomNumberGenerator 未 Dispose

- `FileDownloader.FlushToDisk` FileStream 異時放出

### 📦 構築成果物

- `MegaDownloader.exe` 主程序

- 依存 DLL：`BouncyCastle.Crypto.dll`、`Newtonsoft.Json.dll`、`SharpCompress.dll`、`ObjectListView.dll`、`HttpServer.dll`、`Fadd.dll`、`F5Lib.dll`、`xunit.dll`

***

## [1.9.1] - 2026-07-05

### 🐛 修正

- **ダウンローダーのクラッシュ**：[`Clases/FileDownloader.vb`](../Clases/FileDownloader.vb) 第681-683行の変数名錯誤による `NullReferenceException` を修正。MEGA 服務器が502 gateway 錯誤など異常を返却時、catch 塊が已清空 `exc` 局部変数を誤引用（正しくは `ex`）し、真實異常を掩蔽して事後ダウンロード流程全体を中断していた。

***

## [1.9.0] - 2026-07-05

### MegaDownloader 復活計画初の公開版

MegaDownloader v1.8 逆編訳源に基づき修正と再構築を行い、MEGA 新版鏈接格式対応の回復を核心目標とする。

### ✨ 新規

- **URL 解析**：[`Clases/URLExtractor.vb`](../Clases/URLExtractor.vb) の `patternHTTPURI` に4条正規表現を新規追加し、以下新版 MEGA リンクの識別に対応：

  - `https://mega.nz/file/<FileID>#<FileKey>`

  - `https://mega.nz/folder/<FolderID>#<FolderKey>`

  - `https://mega.co.nz/file/<FileID>#<FileKey>`

  - `https://mega.co.nz/folder/<FolderID>#<FolderKey>`

- **フォルダー識別**：`IsMegaFolder` 方法を同期更新

- **TLS 1.2/1.3**：[`Clases/Conexion.vb`](../Clases/Conexion.vb) で `Tls12 | Tls11 | Tls` 規約を明示有効化

- 本倉庫の [README.md](README.md)、[CONTRIBUTING.md](CONTRIBUTING.md)、[CHANGELOG.md](CHANGELOG.md)、`.gitignore` など開発者文書を追加

### 🐛 修正

- クリップボードから新版 MEGA リンクを複写しても識別されない問題を修正

- 瀏覧器から新版 MEGA リンクを主窗口へ拖拽しても無効となる問題を修正

- 新版フォルダーリンクが子文件一覧へ解析されない問題を修正

- **フォルダー事後ダウンロード時の Base64 復号錯誤を修正**：`mega.nz/folder/` リンクが複数利用者に共有された文件を含む時、MEGA API 返却の `fileN.k` 字段格式は `handle1:key1/handle2:key2[/handle3:key3]`（`/` 区切りの複数 `handle:key` 対）。元 code `fileN.k.Substring(fileN.k.IndexOf(":") + 1)` は最初 `:` 以後の全内容（含 `/handle2:key2`）を key と看做し、`Convert.FromBase64String` が FormatException を送出。修正方案：`ExtractKeyFromK` 補助関数を新規追加。

### 🔄 変更

- `TargetFrameworkVersion` は `v4.8` 維持（元 v1.8 ですでに4.8へ昇級済み）

- 倉庫 LICENSE は MIT 規約維持、復活計画版権声明を補充

### ⚠️ 既知問題

- EncrypterMe.ga は公式 API 服務（`http://encrypterme.ga/api`）已終了のため、現在この種鏈接は解析不可

- 部分 goo.gl 短鎖は Google 当該服務閉鎖により跳転不可

- 簡体中文言語包は依然部分条目の翻訳補充が必要

### 📦 構築成果物

- `MegaDownloader.exe` 主程序

- 依存 DLL：`BouncyCastle.Crypto.dll`、`Newtonsoft.Json.dll`、`SharpCompress.dll`、`ObjectListView.dll`、`HttpServer.dll`、`Fadd.dll`、`F5Lib.dll`、`xunit.dll`

***

## [1.8.0] - オリジナル (逆コンパイルソース)

復活計画が基づく元始版で、本倉庫は逆編訳で其源を得て修正起点とする。

### 主要特性

- マルチスレッド並行ダウンロード

- MEGA フォルダー遞帰解析

- 暗号化リンク (`enc`/`enc2`/`fenc`/`fenc2`/`elc`) 対応

- 第三方 Crypter 集成 (MegaCrypter、YouPaste、LinkCrypter、EncrypterMe.ga)

- VLC ストリーミング辺下辺播

- 内蔵 HttpServer Web 管理画面

- SharpCompress 自動解凍

- 多言語画面 (10種)

- Stegano ステガノグラフィ

- 自動更新確認

***

## バージョン番号説明

- 主版本番号：重大機能変更または下位互換でない修正

- 次版本番号：新規機能、下位互換あり

- 改訂号：バグ修正、下位互換あり
