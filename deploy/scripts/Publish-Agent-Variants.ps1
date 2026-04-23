param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot "..\.."))
$publishRoot = Join-Path $repoRoot "deploy\publish\Agent"
$tempRoot = Join-Path $repoRoot "artifacts\publish-agent-variants"
$x64TempRoot = Join-Path $tempRoot "x64"
$x86TempRoot = Join-Path $tempRoot "x86"
$publishScript = Join-Path $scriptRoot "Publish-App.ps1"
$existingSettingsPath = Join-Path $publishRoot "appsettings.json"
$existingDevelopmentSettingsPath = Join-Path $publishRoot "appsettings.Development.json"
$settingsBackupPath = Join-Path $tempRoot "appsettings.json"
$developmentSettingsBackupPath = Join-Path $tempRoot "appsettings.Development.json"

function Publish-AgentVariant {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Framework,

        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier,

        [Parameter(Mandatory = $true)]
        [string]$OutputRelativePath
    )

    & pwsh -File $publishScript `
        -ProjectRelativePath "src\RemoteDesktop.Agent\RemoteDesktop.Agent.csproj" `
        -OutputRelativePath $OutputRelativePath `
        -ExecutableName "RemoteDesktop.Agent.exe" `
        -Configuration $Configuration `
        -Framework $Framework `
        -RuntimeIdentifier $RuntimeIdentifier `
        -SelfContained:$true `
        -PublishSingleFile:$true `
        -EnableCompressionInSingleFile:$true `
        -IncludeNativeLibrariesForSelfExtract:$true

    if ($LASTEXITCODE -ne 0) {
        throw "Agent publish failed for $RuntimeIdentifier with exit code $LASTEXITCODE"
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

Publish-AgentVariant -Framework "net6.0-windows" -RuntimeIdentifier "win7-x64" -OutputRelativePath "artifacts\publish-agent-variants\x64"
Publish-AgentVariant -Framework "net6.0-windows" -RuntimeIdentifier "win7-x86" -OutputRelativePath "artifacts\publish-agent-variants\x86"

if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $x64TempRoot "RemoteDesktop.Agent.exe") -Destination (Join-Path $publishRoot "RemoteDesktop.Agent.x64.exe") -Force
Copy-Item -LiteralPath (Join-Path $x86TempRoot "RemoteDesktop.Agent.exe") -Destination (Join-Path $publishRoot "RemoteDesktop.Agent.x86.exe") -Force

if (Test-Path -LiteralPath $settingsBackupPath) {
    Copy-Item -LiteralPath $settingsBackupPath -Destination $existingSettingsPath -Force
}
elseif (Test-Path -LiteralPath (Join-Path $x64TempRoot "appsettings.json")) {
    Copy-Item -LiteralPath (Join-Path $x64TempRoot "appsettings.json") -Destination $existingSettingsPath -Force
}

if (Test-Path -LiteralPath $developmentSettingsBackupPath) {
    Copy-Item -LiteralPath $developmentSettingsBackupPath -Destination $existingDevelopmentSettingsPath -Force
}
elseif (Test-Path -LiteralPath (Join-Path $x64TempRoot "appsettings.Development.json")) {
    Copy-Item -LiteralPath (Join-Path $x64TempRoot "appsettings.Development.json") -Destination $existingDevelopmentSettingsPath -Force
}

Write-Host "Agent publish variants completed successfully."
Write-Host "x64: $(Join-Path $publishRoot 'RemoteDesktop.Agent.x64.exe')"
Write-Host "x86: $(Join-Path $publishRoot 'RemoteDesktop.Agent.x86.exe')"
