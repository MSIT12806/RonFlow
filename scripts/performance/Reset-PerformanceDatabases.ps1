[CmdletBinding()]
param(
  [string]$RonFlowDatabasePath,
  [string]$RonAuthDatabasePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$workspaceRoot = Split-Path -Parent $repoRoot

if ([string]::IsNullOrWhiteSpace($RonFlowDatabasePath)) {
  $RonFlowDatabasePath = Join-Path $repoRoot 'code-backend\RonFlow.Api\App_Data\performance\ronflow.performance.db'
}

if ([string]::IsNullOrWhiteSpace($RonAuthDatabasePath)) {
  $RonAuthDatabasePath = Join-Path $workspaceRoot 'RonAuth\code-backend\RonAuth.Api\App_Data\performance\ronauth.performance.db'
}

function Remove-SqliteArtifacts {
  param(
    [Parameter(Mandatory = $true)]
    [string]$DatabasePath
  )

  $resolvedPath = [System.IO.Path]::GetFullPath($DatabasePath)
  $artifacts = @(
    $resolvedPath,
    "$resolvedPath-wal",
    "$resolvedPath-shm"
  )

  foreach ($artifact in $artifacts) {
    if (-not (Test-Path $artifact)) {
      continue
    }

    try {
      Remove-Item $artifact -Force
      Write-Host "Removed $artifact"
    }
    catch {
      throw "Failed to remove '$artifact'. Stop the running RonFlow/RonAuth Performance APIs and retry. $($_.Exception.Message)"
    }
  }
}

Write-Host 'Resetting Performance SQLite databases...'
Remove-SqliteArtifacts -DatabasePath $RonFlowDatabasePath
Remove-SqliteArtifacts -DatabasePath $RonAuthDatabasePath
Write-Host 'Performance databases reset complete.'