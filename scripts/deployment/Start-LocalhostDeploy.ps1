[CmdletBinding()]
param(
  [string]$DeployScriptPath,
  [string]$PwshPath,
  [string]$LogDirectory = (Join-Path $env:LOCALAPPDATA 'RonFlow\localhost-deploy'),
  [string[]]$DeployArguments = @('-EnsureIisApplications', '-StopIisHosting', '-SkipFrontendInstall'),
  [string]$InvocationStatePath,
  [switch]$NoSelfElevate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$scriptPath = [System.IO.Path]::GetFullPath($PSCommandPath)

function Test-IsElevated {
  $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = [Security.Principal.WindowsPrincipal]::new($identity)
  return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Resolve-PwshPath {
  param([string]$RequestedPath)

  if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
    $resolvedPath = [System.IO.Path]::GetFullPath($RequestedPath)
    if (-not (Test-Path -LiteralPath $resolvedPath)) {
      throw "Requested PowerShell 7 path not found: $resolvedPath"
    }

    return $resolvedPath
  }

  $candidatePaths = @(
    (Join-Path $env:ProgramFiles 'PowerShell\7\pwsh.exe'),
    (Join-Path $env:ProgramFiles 'PowerShell\7-preview\pwsh.exe')
  )

  foreach ($candidatePath in $candidatePaths) {
    if (Test-Path -LiteralPath $candidatePath) {
      return $candidatePath
    }
  }

  $command = Get-Command pwsh -ErrorAction Stop
  return $command.Source
}

function Resolve-DeployScriptPath {
  param([string]$RequestedPath)

  if ([string]::IsNullOrWhiteSpace($RequestedPath)) {
    $RequestedPath = Join-Path $PSScriptRoot 'Deploy-LocalhostSites.ps1'
  }

  $resolvedPath = [System.IO.Path]::GetFullPath($RequestedPath)
  if (-not (Test-Path -LiteralPath $resolvedPath)) {
    throw "Deploy script not found: $resolvedPath"
  }

  return $resolvedPath
}

function Import-InvocationState {
  param([Parameter(Mandatory = $true)][string]$Path)

  if (-not (Test-Path -LiteralPath $Path)) {
    throw "Invocation state file not found: $Path"
  }

  return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

if (-not [string]::IsNullOrWhiteSpace($InvocationStatePath)) {
  $state = Import-InvocationState -Path $InvocationStatePath
  $DeployScriptPath = [string]$state.deployScriptPath
  $PwshPath = [string]$state.pwshPath
  $LogDirectory = [string]$state.logDirectory
  $DeployArguments = @($state.deployArguments)
}

New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null

if (-not (Test-IsElevated)) {
  if ($NoSelfElevate) {
    throw 'Localhost deployment requires an elevated PowerShell session.'
  }

  $statePath = Join-Path $LogDirectory ('localhost-deploy-invocation-' + [Guid]::NewGuid().ToString('N') + '.json')
  [ordered]@{
    deployScriptPath = Resolve-DeployScriptPath -RequestedPath $DeployScriptPath
    pwshPath = Resolve-PwshPath -RequestedPath $PwshPath
    logDirectory = [System.IO.Path]::GetFullPath($LogDirectory)
    deployArguments = $DeployArguments
  } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $statePath -Encoding UTF8

  $elevatedHostPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
  if (-not (Test-Path -LiteralPath $elevatedHostPath)) {
    throw "Windows PowerShell host not found: $elevatedHostPath"
  }

  $arguments = @(
    '-NoLogo',
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    "`"$scriptPath`"",
    '-InvocationStatePath',
    "`"$statePath`"",
    '-NoSelfElevate'
  )

  Write-Host 'Requesting elevation to deploy RonFlow localhost...'
  try {
    $process = Start-Process -FilePath $elevatedHostPath -ArgumentList $arguments -Verb RunAs -Wait -PassThru -WorkingDirectory (Split-Path -Parent $scriptPath)
  }
  catch {
    throw "Elevation was not completed. $($_.Exception.Message)"
  }

  $statusPath = Join-Path $LogDirectory 'self-elevated-last-run.json'
  $latestLogPath = Join-Path $LogDirectory 'latest-self-elevated.log'
  if (Test-Path -LiteralPath $statusPath) {
    Write-Host "Deployment status: $statusPath"
  }

  if (Test-Path -LiteralPath $latestLogPath) {
    Write-Host "Latest log:        $latestLogPath"
  }

  exit $process.ExitCode
}

$DeployScriptPath = Resolve-DeployScriptPath -RequestedPath $DeployScriptPath
$resolvedPwshPath = Resolve-PwshPath -RequestedPath $PwshPath

$startedAtUtc = [DateTimeOffset]::UtcNow
$timestamp = $startedAtUtc.ToString('yyyyMMdd-HHmmss')
$logPath = Join-Path $LogDirectory "localhost-deploy-self-elevated-$timestamp.log"
$latestLogPath = Join-Path $LogDirectory 'latest-self-elevated.log'
$statusPath = Join-Path $LogDirectory 'self-elevated-last-run.json'

$status = 'failed'
$exitCode = 0
$errorMessage = ''
$transcriptStarted = $false

try {
  Start-Transcript -Path $logPath -Force | Out-Null
  $transcriptStarted = $true

  Write-Host "RonFlow self-elevated localhost deployment started at $($startedAtUtc.ToString('O'))"
  Write-Host "PowerShell 7: $resolvedPwshPath"
  Write-Host "Deploy script: $DeployScriptPath"
  Write-Host "Deploy arguments: $($DeployArguments -join ' ')"

  & $resolvedPwshPath -NoLogo -NoProfile -File $DeployScriptPath @DeployArguments
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
