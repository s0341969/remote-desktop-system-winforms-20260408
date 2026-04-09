param(
    [switch]$IncludePublish,
    [switch]$IncludeDotnetHome
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot "..\.."))

function Remove-WorkspacePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LiteralPath
    )

    if (-not (Test-Path -LiteralPath $LiteralPath)) {
        return
    }

    $resolvedPath = (Resolve-Path -LiteralPath $LiteralPath).Path
    if ($resolvedPath -notlike "$repoRoot*") {
        throw "Refusing to remove path outside repository: $resolvedPath"
    }

    Remove-Item -LiteralPath $resolvedPath -Recurse -Force
    Write-Host "Removed: $resolvedPath"
}

$artifactDirectories = Get-ChildItem -Path $repoRoot -Directory -Recurse -Force |
    Where-Object { $_.Name -in @("bin", "obj") } |
    Select-Object -ExpandProperty FullName

foreach ($artifactDirectory in $artifactDirectories) {
    Remove-WorkspacePath -LiteralPath $artifactDirectory
}

$runtimeFiles = @(
    (Join-Path $repoRoot "audit-log.ndjson")
)

foreach ($runtimeFile in $runtimeFiles) {
    Remove-WorkspacePath -LiteralPath $runtimeFile
}

if ($IncludeDotnetHome) {
    Remove-WorkspacePath -LiteralPath (Join-Path $repoRoot ".dotnet")
}

if ($IncludePublish) {
    Remove-WorkspacePath -LiteralPath (Join-Path $repoRoot "deploy\publish\Host")
    Remove-WorkspacePath -LiteralPath (Join-Path $repoRoot "deploy\publish\Agent")
}

Write-Host "Cleanup completed successfully."
