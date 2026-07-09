param(
    [string]$Version
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "TypeClipboard\TypeClipboard.csproj"
$projectXml = [xml](Get-Content -LiteralPath $projectPath)

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = [string]($projectXml.Project.PropertyGroup.Version | Select-Object -First 1)
}

if ($Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw "Version must contain three or four numeric components, for example 0.2.2."
}

$artifactsDir = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsDir "publish"
$portableZip = Join-Path $artifactsDir "TypeClipboard-Portable-v$Version.zip"

New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version `
    -p:AssemblyVersion=$Version `
    -p:FileVersion=$Version `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir

if (Test-Path $portableZip) { Remove-Item -Force $portableZip }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $portableZip -Force

Get-Item $portableZip | Select-Object FullName, Length
