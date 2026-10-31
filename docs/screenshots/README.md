# 2.6.1 界面截图

以下图片来自 2.6.1 编译产物的原生 WinForms 控件，通过隔离界面夹具与 `PrintWindow` 截取。任务、元数据和下载路径均为示例；不加载真实账号或队列，也不启动 MEGA 下载。界面语言为简体中文。`banner.png` 与 `social-preview.png` 为项目宣传图。

在仓库根目录运行：

```powershell
.\Tests\Run-RegressionTests.ps1 -OutputDirectory bin\Release-2.6.1 -UiOnly
```

终端最后会输出截图所在的临时目录。按清单复制图片，不提交整个临时目录：

| 临时截图 | 文档图片 |
| --- | --- |
| `workbench-tasks-Light.png` | `main-light.png` |
| `workbench-tasks-Dark.png` | `main-dark.png` |
| `workbench-empty-Light.png` | `main-empty.png` |
| `workbench-details-900-Light.png` | `details-bottom.png` |
| `add-links-Light.png` | `add-links.png` |
| `add-preflight-Light.png` | `preflight.png` |
| `settings-loaded-first-open.png` | `settings-general.png` |
| `settings-loaded-dark.png` | `settings-dark.png` |
| `settings-loaded-advanced.png` | `settings-advanced.png` |
| `settings-loaded-connection.png` | `settings-connection.png` |
| `download-schedule.png` | `download-schedule.png` |

首页按 GitHub 主题选择主窗口与设置的深浅色图片。其他视图可直接查看：

- [空队列](main-empty.png)
- [窄窗口的底部详情](details-bottom.png)
- [连接设置](settings-connection.png)
- [高级设置](settings-advanced.png)
- [定时下载](download-schedule.png)

截图不代表真实网络传输、多显示器切换或其他设备的高 DPI 场景已经验收。
