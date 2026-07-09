param(
    [string]$Version = "0.2.0"
)

$ErrorActionPreference = "Stop"
if (Get-Variable PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "TypeClipboard\TypeClipboard.csproj"
$artifactsDir = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsDir "publish"
$installerWorkDir = Join-Path $artifactsDir "installer-work"
$installerPath = Join-Path $artifactsDir "TypeClipboard-Setup-v$Version.exe"
$portableZip = Join-Path $artifactsDir "TypeClipboard-Portable-v$Version.zip"
$sedPath = Join-Path $artifactsDir "iexpress-TypeClipboard.sed"

New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
if (Test-Path $installerWorkDir) { Remove-Item -Recurse -Force $installerWorkDir }
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $installerWorkDir | Out-Null

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir

Copy-Item -LiteralPath (Join-Path $publishDir "TypeClipboard.exe") -Destination (Join-Path $installerWorkDir "TypeClipboard.exe") -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "install.cmd") -Destination (Join-Path $installerWorkDir "install.cmd") -Force

if (Test-Path $portableZip) { Remove-Item -Force $portableZip }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $portableZip -Force

if (Test-Path $installerPath) { Remove-Item -Force $installerPath }

$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=%DisplayLicense%
FinishMessage=%FinishMessage%
TargetName=%TargetName%
FriendlyName=%FriendlyName%
AppLaunched=%AppLaunched%
PostInstallCmd=%PostInstallCmd%
AdminQuietInstCmd=
UserQuietInstCmd=
SourceFiles=SourceFiles
[Strings]
InstallPrompt=
DisplayLicense=
FinishMessage=Type Clipboard has been installed.
TargetName=$installerPath
FriendlyName=Type Clipboard
AppLaunched=install.cmd
PostInstallCmd=<None>
FILE0=TypeClipboard.exe
FILE1=install.cmd
[SourceFiles]
SourceFiles0=$installerWorkDir\
[SourceFiles0]
%FILE0%=
%FILE1%=
"@

Set-Content -LiteralPath $sedPath -Value $sed -Encoding ASCII
$iexpress = Start-Process -FilePath "iexpress.exe" -ArgumentList "/N /Q $sedPath" -Wait -PassThru
$iexpressExitCode = $iexpress.ExitCode

if (-not (Test-Path $installerPath)) {
    throw "Installer was not created: $installerPath. IExpress exit code: $iexpressExitCode"
}

Get-Item $installerPath, $portableZip | Select-Object FullName, Length
exit 0
