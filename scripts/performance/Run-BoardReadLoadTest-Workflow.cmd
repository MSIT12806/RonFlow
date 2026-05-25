@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
powershell -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Run-BoardReadLoadTest-Workflow.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if "%EXIT_CODE%"=="0" (
  echo Board read load test workflow completed successfully.
) else (
  echo Board read load test workflow failed with exit code %EXIT_CODE%.
)

pause
exit /b %EXIT_CODE%