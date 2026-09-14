# MegaDownloader 復活プロジェクト

**言語**：[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · **日本語** · [한국어](README.ko-KR.md)

> クラシックな MEGA ダウンローダーを再び利用可能にします。v1.8 のソースコードを逆コンパイルして修正したもので、60 以上の修正が完了しています。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://dotnet.microsoft.com/)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![Language](https://img.shields.io/badge/Language-VB.NET-005a9c.svg)](https://docs.microsoft.com/dotnet/visual-basic/)
[![Build](https://github.com/a1175815821/MegaDownloader-Revival/actions/workflows/build.yml/badge.svg)](../../../actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/a1175815821/MegaDownloader-Revival?include_prereleases)](../../../releases/latest)
[![Downloads](https://img.shields.io/github/downloads/a1175815821/MegaDownloader-Revival/total)](../../../releases)
[![Stars](https://img.shields.io/github/stars/a1175815821/MegaDownloader-Revival?style=social)](../../../stargazers)

> ⭐ このプロジェクトが役に立ったら、ぜひ右上の Star で応援をお願いします！

---

## 概要

MegaDownloader は、スペインの開発者 **Andres Soliño** によって作成された MEGA クラウドストレージ用ダウンロードマネージャーで、軽量、安定性、マルチスレッド対応で知られています。元のプロジェクトは v1.8 以降メンテナンスが停止しており、MEGA はリンク形式を変更したため（`mega.nz/file/...`、`mega.nz/folder/...`）に変更されたため、旧バージョンは新しいリンクを認識できず、コア機能が動作しなくなりました。

このリポジトリは、その復活プロジェクトです。v1.8を逆コンパイルしてソースコードを取得し、それを基に修正とリファクタリングを行っています。

**現在のバージョン：v2.5.0**。完全な変更履歴は [CHANGELOG](CHANGELOG.ja-JP.md) をご覧ください。

> ⚠️ **法的声明**：本プロジェクトは、サードパーティが公開したソフトウェアの逆コンパイルに基づいており、その目的は互換性の問題を修正して使用可能性を回復することのみです。原著者が本リポジトリが自身の権利を侵害しているとお考えの場合は、Issue を通じてご連絡ください。対応させていただきます。

---

## クイックスタート

1. [Releases](../../../releases) からダウンロードしてください。以下の2つの選択肢から1つを選んでください：
   - **`MegaDownloader-Revival-win-x86.zip`** —— ポータブル版。任意のディレクトリに解凍し、`MegaDownloader.exe` をダブルクリックしてください
   - **`MegaDownloader.exe`** —— 単一ファイル版。12個の依存DLLが組み込まれているため、ダウンロード後、解凍せずにダブルクリックしてください
2. MEGAのリンクをコピーすると、プログラムがクリップボードの内容を自動的に認識します
3. ツールバーの **リンクを追加** をクリックして手動で貼り付けるか、リンクをメインウィンドウにドラッグ＆ドロップすることも可能です
4. **設定** でダウンロード先、同時ダウンロード数、速度制限を設定します
5. **設定 → 一般 → テーマ** でダークモード/ライトモードを切り替えます（保存後すぐに反映されます）

### リンクの例

新形式（v1.9 以降で対応）：

```
https://mega.nz/file/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
https://mega.nz/folder/abcDEFgh#IjklMNopQRstUVwxYZ1234567890
```

旧形式（引き続き対応）：

```
https://mega.nz/#!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
https://mega.co.nz/#F!abcDEFgh!IjklMNopQRstUVwxYZ1234567890
```

暗号化リンク：

```
mega://enc?_xlPqemSILarh5VBKbhSTFyQQQ0
mega://enc2?abcDEFgh-IjklMNop
```

---

## 機能・特徴

| 機能 | 説明 |
| --- | --- |
| マルチスレッドダウンロード | 同一ファイルに対して複数の接続を並行して確立し、速度を大幅に向上 |
| レジューム | 一時停止、再開、エラー時の再試行に対応 |
| 速度制限 | 全体または個々のタスクごとの速度制限 |
| クリップボード監視 | クリップボードにコピーされた MEGA リンクを自動認識 |
| ドラッグ＆ドロップ対応 | リンクをメインウィンドウにドラッグするだけでキューに追加 |
| MEGA フォルダ | 共有フォルダ全体を再帰的に解析してダウンロード。指定したサブフォルダやファイルのみのダウンロードにも対応 |
| 暗号化リンク | `enc` / `enc2` / `fenc` / `fenc2` / `elc` などの各種形式に対応 |
| ELCコンテナ | 暗号化リンクコンテナのインポートとエクスポート |
| ストリーミング再生 | VLCを統合し、ダウンロードしながら再生可能 |
| Webインターフェース | HttpServerを内蔵し、ブラウザからリモート管理が可能；LANアクセスを有効化可能 |
| ストリーミングライブラリ | ストリーミングリソースの可視化管理 |
| Stegano ステガノグラフィ | 画像・動画のステガノグラフィ符号化・復号 |
| 自動解凍 | SharpCompress ベース、RAR / 7Z / ZIP に対応 |
| サーキットブレーカー | MEGAのクォータが枯渇した際に自動的に一時停止し、カウントダウンを開始。期限が切れると自動的に再開（v2.5） |
| 失敗時の自動復旧 | 失敗したタスクを定期的に自動再試行。恒久的な失敗は自動的に除外（v2.5） |
| 多言語インターフェース | 10言語に対応、拡張可能 |
| ダーク／ライトテーマ | システムに追従、または手動で切り替え可能。Autoモードではリアルタイムで追従 |

### 対応リンク形式

- `mega.nz/#...!FileID!FileKey`（旧バージョン）
- `mega.nz/file/FileID#FileKey`（新バージョン）
- `mega.nz/folder/FolderID#FolderKey`（新バージョンのフォルダ）
- MEGA URIプロトコル：`mega://#!...`、`mega://enc?...`、`mega://elc?...`

> v2.0 以降、以下のサービス終了に伴いサポートが削除されました：MegaCrypter、YouPaste、LinkCrypter、EncrypterMe.ga、goo.gl 短縮URL。

---

## ビルド

### 環境要件

- Visual Studio 2019 / 2022
- .NET Framework 4.8 SDK（Visual Studio とともにインストールされます）
- Windows 7 SP1 以降

### ビルド設定

| 設定 | 説明 |
| --- | --- |
| `Debug` | デバッグ版、`bin/Debug/` に出力 |
| `Release` | リリース版、`bin/Release/` に出力 |
| `Debug_MSD` | MegaSearchDesktop 統合版のデバッグ |
| `Release_MSD` | MegaSearchDesktop 統合版のリリースビルド、出力先：`Resources/Installer MSD/` |

### 手順

```bash
git clone https://github.com/a1175815821/MegaDownloader-Revival.git
cd MegaDownloader-Revival
# 用 Visual Studio 打开 MegaDownloader.sln，然后 Ctrl+Shift+B
```

コマンドラインからのビルド：

```bash
msbuild MegaDownloader.sln /p:Configuration=Release /p:Platform=x86
```

ビルド結果は `bin/<Configuration>/MegaDownloader.exe` にあります。

### 技術スタック

VB.NET · .NET Framework 4.8 · WinForms · BouncyCastle（暗号化）· Newtonsoft.Json · ObjectListView · SharpCompress（解凍）· HttpServer/Fadd（Web）· F5Lib（ステガノグラフィ）

## プロジェクト構造

```
MegaDownloader/
├── Clases/                 # 核心类库（加密、下载、配置、主题、更新）
├── Controls/               # 自定义控件
├── Forms/                  # WinForms 窗体（12 个）
├── HttpModule/             # 内置 Web 服务器模块与 HTML 模板
├── Stegano/                # 隐写术窗体
├── Resources/
│   ├── DLLs/               # 第三方 DLL 依赖
│   ├── Language/           # 多语言 XML（10 种）
│   └── Installer MSD/      # WiX 安装包工程
├── docs/                   # ドキュメント（README / 変更履歴 / 貢献ガイド）
├── My Project/             # VS 项目元数据
└── MegaDownloader.sln
```

完全なディレクトリツリーとファイルの用途については、[CONTRIBUTING](CONTRIBUTING.ja-JP.md)を参照してください。

---

## 対応言語

ソフトウェアのインターフェースには、10種類の言語が標準で搭載されています：

| 言語 | ファイル |
| --- | --- |
| English | `en-US-Language.xml` |
| Español | `es-ES-Language.xml` |
| 簡体中文 | `zh-CN-Language.xml` |
| 繁體中文 | `zh-TW-Language.xml` |
| Français | `fr-FR-Language.xml` |
| Deutsch | `de-DE-Language.xml` |
| Italiano | `it-IT-Language.xml` |
| Português (Brasil) | `pt-BR-Language.xml` |
| Magyar | `hu-HU-Language.xml` |
| Română | `ro-RO-Language.xml` |

言語を追加したり、既存の翻訳を改善したりするには、[CONTRIBUTING](CONTRIBUTING.ja-JP.md)を参照してください。

---

## ドキュメント

| ドキュメント | 内容 |
| --- | --- |
| [CHANGELOG](CHANGELOG.ja-JP.md) | 完全なバージョン履歴と各バージョンの修正詳細 |
| [CONTRIBUTING](CONTRIBUTING.ja-JP.md) | 貢献プロセス、コーディングスタイル、リリースプロセス |

このドキュメントは複数の言語で提供されています。すべての言語版は手動でメンテナンスされています。一つの言語を更新した際は、他の言語も合わせて更新してください：

| 言語 | README | CHANGELOG | CONTRIBUTING | |
| --- | --- | --- | --- | --- |
| 英語 | `docs/README.md` | `CHANGELOG.md` | `CONTRIBUTING.md` | 手動メンテナンス |
| 簡体字中国語 | `docs/README.zh-CN.md` | `CHANGELOG.zh-CN.md` | `CONTRIBUTING.zh-CN.md` | **公式ソース** |
| 繁体字中国語 | `docs/README.zh-TW.md` | `CHANGELOG.zh-TW.md` | `CONTRIBUTING.zh-TW.md` | 手動メンテナンス |
| 日本語 | `docs/README.ja-JP.md` | `CHANGELOG.ja-JP.md` | `CONTRIBUTING.ja-JP.md` | 手動メンテナンス |
| 韓国語 | `docs/README.ko-KR.md` | `CHANGELOG.ko-KR.md` | `CONTRIBUTING.ko-KR.md` | 手動メンテナンス |

> 簡体字中国語は公式ソースです。ソースファイル自体が簡体字中国語版のため、別途 `README.zh-CN.md` は存在しません。

---

## 謝辞

- オリジナルの MegaDownloader 作成者 **Andres Soliño**
- 復活プロジェクトのメンテナ **Yingxue**（v2.0+）
- オープンソース依存関係：[BouncyCastle](https://www.bouncycastle.org/) · [Newtonsoft.Json](https://www.newtonsoft.com/json) · [SharpCompress](https://github.com/adamhathcock/sharpcompress) · [ObjectListView](http://objectlistview.sourceforge.net/) · [7-Zip](https://www.7-zip.org/) · [mpress](https://www.matcode.com/mpress.htm)

## ライセンス

[MIT License](LICENSE) に基づいて公開されています。元の著作権 © 2018 Andres Soliño、復活プロジェクトによる修正の著作権 © 2026 MegaDownloader Revival Project 貢献者。

> リポジトリ内のサードパーティ製 DLL は、それぞれの元のライセンスに従います。利用者は、コンプライアンスを自身で確認する必要があります。
