# 固定测试数据

三组验收数据由同一个生成器产生，所以每台机器的队列、配置和加密字段都是本机
构建自己写出来的，不跨机器复制。生成器在 `MegaDownloader.Core.Tests/TestDataSets.cs`。

## 1. 空配置（首开）

```powershell
dotnet run --project MegaDownloader.Core.Tests -- --write-empty-profile C:\Temp\md-empty
$env:MEGADOWNLOADER_DATA_DIR = 'C:\Temp\md-empty'
.\MegaDownloader.UI\bin\Debug\net10.0\MegaDownloader.exe
```

没有配置文件、没有队列。用来记录首屏可操作时间，以及确认空态能直接添加链接。

## 2. 679 任务队列（恢复与工作台）

```powershell
dotnet run --project MegaDownloader.Core.Tests -- --write-test-data C:\Temp\md-679
$env:MEGADOWNLOADER_DATA_DIR = 'C:\Temp\md-679'
.\MegaDownloader.UI\bin\Debug\net10.0\MegaDownloader.exe
```

23 个任务包、679 个文件，包内计数固定为 40/40/…/39，状态按 完成 / 排队 /
暂停 / 排队 循环，大小从文件序号推算。`ComenzarDescargando=False`，所以打开后
不会尝试下载这些合成链接。用来记录队列就绪时间、展开/滚动响应，以及对照
Windows 2.6.5 与 Avalonia 基线。

记录字段：构建版本、机器、系统、缩放、数据版本、首屏可操作、队列就绪、
添加完成、下载完成。

## 3. 约 500 文件的分享（预检选择）

分享本身需要真实的 MEGA 链接（生成器无法伪造服务端数据），形状要求由生成器
固定下来：

```powershell
dotnet run --project MegaDownloader.Core.Tests -- --describe-test-data
```

按输出的形状准备一次分享，上传后把链接记在这里；之后每轮验收都用同一个链接，
并在记录里写明链接版本。要点是 41 个目录 / 500 个文件、四层深度、两个不同父级
下的同名 `Season 1`、含中文与空格的名称、一个 180 字符长名、一个空目录，以及
一个名为 `Extras.mkv` 的目录和一个名为 `sample.mp4` 的目录。

## 4. 已知 SHA-256 的公开单文件（真实下载）

同样的 `--describe-test-data` 输出里给出了记录格式。选一个允许下载的公开文件，
记录链接、字节数与 SHA-256，然后用 `Tests/Test-PublishedTransfer.ps1` 在
Windows 上跑完整验收；macOS/Linux 按同一份检查项手工核对：队列为完成、进度
100%、目标目录只有一个成品、大小与哈希一致。

## 说明

- 三组数据都配合独立的 `MEGADOWNLOADER_DATA_DIR` 使用，互不影响，也不碰用户
  自己的配置。
- 队列里的链接与密钥是合成的：这些任务只用于界面与恢复测量，永远不会成功下载。
- 生成器不写任何真实账号信息。
