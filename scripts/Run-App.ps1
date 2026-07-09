<#
.SYNOPSIS
WslContainersDesktop.App をビルドして起動するだけのスクリプト。

.DESCRIPTION
ローカルで動作確認したいときに使う、単純なbuild+runラッパー。
実行中マシンのアーキテクチャ(ARM64/x64)を自動判定し、対応する RuntimeIdentifier を
明示的に指定してビルドしたのち、winapp run で起動する(.exe を直接実行しない)。

高度なビルドオプション(アナライザー注入、MSBuild自動検出等)が必要な場合は、
winui-dev-workflow skill の BuildAndRun.ps1 を利用すること(本スクリプトでは重複させない)。

.EXAMPLE
.\scripts\Run-App.ps1
.\scripts\Run-App.ps1 -Configuration Release
#>

[CmdletBinding()]
param(
    [string]$ProjectPath = "$PSScriptRoot\..\src\WslContainersDesktop.App\WslContainersDesktop.App.csproj",
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$platform = if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { "ARM64" } else { "x64" }
$rid = "win-$($platform.ToLowerInvariant())"
$resolvedProject = (Resolve-Path $ProjectPath).Path
$projectDir = Split-Path $resolvedProject -Parent

Write-Host "--> Building $resolvedProject (Platform: $platform, Configuration: $Configuration, RID: $rid)" -ForegroundColor Cyan

& dotnet build $resolvedProject -c $Configuration -p:Platform=$platform -p:RuntimeIdentifier=$rid
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED (exit code $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "BUILD SUCCEEDED" -ForegroundColor Green

$winapp = Get-Command winapp -ErrorAction SilentlyContinue
if (-not $winapp) {
    Write-Host "WARNING: winapp CLI が見つかりません。起動をスキップします。" -ForegroundColor Yellow
    exit 0
}

$binDir = Join-Path $projectDir "bin\$platform\$Configuration"
$tfmDir = Get-ChildItem $binDir -Directory -ErrorAction Stop |
    Where-Object { $_.Name -match "^net\d" } |
    Sort-Object Name -Descending |
    Select-Object -First 1
if (-not $tfmDir) {
    throw "ビルド出力フォルダが見つかりません: $binDir"
}

$outputDir = Join-Path $tfmDir.FullName $rid
if (-not (Test-Path $outputDir)) {
    $outputDir = $tfmDir.FullName
}

Write-Host "--> Launching app: winapp run $outputDir --debug-output" -ForegroundColor Cyan
& winapp run $outputDir --debug-output
