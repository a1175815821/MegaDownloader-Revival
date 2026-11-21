# MEGA 账号下载：首轮技术验证

验证日期：2026-11-21。上游基线：[MEGA SDK v10.21.1](https://github.com/meganz/sdk/releases/tag/v10.21.1)，提交 `81755609b860b0c839cd08e4d5c708d65a731d54`。

## 结论

**可以保留本项目的任务队列和界面，但不能把 `getDownloadUrl` 当作普通公开链接的直链适配器，直接接到现有 `FileDownloader`。** 推荐首版让 SDK 处理已登录任务的传输，让原下载器继续处理匿名任务。这不要求重写现有队列，也不需要把公开文件导入用户的云盘。

## 已核实的边界

| 验证点 | 结果 | 证据 |
| --- | --- | --- |
| 官方 SDK 是否有登录、两步验证和恢复会话接口 | 有。`login`、`multiFactorAuthLogin`、`dumpSession`、`fastLogin` 的公开签名通过了本机 MSVC x64/x86 头文件编译检查。 | [两步验证](https://github.com/meganz/sdk/blob/81755609b860b0c839cd08e4d5c708d65a731d54/include/megaapi.h#L12997-L13014)、[密码登录](https://github.com/meganz/sdk/blob/81755609b860b0c839cd08e4d5c708d65a731d54/include/megaapi.h#L13109-L13127)、[导出会话](https://github.com/meganz/sdk/blob/81755609b860b0c839cd08e4d5c708d65a731d54/include/megaapi.h#L13475-L13485)、[恢复会话](https://github.com/meganz/sdk/blob/81755609b860b0c839cd08e4d5c708d65a731d54/include/megaapi.h#L13195-L13206) |
| 普通公开文件能否用 `getPublicNode` → `getDownloadUrl` 拿直链 | **不能作为通用路径。** `getPublicNode` 把公开节点返回给回调，不把它添加到账号节点树。`getDownloadUrl` 根据句柄再次查找当前 SDK 客户端的节点；找不到就返回 `API_EARGS`。这是固定版本实现的结论，不是线上账号测出的错误。 | [公开节点的返回](https://github.com/meganz/sdk/blob/81755609b860b0c839cd08e4d5c708d65a731d54/src/megaapi_impl.cpp#L17064-L17074)、[取直链的查找条件](https://github.com/meganz/sdk/blob/81755609b860b0c839cd08e4d5c708d65a731d54/src/megaapi_impl.cpp#L22929-L22953) |
| SDK 是否提供公开文件的另一条下载路径 | 提供。文档明确支持把 `getPublicNode` 的结果交给 `startDownload`，实现也单独保留公开节点。两个接口的公开签名通过同一头文件编译检查。 | [公开节点接口文档](https://github.com/meganz/sdk/blob/81755609b860b0c839cd08e4d5c708d65a731d54/include/megaapi.h#L15328-L15348)、[构建 SDK 下载任务](https://github.com/meganz/sdk/blob/81755609b860b0c839cd08e4d5c708d65a731d54/src/megaapi_impl.cpp#L10843-L10853) |
| 现有下载器的本地续传与重试 | 使用本机 HTTP 夹具，4 项用例、79 项断言通过。当前下载器需要可按 Range 读取的**加密数据**，它会自行解密；不能把 SDK 下载好的文件或解密后的数据流当作原始直链喂给它。 | [任务启动时交给原下载器](../../Clases/Fichero.vb:620)、[Range 处理](../../Clases/FileDownloader.vb:1382)、[本地解密](../../Clases/FileDownloader.vb:1489) |

本地回归使用已有 `bin/Release-2.6.4`，未重新编译主程序。覆盖断点续传（52 项断言）、失效地址刷新（19 项）、单任务恢复（5 项）、停止后旧任务不得继续（3 项）。可复跑：

```powershell
.\Tests\Run-RegressionTests.ps1 -OutputDirectory bin\Release-2.6.4 -NoBuild -Cases download_resume,refreshed_retry,single_task_resume,download_generation_stop
```

## 建议如何接入

- **保留现有队列、任务信息和界面。** 在任务启动边界按任务身份选择传输后端：匿名继续走 `FileDownloader`；明确选择账号的公开单文件走 `MegaApi` 登录后的 `getPublicNode` / `startDownload`。SDK 负责已登录任务的解密与传输，不混用两个后端的临时文件、直链或分片状态。
- **只做一个本地隔离原型。** 当前程序是 VB.NET / .NET Framework 4.8；头文件可编译不代表原生 SDK 已构建、已链接或能从 VB.NET 调用。用单独目录、SDK App Key 和测试账号验证同一 SDK 实例内的会话与下载。可以先用原生探针完成验收，再决定正式桥接和打包方式。
- **传输完成之后才接回队列。** 正式接入时需要适配进度、取消、完成、会话失效与配额错误；只在安全落盘并校验完成后把状态标为完成。不要用会话失效来触发匿名回退，也不要自动换账号重试配额错误。

## 仍需真实账号验证

本轮未构建或链接完整 SDK、未访问真实账号、未测试线上配额归属和 SDK 断点恢复；没有修改生产代码。后续实测应使用一个测试账号和一个未导入该账号的小型公开文件，做到：

1. 在本地交互输入密码和两步验证码；下载到隔离目录，并与原文件比对 SHA-256。登录状态可独立确认，不能仅凭公开文件下载成功就认定传输使用了账号配额。
2. 选择记住登录后，将 SDK 会话放入 Windows 当前用户保护的存储，重启探针并用 `fastLogin` 验证会话。密码、验证码、会话、公开链接密钥和临时下载地址均不输出到日志，也不通过命令行参数传递。
3. 验证明确退出和会话失效：退出应清除本机保存的凭据，并在可用时使当前会话失效；失效后只给出重新登录提示。另验证 SDK 暂停、取消和续传，避免把原下载器的回归结果当作 SDK 的行为保证。
