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
- Type 和 Stop 快捷键可分别选择，支持在其他窗口获得焦点时生效的全局选项。
- 支持窗口置顶，默认启用。
- 开始延迟结束后锁定当前前台目标窗口；焦点切换到其他窗口时自动停止。
- 自动保存通过 **Type** 操作发送的文本，并记录 Started、Completed、Stopped、Failed 状态。
- 完全相同的历史内容会复用原记录，同时更新时间和使用次数。
- 历史卡片支持搜索、加载、再次输入、复制、置顶和删除。
- 支持暂停历史、关闭新记录，并在普通清理时保留置顶记录。
- 未置顶历史数量可设置为 20–1000 条，默认 100 条。

## 安装

在 GitHub Release 下载最新版 `TypeClipboard-Portable-vX.Y.Z.zip`，解压后运行 `TypeClipboard.exe`。

## 使用流程

1. 在本机正常复制文本。
2. 打开 **Type Clipboard**，预览框会自动更新。
3. 点击 **Type**；也可以先聚焦目标窗口，再按 F9 等全局 Type 快捷键。
4. 在开始延迟结束前，把焦点切到目标 RDP、服务器或应用窗口。
5. 需要中断时按所选全局急停热键、所选 Stop 快捷键，或点击 **Stop**；全局 Stop 选项在目标窗口获得焦点时仍然有效。
6. 点击 **Type History** 或按 Ctrl+H，可重新使用以前输入过的文本。

## 控件说明

- **Refresh clipboard**：手动重新读取 Windows 剪贴板文本。
- **Type**：开始延迟后，向当前活动窗口模拟输入。
- **Stop**：请求立即取消。
- **Type Enter**：输入完成后追加 Enter。
- **F8 hotkey**：启用所选全局急停热键。
- **Emergency hotkey**：选择 F8、Ctrl+Alt+F8 或 Pause/Break。
- **Start delay (ms)**：点击 Type 后留给用户切换目标窗口的时间。
- **Interkey delay (ms)**：每个字符或换行后的等待时间。
- **Type shortcut**：可选窗口内 Ctrl+T、全局 Ctrl+Shift+T、全局 Ctrl+Alt+T、全局 F9 或 Disabled。
- **Stop shortcut**：可选窗口内 Esc、全局 Ctrl+Shift+S、全局 Ctrl+Alt+S、全局 F10 或 Disabled。
- **Always on top**：让 Type Clipboard 保持在其他窗口上方，同时保留正常最小化功能。
- **Type History**：展开或收起可搜索的历史侧栏。
- **Save Type History**：控制后续 Type 操作是否写入历史。
- **Pause History**：在当前程序运行期间暂停记录，已有历史仍可使用。
- **Maximum history items**：设置 20–1000 条未置顶历史上限。

历史卡片菜单包含 **Load to textbox**、**Type again**、**Copy to clipboard**、**Pin / Unpin** 和 **Delete**。**Clear all** 会在确认后清除未置顶记录；侧栏菜单可以在更强确认后删除包括置顶记录在内的全部历史。

键盘操作：

- **Ctrl+H**：打开并聚焦 Type History。
- **Enter**：把选中历史加载到文本框。
- **Ctrl+Enter**：再次输入选中历史。
- **Delete**：确认后删除选中历史。
- **Escape**：收起 Type History。

快捷键、窗口置顶、历史保存开关和历史数量上限保存在 `%LOCALAPPDATA%\TypeClipboard\settings.json`。

Type History 保存在 `%APPDATA%\TypeClipboard\type-history.json`。历史记录来源仅为程序的 **Type** 操作；剪贴板自动刷新不会创建历史记录。历史文件以明文 JSON 保存输入内容，处理密码、API Key、令牌或私密命令时，应使用 **Pause History**、关闭 **Save Type History**，或及时删除敏感记录。

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

脚本默认从项目文件读取版本号。需要构建指定数字版本时，可添加 `-Version 0.2.5`；ZIP 文件名和 EXE 版本信息会使用同一个值。

## 已知运行边界

- 目标程序以管理员权限运行时，Type Clipboard 通常也需要同等权限。
- 某些远程控制台和特殊应用对模拟输入的处理可能不同。
- 停止逻辑会在每个字符前和每次延迟后检查取消；已经发给 Windows 的单个按键事件无法撤回。
- 开始延迟结束后会记录当前前台目标窗口；切换到其他本地窗口会停止本次输入。
- 全局快捷键被其他程序占用时可能注册失败，Type Clipboard 会在状态栏显示提示。
- 置顶历史不会被容量上限自动清理，也不会被普通 **Clear all** 删除。
- 历史文件损坏时会重命名为 `type-history-corrupted-<时间戳>.json`，程序会使用空历史继续运行。
