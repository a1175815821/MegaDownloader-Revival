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

## 贡献流程

### 1. Fork 并克隆仓库

```bash
# Fork 仓库到自己的 GitHub 账户后:
git clone https://github.com/<你的用户名>/MegaDownloader.git
cd MegaDownloader
git remote add upstream https://github.com/<原始仓库>/MegaDownloader.git
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

## 添加新的语言翻译

1. 复制 `Resources/Language/en-US-Language.xml` 为 `<locale>-Language.xml`(如 `ja-JP-Language.xml`)
2. 翻译所有 `<Text>` 节点的 `CDATA` 内容
3. 在 `MegaDownloader.vbproj` 中添加 `<EmbeddedResource>` 引用:

```xml
<EmbeddedResource Include="Resources\Language\ja-JP-Language.xml" />
```

4. 在 README 的「支持的语言」表格中添加新语言

## 添加新的 Crypter / Link Protector

参考 [`Clases/Crypters/EncrypterMega.vb`](../Clases/Crypters/EncrypterMega.vb) 的实现模式:

1. 在 `Clases/Crypters/` 下新建 `<Name>.vb`
2. 实现 `ObtenerInformacionFichero` 方法,返回 `Conexion.InformacionFichero`
3. 在 [`Clases/URLExtractor.vb`](../Clases/URLExtractor.vb) 中:
   - 添加 `<NAME>TOKEN` 常量
   - 在 `patternOthers` 中添加匹配正则
   - 在 `ExtraerFileID` 中添加分支
4. 必要时在 `Forms/Main.vb` 中接入 UI

## 报告 Bug

提交 Bug 时请在 Issue 中包含以下信息:

- **MegaDownloader 版本**(查看 关于 → 版本)
- **Windows 版本**
- **链接类型**(完整复制一个示例链接,敏感部分可脱敏)
- **复现步骤**
- **预期行为** vs **实际行为**
- **错误日志**(如有,位于程序目录下的日志文件)

## 发布流程(仅维护者)

1. 确认所有测试通过
2. 更新 [CHANGELOG.md](CHANGELOG.md)
3. 更新 `My Project/AssemblyInfo.vb` 中的 `AssemblyVersion` 与 `AssemblyFileVersion`
4. 在 `InternalConfig.xml`(Base64 编码)中更新 `VERSION_MEGADOWNLOADER` 与 `VERSION_UPDATE`
5. 创建 Git Tag:`git tag -a v1.9.0 -m "Release v1.9.0"`
6. 推送 Tag:`git push origin v1.9.0`
7. 在 GitHub Releases 中上传 `bin/Release/MegaDownloader.exe`

## 联系方式

- 提交 Issue:GitHub Issues
- 安全相关问题:请勿在公开 Issue 中讨论,通过邮件联系维护者

---

再次感谢你的贡献!让我们一起让 MegaDownloader 焕发新生。 🚀
