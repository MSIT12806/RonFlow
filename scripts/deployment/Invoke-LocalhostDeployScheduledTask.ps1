[CmdletBinding()]
param(
  [string]$TaskName = 'RonFlowLocalhostDeploy',
  [string]$TaskPath = '\RonFlow\',
  [string]$LogDirectory = (Join-Path $env:LOCALAPPDATA 'RonFlow\localhost-deploy'),
  [switch]$ShowLastRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$task = Get-ScheduledTask -TaskName $TaskName -TaskPath $TaskPath -ErrorAction Stop

if ($ShowLastRun) {
  $statusPath = Join-Path $LogDirectory 'last-run.json'
  if (Test-Path -LiteralPath $statusPath) {
    Get-Content -LiteralPath $statusPath
  }
  else {
    Write-Host "No deployment status has been written yet: $statusPath"
  }

  Get-ScheduledTaskInfo -InputObject $task
  return
}

Start-ScheduledTask -InputObject $task

Write-Host "Started scheduled task: $TaskPath$TaskName"
Write-Host "Last-run status: $(Join-Path $LogDirectory 'last-run.json')"
Write-Host "Latest log:       $(Join-Path $LogDirectory 'latest.log')"