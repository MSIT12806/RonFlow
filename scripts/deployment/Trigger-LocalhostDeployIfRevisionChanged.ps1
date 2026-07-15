[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$RepositoryRoot,
  [string]$EventName = 'git-hook',
  [string]$DeploymentRoot = 'C:\inetpub'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
$revisionOutput = & git -C $repoRoot rev-parse HEAD 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($revisionOutput)) {
  Write-Warning "[$EventName] Could not resolve the current Git revision."
  exit 0
}

$revision = ([string]($revisionOutput | Select-Object -First 1)).Trim()
$logDirectory = Join-Path $env:LOCALAPPDATA 'RonFlow\localhost-deploy'
$statePath = Join-Path $logDirectory 'last-triggered-revision.txt'
$lockPath = Join-Path $logDirectory 'revision-trigger.lock'
$buildInfoPath = Join-Path (Join-Path $DeploymentRoot 'ronflow-web') 'build-info.json'
$invokeScriptPath = Join-Path $repoRoot 'scripts\deployment\Invoke-LocalhostDeployScheduledTask.ps1'

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

$lockStream = $null
try {
  $lockStream = [System.IO.File]::Open(
    $lockPath,
    [System.IO.FileMode]::OpenOrCreate,
    [System.IO.FileAccess]::ReadWrite,
    [System.IO.FileShare]::None)

  $lastTriggeredRevision = if (Test-Path -LiteralPath $statePath) {
    (Get-Content -LiteralPath $statePath -Raw).Trim()
  }
  else {
    ''
  }

  $deployedRevision = ''
  if (Test-Path -LiteralPath $buildInfoPath) {
    try {
      $buildInfo = Get-Content -LiteralPath $buildInfoPath -Raw | ConvertFrom-Json
      $deployedRevision = [string]$buildInfo.sourceRevision
    }
    catch {
      $deployedRevision = ''
    }
  }

  if ($deployedRevision -eq $revision) {
    Set-Content -LiteralPath $statePath -Value $revision -Encoding UTF8
    Write-Host "[$EventName] HEAD $revision is already deployed; skipping."
    exit 0
  }

  if ($lastTriggeredRevision -eq $revision) {
    Write-Host "[$EventName] HEAD $revision has already triggered deployment; skipping duplicate trigger."
    exit 0
  }

  if (-not (Test-Path -LiteralPath $invokeScriptPath)) {
    Write-Warning "[$EventName] Deployment task trigger not found: $invokeScriptPath"
    exit 0
  }

  Write-Host "[$EventName] HEAD changed to $revision; starting IIS deployment task."
  & $invokeScriptPath
  Set-Content -LiteralPath $statePath -Value $revision -Encoding UTF8
  Write-Host "[$EventName] IIS deployment task started. Check $logDirectory for completion status."
}
catch {
  Write-Warning "[$EventName] Could not start the IIS deployment task: $($_.Exception.Message)"
}
finally {
  if ($null -ne $lockStream) {
    $lockStream.Dispose()
  }
}

exit 0
