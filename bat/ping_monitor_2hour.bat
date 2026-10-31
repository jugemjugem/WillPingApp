@echo off
chcp 65001 >nul
setlocal

rem ============================================================
rem IPv4 / IPv6 ping monitor
rem Runs immediately, then every 2 hours.
rem Press any key during the wait to stop.
rem The log is saved beside this BAT with a timestamped filename.
rem ============================================================

set "INTERVAL=7200"
set "LOGDIR=%~dp0"
for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set "STAMP=%%i"
set "LOG=%LOGDIR%ping_log_%STAMP%.txt"

echo.
echo Ping monitor starting...
echo Log: "%LOG%"
echo Interval: 2 hours
echo Press any key to stop.
echo.

>>"%LOG%" echo ============================================================
>>"%LOG%" echo Ping monitor started: %date% %time%
>>"%LOG%" echo Computer: %COMPUTERNAME%
>>"%LOG%" echo Interval: 2 hours
>>"%LOG%" echo ============================================================

:LOOP
call :RUN_TESTS

echo.
echo Next test in 2 hours. Press any key to stop.
echo.

powershell -NoProfile -Command "$until=(Get-Date).AddSeconds(%INTERVAL%); while((Get-Date) -lt $until) { if([Console]::KeyAvailable) { [void][Console]::ReadKey($true); exit 1 }; Start-Sleep -Milliseconds 250 }; exit 0"
if errorlevel 1 goto STOP
goto LOOP

:RUN_TESTS
echo [%date% %time%] Running tests...
>>"%LOG%" echo.
>>"%LOG%" echo ============================================================
>>"%LOG%" echo Test time: %date% %time%
>>"%LOG%" echo ============================================================

>>"%LOG%" echo.
>>"%LOG%" echo [1] IPv4 ping 8.8.8.8
ping -4 -n 4 8.8.8.8 >>"%LOG%" 2>&1
if errorlevel 1 (
    echo [FAIL] IPv4 8.8.8.8
    >>"%LOG%" echo *** PING COMMAND FAILED: IPv4 8.8.8.8 ***
) else (
    echo [OK]   IPv4 8.8.8.8
    >>"%LOG%" echo *** PING COMMAND EXITED SUCCESSFULLY: IPv4 8.8.8.8 ***
)

>>"%LOG%" echo.
>>"%LOG%" echo [2] IPv4 ping google.com
ping -4 -n 4 google.com >>"%LOG%" 2>&1
if errorlevel 1 (
    echo [FAIL] IPv4 google.com
    >>"%LOG%" echo *** PING COMMAND FAILED: IPv4 google.com ***
) else (
    echo [OK]   IPv4 google.com
    >>"%LOG%" echo *** PING COMMAND EXITED SUCCESSFULLY: IPv4 google.com ***
)

>>"%LOG%" echo.
>>"%LOG%" echo [3] IPv6 ping google.com
ping -6 -n 4 google.com >>"%LOG%" 2>&1
if errorlevel 1 (
    echo [FAIL] IPv6 google.com
    echo *** PING COMMAND FAILED: IPv6 google.com ***
) else (
    echo [OK]   IPv6 google.com
    >>"%LOG%" echo *** PING COMMAND EXITED SUCCESSFULLY: IPv6 google.com ***
)

exit /b

:STOP
>>"%LOG%" echo.
>>"%LOG%" echo Ping monitor stopped: %date% %time%
echo.
echo Monitoring stopped.
echo Log saved to:
echo "%LOG%"
pause
endlocal
