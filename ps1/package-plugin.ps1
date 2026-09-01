[CmdletBinding()]
param(
    [string]$PluginPath = "artifacts\plugin",
    [string]$OutputPath = "artifacts\release\YmmSourceControl.ymme"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot

if (-not [IO.Path]::IsPathRooted($PluginPath)) {
    $PluginPath = Join-Path $repositoryRoot $PluginPath
}
$PluginPath = [IO.Path]::GetFullPath($PluginPath)

if (-not [IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $repositoryRoot $OutputPath
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)

$requiredFiles = @(
    "YmmSourceControl.dll",
    "YmmSourceControl.Core.dll",
    "YmmSourceControl.deps.json",
    "YmmSourceControl.Core.deps.json"
)

foreach ($file in $requiredFiles) {
    $source = Join-Path $PluginPath $file
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "パッケージに必要なビルド成果物が見つかりません: $source"
    }
}

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) "YmmSourceControl-package-$([Guid]::NewGuid())"
$temporaryZip = "$OutputPath.zip"

try {
    New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null
    foreach ($file in $requiredFiles) {
        Copy-Item -LiteralPath (Join-Path $PluginPath $file) -Destination (Join-Path $temporaryDirectory $file)
    }

    if (Test-Path -LiteralPath $temporaryZip -PathType Leaf) {
        Remove-Item -LiteralPath $temporaryZip -Force
    }
    if (Test-Path -LiteralPath $OutputPath -PathType Leaf) {
        Remove-Item -LiteralPath $OutputPath -Force
    }

    Compress-Archive -Path (Join-Path $temporaryDirectory "*") -DestinationPath $temporaryZip -CompressionLevel Optimal
    Move-Item -LiteralPath $temporaryZip -Destination $OutputPath -Force
}
finally {
    if (Test-Path -LiteralPath $temporaryZip -PathType Leaf) {
        Remove-Item -LiteralPath $temporaryZip -Force
    }
    if (Test-Path -LiteralPath $temporaryDirectory -PathType Container) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}

Write-Host "YMMEパッケージを作成しました: $OutputPath" -ForegroundColor Green
