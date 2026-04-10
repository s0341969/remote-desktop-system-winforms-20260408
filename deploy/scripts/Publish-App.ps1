param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRelativePath,

    [Parameter(Mandatory = $true)]
    [string]$OutputRelativePath,

    [Parameter(Mandatory = $true)]
    [string]$ExecutableName,

    [string]$Configuration = "Release",

    [string]$Framework = "",

    [string]$SatelliteResourceLanguages = "zh-Hant"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot "..\.."))
$projectPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ProjectRelativePath))
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRelativePath))
$dotnetExe = "C:\Program Files\dotnet\dotnet.exe"

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Project file not found: $projectPath"
}

Write-Host "Cleaning publish directory: $outputPath"
if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$publishArgs = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-o", $outputPath,
    "-p:SelfContained=false",
    "-p:UseAppHost=true",
    "-p:CopyOutputSymbolsToPublishDirectory=false",
    "-p:DebugSymbols=false",
    "-p:DebugType=None",
    "-p:SatelliteResourceLanguages=$SatelliteResourceLanguages"
)

if (-not [string]::IsNullOrWhiteSpace($Framework)) {
    $publishArgs += @("-f", $Framework)
}

Push-Location $repoRoot
try {
    & $dotnetExe @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

$executablePath = Join-Path $outputPath $ExecutableName
if (-not (Test-Path -LiteralPath $executablePath)) {
    throw "Published executable not found: $executablePath"
}

$publishedFiles = Get-ChildItem -Path $outputPath -Recurse -File
$totalSizeBytes = ($publishedFiles | Measure-Object -Property Length -Sum).Sum
$totalSizeMb = [Math]::Round(($totalSizeBytes / 1MB), 2)

Write-Host "Publish completed successfully."
Write-Host "Executable: $executablePath"
Write-Host "Published files: $($publishedFiles.Count)"
Write-Host "Total size: $totalSizeMb MB"
