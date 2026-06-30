[CmdletBinding()]
param(
  [string]$TaskName = 'RonFlowLocalhostDeploy',
  [string]$TaskPath = '\RonFlow\',
  [string]$RunnerScriptPath,
  [string]$PwshPath,
  [string]$LogDirectory = (Join-Path $env:LOCALAPPDATA 'RonFlow\localhost-deploy'),
  [switch]$NoSelfElevate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$scriptPath = [System.IO.Path]::GetFullPath($PSCommandPath)

New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null
$installLogPath = Join-Path $LogDirectory 'install-scheduled-task.log'
$transcriptStarted = $false

try {
  Start-Transcript -Path $installLogPath -Force | Out-Null
  $transcriptStarted = $true
}
catch {
}

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

function Ensure-ScheduledTaskFolder {
  param([Parameter(Mandatory = $true)][string]$RequestedTaskPath)

  $normalizedPath = $RequestedTaskPath.Trim()
  if ([string]::IsNullOrWhiteSpace($normalizedPath) -or $normalizedPath -eq '\') {
    return
  }

  $segments = $normalizedPath.Trim('\').Split('\', [System.StringSplitOptions]::RemoveEmptyEntries)
  if ($segments.Count -eq 0) {
    return
  }

  $service = New-Object -ComObject 'Schedule.Service'
  $service.Connect()

  $currentFolder = $service.GetFolder('\')
  $currentPath = ''

  foreach ($segment in $segments) {
    $nextPath = "${currentPath}\$segment"
    try {
      $currentFolder = $service.GetFolder($nextPath)
    }
    catch {
      $currentFolder = $currentFolder.CreateFolder($segment)
    }

    $currentPath = $nextPath
  }
}

if ([string]::IsNullOrWhiteSpace($RunnerScriptPath)) {
  $RunnerScriptPath = Join-Path $PSScriptRoot 'Run-LocalhostDeployScheduledTask.ps1'
}

$RunnerScriptPath = [System.IO.Path]::GetFullPath($RunnerScriptPath)
if (-not (Test-Path -LiteralPath $RunnerScriptPath)) {
  throw "Scheduled task runner not found: $RunnerScriptPath"
}

$resolvedPwshPath = Resolve-PwshPath -RequestedPath $PwshPath

if (-not (Test-IsElevated)) {
  if ($NoSelfElevate) {
    throw 'Installing the elevated scheduled task requires an elevated PowerShell session.'
  }

  $elevatedHostPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
  $arguments = @(
    '-NoLogo',
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    $scriptPath,
    '-TaskName',
    $TaskName,
    '-TaskPath',
    $TaskPath,
    '-RunnerScriptPath',
    $RunnerScriptPath,
    '-PwshPath',
    $resolvedPwshPath,
    '-LogDirectory',
    $LogDirectory,
    '-NoSelfElevate'
  )

  Write-Host 'Requesting elevation to register the RonFlow localhost deployment scheduled task...'
  if ($transcriptStarted) {
    Stop-Transcript | Out-Null
    $transcriptStarted = $false
  }

  $process = Start-Process -FilePath $elevatedHostPath -ArgumentList $arguments -Verb RunAs -Wait -PassThru -WorkingDirectory (Split-Path -Parent $scriptPath)
  if ($process.ExitCode -ne 0) {
    throw "Elevated scheduled task installation failed with exit code $($process.ExitCode)."
  }

  Write-Host "Scheduled task installed: $TaskPath$TaskName"
  Write-Host "Install log: $installLogPath"
  if ($transcriptStarted) {
    Stop-Transcript | Out-Null
  }
  return
}

$currentUserName = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$actionArguments = @(
  '-NoLogo',
  '-NoProfile',
  '-ExecutionPolicy',
  'Bypass',
  '-File',
  "`"$RunnerScriptPath`""
) -join ' '

$action = New-ScheduledTaskAction `
  -Execute $resolvedPwshPath `
  -Argument $actionArguments `
  -WorkingDirectory (Split-Path -Parent $RunnerScriptPath)

$principal = New-ScheduledTaskPrincipal `
  -UserId $currentUserName `
  -LogonType Interactive `
  -RunLevel Highest

$settings = New-ScheduledTaskSettingsSet `
  -AllowStartIfOnBatteries `
  -DontStopIfGoingOnBatteries `
  -ExecutionTimeLimit (New-TimeSpan -Hours 2)

Ensure-ScheduledTaskFolder -RequestedTaskPath $TaskPath

Register-ScheduledTask `
  -TaskName $TaskName `
  -TaskPath $TaskPath `
  -Action $action `
  -Principal $principal `
  -Settings $settings `
  -Description 'Runs the full RonFlow localhost deployment with elevated IIS control.' `
  -Force | Out-Null

Write-Host "Scheduled task installed: $TaskPath$TaskName"
Write-Host "Runner script: $RunnerScriptPath"
Write-Host "Install log: $installLogPath"

if ($transcriptStarted) {
  Stop-Transcript | Out-Null
}
