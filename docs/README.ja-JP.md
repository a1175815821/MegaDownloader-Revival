# MegaDownloader 復活プロジェクト

**言語**：[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · **日本語** · [한국어](README.ko-KR.md)

> クラシックな MEGA ダウンローダーを、今の MEGA で再び使えるように。新しいリンク形式に対応し、マルチスレッド高速ダウンロード・レジューム・quota 自動回復を、単一実行ファイル・インストール不要で。
> 非公式プロジェクトであり、MEGA（MEGA Ltd.）とは関係ありません。

**[Windows 版をダウンロード](../../../releases/latest)** · 単一ファイルまたは ZIP · Windows 7 SP1+ · .NET Framework 4.8

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](../LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://dotnet.microsoft.com/)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![Language](https://img.shields.io/badge/Language-VB.NET-005a9c.svg)](https://docs.microsoft.com/dotnet/visual-basic/)
[![Build](https://github.com/a1175815821/MegaDownloader-Revival/actions/workflows/build.yml/badge.svg)](../../../actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/a1175815821/MegaDownloader-Revival?include_prereleases)](../../../releases/latest)
[![Downloads](https://img.shields.io/github/downloads/a1175815821/MegaDownloader-Revival/total)](../../../releases)
[![Stars](https://img.shields.io/github/stars/a1175815821/MegaDownloader-Revival?style=social)](../../../stargazers)

## スクリーンショット

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="screenshots/main-dark.png">
  <source media="(prefers-color-scheme: light)" srcset="screenshots/main-light.png">
  <img alt="MegaDownloader メインウィンドウ——左にキュー統計、中央にタスク一覧、右に選択中タスクの詳細" src="screenshots/main-light.png">
</picture>

左側はリアルタイムのキュー統計（合計速度、実行中 / 待機 / 失敗、キューの進捗、残り時間）とショートカット、中央はタスク一覧、右側は選択中タスクの詳細です。MEGA のリンクをウィンドウにドロップするか、コピーするだけでクリップボード監視が自動的に認識します。テーマはシステム設定に追従し、上の画像も GitHub のテーマに合わせて切り替わります。

| 設定 · 一般 | 設定 · 詳細 |
| :---: | :---: |
| [![設定——一般タブ](screenshots/settings-general.png)](screenshots/settings-general.png) | [![設定——詳細タブ](screenshots/settings-advanced.png)](screenshots/settings-advanced.png) |
| 保存先、クリップボード監視、自動起動、言語、テーマ、速度制限、再試行 | バッファとログレベル、プロキシ、内蔵 Web サーバーと LAN アクセス |

<sub>スクリーンショットは v2.5.9（UI は簡体字中国語）。UI 言語は 10 種類から選べます。</sub>

---

## ⚡ 30 秒で開始

1. [Releases](../../../releases/latest) からどちらか一方をダウンロード：

   | ファイル | こんな人に |
   | --- | --- |
   | **`MegaDownloader.exe`**（単一ファイル） | 一番手軽に：ダウンロードしてダブルクリックするだけ。依存 DLL 15 個をすべて内蔵 |
   | **`MegaDownloader-Revival-win-x86.zip`**（ポータブル版） | ZIP を使いたい場合：展開して実行 |

2. ダブルクリックで実行 —— インストール不要、管理者権限も不要
   > 動作環境：Windows 7 SP1 以降。.NET Framework 4.8（Windows 10 1903+ には標準搭載、旧 OS は案内に従ってインストール）
3. MEGA リンクをコピー —— プログラムがクリップボードを自動検出し、確認するとダウンロードキューに追加

これでダウンロードできます。以降は必要に応じて：[機能](#-何ができるか) → [プロジェクトの背景と比較](#-このプロジェクトはどこから来たか) → [FAQ](#-faq) → [開発者向け](#️-開発者向け)。

リンク追加は他にも：リンクをメインウィンドウへドラッグ＆ドロップ · ツールバーの「リンク追加」から貼り付け · ブラウザーで `mega://` プロトコルのリンクをクリック。

> 🆕 **最新版 [v2.5.9](../../../releases/latest)**（2026-10-28）：同名ファイルの並列ダウンロードの競合を修正 · 解凍時に既存ファイルを保護 · 完了済みファイルを検証して再利用 · ストリーミングの Range データ破損を修正。履歴は [CHANGELOG](CHANGELOG.md) へ。

---

## 🚀 何ができるか

**ダウンロードのコア**

- **マルチスレッド** —— 1 ファイルへ複数接続を同時に張り、回線を最大限活用
- **レジューム** —— 一時停止 / 再開 / エラー時の再試行。プログラム再起動後も続きから
- **フォルダーダウンロード** —— 共有フォルダー全体を再帰的に解析し、必要なサブフォルダーやファイルだけを選択可能
- **速度制限** —— 全体またはタスクごと
- **失敗の自己修復**（v2.5）—— 失敗タスクを定時に自動再試行し、永久に失敗するものは自動除外
- **quota ブレーカー**（v2.5）—— MEGA のダウンロード quota を使い切ると自動一時停止してカウントダウン、時間になると自動再開。旧式の「quota 突破」リトライモードへ切り替えも可能（既定ではオフ）
- **自動解凍** —— ダウンロード完了後に RAR / 7Z / ZIP を自動解凍。分割・パスワード付きにも対応

**利便性とリモート**

- **リンク追加は 4 つの方法** —— クリップボード自動検出 / ドラッグ＆ドロップ / 手動貼り付け / `mega://` プロトコル
- **Web インターフェイス** —— 内蔵 HTTP サーバーで、ブラウザーからタスク追加や進捗確認が可能。LAN アクセスを有効にすればスマホからも
- **ELC コンテナ** —— 暗号化リンクコンテナのインポート／エクスポート。`enc` / `enc2` / `fenc` / `fenc2` / `elc` すべて対応

**インターフェイスと体験**

- **ダウンロードしながら再生** —— VLC 統合で、ダウンロード中の動画をストリーミング再生
- **ストリーミングライブラリ** —— よく見るストリーミングソースを視覚的に管理
- **ダーク／ライトテーマ**（v2.0）—— システム設定に追従して自動切替、手動指定も可
- **10 の UI 言語** —— English · Español · 简体中文 · 繁體中文 · Français · Deutsch · Italiano · Português (BR) · Magyar · Română、追加も可能
- **ステガノグラフィ** —— 画像 / 動画にファイルを隠す（F5 ステガノ）

<details>
<summary><b>対応するリンク形式一覧（クリックで展開）</b></summary>

- 新形式：`mega.nz/file/ID#KEY`、`mega.nz/folder/ID#KEY`（v1.9 以降）
- 旧形式：`mega.nz/#!ID!KEY`、`mega.co.nz/#F!ID!KEY`
- 暗号化：`mega://enc?…`、`mega://enc2?…`、`mega://fenc?…`、`mega://fenc2?…`、`mega://elc?…`

リンク例：

```
https://mega.nz/file/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/folder/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/#!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
mega://enc?_xlPqemSILarh5VBKbhSTFyQQQ0
```

> v2.0 から、サービス終了済みの以下には対応していません：MegaCrypter、YouPaste、LinkCrypter、EncrypterMe.ga、goo.gl 短縮リンク。

</details>

---

## 📖 このプロジェクトはどこから来たか

MegaDownloader は、スペインの開発者 **Andres Soliño** が作った MEGA ダウンロードマネージャーで、軽量・安定・マルチスレッドで知られていました。上流は v1.8 で開発停止となり、その後 MEGA がリンク形式を変更した（`mega.nz/file/...`、`mega.nz/folder/...`）ため、旧版は新しいリンクを認識できず、中核機能が動かなくなりました。

このリポジトリがそれを復活させました：v1.8 を逆コンパイルしてソースコードを取り出し、その上に修正とリファクタリングを続けています。これまでに **200 件超の修正と強化**（クラッシュ、サイレントなデータ喪失、セキュリティ、互換性）を積み重ね、セマンティックバージョニングで継続リリースしています。

|  | 旧版 v1.8（2018 年に停止） | **本リポジトリ v2.5.x** |
| --- | --- | --- |
| 新形式 `mega.nz/file` / `/folder` リンク | ❌ 認識不可 | ✅ v1.9 以降で対応 |
| MEGA quota 超過 | フリーズ・エラー | ✅ 自動一時停止＋カウントダウン再開（v2.5） |
| 失敗タスク | 手動での再試行のみ | ✅ 定時自己修復（v2.5） |
| UI テーマ | システムグレーのみ | ✅ ダーク／ライト、システム追従（v2.0） |
| 安定性 | — | ✅ 200 件超のクラッシュ／データ喪失／セキュリティ修正 |
| メンテナンス | 停止中 | ✅ 積極的にメンテナンス中、Issue にも対応 |

> ⚠️ **法的なお知らせ**：本プロジェクトは第三者が公開したソフトウェアの逆コンパイルに由来し、目的は互換性問題を修正して再び使えるようにすることに限られます。原作者が権利の侵害だとお考えの場合は Issue でご連絡ください。対応いたします。

---

## ❓ FAQ

**ウイルス対策ソフトが警告を出します。**
警告が表示された場合は、実行前に警告内容とダウンロード元を確認してください。公開されている [ビルド手順](../../../actions/workflows/build.yml) を確認し、ZIP 版を試すか、ソースからビルドすることもできます。パッケージを変えても警告が消えるとは限りません。

**quota exceeded（quota 超過）と表示されます。**
MEGA は無料アカウントと IP にダウンロード quota を設けています。v2.5 以降は自動で一時停止し、カウントダウン後に再開します。待てない場合は設定で「quota 突破モード」を有効にするとリトライを続けます。

**設定とダウンロードキューはどこに保存されますか？**
`%LOCALAPPDATA%\MegaDownloader\` です。プログラムのフォルダーは自由に移動・削除でき、設定には影響しません。OS 再インストール前にはこのフォルダーをバックアップしてください。

**旧形式のリンクもダウンロードできますか？**
できます。新旧形式、`mega://` 暗号化リンク、ELC コンテナすべて対応しています（一覧は上の折りたたみ）。

**管理者権限は必要ですか？**
不要です。インストール不要・管理者権限不要で動きます。

**Mac / Linux 版はありますか？**
ありません —— WinForms 製のため Windows 専用です。ただし内蔵 Web インターフェイスを有効にすれば、スマホや他の端末のブラウザーからダウンロードを管理できます。

---

## 🛠️ 開発者向け

<details>
<summary><b>ソースからビルド（クリックで展開）</b></summary>

**必要環境**：Visual Studio 2013 以降（ソリューションは VS2013 形式）· .NET Framework 4.8 SDK（VS に同梱）· Windows 7 SP1+

```bash
git clone https://github.com/a1175815821/MegaDownloader-Revival.git
cd MegaDownloader-Revival
# MegaDownloader.sln を Visual Studio で開き、Ctrl+Shift+B
```

コマンドラインビルド：

```bash
msbuild MegaDownloader.sln /p:Configuration=Release /p:Platform=x86
```

成果物は `bin/<Configuration>/MegaDownloader.exe`。ビルド構成：`Debug` / `Release` / `Debug_MSD` / `Release_MSD`（MegaSearchDesktop 統合版、出力先 `Resources/Installer MSD/`）。

回帰テスト：PowerShell で `.\Tests\Run-RegressionTests.ps1` を実行します（.NET SDK が必要）。一時ファイルとローカル HTTP サーバーを使用するため、MEGA アカウントは不要です。[Tests/README.md](../Tests/README.md) を参照してください。

**技術スタック**：VB.NET · .NET Framework 4.8 · WinForms · BouncyCastle（暗号）· Newtonsoft.Json · ObjectListView · SharpCompress（解凍）· HttpServer/Fadd（Web）· F5Lib（ステガノ）

</details>

<details>
<summary><b>プロジェクト構成（クリックで展開）</b></summary>

```
MegaDownloader/
├── Clases/                 # コアライブラリ（暗号、ダウンロード、設定、テーマ、更新）
├── Controls/               # カスタムコントロール
├── Forms/                  # WinForms フォーム（12 個）
├── HttpModule/             # 内蔵 Web サーバーモジュールと HTML テンプレート
├── Stegano/                # ステガノグラフィのフォーム
├── Resources/
│   ├── DLLs/               # サードパーティ DLL 依存
│   ├── Language/           # 多言語 XML（10 言語）
│   └── Installer MSD/      # WiX インストーラープロジェクト
├── docs/                   # ドキュメント（README / CHANGELOG / CONTRIBUTING）
├── Tests/                  # ダウンロード・解凍・ストリーミングの回帰テスト
└── MegaDownloader.sln
```

完全なディレクトリツリーとファイル説明は [CONTRIBUTING](CONTRIBUTING.zh-CN.md) を参照。

</details>

---

## 📚 ドキュメント

| ドキュメント | 内容 |
| --- | --- |
| [CHANGELOG](CHANGELOG.md) | 全バージョン履歴と各版の修正詳細（簡体字中国語のみ） |
| [CONTRIBUTING](CONTRIBUTING.zh-CN.md) | コントリビューションの流れ、コードスタイル、リリース手順 |

README は 5 言語すべてを手動で管理しています。CONTRIBUTING は英語と簡体字中国語のみ、CHANGELOG は簡体字中国語のみです。更新時は他言語も同期してください：

| 言語 | README | CHANGELOG | CONTRIBUTING |
| --- | --- | --- | --- |
| English | `README.md`（リポジトリ直下） | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |
| 简体中文 | `docs/README.zh-CN.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.zh-CN.md` |
| 繁體中文 | `docs/README.zh-TW.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |
| 日本語 | `docs/README.ja-JP.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |
| 한국어 | `docs/README.ko-KR.md` | `docs/CHANGELOG.md` | `docs/CONTRIBUTING.md` |

> 简体中文が正式版（権威ソース）です。

---

## ⭐ このプロジェクトを応援する

MegaDownloader Revival が役に立ったら、以下の方法で応援していただけるとうれしいです：

- ⭐ 右上の **Star** を押してプロジェクトを応援
- 🐛 問題があれば [Issue を報告](../../../issues) —— 最近の修正の多くはユーザーの報告がきっかけです
- 🌍 [翻訳](CONTRIBUTING.zh-CN.md)の改善に協力 —— 10 言語ありますが、ネイティブチェックを募集中
- 🔗 まだ MEGA ダウンローダーを探している友人に教える

<a href="https://star-history.com/#a1175815821/MegaDownloader-Revival&Date">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=a1175815821/MegaDownloader-Revival&type=Date&theme=dark" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=a1175815821/MegaDownloader-Revival&type=Date" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=a1175815821/MegaDownloader-Revival&type=Date" />
 </picture>
</a>

---

## 謝辞

- 原 MegaDownloader の作者 **Andres Soliño**
- 復活プロジェクトのメンテナー **Yingxue**（v2.0+）、Issue 報告と翻訳改善に協力してくれたすべての人
- オープンソース依存：[BouncyCastle](https://www.bouncycastle.org/) · [Newtonsoft.Json](https://www.newtonsoft.com/json) · [SharpCompress](https://github.com/adamhathcock/sharpcompress) · [ObjectListView](http://objectlistview.sourceforge.net/) · [7-Zip](https://www.7-zip.org/)

## ライセンス

[MIT License](../LICENSE) で公開。元の著作権 © 2018 Andres Soliño、復活プロジェクトの修正に関する著作権 © 2026 MegaDownloader Revival Project コントリビューター。

> リポジトリ内のサードパーティ DLL はそれぞれの元ライセンスに従います。利用者は適合性を自己確認してください。
