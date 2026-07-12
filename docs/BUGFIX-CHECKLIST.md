# MegaDownloader Revival 修复清单

> 基于 2026-07-13 全项目代码审查生成
> 共发现问题 200+ 个,按优先级分阶段处理

---

## 第一阶段:P0 严重 Bug(必须修复)

### 1.1 安全漏洞

| # | 位置 | 问题描述 | 状态 |
|---|---|---|---|
| 1 | `Clases/ServidorWebController.vb:37,62` | Web 服务器绑定 `0.0.0.0` 监听所有网卡,公网直接暴露 | ⬜ |
| 2 | `Clases/MD5Utils.vb:29`、`HttpModule/WebInterfaceModule.vb:74` | 密码使用 MD5 无盐哈希,且用 `Encoding.Default` 而非 UTF-8 | ⬜ |
| 3 | `Clases/ServidorWebController.vb` | 整个 Web 服务器走明文 HTTP,无 HTTPS 支持 | ⬜(第二阶段) |
| 4 | `Forms/Configuration.vb:337` | 密码校验逻辑反了:密码非空但长度<8 不会报错 | ⬜ |
| 5 | `HttpModule/WebInterfaceModule.vb` | 无 CSRF 防护,POST 操作仅靠 session cookie | ⬜(第二阶段) |
| 6 | `Stegano/SteganoManager.vb:8` | 默认密码硬编码为 `URLExtractor.ENCODE_PASSWORD` | ⬜(第二阶段) |
| 7 | `Clases/Fichero.vb:751,827` | ZIP 密码加密密钥硬编码 `"passZIP"` | ⬜(第二阶段) |
| 8 | `Clases/Criptografia.vb:15` | DPAPI entropy 硬编码在源码中 | ⬜(第二阶段) |
| 9 | `Clases/StreamingLibrary/LibraryElement.vb:132` | 流媒体库导出密码硬编码 `"ae7}Kazdje/twiev"` | ⬜(第二阶段) |
| 10 | `Clases/ServerEncoderLinkHelper.vb:7` | `ENCODE_PASSWORD` 死常量 | ⬜(删除时清理) |

### 1.2 功能性 Bug(用户可感知)

| # | 位置 | 问题描述 | 状态 |
|---|---|---|---|
| 11 | `Clases/Conexion.vb:47-48` | 代理凭据自赋值 `_ProxyUser = _ProxyUser`,代理认证用户无法连接 | ⬜ |
| 12 | `Clases/Configuracion.vb:170,344` | `ApagarPC` 选项无法持久化 | ⬜ |
| 13 | `Clases/Configuracion.vb:160,355` | `MaxConexionesGuardadas` 无法持久化 | ⬜ |
| 14 | `Clases/Updater.vb:30-32` | 版本号比较用 Double,1.10 被误判为低于 1.7 | ⬜ |
| 15 | `Resources/InternalConfig.xml` | 版本号过时 `VERSION_MEGADOWNLOADER=1.7` | ⬜ |
| 16 | `My Project/AssemblyInfo.vb:34-35` | 程序集版本 `1.0.0.0` 与项目 1.9 不一致 | ⬜ |
| 17 | `Clases/Configuracion.vb:484-491` | `RegisterInStartup` NRE:注册表项不存在时崩溃 | ⬜ |
| 18 | `Clases/Configuracion.vb:421` | `_Usuario.Length` 空引用:DPAPI 解密返回 Nothing 时崩溃 | ⬜ |
| 19 | `ApplicationEvents.vb:36-47` | `SingleInstanceCallback` 空实现,`mega://` 参数丢失 | ⬜ |
| 20 | `ApplicationEvents.vb:59-62` | `LoadDLLFromStream` 未检查 stream 是否为 Nothing | ⬜ |
| 21 | `Clases/Language.vb:46-51` | Stream 资源泄漏 + NRE:资源不存在时崩溃 | ⬜ |
| 22 | `Clases/DescompresorController.vb:449` | 7z multipart 显式抛 `NotImplementedException` | ⬜(第二阶段) |

### 1.3 死锁风险(高严重度)

| # | 位置 | 问题描述 | 状态 |
|---|---|---|---|
| 23 | `Forms/Main.vb`(15+ 处) | Mutex 释放不在 Try/Finally,异常导致永久死锁 | ⬜ |
| 24 | `Clases/Paquete.vb:43-49,74-100,251-264` | 3 处 Mutex 无 Try/Finally | ⬜ |
| 25 | `Clases/DescompresorController.vb:529-535,573-579,628-634` | Mutex 重入未配对 | ⬜(第二阶段) |
| 26 | `Clases/FileDownloader.vb:1215-1222` | Dispose 未释放 listDownloaders 中的 worker | ⬜(第二阶段) |

### 1.4 加密异常吞噬(数据损坏风险)

| # | 位置 | 问题描述 | 状态 |
|---|---|---|---|
| 27 | `Clases/Criptografia.vb:121-123` | `AES_EncryptString` 空 Catch,加密失败返回空字符串 | ⬜ |
| 28 | `Clases/Criptografia.vb:203-205` | `AES_DecryptString` 空 Catch | ⬜ |
| 29 | `Clases/Criptografia.vb:197-198` | `FlushFinalBlock` 异常被吞 | ⬜ |
| 30 | `Clases/ServerEncoderLinkHelper.vb:266-269` | 解密路径 `FlushFinalBlock` 异常被吞 | ⬜(第二阶段) |
| 31 | `Clases/PreSharedKeyManager.vb:21-25` | `DecryptFileInfo` 吞掉所有解密异常 | ⬜(第二阶段) |

---

## 第二阶段:删除死代码

### 2.1 整体删除的失效文件(11 个)

| # | 文件 | 失效原因 | 状态 |
|---|---|---|---|
| 1 | `Clases/Crypters/EncrypterMega.vb` | `encrypterme.ga` 域名失效 | ⬜ |
| 2 | `Clases/Crypters/MegaCrypter.vb` | `megacrypter.com` 域名失效 | ⬜ |
| 3 | `Clases/Crypters/Youpaste.vb` | `youpaste.co` 域名失效 | ⬜ |
| 4 | `Clases/Crypters/LinkCrypter.vb` | `linkcrypter.net` 域名失效 | ⬜ |
| 5 | `Clases/Linkdecrypter.vb` | `linkdecrypter.com` 域名失效,依赖已废弃的 `__cfduid` | ⬜ |
| 6 | `Clases/LinkProtectors.vb` | 依赖已死的 Linkdecrypter,`IsShSt` 已是死代码 | ⬜ |
| 7 | `Clases/MovieInfo/Allocine.vb` | AlloCiné REST v3 API 下线 | ⬜ |
| 8 | `Clases/MovieInfo/Filmaffinity.vb` | 脆弱 HTML 抓取,网站改版即失效 | ⬜ |
| 9 | `Clases/MovieInfo/IMDB.vb` | OMDb 强制 API key,代码未提供 | ⬜ |
| 10 | `Clases/DLCHelper.vb` | 完全依赖不稳定的 dcrypt.it | ⬜ |
| 11 | `Clases/Serializer.vb` | 整个文件被注释 | ⬜ |
| 12 | `Clases/ClipboardChangeNotifier.vb` | 整个文件被注释,.vbproj 仍包含 | ⬜ |

**清理引用时需同步移除**:
- `Clases/Conexion.vb:297-300` 中对 4 个 Crypter 的调用
- `Clases/URLProcessor.vb:20-21` 对 LinkProtectors 的调用
- `Clases/URLExtractor.vb:21-24` 的 4 个 token 常量
- `Clases/Conexion.vb:67,72-80` 对 linkcrypter.net 的证书验证跳过
- `Clases/StreamingLibrary/StreamingLibraryManager.vb:55-57` 三个 `FillMissingFields` 调用
- `HttpModule/StreamingLibraryModule.vb:285-287` AJAX Save 中的 `FillMissingFields` 调用
- `Clases/StreamingLibrary/LibraryElement.vb:21-23` `IMDB`/`Allocine`/`Filmaffinity` 字段

### 2.2 InternalConfig.xml 中所有失效的 goo.gl 短链(14 个)

Google 短链服务于 2019 年 3 月完全停止解析

| # | 配置键 | 引用位置 | 状态 |
|---|---|---|---|
| 1 | `URL_NEW_VERSION` | `Clases/Conexion.vb:496` | ⬜ |
| 2 | `URL_NEW_USER` | `Clases/Conexion.vb:507` | ⬜ |
| 3 | `MEGA_TERMS` | `Forms/Main.vb:569` | ⬜ |
| 4-6 | `FAQ_LINK_ES/EN/FR` | `Forms/Main.vb:2491-2495` | ⬜ |
| 7-8 | `MEGAUPLOADER_LINK_ES/EN` | `Forms/Main.vb:2522-2524` | ⬜ |
| 9 | `CREDITS_LINK` | `Forms/Credits.vb:36` | ⬜ |
| 10-11 | `COLLABORATE_LINK_ES/EN` | `Forms/Main.vb:2512-2514` | ⬜ |
| 12-13 | `DOWNLOAD_LINK_ES/EN` | `Forms/Main.vb:2502-2504` | ⬜ |
| 14-15 | `STEGANO_LINK_ES/EN` | `Stegano/SteganoWizardLoad.vb:118` | ⬜ |
| 16 | `MEGA_SEARCH_CURL` | 显式标注 `OBSOLETE` | ⬜ |
| 17 | `SEARCH_LIST` | Reddit /r/megalinks 已封禁 | ⬜ |

### 2.3 URLExtractor 中失效的链接格式

| # | 行号 | 失效内容 | 状态 |
|---|---|---|---|
| 1 | `Clases/URLExtractor.vb:29` | `megashur.se/out.php` 已下线 | ⬜ |
| 2 | `Clases/URLExtractor.vb:30` | `chrome://mega/content/secure.html` 扩展已下架 | ⬜ |
| 3 | `Clases/URLExtractor.vb:52-55` | `lix.in`、`j.gs`、`q.gs`、`adf.ly` | ⬜ |
| 4 | `Clases/URLExtractor.vb:319-323` | `EsUrlAcortador()` 仅支持 `goo.gl` | ⬜ |

### 2.4 其他死代码

| # | 位置 | 问题描述 | 状态 |
|---|---|---|---|
| 1 | `Clases/Fichero.vb:545-571` | MD5 校验代码块 | ⬜ |
| 2 | `Clases/Fichero.vb:197-212,805` | `Cache.OptionalPassword` 死字段 | ⬜ |
| 3 | `Clases/Criptografia.vb:497-539` | 注释掉的 `DecryptFile` 和 `cipherData` | ⬜ |
| 4 | `Clases/Conexion.vb:65,105-106,279-284,287-290,439-446` | 死代码 | ⬜ |
| 5 | `Forms/Main.vb:2332-2354` | Skin 相关注释代码 | ⬜ |
| 6 | `Forms/Main.vb:1338-1340` | 失效的性能警告 | ⬜ |
| 7 | `Forms/Main.vb:238-239` | `btnCollaborate`/`btnUpdate` 硬编码 `Visible=False` | ⬜ |
| 8 | `Clases/Configuracion.vb:45,159,323,338` | `PermitirSkins` 字段全注释 | ⬜ |
| 9 | `Clases/ConfiguracionUI.vb` | `RutaSkin` 孤儿数据 | ⬜ |
| 10 | `Clases/ClipBoardViewer.vb:261-266` | `IsVista()` 函数死代码 | ⬜ |
| 11 | `Clases/ApplicationInstanceManager.vb:182-197,247-262` | IPC Remoting 注释代码 | ⬜ |

---

## 第三阶段:P1 高优先级 Bug

### 3.1 并发与线程安全

| # | 位置 | 问题描述 | 状态 |
|---|---|---|---|
| 1 | `Clases/StreamingHelper.vb:5` | `TempStreamingCache` 共享字典非线程安全 | ⏭️ |
| 2 | `HttpModule/StreamingModule.vb:24` | `Urls` 共享字典非线程安全 | ⏭️ |
| 3 | `HttpModule/StreamingLibraryModule.vb:26` | `_RespuestaAjax` 实例字段被并发请求共享 | ⏭️ |
| 4 | `Clases/InternalConfiguration.vb:5` | `XML_CONFIG` 共享字段非线程安全 | ⏭️ |
| 5 | `Forms/ELCForm.vb:229,104,136` | `BackgroundWorkerBusy` 标志无同步 | ⏭️ |
| 6 | `Clases/FileDownloader.vb:595-598` | 迭代 `ChunkList` 无锁 | ⏭️ |

### 3.2 资源泄漏

| # | 位置 | 问题描述 | 状态 |
|---|---|---|---|
| 1 | `Controls/ELCAccountControl.vb:309,327,345,363` | 4 个 `MouseHover` 处理程序 ToolTip 未 Dispose | ✅ |
| 2 | `Forms/AddLinks.vb:291,300,319` | ToolTip 资源泄漏 | ✅ |
| 3 | `Stegano/SteganoWizardSave.vb:133-146` | ToolTip 资源泄漏 | ✅ |
| 4 | `Stegano/SteganoManager.vb:26,41,62` | `Image.FromFile` 锁定源文件,FileStream 从未 Dispose | ✅ |
| 5 | `Clases/StreamingLibrary/StreamingLibraryManager.vb:290-306` | `CompressString`/`UnCompressString` 未 Using | ✅ |
| 6 | `HttpModule/WebInterfaceModule.vb:52,179,226,278` | 多处 StreamReader/StreamWriter 未 Using | ✅ |
| 7 | `Clases/ServerEncoderLinkHelper.vb:35` | `RandomNumberGenerator.Create()` 未 Dispose | ⬜ |
| 8 | `Forms/Main.vb:215-233` | 7 个 `Image.FromStream(file)` file stream 未关闭 | ✅ |
| 9 | `Clases/MegaURIProtocol.vb:13-37` | 注册表操作无 Finally 释放 | ✅ |
| 10 | `Clases/FileDownloader.vb:1068-1076` | `FlushToDisk` 中 FileStream 异时不释放 | ⬜ |

### 3.3 UI/逻辑错误

| # | 位置 | 问题描述 | 状态 |
|---|---|---|---|
| 1 | `Forms/Main.vb:641-642` | `clipChange.DestroyHandle/Uninstall` 顺序反了 | ✅ |
| 2 | `Forms/Main.vb:1525-1551` | `EsperarParadeDescargasYWorkers` 漏检查 `bgwDescompresorCompleted` | ✅ |
| 3 | `Forms/Main.vb:3066` | `Thread.Abort()` 危险 | ⬜ |
| 4 | `Forms/Main.vb:1104,1205,1336` | 跨线程 MsgBox 未检查窗体是否已关闭 | ⬜ |
| 5 | `Clases/FileDownloader.vb:509` | 后台线程弹 MessageBox | ⬜ |
| 6 | `Clases/DescompresorController.vb:138-144` | `Thread.Abort()` 用于取消解压 | ⬜ |
| 7 | `HttpModule/StreamingLibraryModule.vb:239-241` | `Case "Delete"` 缺 `Return True` | ✅ |
| 8 | `HttpModule/StreamingLibraryModule.vb:119-125` | `UsuarioLogueado` 超时逻辑错误 | ✅ |
| 9 | `Controls/ELCAccountControl.vb:132-133` | CellClick 未校验 `e.RowIndex` | ✅ |
| 10 | `Clases/MegaFolderHelper.vb:207` | `FillFolderStructure` 递归无 KeyNotFound 保护 | ⬜ |
| 11 | `Clases/Conexion.vb:496-512` | `bgPingNewVersion/User` 未解密 URL | ✅ |
| 12 | `Forms/ELCForm.vb:241-249` | 无限循环每 300ms 轮询 | ⬜ |
| 13 | `Clases/StreamingHelper.vb:85` | `Keys.Count / 2` 浮点除法 | ✅ |

---

## 第四阶段:P2 中优先级问题

### 4.1 协议过时

| # | 位置 | 问题描述 | 状态 |
|---|---|---|---|
| 1 | `Clases/Conexion.vb:52` | 仍启用 TLS 1.0/1.1 | ⬜ |
| 2 | `Clases/Conexion.vb:95,325` | `%SEQ%`/`%ID%` 用毫秒非真正序列号 | ⬜ |
| 3 | `Clases/Conexion.vb:9` | `useGlobalCDN` 硬编码 True | ⬜ |
| 4 | `Clases/MegaFolderHelper.vb:143` | 生成 `http://mega.co.nz/#N!` 旧格式 | ⬜ |
| 5 | 全项目 | 大量 `http://` 应改 `https://` | ⬜ |

### 4.2 代码质量问题

| # | 位置 | 问题描述 | 状态 |
|---|---|---|---|
| 1 | `HttpModule/StreamingModule.vb:257-264` | `ClientConnected` 反射访问私有字段 | ⬜ |
| 2 | `HttpModule/StreamingModule.vb:134-135` | `AddWithoutValidate` 反射调用 | ⬜ |
| 3 | `Clases/FileDownloader.vb:821-825` | 反射调用添加 Range 头 | ⬜ |
| 4 | `Clases/StreamingLibrary/LibraryElement.vb:30-61` | `ToJSON` 手工拼接 JSON | ⬜ |
| 5 | `HttpModule/StreamingLibraryModule.vb:389-396` | 手工拼接 JSON | ⬜ |
| 6 | `Clases/Paquete.vb:274-276` | 用 `GetHashCode` 比较 XML 内容 | ⬜ |
| 7 | `Clases/Configuracion.vb:217` | 用 HashCode 比较内容 | ⬜ |
| 8 | `Clases/DLCHelper.vb:30-32,49-54` | `While ReadToEnd` 死循环写法 | ⬜(整体删除时清理) |
| 9 | `Clases/Mutex.vb` | 类名 `Mutex` 与 `System.Threading.Mutex` 冲突 | ⬜ |
| 10 | `Clases/MegaFolderHelper.vb:86,126` | 变量名 `ex` 用于 Regex | ⬜ |
| 11 | `Clases/ThrottledStream.vb:188` | 变量名 `int` 是 VB.NET 关键字 | ⬜ |

---

## 第五阶段:P3 低优先级问题

### 5.1 国际化(zh-CN 不友好)

| # | 位置 | 问题描述 | 状态 |
|---|---|---|---|
| 1 | `Forms/Main.vb:350` | `"MegaDownloader started"` 未走 Language.GetText | ⬜ |
| 2 | `Forms/Main.vb:357,846,883,1104,1167,1205,1336` | 多处硬编码英文 | ⬜ |
| 3 | `Forms/Main.vb:2489-2496` | FAQ 链接仅区分 ES/FR/EN | ⬜ |
| 4 | 全项目 | 西班牙语控件名 | ⬜ |

### 5.2 过时 API

| # | 位置 | 问题描述 | 状态 |
|---|---|---|---|
| 1 | `Clases/MovieInfo/Allocine.vb:104` | `SHA1Managed.Create()` 已过时 | ⬜(整体删除) |
| 2 | `Stegano/SteganoManager.vb:95-98` | `Net.WebClient` 已标记 obsolete | ⬜ |
| 3 | `Clases/Cryptography/AES.vb:10` | PBKDF2 迭代次数过低 | ⬜ |
| 4 | `Forms/Main.vb:2179` | VB6 `Declare` 风格 | ⬜ |
| 5 | `Clases/MEGA_ErrorHandler.vb` | 错误码列表不完整 | ⬜ |
| 6 | `My Project/AssemblyInfo.vb:15` | `Copyright © 2015` 过时 | ⬜(第一阶段修复) |
| 7 | `Clases/Log.vb` | 日志无轮转 | ⬜ |
| 8 | `Clases/Log.vb:50-52,67,79` | 用 `Now` 而非 `DateTime.UtcNow` | ⬜ |

---

## 版本号统一规划

**目标版本**:2.0

| 位置 | 当前值 | 目标值 | 状态 |
|---|---|---|---|
| `My Project/AssemblyInfo.vb:34` (AssemblyVersion) | 1.0.0.0 | 2.0.0.0 | ⬜ |
| `My Project/AssemblyInfo.vb:35` (AssemblyFileVersion) | 1.0.0.0 | 2.0.0.0 | ⬜ |
| `My Project/AssemblyInfo.vb:15` (AssemblyCopyright) | Copyright © 2015 | Copyright © 2026 | ⬜ |
| `Resources/InternalConfig.xml` VERSION_MEGADOWNLOADER | 1.7 | 2.0 | ⬜ |
| `Resources/InternalConfig.xml` VERSION_UPDATE | 1.7 | 2.0 | ⬜ |

---

## 状态标记说明

- ⬜ 待处理
- 🔄 进行中
- ✅ 已完成
- ⏭️ 跳过(延后)
- ❌ 已废弃(整体删除时清理)

---

*最后更新:2026-07-13*
