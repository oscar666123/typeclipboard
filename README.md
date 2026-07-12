# Type Clipboard

Type Clipboard is a small Windows desktop app that simulates typing clipboard text into the currently focused window. It is useful when an RDP, server, remote console, or locked-down app blocks normal paste.

[中文说明](README.zh-CN.md)

## Features

- Loads Windows clipboard text into an editable preview box.
- Automatically refreshes the preview when the Windows clipboard changes.
- Clears the preview when the clipboard no longer contains text.
- Types text one character at a time with `SendInput`.
- Uses Unicode input for normal characters.
- Converts line breaks to real Enter key presses.
- Optional final Enter after typing.
- Configurable start delay and interkey delay.
- Responsive async typing loop with cancellation.
- Emergency stop by button or global hotkey.
- Hotkey choices: F8, Ctrl+Alt+F8, Pause/Break.
- Configurable Type and Stop shortcuts, including global options that work while another window is focused.
- Optional always-on-top mode, enabled by default.
- Locks each typing run to the foreground target selected after the start delay and stops if focus moves to another window.

## Installation

Download `TypeClipboard-Portable-vX.Y.Z.zip` from the latest GitHub Release, extract it, and run `TypeClipboard.exe`.

## Usage

1. Copy text on the local PC.
2. Open **Type Clipboard**. The preview updates automatically.
3. Click **Type**, or focus the target window and press a global Type shortcut such as F9.
4. Focus the target RDP, server, or app window before the start delay ends.
5. Press the selected emergency hotkey, use the selected Stop shortcut, or click **Stop** to interrupt. Global Stop options work while the target window is focused.

## Controls

- **Refresh clipboard**: manually reloads text from the Windows clipboard.
- **Type**: starts typing into the active window after the start delay.
- **Stop**: requests immediate cancellation.
- **Type Enter**: sends Enter after all text is typed.
- **F8 hotkey**: enables the selected global emergency hotkey.
- **Emergency hotkey**: selects F8, Ctrl+Alt+F8, or Pause/Break.
- **Start delay (ms)**: time to switch focus to the target window.
- **Interkey delay (ms)**: delay after each typed character or line break.
- **Type shortcut**: selects local Ctrl+T, global Ctrl+Shift+T, global Ctrl+Alt+T, global F9, or Disabled.
- **Stop shortcut**: selects local Esc, global Ctrl+Shift+S, global Ctrl+Alt+S, global F10, or Disabled.
- **Always on top**: keeps Type Clipboard above other windows while preserving normal minimization.

Shortcut and always-on-top selections are stored in `%LOCALAPPDATA%\TypeClipboard\settings.json`.

## Build From Source

Requirements:

- Windows
- .NET 8 SDK or newer stable .NET SDK
- Visual Studio 2022 or `dotnet` CLI

Build:

```powershell
dotnet build .\TypeClipboard.sln
```

Run:

```powershell
dotnet run --project .\TypeClipboard\TypeClipboard.csproj
```

Publish a self-contained Windows x64 build:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\packaging\build-release.ps1
```

The script reads the default version from the project file. To build a specific numeric version, add `-Version 0.2.5`; the ZIP name and EXE metadata use the same value.

## Known Runtime Boundaries

- Apps running as administrator may require Type Clipboard to run with the same integrity level.
- Some remote consoles and specialized apps may handle synthetic input differently.
- Stop cancellation is checked before each character and after each delay. A key event already sent to Windows cannot be recalled.
- The foreground target is captured after the start delay. Changing to another local window stops the run.
- A global shortcut may be unavailable when another app already registered it; Type Clipboard reports this in the status bar.
