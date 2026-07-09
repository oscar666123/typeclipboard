# Type Clipboard

Type Clipboard 是一个 Windows 桌面小工具，用来把剪贴板文本模拟成键盘输入，打到当前获得焦点的窗口里。它适合 RDP、服务器窗口、远程控制台、受限系统等普通粘贴被禁用的场景。

[English README](README.md)

## 功能

- 从 Windows 剪贴板读取文本到可编辑预览框。
- Windows 剪贴板变化时自动刷新预览框。
- 剪贴板不再包含文本时自动清空预览框。
- 使用 `SendInput` 逐字符输入。
- 普通字符使用 Unicode 输入。
- 文本换行会转换成真实 Enter 键。
- 可选在输入完成后追加 Enter。
- 可配置开始延迟和字符间延迟。
- 异步输入，界面保持响应。
- 支持按钮停止和全局热键急停。
- 急停热键可选：F8、Ctrl+Alt+F8、Pause/Break。
- 窗口快捷键：Ctrl+T 开始输入，Esc 停止输入。
- 开始延迟结束后锁定当前前台目标窗口；焦点切换到其他窗口时自动停止。

## 安装

在 GitHub Release 下载最新版 `TypeClipboard-Portable-vX.Y.Z.zip`，解压后运行 `TypeClipboard.exe`。

## 使用流程

1. 在本机正常复制文本。
2. 打开 **Type Clipboard**，预览框会自动更新。
3. 点击 **Type** 或按 **Ctrl+T**。
4. 在开始延迟结束前，把焦点切到目标 RDP、服务器或应用窗口。
5. 需要中断时按所选急停热键；软件窗口有焦点时也可以按 **Esc**，或点击 **Stop**。

## 控件说明

- **Refresh clipboard**：手动重新读取 Windows 剪贴板文本。
- **Type (Ctrl+T)**：开始延迟后，向当前活动窗口模拟输入。
- **Stop (Esc)**：请求立即取消。
- **Type Enter**：输入完成后追加 Enter。
- **F8 hotkey**：启用所选全局急停热键。
- **Emergency hotkey**：选择 F8、Ctrl+Alt+F8 或 Pause/Break。
- **Start delay (ms)**：点击 Type 后留给用户切换目标窗口的时间。
- **Interkey delay (ms)**：每个字符或换行后的等待时间。

## 从源码构建

要求：

- Windows
- .NET 8 SDK 或更新稳定版 .NET SDK
- Visual Studio 2022 或 `dotnet` CLI

构建：

```powershell
dotnet build .\TypeClipboard.sln
```

运行：

```powershell
dotnet run --project .\TypeClipboard\TypeClipboard.csproj
```

发布 Windows x64 自包含单文件：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\packaging\build-release.ps1
```

脚本默认从项目文件读取版本号。需要构建指定数字版本时，可添加 `-Version 0.2.2`；ZIP 文件名和 EXE 版本信息会使用同一个值。

## 已知运行边界

- 目标程序以管理员权限运行时，Type Clipboard 通常也需要同等权限。
- 某些远程控制台和特殊应用对模拟输入的处理可能不同。
- 停止逻辑会在每个字符前和每次延迟后检查取消；已经发给 Windows 的单个按键事件无法撤回。
- 开始延迟结束后会记录当前前台目标窗口；切换到其他本地窗口会停止本次输入。
