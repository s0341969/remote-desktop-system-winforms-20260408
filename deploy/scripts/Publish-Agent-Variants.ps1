param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot "..\.."))
$projectPath = Join-Path $repoRoot "src\RemoteDesktop.Agent\RemoteDesktop.Agent.csproj"
$publishRoot = Join-Path $repoRoot "deploy\publish\Agent"
$tempRoot = Join-Path $repoRoot "artifacts\publish-agent-variants"
$x64TempRoot = Join-Path $tempRoot "x64"
$x86TempRoot = Join-Path $tempRoot "x86"
$dotnetExe = "C:\Program Files\dotnet\dotnet.exe"
$existingSettingsPath = Join-Path $publishRoot "appsettings.json"
$existingDevelopmentSettingsPath = Join-Path $publishRoot "appsettings.Development.json"
$settingsBackupPath = Join-Path $tempRoot "appsettings.json"
$developmentSettingsBackupPath = Join-Path $tempRoot "appsettings.Development.json"

function Build-AgentVariant {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PlatformTarget,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath
    )

    if (Test-Path -LiteralPath $OutputPath) {
        Remove-Item -LiteralPath $OutputPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

    & $dotnetExe build $projectPath `
        -c $Configuration `
        -o $OutputPath `
        -p:TargetFramework=net48 `
        -p:PlatformTarget=$PlatformTarget `
        -p:Prefer32Bit=$([string]::Equals($PlatformTarget, 'x86', [System.StringComparison]::OrdinalIgnoreCase))

    if ($LASTEXITCODE -ne 0) {
        throw "Agent build failed for $PlatformTarget with exit code $LASTEXITCODE"
    }
}

function Copy-DirectoryContent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    Get-ChildItem -LiteralPath $SourcePath -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $DestinationPath $_.Name) -Force
    }
}

if (Test-Path -LiteralPath $tempRoot) {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

if (Test-Path -LiteralPath $existingSettingsPath) {
    Copy-Item -LiteralPath $existingSettingsPath -Destination $settingsBackupPath -Force
}

if (Test-Path -LiteralPath $existingDevelopmentSettingsPath) {
    Copy-Item -LiteralPath $existingDevelopmentSettingsPath -Destination $developmentSettingsBackupPath -Force
}

Build-AgentVariant -PlatformTarget "x64" -OutputPath $x64TempRoot
Build-AgentVariant -PlatformTarget "x86" -OutputPath $x86TempRoot

if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

Copy-DirectoryContent -SourcePath $x64TempRoot -DestinationPath $publishRoot

Copy-Item -LiteralPath (Join-Path $x86TempRoot "RemoteDesktop.Agent.exe") -Destination (Join-Path $publishRoot "RemoteDesktop.Agent.x86.exe") -Force

$x86ConfigPath = Join-Path $x86TempRoot "RemoteDesktop.Agent.exe.config"
if (Test-Path -LiteralPath $x86ConfigPath) {
    Copy-Item -LiteralPath $x86ConfigPath -Destination (Join-Path $publishRoot "RemoteDesktop.Agent.x86.exe.config") -Force
}

$x86PdbPath = Join-Path $x86TempRoot "RemoteDesktop.Agent.pdb"
if (Test-Path -LiteralPath $x86PdbPath) {
    Copy-Item -LiteralPath $x86PdbPath -Destination (Join-Path $publishRoot "RemoteDesktop.Agent.x86.pdb") -Force
}

if (Test-Path -LiteralPath $settingsBackupPath) {
    Copy-Item -LiteralPath $settingsBackupPath -Destination $existingSettingsPath -Force
}

if (Test-Path -LiteralPath $developmentSettingsBackupPath) {
    Copy-Item -LiteralPath $developmentSettingsBackupPath -Destination $existingDevelopmentSettingsPath -Force
}

Write-Host "Agent publish variants completed successfully."
Write-Host "x64: $(Join-Path $publishRoot 'RemoteDesktop.Agent.exe')"
Write-Host "x86: $(Join-Path $publishRoot 'RemoteDesktop.Agent.x86.exe')"
