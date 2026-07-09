# Type Clipboard

Type Clipboard 是一个 Windows 桌面小工具，用来把剪贴板文本模拟成键盘输入，打到当前获得焦点的窗口里。它适合 RDP、服务器窗口、远程控制台、受限系统等普通粘贴被禁用的场景。

[English README](README.md)

## 功能

- 从 Windows 剪贴板读取文本到可编辑预览框。
- 使用 `SendInput` 逐字符输入。
- 普通字符使用 Unicode 输入。
- 文本换行会转换成真实 Enter 键。
- 可选在输入完成后追加 Enter。
- 可配置开始延迟和字符间延迟。
- 异步输入，界面保持响应。
- 支持按钮停止和全局热键急停。
- 急停热键可选：F8、Ctrl+Alt+F8、Pause/Break。

## 安装

在 GitHub Release 下载最新版，选择一个包：

- `TypeClipboard-Setup-vX.Y.Z.exe`：当前用户安装包。
- `TypeClipboard-Portable-vX.Y.Z.zip`：便携版。

安装包会把程序放到：

```text
%LOCALAPPDATA%\Programs\Type Clipboard
```

安装后会创建开始菜单快捷方式和桌面快捷方式。

## 使用流程

1. 在本机正常复制文本。
2. 打开 **Type Clipboard**。
3. 点击 **Copy clipboard to textbox**。
4. 点击 **Type**。
5. 在开始延迟结束前，把焦点切到目标 RDP、服务器或应用窗口。
6. 需要中断时按所选急停热键，或点击 **Stop**。

## 控件说明

- **Copy clipboard to textbox**：读取 Windows 剪贴板文本。
- **Type**：开始延迟后，向当前活动窗口模拟输入。
- **Stop**：请求立即取消。
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
dotnet publish .\TypeClipboard\TypeClipboard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## 已知运行边界

- 目标程序以管理员权限运行时，Type Clipboard 通常也需要同等权限。
- 某些远程控制台和特殊应用对模拟输入的处理可能不同。
- 停止逻辑会在每个字符前和每次延迟后检查取消；已经发给 Windows 的单个按键事件无法撤回。
