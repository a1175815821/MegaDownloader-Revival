# 贡献指南

首先,感谢你愿意为 MegaDownloader 复活计划贡献代码!本文档将指引你完成贡献流程。

## 行为准则

请保持友善、尊重所有参与者。我们欢迎任何与项目目标(让 MegaDownloader 重新可用)相关的贡献,无论是修复 Bug、添加功能、完善翻译还是改进文档。

## 我能贡献什么?

| 类型 | 说明 |
| --- | --- |
| 🐛 Bug 修复 | 修复链接解析、下载失败、界面错误等问题 |
| ✨ 新功能 | 支持新的 Crypter、新的链接保护器、新的协议等 |
| 🌐 翻译 | 在 `Resources/Language/` 中改进现有翻译或新增语言 |
| 📚 文档 | 改进 README、CHANGELOG、代码注释 |
| 🎨 UI/UX | 改进 WinForms 界面布局、图标、可用性 |
| 🔧 重构 | 在不影响功能的前提下提升代码质量 |

## 开发环境

- Visual Studio 2019 / 2022
- .NET Framework 4.8 SDK
- Git

## 项目结构

```
MegaDownloader/
├── Clases/                         # 核心类库
│   ├── Cryptography/AES.vb         #   AES 加密
│   ├── StreamingLibrary/           #   流媒体库管理
│   │   ├── LibraryElement.vb
│   │   ├── StreamingLibrary.vb
│   │   └── StreamingLibraryManager.vb
│   ├── ApplicationInstanceManager.vb
│   ├── Conexion.vb                 #   HTTP/网络通信
│   ├── Configuracion.vb            #   配置管理
│   ├── ConfiguracionUI.vb          #   UI 配置(主题等)★ v2.0 新增
│   ├── FileDownloader.vb           #   文件下载核心
│   ├── MegaFolderHelper.vb         #   MEGA 文件夹解析
│   ├── MegaURIProtocol.vb          #   mega:// 协议注册
│   ├── Mutex.vb                    #   互斥锁(进程单实例)
│   ├── Paquete.vb                  #   下载包数据
│   ├── ThrottledStream.vb          #   限速流
│   ├── ThemeManager.vb             #   主题管理器 ★ v2.0 新增
│   ├── URLExtractor.vb             #   URL 解析
│   ├── URLProcessor.vb             #   URL 处理
│   └── Updater.vb                  #   自动更新
├── Controls/                       # 自定义控件
│   └── ELCAccountControl.vb
├── HttpModule/                     # 内置 Web 服务器模块
│   ├── StreamingModule.vb
│   ├── StreamingLibraryModule.vb
│   ├── WebInterfaceModule.vb
│   └── Template/                   #   HTML 模板
├── Stegano/                        # 隐写术窗体
│   ├── SteganoManager.vb
│   ├── SteganoWizardLoad.vb
│   └── SteganoWizardSave.vb
├── Resources/
│   ├── DLLs/                       # 第三方 DLL 依赖
│   ├── Language/                   # 多语言 XML (10 种)
│   └── Installer MSD/              # WiX 安装包工程
├── My Project/                     # VS 项目元数据
├── Forms/                          # WinForms 窗体 (12 个)
│   ├── Main.vb                     #   主窗体
│   ├── AddLinks.vb                 #   添加链接窗体
│   ├── Configuration.vb            #   设置窗体(含主题切换)
│   ├── StreamingForm.vb            #   流媒体播放窗体
│   ├── Credits.vb                  #   关于/致谢
│   ├── SplashScreen.vb             #   启动画面
│   ├── Cerrando.vb                 #   关闭画面
│   ├── Descompresor.vb             #   解压窗体
│   ├── ELCForm.vb                  #   ELC 容器窗体
│   ├── EncodeLinksForm.vb          #   链接加密窗体
│   ├── PantallaMsg.vb              #   消息提示窗体
│   └── PropiedadesDescarga.vb      #   下载属性窗体
├── docs/                           # 项目文档
│   ├── README*.md                   #   项目说明（5 种语言，根 README.md 为英文）
│   ├── CHANGELOG.md                 #   变更日志（仅简体中文）
│   └── CONTRIBUTING*.md             #   贡献指南（英文＋简体中文）
├── MegaDownloader.sln              # VS 解决方案
├── MegaDownloader.vbproj           # VS 工程
├── app.config                      # .NET 运行时配置
├── ApplicationEvents.vb            # 应用级事件处理
├── README.md                       # 项目说明
├── LICENSE                         # MIT 许可证
└── .gitignore                      # Git 忽略规则
```

### 关键目录说明

| 目录 | 内容 |
| --- | --- |
| `Clases/` | 核心类库：加密（`Cryptography/`）、流媒体库（`StreamingLibrary/`）、HTTP 通信（`Conexion.vb`）、配置（`Configuracion.vb`）、下载核心（`FileDownloader.vb`）、文件夹解析（`MegaFolderHelper.vb`）、主题（`ThemeManager.vb`）、更新（`Updater.vb`） |
| `Forms/` | WinForms 窗体。主窗体是 `Main.vb`，含大量下载列表与主题逻辑 |
| `HttpModule/` | 内置 Web 服务器模块，`Template/` 下是 HTML 模板 |
| `Stegano/` | 隐写术相关窗体与逻辑 |
| `Resources/Language/` | 界面多语言 XML，每种语言一个文件 |
| `Resources/DLLs/` | 第三方依赖 DLL，随仓库提交 |
| `docs/` | 本文档与变更日志 |

## 贡献流程

### 1. Fork 并克隆仓库

```bash
# Fork 仓库到自己的 GitHub 账户后:
git clone https://github.com/<你的用户名>/MegaDownloader-Revival.git
cd MegaDownloader
git remote add upstream https://github.com/a1175815821/MegaDownloader-Revival.git
```

### 2. 创建功能分支

```bash
# 从最新的 main 分支创建
git checkout main
git pull upstream main
git checkout -b feature/你的功能名称
# 或: fix/bug-描述, docs/文档主题, i18n/语言-改进
```

### 3. 开发与本地测试

- 在 Visual Studio 中打开 `MegaDownloader.sln`
- 选择 `Debug` 配置构建
- 运行 `bin/Debug/MegaDownloader.exe`,验证你的修改

**测试用例(请务必覆盖):**

- 旧版链接:`https://mega.nz/#!abcDEF!ghijklmnop`
- 新版链接:`https://mega.nz/file/abcDEF#ghijklmnop`
- 文件夹链接:`https://mega.nz/folder/abcDEF#ghijklmnop`
- 加密链接:`mega://enc?...`
- 剪贴板自动识别
- 拖拽链接

### 4. 提交代码

遵循 [Conventional Commits](https://www.conventionalcommits.org/zh-hans/v1.0.0/) 规范:

```
<type>(<scope>): <subject>

<body可选>

<footer可选>
```

常用类型:

- `feat`: 新功能,如 `feat(url): 支持 mega.nz/embed/ 链接格式`
- `fix`: Bug 修复,如 `fix(clipboard): 修复剪贴板监听失效问题`
- `docs`: 文档,如 `docs: 补充 zh-CN 翻译`
- `refactor`: 重构,如 `refactor(conexion): 简化代理设置逻辑`
- `i18n`: 翻译,如 `i18n(zh-CN): 补全未翻译条目`

```bash
git add .
git commit -m "feat(url): 支持 mega.nz/embed/ 链接格式"
```

### 5. 推送并发起 PR

```bash
git push origin feature/你的功能名称
```

到 GitHub 上发起 Pull Request 到 `main` 分支,在 PR 描述中说明:

- 这个 PR 修改了什么?
- 为什么需要修改?(关联 Issue 编号)
- 如何测试?
- 是否影响现有功能?

### 6. 代码评审与合并

维护者会评审你的 PR,可能会请求修改。请耐心配合,所有修改都为了项目的长期可维护性。

## 代码风格约定

- VB.NET 项目已启用 `Option Strict On`、`Option Explicit Off`、`Option Infer On`,**新增代码必须满足这些约束**
- 文件编码:**UTF-8 with BOM**
- 缩进:**4 个空格**
- 命名:
  - 类、方法: PascalCase,如 `ExtraerFileID`
  - 私有字段: camelCase 或带下划线前缀,如 `_ProxyIP`
  - 局部变量: camelCase,如 `fileInfo`
- 注释:
  - 复杂逻辑需用 `'` 单行注释说明
  - 公共 API 用 `''' <summary>` XML 文档注释
- 原项目使用西班牙语命名,如 `Clases`、`Configuracion`、`Fichero`。**为保持一致性,新增代码可使用英语命名**,但不要批量重命名现有标识符

## 添加新的 Crypter / Link Protector

参考 [`Clases/Crypters/EncrypterMega.vb`](../Clases/Crypters/EncrypterMega.vb) 的实现模式:

1. 在 `Clases/Crypters/` 下新建 `<Name>.vb`
2. 实现 `ObtenerInformacionFichero` 方法,返回 `Conexion.InformacionFichero`
3. 在 [`Clases/URLExtractor.vb`](../Clases/URLExtractor.vb) 中:
   - 添加 `<NAME>TOKEN` 常量
   - 在 `patternOthers` 中添加匹配正则
   - 在 `ExtraerFileID` 中添加分支
4. 必要时在 `Forms/Main.vb` 中接入 UI

## 文档与翻译

本项目的**文档**与**软件界面**是两套独立的多语言体系，都欢迎贡献。

### 文档多语言（README / CHANGELOG / CONTRIBUTING）

文档采用手动翻译模式，无自动翻译流程：

- README 维护五个语言版本（`README.md`、`docs/README.*.md`），CONTRIBUTING 只维护英文与简体中文。**CHANGELOG 只维护简体中文版**（`docs/CHANGELOG.md`），不要再建翻译副本；非中文读者可自行机翻
- 改 README 或 CONTRIBUTING 时请同步更新所维护的语言版本，保持内容一致
- 想改进文档措辞 → 直接改对应语言的文件，或提 PR
- 想新增一门语言 → 复制英文（或中文）文档为新语言文件，并更新各文档顶部的语言导航链接与语言对照表

### 软件界面多语言

界面文案在 `Resources/Language/<locale>-Language.xml`，每种语言一个文件。

改进步骤：

1. 找到对应语言的 XML（如 `ja-JP-Language.xml`）
2. 修改 `<Text>` 节点的 CDATA 内容，**不要改 `key` 属性**
3. 若新增键，需同时确保 `en-US-Language.xml` 中有同名 key（作为回退基准）

### 新增界面语言

1. 复制 `Resources/Language/en-US-Language.xml` 为 `<locale>-Language.xml`
2. 翻译所有 `<Text>` 节点的 CDATA 内容
3. 在 `MegaDownloader.vbproj` 中添加嵌入资源：

```xml
<EmbeddedResource Include="Resources\Language\ja-JP-Language.xml" />
```

4. 运行程序，在 **设置 → 语言** 中能看到新语言

> 语言键查找有三级回退：磁盘文件 → 内置资源 → `en-US` → 返回 key 本身。所以即使某些键漏翻，程序也不会崩，只是会显示英文或原始 key。

## 报告 Bug

提交 Bug 时请在 Issue 中包含以下信息:

- **MegaDownloader 版本**(查看 关于 → 版本)
- **Windows 版本**
- **链接类型**(完整复制一个示例链接,敏感部分可脱敏)
- **复现步骤**
- **预期行为** vs **实际行为**
- **错误日志**(如有,位于程序目录下的日志文件)

## 发布流程（仅维护者）

1. 确认 `Debug` 与 `Release` 配置都能构建
2. 更新 `docs/CHANGELOG.md`（仅简体中文），追加新版本章节
3. 更新 `My Project/AssemblyInfo.vb` 中的 `AssemblyVersion` 与 `AssemblyFileVersion`
4. 在 `Resources/InternalConfig.xml`（Base64 编码）中更新 `VERSION_MEGADOWNLOADER` 与 `VERSION_UPDATE`
5. 更新 `docs/version.xml` 中的 `<Version>`
6. 用简体中文撰写发布说明（参考往期 `release-notes-*.md` 的结构）
7. 创建 Git Tag 并推送，CI 会自动构建产物并创建 GitHub Release：

```bash
git tag -a v2.5.0 -m "Release v2.5.0"
git push origin main
git push origin v2.5.0
```

CI 在 tag `v*` 上会自动：
- 构建 Release 配置
- 校验单文件版的 12 个内嵌 DLL 是否齐全（缺失则构建失败，避免发出「双击没反应」的包）
- 打包为 zip 并上传 GitHub Release

> 测试预发布（RC 版）建议先以 `prerelease` 形式发布，验证通过后再转为正式版。

## 联系方式

- 提交 Issue:GitHub Issues
- 安全相关问题:请勿在公开 Issue 中讨论,通过邮件联系维护者

---

再次感谢你的贡献!让我们一起让 MegaDownloader 焕发新生。 🚀
