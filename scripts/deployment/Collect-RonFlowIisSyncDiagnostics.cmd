@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
where pwsh >nul 2>&1
if errorlevel 1 (
  echo PowerShell 7 ^(pwsh^) is required. Install it, then run this script again.
  exit /b 1
)

echo Collecting read-only RonFlow IIS and SQLite-to-Git diagnostics...
echo If prompted by Windows, approve elevation. No IIS, Git, or database data is modified.
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Collect-RonFlowIisSyncDiagnostics.ps1"
exit /b %ERRORLEVEL%
