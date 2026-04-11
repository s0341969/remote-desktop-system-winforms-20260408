param(
    [string]$Configuration = "Release",
    [switch]$SkipBuild,
    [switch]$SkipSmokeTests,
    [switch]$SkipUiAutomation,
    [switch]$SkipZip,
    [switch]$IncludeDotnetHomeCleanup
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot "..\.."))
$publishRoot = Join-Path $repoRoot "deploy\publish"
$releaseRoot = Join-Path $repoRoot "deploy\release"
$currentReleaseRoot = Join-Path $releaseRoot "current"
$dateStamp = Get-Date -Format "yyyy-MM-dd"
$datedReleaseRoot = Join-Path $releaseRoot "RemoteDesktopSystem-$dateStamp"
$zipPath = Join-Path $releaseRoot "RemoteDesktopSystem-$dateStamp.zip"
$dotnetExe = "C:\Program Files\dotnet\dotnet.exe"
$releaseTimestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    Write-Host "=== $Name ==="
    & $Action
}

function Reset-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LiteralPath
    )

    if (Test-Path -LiteralPath $LiteralPath) {
        Remove-Item -LiteralPath $LiteralPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $LiteralPath -Force | Out-Null
}

function Get-DirectorySummary {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LiteralPath
    )

    $files = Get-ChildItem -Path $LiteralPath -Recurse -File
    $totalSizeBytes = ($files | Measure-Object -Property Length -Sum).Sum

    return [ordered]@{
        path = $LiteralPath
        fileCount = $files.Count
        totalSizeBytes = [int64]$totalSizeBytes
        totalSizeMb = [Math]::Round(($totalSizeBytes / 1MB), 2)
    }
}

Push-Location $repoRoot
try {
    Invoke-Step -Name "Clean workspace artifacts" -Action {
        $cleanArgs = @("-File", (Join-Path $scriptRoot "Clean-App.ps1"), "-IncludePublish")
        if ($IncludeDotnetHomeCleanup) {
            $cleanArgs += "-IncludeDotnetHome"
        }

        & pwsh @cleanArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Cleanup failed with exit code $LASTEXITCODE"
        }
    }

    if (-not $SkipBuild) {
        Invoke-Step -Name "Build solution" -Action {
            & $dotnetExe build (Join-Path $repoRoot "RemoteDesktopSystem.sln") -c $Configuration
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet build failed with exit code $LASTEXITCODE"
            }
        }
    }

    if (-not $SkipSmokeTests) {
        Invoke-Step -Name "Run smoke tests" -Action {
            & $dotnetExe run --project (Join-Path $repoRoot "tests\RemoteDesktop.SmokeTests\RemoteDesktop.SmokeTests.csproj") -c $Configuration
            if ($LASTEXITCODE -ne 0) {
                throw "Smoke tests failed with exit code $LASTEXITCODE"
            }
        }
    }

    if (-not $SkipUiAutomation) {
        Invoke-Step -Name "Run UI automation" -Action {
            & $dotnetExe run --project (Join-Path $repoRoot "tests\RemoteDesktop.UiAutomation\RemoteDesktop.UiAutomation.csproj") -c $Configuration
            if ($LASTEXITCODE -ne 0) {
                throw "UI automation failed with exit code $LASTEXITCODE"
            }
        }
    }

    Invoke-Step -Name "Publish Host" -Action {
        & pwsh -File (Join-Path $scriptRoot "Publish-App.ps1") `
            -ProjectRelativePath "src\RemoteDesktop.Host\RemoteDesktop.Host.csproj" `
            -OutputRelativePath "deploy\publish\Host" `
            -ExecutableName "RemoteDesktop.Host.exe" `
            -Configuration $Configuration `
            -Framework "net8.0-windows" `
            -RuntimeIdentifier "win-x64" `
            -SelfContained:$true `
            -PublishSingleFile:$true `
            -EnableCompressionInSingleFile:$true `
            -IncludeNativeLibrariesForSelfExtract:$true

        if ($LASTEXITCODE -ne 0) {
            throw "Host publish failed with exit code $LASTEXITCODE"
        }
    }

    Invoke-Step -Name "Publish Agent" -Action {
        & pwsh -File (Join-Path $scriptRoot "Publish-App.ps1") `
            -ProjectRelativePath "src\RemoteDesktop.Agent\RemoteDesktop.Agent.csproj" `
            -OutputRelativePath "deploy\publish\Agent" `
            -ExecutableName "RemoteDesktop.Agent.exe" `
            -Configuration $Configuration `
            -Framework "net8.0-windows" `
            -RuntimeIdentifier "win-x64" `
            -SelfContained:$true `
            -PublishSingleFile:$true `
            -EnableCompressionInSingleFile:$true `
            -IncludeNativeLibrariesForSelfExtract:$true

        if ($LASTEXITCODE -ne 0) {
            throw "Agent publish failed with exit code $LASTEXITCODE"
        }
    }

    Invoke-Step -Name "Publish Server" -Action {
        & pwsh -File (Join-Path $scriptRoot "Publish-App.ps1") `
            -ProjectRelativePath "src\RemoteDesktop.Server\RemoteDesktop.Server.csproj" `
            -OutputRelativePath "deploy\publish\Server" `
            -ExecutableName "RemoteDesktop.Server.exe" `
            -Configuration $Configuration `
            -Framework "net8.0" `
            -RuntimeIdentifier "win-x64" `
            -SelfContained:$true `
            -PublishSingleFile:$true `
            -EnableCompressionInSingleFile:$true `
            -IncludeNativeLibrariesForSelfExtract:$true

        if ($LASTEXITCODE -ne 0) {
            throw "Server publish failed with exit code $LASTEXITCODE"
        }
    }

    Invoke-Step -Name "Assemble release package" -Action {
        Reset-Directory -LiteralPath $currentReleaseRoot
        Reset-Directory -LiteralPath $datedReleaseRoot

        Copy-Item -LiteralPath (Join-Path $publishRoot "Host") -Destination (Join-Path $currentReleaseRoot "Host") -Recurse -Force
        Copy-Item -LiteralPath (Join-Path $publishRoot "Agent") -Destination (Join-Path $currentReleaseRoot "Agent") -Recurse -Force
        Copy-Item -LiteralPath (Join-Path $publishRoot "Server") -Destination (Join-Path $currentReleaseRoot "Server") -Recurse -Force

        New-Item -ItemType Directory -Path (Join-Path $currentReleaseRoot "Docs") -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination (Join-Path $currentReleaseRoot "Docs\README.md") -Force
        Copy-Item -LiteralPath (Join-Path $repoRoot "CHANGELOG.md") -Destination (Join-Path $currentReleaseRoot "Docs\CHANGELOG.md") -Force
        Copy-Item -LiteralPath (Join-Path $repoRoot "TODO.md") -Destination (Join-Path $currentReleaseRoot "Docs\TODO.md") -Force
        Copy-Item -LiteralPath (Join-Path $repoRoot "INSTALLATION_GUIDE.md") -Destination (Join-Path $currentReleaseRoot "Docs\INSTALLATION_GUIDE.md") -Force

        New-Item -ItemType Directory -Path (Join-Path $currentReleaseRoot "Scripts") -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $scriptRoot "Start-Host.cmd") -Destination (Join-Path $currentReleaseRoot "Scripts\Start-Host.cmd") -Force
        Copy-Item -LiteralPath (Join-Path $scriptRoot "Start-Agent.cmd") -Destination (Join-Path $currentReleaseRoot "Scripts\Start-Agent.cmd") -Force
        Copy-Item -LiteralPath (Join-Path $scriptRoot "Start-Server.cmd") -Destination (Join-Path $currentReleaseRoot "Scripts\Start-Server.cmd") -Force
        Copy-Item -LiteralPath (Join-Path $scriptRoot "Verify-Central-Release.ps1") -Destination (Join-Path $currentReleaseRoot "Scripts\Verify-Central-Release.ps1") -Force

        $gitCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitCommit)) {
            throw "Unable to resolve git commit hash for release manifest."
        }

        $manifest = [ordered]@{
            generatedAt = $releaseTimestamp
            configuration = $Configuration
            gitCommit = $gitCommit
            build = [ordered]@{
                skipped = [bool]$SkipBuild
                smokeTestsSkipped = [bool]$SkipSmokeTests
                uiAutomationSkipped = [bool]$SkipUiAutomation
                zipSkipped = [bool]$SkipZip
            }
            artifacts = [ordered]@{
                host = Get-DirectorySummary -LiteralPath (Join-Path $publishRoot "Host")
                agent = Get-DirectorySummary -LiteralPath (Join-Path $publishRoot "Agent")
                server = Get-DirectorySummary -LiteralPath (Join-Path $publishRoot "Server")
            }
            release = [ordered]@{
                currentPath = $currentReleaseRoot
                datedPath = $datedReleaseRoot
                zipPath = $(if ($SkipZip) { $null } else { $zipPath })
            }
        }

        $manifestJson = $manifest | ConvertTo-Json -Depth 8
        $summaryLines = @(
            "RemoteDesktopSystem Release Summary"
            "GeneratedAt: $releaseTimestamp"
            "Configuration: $Configuration"
            "GitCommit: $gitCommit"
            "Host: $($manifest.artifacts.host.fileCount) files, $($manifest.artifacts.host.totalSizeMb) MB"
            "Agent: $($manifest.artifacts.agent.fileCount) files, $($manifest.artifacts.agent.totalSizeMb) MB"
            "Server: $($manifest.artifacts.server.fileCount) files, $($manifest.artifacts.server.totalSizeMb) MB"
            "CurrentRelease: $currentReleaseRoot"
            "DatedRelease: $datedReleaseRoot"
            "Zip: $(if ($SkipZip) { 'SKIPPED' } else { $zipPath })"
        )

        Set-Content -LiteralPath (Join-Path $currentReleaseRoot "release-manifest.json") -Value $manifestJson -Encoding UTF8
        Set-Content -LiteralPath (Join-Path $currentReleaseRoot "release-summary.txt") -Value $summaryLines -Encoding UTF8
        Copy-Item -LiteralPath (Join-Path $currentReleaseRoot "release-manifest.json") -Destination (Join-Path $datedReleaseRoot "release-manifest.json") -Force -ErrorAction SilentlyContinue
        Copy-Item -LiteralPath (Join-Path $currentReleaseRoot "release-summary.txt") -Destination (Join-Path $datedReleaseRoot "release-summary.txt") -Force -ErrorAction SilentlyContinue

        Copy-Item -Path (Join-Path $currentReleaseRoot "*") -Destination $datedReleaseRoot -Recurse -Force

        if (-not $SkipZip) {
            if (Test-Path -LiteralPath $zipPath) {
                Remove-Item -LiteralPath $zipPath -Force
            }

            Compress-Archive -Path (Join-Path $datedReleaseRoot "*") -DestinationPath $zipPath -Force
        }
    }

    Write-Host "Deployment completed successfully."
    Write-Host "Publish root: $publishRoot"
    Write-Host "Release current: $currentReleaseRoot"
    Write-Host "Release dated: $datedReleaseRoot"
    if (-not $SkipZip) {
        Write-Host "Release zip: $zipPath"
    }
}
finally {
    Pop-Location
}
