[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$OutputPath = "artifacts\plugin"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $repositoryRoot ".env"
$projectPath = Join-Path $repositoryRoot "src\YmmSourceControl.Plugin\YmmSourceControl.Plugin.csproj"

if (-not (Test-Path -LiteralPath $environmentFile -PathType Leaf)) {
    throw "`.env` が見つかりません。`.env.example` をコピーして YMM4_ROOT または YMM4_PATH を設定してください。"
}

$environmentValues = @{}
foreach ($line in Get-Content -LiteralPath $environmentFile) {
    $trimmedLine = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmedLine) -or $trimmedLine.StartsWith("#")) {
        continue
    }

    if ($trimmedLine -notmatch "^\s*([^=\s]+)\s*=\s*(.*?)\s*$") {
        throw ".env の形式が不正です: $line"
    }

    $name = $Matches[1]
    $value = $Matches[2].Trim()
    if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'")))) {
        $value = $value.Substring(1, $value.Length - 2)
    }

    $environmentValues[$name] = $value
}

$ymm4Path = $environmentValues["YMM4_ROOT"]
if ([string]::IsNullOrWhiteSpace($ymm4Path)) {
    $ymm4Path = $environmentValues["YMM4_PATH"]
}
if ([string]::IsNullOrWhiteSpace($ymm4Path)) {
    throw ".env に YMM4_ROOT または YMM4_PATH を設定してください。"
}

$ymm4Path = [Environment]::ExpandEnvironmentVariables($ymm4Path)
if (-not [IO.Path]::IsPathRooted($ymm4Path)) {
    $ymm4Path = Join-Path $repositoryRoot $ymm4Path
}
$ymm4Path = [IO.Path]::GetFullPath($ymm4Path)
$pluginAssemblyPath = Join-Path $ymm4Path "YukkuriMovieMaker.Plugin.dll"
if (-not (Test-Path -LiteralPath $pluginAssemblyPath -PathType Leaf)) {
    throw "YMM4 の Plugin DLL が見つかりません: $pluginAssemblyPath"
}

if (-not [IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $repositoryRoot $OutputPath
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)

Write-Host "YMM4: $ymm4Path"
Write-Host "構成: $Configuration"
Write-Host "出力先: $OutputPath"

& dotnet build $projectPath `
    -c $Configuration `
    "-p:Ymm4Path=$ymm4Path" `
    -o $OutputPath

if ($LASTEXITCODE -ne 0) {
    throw "Plugin のビルドに失敗しました。終了コード: $LASTEXITCODE"
}

Write-Host "Plugin のビルドに成功しました。" -ForegroundColor Green
