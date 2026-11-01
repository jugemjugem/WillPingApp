@echo off
setlocal
title IPv4 IPv6 Network Monitor
cd /d "%~dp0"
set "monitor=%~dp0network_monitor.ps1"

if not exist "%monitor%" (
    echo ERROR: network_monitor.ps1 was not found.
    echo Keep network_monitor_start.bat and network_monitor.ps1 in the same folder.
    pause
    exit /b 1
)

echo Starting network monitor...
echo Log files will be saved in NetworkMonitorLogs.
echo Press any key in this window to stop.
echo.
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%monitor%"
set "result=%errorlevel%"
echo.
echo Monitor process ended. Check the message above if there was an error.
pause
exit /b %result%
