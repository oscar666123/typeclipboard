@echo off
setlocal

set "APP_NAME=Type Clipboard"
set "APP_DIR=%LOCALAPPDATA%\Programs\Type Clipboard"
set "START_MENU_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Type Clipboard"
set "DESKTOP_SHORTCUT=%USERPROFILE%\Desktop\Type Clipboard.lnk"
set "START_MENU_SHORTCUT=%START_MENU_DIR%\Type Clipboard.lnk"
set "UNINSTALL_SCRIPT=%APP_DIR%\Uninstall Type Clipboard.cmd"

if not exist "%APP_DIR%" mkdir "%APP_DIR%"
if errorlevel 1 goto failed

copy /Y "%~dp0TypeClipboard.exe" "%APP_DIR%\TypeClipboard.exe" >nul
if errorlevel 1 goto failed

if not exist "%START_MENU_DIR%" mkdir "%START_MENU_DIR%"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$shell = New-Object -ComObject WScript.Shell; $shortcut = $shell.CreateShortcut('%DESKTOP_SHORTCUT%'); $shortcut.TargetPath = '%APP_DIR%\TypeClipboard.exe'; $shortcut.WorkingDirectory = '%APP_DIR%'; $shortcut.Save(); $shortcut = $shell.CreateShortcut('%START_MENU_SHORTCUT%'); $shortcut.TargetPath = '%APP_DIR%\TypeClipboard.exe'; $shortcut.WorkingDirectory = '%APP_DIR%'; $shortcut.Save()"
if errorlevel 1 goto failed

(
  echo @echo off
  echo del "%DESKTOP_SHORTCUT%" 2^>nul
  echo del "%START_MENU_SHORTCUT%" 2^>nul
  echo rmdir "%START_MENU_DIR%" 2^>nul
  echo del "%APP_DIR%\TypeClipboard.exe" 2^>nul
  echo del "%APP_DIR%\Uninstall Type Clipboard.cmd" 2^>nul
  echo rmdir "%APP_DIR%" 2^>nul
) > "%UNINSTALL_SCRIPT%"

start "" "%APP_DIR%\TypeClipboard.exe"
exit /b 0

:failed
echo Failed to install Type Clipboard.
pause
exit /b 1
