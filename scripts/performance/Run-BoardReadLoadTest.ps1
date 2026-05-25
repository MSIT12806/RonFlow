[CmdletBinding()]
param(
  [int]$Vus = 20,
  [string]$Duration = '1m',
  [string]$RonAuthApiBaseUrl = 'http://127.0.0.1:5146/api/auth',
  [string]$RonFlowApiBaseUrl = 'http://127.0.0.1:5088/api',
  [string]$UserName = 'perf-owner',
  [string]$Password = 'Admin123!',
  [double]$PacingSeconds = 1,
  [string]$SummaryExportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-K6Command {
  $command = Get-Command k6 -ErrorAction SilentlyContinue
  if ($null -ne $command) {
    return $command.Source
  }

  $candidatePaths = @(
    (Join-Path $env:ProgramFiles 'k6\k6.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'k6\k6.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\k6\k6.exe')
  ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

  foreach ($candidatePath in $candidatePaths) {
    if (Test-Path -LiteralPath $candidatePath) {
      return $candidatePath
    }
  }

  throw 'k6 is not installed or not available on PATH.'
}

$k6Path = Resolve-K6Command

$scriptPath = Join-Path $PSScriptRoot 'k6\board-read.k6.js'
if (-not (Test-Path -LiteralPath $scriptPath)) {
  throw "Load test script not found: $scriptPath"
}

$arguments = @(
  'run',
  '--vus', $Vus,
  '--duration', $Duration,
  '--env', "RONFLOW_LOAD_TEST_RONAUTH_API_BASE_URL=$RonAuthApiBaseUrl",
  '--env', "RONFLOW_LOAD_TEST_RONFLOW_API_BASE_URL=$RonFlowApiBaseUrl",
  '--env', "RONFLOW_LOAD_TEST_USER_NAME=$UserName",
  '--env', "RONFLOW_LOAD_TEST_PASSWORD=$Password",
  '--env', "RONFLOW_LOAD_TEST_PACING_SECONDS=$PacingSeconds"
)

if (-not [string]::IsNullOrWhiteSpace($SummaryExportPath)) {
  $arguments += @('--summary-export', $SummaryExportPath)
}

$arguments += $scriptPath

& $k6Path @arguments