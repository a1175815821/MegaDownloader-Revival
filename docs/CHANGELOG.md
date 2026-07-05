# 变更日志 (Changelog)

本项目所有重要变更均会记录在此文件中。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/),并遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

---

## [Unreleased]

### 🐛 修复

- **下载器崩溃**:修复 [`Clases/FileDownloader.vb`](../Clases/FileDownloader.vb) 第 681-683 行变量名错配导致的 `NullReferenceException`。当 MEGA 服务器返回 502 网关错误等异常时,catch 块误引用已被清空的 `exc` 局部变量(应为 `ex`),导致掩盖真实异常并中断整个下载流程。修复后,后台下载线程能正确传递真实异常,后续文件可继续下载。

### 计划中

- 修复 EncrypterMe.ga 链接因 API 端点下线而无法解析的问题
- 评估将 .NET Framework 4.8 迁移至 .NET 8/9 的可行性
- 完善 zh-CN 简体中文语言包的覆盖率
- 增加 GitHub Actions 自动构建 Release

## [1.9.0] - 2026-07-05

### MegaDownloader 复活计划首个公开发布版本

基于 MegaDownloader v1.8 反编译源码进行修复与重构,核心目标是恢复对 MEGA 新版链接格式的支持。

### ✨ 新增

- **URL 解析**:在 [`Clases/URLExtractor.vb`](../Clases/URLExtractor.vb) 的 `patternHTTPURI` 中新增 4 条正则,支持识别以下新版 MEGA 链接:
  - `https://mega.nz/file/<FileID>#<FileKey>`
  - `https://mega.nz/folder/<FolderID>#<FolderKey>`
  - `https://mega.co.nz/file/<FileID>#<FileKey>`
  - `https://mega.co.nz/folder/<FolderID>#<FolderKey>`
- **文件夹识别**:同步更新 `IsMegaFolder` 方法,使其能正确识别 `mega.nz/folder/` 与 `mega.co.nz/folder/` 风格的文件夹链接
- **TLS 1.2/1.3**:在 [`Clases/Conexion.vb`](../Clases/Conexion.vb) 中显式启用 `Tls12 | Tls11 | Tls` 协议,确保与 MEGA API 通信的兼容性
- 增加本仓库的 [README.md](../README.md)、[CONTRIBUTING.md](CONTRIBUTING.md)、[CHANGELOG.md](CHANGELOG.md)、`.gitignore` 等开发者文档

### 🐛 修复

- 修复从剪贴板复制新版 MEGA 链接时无法被识别的问题
- 修复从浏览器拖拽新版 MEGA 链接到主窗口无效的问题
- 修复新版文件夹链接无法被解析为子文件列表的问题
- **修复文件夹下载时 Base64 解码错误**:`mega.nz/folder/` 链接包含被多个用户分享的文件时,MEGA API 返回的 `fileN.k` 字段格式为 `handle1:key1/handle2:key2[/handle3:key3]`(用 `/` 分隔多个 `handle:key` 对)。原代码 `fileN.k.Substring(fileN.k.IndexOf(":") + 1)` 会把第一个 `:` 之后的所有内容(包括 `/handle2:key2`)当作 key,导致 `Convert.FromBase64String` 抛出 FormatException。修复方案:新增 `ExtractKeyFromK` 辅助函数,先找到文件夹本身的内部 handle (root),然后从 k 字段中提取与 root 匹配的 key;同时为解密步骤添加 try-catch,跳过无法解密的文件而不是抛出异常。参见 [`Clases/MegaFolderHelper.vb`](../Clases/MegaFolderHelper.vb)

### 🔄 变更

- `TargetFrameworkVersion` 维持 `v4.8`(原 v1.8 即已升级至 4.8)
- 仓库 LICENSE 维持 MIT 协议,补充复活计划版权声明

### ⚠️ 已知问题

- EncrypterMe.ga 因其官方 API 服务 (`http://encrypterme.ga/api`) 已下线,目前无法解析此类链接
- 部分 goo.gl 短链因 Google 关闭该服务而无法跳转
- 简体中文语言包尚有部分条目需补充翻译

### 📦 构建产物

- `MegaDownloader.exe` 主程序
- 依赖 DLL:`BouncyCastle.Crypto.dll`、`Newtonsoft.Json.dll`、`SharpCompress.dll`、`ObjectListView.dll`、`HttpServer.dll`、`Fadd.dll`、`F5Lib.dll`、`xunit.dll`

---

## [1.8.0] - 原版 (反编译源)

复活计划所基于的原始版本,本仓库通过反编译得到其源码作为修复起点。

### 主要特性

- 多线程并发下载
- MEGA 文件夹递归解析
- 加密链接 (`enc`/`enc2`/`fenc`/`fenc2`/`elc`) 支持
- 第三方 Crypter 集成 (MegaCrypter、YouPaste、LinkCrypter、EncrypterMe.ga)
- VLC 流媒体边下边播
- 内置 HttpServer Web 管理界面
- SharpCompress 自动解压
- 多语言界面 (10 种)
- Stegano 隐写术
- 自动更新检查

---

## 版本号说明

- 主版本号:重大功能变更或不向下兼容的修改
- 次版本号:新增功能,向下兼容
- 修订号:Bug 修复,向下兼容
