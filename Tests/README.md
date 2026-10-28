# 回归验证

在 Windows 项目根目录运行：

```powershell
.\Tests\Run-RegressionTests.ps1
```

脚本构建 Release，使用系统 .NET Framework C# 编译器编译测试程序，再运行真实应用程序集。需要本项目已有的 .NET SDK、.NET Framework 4.8 构建环境及资源依赖，无需安装测试框架或连接 MEGA。

覆盖解压同名文件保护、ZIP 空目录、盘符和 UNC 根路径、无扩展名重命名、下载断点恢复、同名并发任务、旧版临时文件迁移、新临时文件身份的持久化及清理、完整成品恢复、流媒体 Range 分段和尾块。7z 测试使用仓库自带的 `Resources/Binaries/7zr.exe` 创建小型压缩包。

测试数据及日志保留在系统临时目录，控制台会输出位置；不读取真实账户或下载队列。

也可以验证 Debug：

```powershell
.\Tests\Run-RegressionTests.ps1 -Configuration Debug
```
