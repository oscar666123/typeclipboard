# Type Clipboard

Type Clipboard 是一个 Windows 桌面小工具，用来把剪贴板文本模拟成键盘输入，打到用户选定的外部目标窗口里。它适合 RDP、服务器窗口、远程控制台、受限系统等普通粘贴被禁用的场景。

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
- Emergency、Type、Stop 三个全局快捷键均可自定义和持久保存，默认分别为 F8、F9、F10。
- 点击快捷键按钮后按下新按键即可完成修改；F1–F24 和 Pause 支持单键，其他按键使用 Ctrl 或 Alt 组合。
- 支持窗口置顶，默认启用。
- 点击 **Type** 后自动恢复最近选定的外部目标窗口，并在开始延迟结束后锁定目标窗口和可识别的目标输入控件。
- 输入期间切换窗口或输入控件会自动停止。
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
3. 把光标放进目标 RDP、服务器或应用的输入框，再点击 **Type**；程序会自动恢复该目标窗口。也可以在目标窗口中直接按全局 Type 快捷键，默认是 F9。
4. 程序恢复目标窗口后等待设定的开始延迟，然后从当前获得焦点的输入位置开始输入。
5. 需要中断时按 Emergency 快捷键、Stop 快捷键，或点击 **Stop**；默认按键是 F8 和 F10。
6. 点击 **Type History** 或按 Ctrl+H，可重新使用以前输入过的文本。

## 控件说明

- **Refresh clipboard**：手动重新读取 Windows 剪贴板文本。
- **Type**：恢复最近选定的外部目标窗口，等待开始延迟后模拟输入。
- **Stop**：请求立即取消。
- **Type Enter**：输入完成后追加 Enter。
- **Emergency enabled**：启用 Emergency 全局急停快捷键。
- **Emergency key**：点击按键按钮，再按下新的 Emergency 快捷键；默认 F8。
- **Start delay (ms)**：目标窗口恢复后，到首个字符输入前的等待时间。
- **Interkey delay (ms)**：每个字符或换行后的等待时间。
- **Type shortcut**：点击按键按钮，再按下新的全局 Type 快捷键；默认 F9。
- **Stop shortcut**：点击按键按钮，再按下新的全局 Stop 快捷键；默认 F10。
- **Always on top**：让 Type Clipboard 保持在其他窗口上方，同时保留正常最小化功能。
- **Type History**：展开或收起可搜索的历史侧栏。
- **Save Type History**：控制后续 Type 操作是否写入历史。
- **Pause History**：在当前程序运行期间暂停记录，已有历史仍可使用。
- **Maximum history items**：设置 20–1000 条未置顶历史上限。

从旧版本迁移的设置会保留已禁用的 Type 或 Stop 快捷键；点击对应按钮录入新按键后，该快捷键会启用。

历史卡片菜单包含 **Load to textbox**、**Type again**、**Copy to clipboard**、**Pin / Unpin** 和 **Delete**。**Clear all** 会在确认后清除未置顶记录；侧栏菜单可以在更强确认后删除包括置顶记录在内的全部历史。

键盘操作：

- **Ctrl+H**：打开并聚焦 Type History。
- **Enter**：把选中历史加载到文本框。
- **Ctrl+Enter**：再次输入选中历史。
- **Delete**：确认后删除选中历史。
- **Escape**：收起 Type History。
- **Escape（修改快捷键时）**：取消本次快捷键修改。

三个自定义快捷键、Emergency 启用状态、窗口置顶、历史保存开关和历史数量上限保存在 `%LOCALAPPDATA%\TypeClipboard\settings.json`。

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

脚本默认从项目文件读取版本号。需要构建指定数字版本时，可添加 `-Version 0.4.0`；ZIP 文件名和 EXE 版本信息会使用同一个值。

## 已知运行边界

- 目标程序以管理员权限运行时，Type Clipboard 通常也需要同等权限。
- 某些远程控制台和特殊应用对模拟输入的处理可能不同。
- 停止逻辑会在每个字符前和每次延迟后检查取消；已经发给 Windows 的单个按键事件无法撤回。
- 程序在开始输入前恢复并锁定目标窗口；切换到其他本地窗口或可识别的其他输入控件会停止本次输入。
- 全局快捷键被其他程序占用时可能注册失败，Type Clipboard 会在状态栏显示提示。
- 置顶历史不会被容量上限自动清理，也不会被普通 **Clear all** 删除。
- 历史文件损坏时会重命名为 `type-history-corrupted-<时间戳>.json`，程序会使用空历史继续运行。
