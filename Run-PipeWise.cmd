@echo off
setlocal
set SCRIPT=%~dp0Run-PipeWise.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%"
if errorlevel 1 (
  echo.
  echo Failed to start. Check the log next to the script: PipeWise-API.log
  echo Press any key to close...
  pause >nul
)
endlocal
