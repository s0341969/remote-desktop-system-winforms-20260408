param(
    [string]$ServerUrl = "http://127.0.0.1:5206",
    [int]$StartupWaitSeconds = 3
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot ".."))
$serverDir = Join-Path $releaseRoot "Server"
$hostDir = Join-Path $releaseRoot "Host"
$agentDir = Join-Path $releaseRoot "Agent"
$serverExe = Join-Path $serverDir "RemoteDesktop.Server.exe"
$healthUrl = "$($ServerUrl.TrimEnd('/'))/healthz"

foreach ($requiredPath in @($hostDir, $agentDir, $serverDir, $serverExe)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required release artifact not found: $requiredPath"
    }
}

$serverProcess = $null
try {
    $serverProcess = Start-Process -FilePath $serverExe -WorkingDirectory $serverDir -PassThru
    Start-Sleep -Seconds $StartupWaitSeconds

    $health = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 10
    if ($health.status -ne "ok") {
        throw "Unexpected healthz payload: $($health | ConvertTo-Json -Compress)"
    }

    [pscustomobject]@{
        hostPath = $hostDir
        agentPath = $agentDir
        serverPath = $serverDir
        healthUrl = $healthUrl
        serverStatus = $health.status
        persistenceMode = $health.persistenceMode
        onlineDevices = $health.onlineDevices
        totalDevices = $health.totalDevices
    } | ConvertTo-Json -Compress
}
finally {
    if ($serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force
    }
}
