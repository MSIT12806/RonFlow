[CmdletBinding()]
param(
  [ValidateSet('S', 'M', 'L', 'Custom')]
  [string]$Scale = 'S',
  [int]$ProjectCount,
  [int]$TasksPerProject,
  [int]$MembersPerProject = 0,
  [int]$PendingInvitationsPerProject = 0,
  [int]$Vus = 20,
  [string]$Duration = '1m',
  [double]$PacingSeconds = 1,
  [string]$SummaryExportPath,
  [switch]$KeepApiRunning,
  [switch]$DisableTaskStateDistribution
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$workspaceRoot = Split-Path -Parent $repoRoot
$ronAuthRepoRoot = Join-Path $workspaceRoot 'RonAuth'
$ronFlowApiProjectPath = Join-Path $repoRoot 'code-backend\RonFlow.Api'
$ronAuthApiProjectPath = Join-Path $ronAuthRepoRoot 'code-backend\RonAuth.Api'
$performanceLogsPath = Join-Path $PSScriptRoot 'logs'
$resultsPath = Join-Path $PSScriptRoot 'results'
$ronFlowStdOutLog = Join-Path $performanceLogsPath 'ronflow.performance.stdout.log'
$ronFlowStdErrLog = Join-Path $performanceLogsPath 'ronflow.performance.stderr.log'
$ronAuthStdOutLog = Join-Path $performanceLogsPath 'ronauth.performance.stdout.log'
$ronAuthStdErrLog = Join-Path $performanceLogsPath 'ronauth.performance.stderr.log'

$ronAuthApiBaseUrl = 'http://127.0.0.1:5146/api/auth'
$ronFlowApiBaseUrl = 'http://127.0.0.1:5088/api'

$startedProcesses = @()

function Write-Step {
  param([Parameter(Mandatory = $true)][string]$Message)

  Write-Host ''
  Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-DefaultResultPaths {
  param(
    [Parameter(Mandatory = $true)][string]$ScaleName,
    [Parameter(Mandatory = $true)][int]$VirtualUsers,
    [Parameter(Mandatory = $true)][string]$RunDuration
  )

  $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
  $normalizedDuration = ($RunDuration -replace '[^a-zA-Z0-9]', '_')
  $baseName = "board-read-scale-$($ScaleName.ToLowerInvariant())-vus$VirtualUsers-$normalizedDuration-$timestamp"
  return @{
    Summary = Join-Path $resultsPath ($baseName + '.json')
    Report = Join-Path $resultsPath ($baseName + '.html')
  }
}

function Get-PortProcessIds {
  param([Parameter(Mandatory = $true)][int]$Port)

  $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
  if ($null -eq $connections) {
    return @()
  }

  return $connections | Select-Object -ExpandProperty OwningProcess -Unique
}

function Stop-ProcessesOnPort {
  param([Parameter(Mandatory = $true)][int]$Port)

  $processIds = Get-PortProcessIds -Port $Port
  foreach ($processId in $processIds) {
    try {
      $process = Get-Process -Id $processId -ErrorAction Stop
      Write-Host "Stopping process on port ${Port}: $($process.ProcessName) ($processId)"
      Stop-Process -Id $processId -Force
    }
    catch {
      Write-Warning "Failed to stop process $processId on port $Port. $($_.Exception.Message)"
    }
  }
}

function Test-PortOpen {
  param(
    [Parameter(Mandatory = $true)][string]$HostName,
    [Parameter(Mandatory = $true)][int]$Port,
    [int]$TimeoutMs = 1000
  )

  $client = New-Object System.Net.Sockets.TcpClient
  try {
    $asyncResult = $client.BeginConnect($HostName, $Port, $null, $null)
    if (-not $asyncResult.AsyncWaitHandle.WaitOne($TimeoutMs)) {
      return $false
    }

    $client.EndConnect($asyncResult)
    return $true
  }
  catch {
    return $false
  }
  finally {
    $client.Dispose()
  }
}

function Wait-ForPort {
  param(
    [Parameter(Mandatory = $true)][string]$HostName,
    [Parameter(Mandatory = $true)][int]$Port,
    [int]$TimeoutSeconds = 60,
    [string]$FailureHint
  )

  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  while ((Get-Date) -lt $deadline) {
    if (Test-PortOpen -HostName $HostName -Port $Port) {
      return
    }

    [System.Threading.Thread]::Sleep(200)
  }

  if ([string]::IsNullOrWhiteSpace($FailureHint)) {
    throw "Timed out waiting for ${HostName}:${Port} to accept TCP connections."
  }

  throw "Timed out waiting for ${HostName}:${Port} to accept TCP connections. $FailureHint"
}

function Start-PerformanceApi {
  param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][int]$Port,
    [Parameter(Mandatory = $true)][string]$StdOutLogPath,
    [Parameter(Mandatory = $true)][string]$StdErrLogPath,
    [Parameter(Mandatory = $true)][string]$ServiceName
  )

  $dotnet = Get-Command dotnet -ErrorAction Stop
  $process = Start-Process -FilePath $dotnet.Source -ArgumentList @('run', '--launch-profile', 'performance') -WorkingDirectory $ProjectPath -RedirectStandardOutput $StdOutLogPath -RedirectStandardError $StdErrLogPath -PassThru

  Write-Host "Started $ServiceName (PID $($process.Id))"
  $script:startedProcesses += $process
  Wait-ForPort -HostName '127.0.0.1' -Port $Port -FailureHint "Check logs: $StdOutLogPath / $StdErrLogPath"
}

try {
  if (-not (Test-Path -LiteralPath $ronAuthApiProjectPath)) {
    throw "RonAuth performance API project path not found: $ronAuthApiProjectPath"
  }

  if (-not (Test-Path -LiteralPath $ronFlowApiProjectPath)) {
    throw "RonFlow performance API project path not found: $ronFlowApiProjectPath"
  }

  New-Item -ItemType Directory -Path $performanceLogsPath -Force | Out-Null
  New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null

  $resolvedSummaryExportPath = $SummaryExportPath
  if ([string]::IsNullOrWhiteSpace($resolvedSummaryExportPath)) {
    $defaultResultPaths = Get-DefaultResultPaths -ScaleName $Scale -VirtualUsers $Vus -RunDuration $Duration
    $resolvedSummaryExportPath = $defaultResultPaths.Summary
    $resolvedReportPath = $defaultResultPaths.Report
  }
  else {
    $resolvedReportPath = [System.IO.Path]::ChangeExtension($resolvedSummaryExportPath, '.html')
  }

  Write-Step 'Stopping existing performance API processes on reserved ports'
  Stop-ProcessesOnPort -Port 5146
  Stop-ProcessesOnPort -Port 5088

  Write-Step 'Resetting performance databases'
  & (Join-Path $PSScriptRoot 'Reset-PerformanceDatabases.ps1')

  Write-Step 'Starting RonAuth performance API'
  Start-PerformanceApi -ProjectPath $ronAuthApiProjectPath -Port 5146 -StdOutLogPath $ronAuthStdOutLog -StdErrLogPath $ronAuthStdErrLog -ServiceName 'RonAuth'

  Write-Step 'Starting RonFlow performance API'
  Start-PerformanceApi -ProjectPath $ronFlowApiProjectPath -Port 5088 -StdOutLogPath $ronFlowStdOutLog -StdErrLogPath $ronFlowStdErrLog -ServiceName 'RonFlow'

  Write-Step 'Seeding performance data'
  $seedArguments = @{
    Scale = $Scale
    MembersPerProject = $MembersPerProject
    PendingInvitationsPerProject = $PendingInvitationsPerProject
    RonAuthApiBaseUrl = $ronAuthApiBaseUrl
    RonFlowApiBaseUrl = $ronFlowApiBaseUrl
  }

  if ($PSBoundParameters.ContainsKey('ProjectCount')) {
    $seedArguments.ProjectCount = $ProjectCount
  }

  if ($PSBoundParameters.ContainsKey('TasksPerProject')) {
    $seedArguments.TasksPerProject = $TasksPerProject
  }

  if ($DisableTaskStateDistribution.IsPresent) {
    $seedArguments.DisableTaskStateDistribution = $true
  }

  & (Join-Path $PSScriptRoot 'Seed-PerformanceData.ps1') @seedArguments

  Write-Step 'Running board read load test'
  $loadTestArguments = @{
    Vus = $Vus
    Duration = $Duration
    RonAuthApiBaseUrl = $ronAuthApiBaseUrl
    RonFlowApiBaseUrl = $ronFlowApiBaseUrl
    UserName = 'perf-owner'
    Password = 'Admin123!'
    PacingSeconds = $PacingSeconds
    SummaryExportPath = $resolvedSummaryExportPath
    ReportPath = $resolvedReportPath
    ReportTitle = "RonFlow Board Read Load Test - Scale $Scale - VUs $Vus - Duration $Duration"
  }

  & (Join-Path $PSScriptRoot 'Run-BoardReadLoadTest.ps1') @loadTestArguments

  Write-Host ''
  Write-Host "Summary JSON: $resolvedSummaryExportPath" -ForegroundColor Green
  Write-Host "HTML report: $resolvedReportPath" -ForegroundColor Green
}
finally {
  if ($KeepApiRunning.IsPresent) {
    Write-Host ''
    Write-Host 'Keeping performance APIs running.' -ForegroundColor Yellow
  }

  if (-not $KeepApiRunning.IsPresent) {
    foreach ($process in $startedProcesses) {
      if ($null -eq $process) {
        continue
      }

      try {
        if (-not $process.HasExited) {
          Stop-Process -Id $process.Id -Force
          Write-Host "Stopped process $($process.Id)"
        }
      }
      catch {
        Write-Warning "Failed to stop process $($process.Id). $($_.Exception.Message)"
      }
    }
  }
}