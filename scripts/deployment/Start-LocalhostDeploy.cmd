@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%Start-LocalhostDeploy.ps1"
set "LOG_DIR=%LOCALAPPDATA%\RonFlow\localhost-deploy"

if not exist "%PS_SCRIPT%" (
  echo Deploy entry script not found:
  echo %PS_SCRIPT%
  echo.
  pause
  exit /b 1
)

where pwsh.exe >nul 2>nul
if errorlevel 1 (
  echo PowerShell 7 ^(pwsh.exe^) was not found on PATH.
  echo Install PowerShell 7, then run this file again.
  echo.
  echo Expected command:
  echo pwsh -NoLogo -NoProfile -File "%PS_SCRIPT%"
  echo.
  pause
  exit /b 1
)

echo Starting RonFlow localhost deployment...
echo This may show a Windows UAC prompt. Approve it to continue.
echo.

pwsh.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%"
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if "%EXIT_CODE%"=="0" (
  echo RonFlow localhost deployment completed.
) else (
  echo RonFlow localhost deployment failed or was cancelled.
  echo Exit code: %EXIT_CODE%
)

echo.
echo Latest self-elevated log:
echo %LOG_DIR%\latest-self-elevated.log
echo.
echo Last run status:
echo %LOG_DIR%\self-elevated-last-run.json
echo.
pause
exit /b %EXIT_CODE%
