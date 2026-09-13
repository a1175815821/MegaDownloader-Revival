# 貢献ガイド

まず、MegaDownloader 復活プロジェクトに貢献しようとしてくださりありがとうございます！本ドキュメントでは貢献の手順を説明します。

## 行動規範

親切に、すべての参加者に敬意を持って接してください。MegaDownloader を再び利用可能にするというプロジェクトの目標に関連する貢献であれば、Bug 修正、新機能の追加、翻訳の改善、ドキュメントの改善など、すべて歓迎します。

## どんな貢献ができる？

| 種類 | 説明 |
| --- | --- |
| 🐛 Bug 修正 | リンク解析、ダウンロード失敗、UI エラーなどの修正 |
| ✨ 新機能 | 新しい Crypter、新しいリンクプロテクター、新しいプロトコルなどのサポート |
| 🌐 翻訳 | `Resources/Language/` 内の既存翻訳の改善または新しい言語の追加 |
| 📚 ドキュメント | README、CHANGELOG、コードコメントの改善 |
| 🎨 UI/UX | WinForms 画面レイアウト、アイコン、ユーザビリティの改善 |
| 🔧 リファクタリング | 機能に影響を与えずにコード品質を向上 |

## 開発環境

- Visual Studio 2019 / 2022
- .NET Framework 4.8 SDK
- Git

## プロジェクト構成

```
MegaDownloader/
├── Clases/                         # コアクラスライブラリ
│   ├── Cryptography/AES.vb         #   AES 暗号化
│   ├── StreamingLibrary/           #   ストリーミングライブラリ管理
│   │   ├── LibraryElement.vb
│   │   ├── StreamingLibrary.vb
│   │   └── StreamingLibraryManager.vb
│   ├── ApplicationInstanceManager.vb
│   ├── Conexion.vb                 #   HTTP/ネットワーク通信
│   ├── Configuracion.vb            #   構成管理
│   ├── ConfiguracionUI.vb          #   UI 構成（テーマ等）★ v2.0 新規
│   ├── FileDownloader.vb           #   ファイルダウンロードコア
│   ├── MegaFolderHelper.vb         #   MEGA フォルダー解析
│   ├── MegaURIProtocol.vb          #   mega:// プロトコル登録
│   ├── Mutex.vb                    #   ミューテックス（プロセス単一インスタンス）
│   ├── Paquete.vb                  #   ダウンロードパッケージデータ
│   ├── ThrottledStream.vb          #   レート制限ストリーミング
│   ├── ThemeManager.vb             #   テーママネージャー ★ v2.0 新規
│   ├── URLExtractor.vb             #   URL 解析
│   ├── URLProcessor.vb             #   URL 処理
│   └── Updater.vb                  #   自動更新
├── Controls/                       # カスタムコントロール
│   └── ELCAccountControl.vb
├── HttpModule/                     # 組み込み Web サーバーモジュール
│   ├── StreamingModule.vb
│   ├── StreamingLibraryModule.vb
│   ├── WebInterfaceModule.vb
│   └── Template/                   #   HTML テンプレート
├── Stegano/                        # ステガノグラフィフォーム
│   ├── SteganoManager.vb
│   ├── SteganoWizardLoad.vb
│   └── SteganoWizardSave.vb
├── Resources/
│   ├── DLLs/                       # サードパーティ DLL 依存関係
│   ├── Language/                   # 多言語 XML（10 種類）
│   └── Installer MSD/              # WiX インストーラープロジェクト
├── My Project/                     # VS プロジェクトメタデータ
├── Forms/                          # WinForms フォーム（12 個）
│   ├── Main.vb                     #   メインフォーム
│   ├── AddLinks.vb                 #   リンク追加フォーム
│   ├── Configuration.vb            #   設定フォーム（含テーマ切り替え）
│   ├── StreamingForm.vb            #   ストリーミング再生フォーム
│   ├── Credits.vb                  #   バージョン情報/謝辞
│   ├── SplashScreen.vb             #   スプラッシュスクリーン
│   ├── Cerrando.vb                 #   終了画面
│   ├── Descompresor.vb             #   解凍フォーム
│   ├── ELCForm.vb                  #   ELC コンテナフォーム
│   ├── EncodeLinksForm.vb          #   リンク暗号化フォーム
│   ├── PantallaMsg.vb              #   メッセージ通知フォーム
│   └── PropiedadesDescarga.vb      #   ダウンロードプロパティフォーム
├── docs/                           # プロジェクトドキュメント
│   ├── CHANGELOG.md                #   変更履歴
│   └── CONTRIBUTING.md             #   貢献ガイド
├── MegaDownloader.sln              # VS ソリューション
├── MegaDownloader.vbproj           # VS プロジェクト
├── app.config                      # .NET ランタイム構成
├── ApplicationEvents.vb            # アプリケーションレベルのイベント処理
├── README.md                       # プロジェクト説明
├── LICENSE                         # MIT ライセンス
└── .gitignore                      # Git 無視ルール
```

### 主要ディレクトリの説明

| ディレクトリ | 内容 |
| --- | --- |
| `Clases/` | コアクラスライブラリ：暗号化（`Cryptography/`）、ストリーミングライブラリ（`StreamingLibrary/`）、HTTP 通信（`Conexion.vb`）、構成（`Configuracion.vb`）、ダウンロードコア（`FileDownloader.vb`）、フォルダー解析（`MegaFolderHelper.vb`）、テーマ（`ThemeManager.vb`）、更新（`Updater.vb`） |
| `Forms/` | WinForms フォーム。メインフォームは `Main.vb` で、ダウンロードリストとテーマロジックを大量に含む |
| `HttpModule/` | 組み込み Web サーバーモジュール。`Template/` 配下は HTML テンプレート |
| `Stegano/` | ステガノグラフィ関連フォームとロジック |
| `Resources/Language/` | UI 多言語 XML。各言語 1 ファイル |
| `Resources/DLLs/` | サードパーティ依存 DLL。リポジトリにコミットされる |
| `docs/` | 本ドキュメントと変更履歴 |

## 貢献フロー

### 1. Fork とリポジトリのクローン

```bash
# 自分の GitHub アカウントに Fork した後：
git clone https://github.com/<あなたのユーザー名>/MegaDownloader-Revival.git
cd MegaDownloader
git remote add upstream https://github.com/a1175815821/MegaDownloader-Revival.git
```

### 2. 機能ブランチの作成

```bash
# 最新の main ブランチから作成
git checkout main
git pull upstream main
git checkout -b feature/あなたの機能名
# または： fix/bug-説明， docs/ドキュメント主題， i18n/言語-改善
```

### 3. 開発とローカルテスト

- Visual Studio で `MegaDownloader.sln` を開く
- `Debug` 構成でビルドする
- `bin/Debug/MegaDownloader.exe` を実行し、変更を検証する

**テストケース（必ずカバーしてください）：**

- 旧版リンク：`https://mega.nz/#!abcDEF!ghijklmnop`
- 新版リンク：`https://mega.nz/file/abcDEF#ghijklmnop`
- フォルダーリンク：`https://mega.nz/folder/abcDEF#ghijklmnop`
- 暗号化リンク：`mega://enc?...`
- クリップボード自動認識
- リンクのドラッグ＆ドロップ

### 4. コードのコミット

[Conventional Commits](https://www.conventionalcommits.org/zh-hans/v1.0.0/) 仕様に従ってください：

```
<type>(<scope>): <subject>

<body任意>

<footer任意>
```

よく使う種類：

- `feat`： 新機能。例： `feat(url): 支持 mega.nz/embed/ 链接格式`
- `fix`： Bug 修正。例： `fix(clipboard): 修复剪贴板监听失效问题`
- `docs`： ドキュメント。例： `docs: 补充 zh-CN 翻译`
- `refactor`： リファクタリング。例： `refactor(conexion): 简化代理设置逻辑`
- `i18n`： 翻訳。例： `i18n(zh-CN): 补全未翻译条目`

```bash
git add .
git commit -m "feat(url): 支持 mega.nz/embed/ 链接格式"
```

### 5. プッシュと PR の作成

```bash
git push origin feature/あなたの機能名
```

GitHub 上で `main` ブランチへの Pull Request を作成し、PR の説明に以下を記載してください：

- この PR は何を変更しましたか？
- なぜ変更が必要ですか？（関連 Issue 番号）
- どのようにテストしますか？
- 既存機能に影響はありますか？

### 6. コードレビューとマージ

メンテナーが PR をレビューし、修正を依頼することがあります。プロジェクトの長期的な保守性のためのものなので、辛抱強くご協力ください。

## コードスタイル規約

- VB.NET プロジェクトでは `Option Strict On`、`Option Explicit Off`、`Option Infer On` が有効です。**新規コードはこれらの制約を満たす必要があります**
- ファイルエンコーディング：**UTF-8 with BOM**
- インデント：**4 スペース**
- 命名：
  - クラス、メソッド： PascalCase。例： `ExtraerFileID`
  - プライベートフィールド： camelCase またはアンダースコア接頭辞付き。例： `_ProxyIP`
  - ローカル変数： camelCase。例： `fileInfo`
- コメント：
  - 複雑なロジックには `'` 一行コメントで説明を付ける
  - 公開 API には `''' <summary>` XML ドキュメントコメントを使用する
- 元のプロジェクトでは `Clases`、`Configuracion`、`Fichero` などのスペイン語命名が使われています。**一貫性を保つため、新規コードは英語命名を使用できます**が、既存の識別子を一括で改名しないでください

## 新しい Crypter / Link Protector の追加

[`Clases/Crypters/EncrypterMega.vb`](../Clases/Crypters/EncrypterMega.vb) の実装パターンを参照してください：

1. `Clases/Crypters/` 配下に `<Name>.vb` を新規作成する
2. `ObtenerInformacionFichero` メソッドを実装し、`Conexion.InformacionFichero` を返す
3. [`Clases/URLExtractor.vb`](../Clases/URLExtractor.vb) 内で：
   - `<NAME>TOKEN` 定数を追加する
   - `patternOthers` にマッチング正規表現を追加する
   - `ExtraerFileID` に分岐を追加する
4. 必要に応じて `Forms/Main.vb` で UI に組み込む

## ドキュメントと翻訳

本プロジェクトの**ドキュメント**と**ソフトウェア UI** は独立した 2 つの多言語体系であり、どちらへの貢献も歓迎します。

### ドキュメントの多言語対応（README / CHANGELOG / CONTRIBUTING）

ドキュメントは手動翻訳モードを採用しており、自動翻訳フローはありません：

- 各言語のドキュメントは人手で保守されています：英語（`README.md`、`docs/CHANGELOG.md`、`docs/CONTRIBUTING.md`）、簡体字中国語（`docs/*.zh-CN.md`、正本）、繁体字中国語/日本語/韓国語の対応ファイル
- ドキュメントを変更する際は、すべての言語バージョンを同期して更新し、内容を一致させてください
- ドキュメントの表現を改善したい場合 → 対応する言語のファイルを直接修正するか、PR を提出してください
- 新しい言語を追加したい場合 → 英語（または中国語）ドキュメントを複製して新しい言語ファイルとし、各ドキュメント上部の言語ナビゲーションリンクと言語対照表を更新してください

### ソフトウェア UI の多言語対応

UI 文言は `Resources/Language/<locale>-Language.xml` にあり、各言語 1 ファイルです。

改善手順：

1. 対応する言語の XML を探す（例： `ja-JP-Language.xml`）
2. `<Text>` ノードの CDATA 内容を修正し、`key` 属性は**変更しないでください**
3. 新しいキーを追加する場合は、`en-US-Language.xml` に同名の key があることを確認してください（フォールバック基準として）

### 新しい UI 言語の追加

1. `Resources/Language/en-US-Language.xml` を `<locale>-Language.xml` として複製する
2. すべての `<Text>` ノードの CDATA 内容を翻訳する
3. `MegaDownloader.vbproj` に埋め込みリソースを追加する：

```xml
<EmbeddedResource Include="Resources\Language\ja-JP-Language.xml" />
```

4. プログラムを実行し、**設定 → 言語** で新しい言語が表示されることを確認する

> 言語キー検索には 3 段階のフォールバックがあります：ディスクファイル → 組み込みリソース → `en-US` → key 自体を返す。そのため、一部のキーが未翻訳でもプログラムはクラッシュせず、英語または元の key が表示されるだけです。

## Bug の報告

Bug を報告する際は、Issue に以下の情報を含めてください：

- **MegaDownloader バージョン**（バージョン情報 → バージョンで確認）
- **Windows バージョン**
- **リンク種類**（サンプルリンクを完全にコピー。機密部分はマスク可）
- **再現手順**
- **期待される動作** vs **実際の動作**
- **エラーログ**（ある場合。プログラムディレクトリ内のログファイル）

## リリース手順（メンテナーのみ）

1. すべてのテストが通過し、`Debug` と `Release` 構成の両方でビルドできることを確認する
2. `docs/CHANGELOG.zh-CN.md` を更新して新しいバージョン章を追加し、他の言語の CHANGELOG も同期して更新する
3. `My Project/AssemblyInfo.vb` 内の `AssemblyVersion` と `AssemblyFileVersion` を更新する
4. `Resources/InternalConfig.xml`（Base64 エンコード）内の `VERSION_MEGADOWNLOADER` と `VERSION_UPDATE` を更新する
5. `docs/version.xml` 内の `<Version>` を更新する
6. 中英バイリンガルのリリースノートを作成する（中国語を先に書き、次に英語を書く中英対照。過去の `release-notes-*.md` の構成を参照）
7. Git Tag を作成してプッシュする。CI が自動的に成果物をビルドして GitHub Release を作成する：

```bash
git tag -a v2.5.0 -m "Release v2.5.0"
git push origin main
git push origin v2.5.0
```

CI は tag `v*` 上で自動的に：
- Release 構成をビルドする
- 単一ファイル版の 12 個の組み込み DLL が揃っているかチェックサム検証する（不足時はビルド失敗とし、「ダブルクリックしても反応なし」のパッケージ流出を防ぐ）
- zip にパッケージ化して GitHub Release にアップロードする

> プレリリース（RC 版）のテストは、先に `prerelease` 形式でリリースし、検証通過後に正式版に切り替えることを推奨します。

## 連絡方法

- Issue の提出：GitHub Issues
- セキュリティ関連の問題：公開 Issue で議論せず、メールでメンテナーに連絡してください

---

改めて貢献に感謝します！MegaDownloader に新たな命を吹き込みましょう。 🚀
