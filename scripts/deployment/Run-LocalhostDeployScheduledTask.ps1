[CmdletBinding()]
param(
  [string]$DeployScriptPath,
  [string]$LogDirectory = (Join-Path $env:LOCALAPPDATA 'RonFlow\localhost-deploy'),
  [string[]]$DeployArguments = @('-EnsureIisApplications', '-StopIisHosting', '-SkipFrontendInstall')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($DeployScriptPath)) {
  $DeployScriptPath = Join-Path $PSScriptRoot 'Deploy-LocalhostSites.ps1'
}

$DeployScriptPath = [System.IO.Path]::GetFullPath($DeployScriptPath)
if (-not (Test-Path -LiteralPath $DeployScriptPath)) {
  throw "Deploy script not found: $DeployScriptPath"
}

New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null

$startedAtUtc = [DateTimeOffset]::UtcNow
$timestamp = $startedAtUtc.ToString('yyyyMMdd-HHmmss')
$logPath = Join-Path $LogDirectory "localhost-deploy-$timestamp.log"
$latestLogPath = Join-Path $LogDirectory 'latest.log'
$statusPath = Join-Path $LogDirectory 'last-run.json'

$status = 'failed'
$exitCode = 0
$errorMessage = ''
$transcriptStarted = $false

try {
  Start-Transcript -Path $logPath -Force | Out-Null
  $transcriptStarted = $true

  Write-Host "RonFlow localhost deployment scheduled task started at $($startedAtUtc.ToString('O'))"
  Write-Host "Deploy script: $DeployScriptPath"
  Write-Host "Deploy arguments: $($DeployArguments -join ' ')"

  & pwsh -NoLogo -NoProfile -File $DeployScriptPath @DeployArguments
  $exitCode = if ($LASTEXITCODE -is [int]) { $LASTEXITCODE } else { 0 }

  if ($exitCode -ne 0) {
    throw "Deploy script failed with exit code $exitCode."
  }

  $status = 'succeeded'
}
catch {
  $errorMessage = $_.Exception.Message
  if ($exitCode -eq 0) {
    $exitCode = 1
  }

  Write-Error $errorMessage
}
finally {
  $finishedAtUtc = [DateTimeOffset]::UtcNow

  if ($transcriptStarted) {
    try {
      Stop-Transcript | Out-Null
    }
    catch {
    }
  }

  Copy-Item -LiteralPath $logPath -Destination $latestLogPath -Force

  [ordered]@{
    status = $status
    exitCode = $exitCode
    startedAtUtc = $startedAtUtc.ToString('O')
    finishedAtUtc = $finishedAtUtc.ToString('O')
    deployScriptPath = $DeployScriptPath
    deployArguments = $DeployArguments
    logPath = $logPath
    latestLogPath = $latestLogPath
    errorMessage = $errorMessage
  } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $statusPath -Encoding UTF8
}

exit $exitCode