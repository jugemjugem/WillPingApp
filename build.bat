@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK was not found.
    echo Install the .NET 10 SDK and run this file again.
    echo https://dotnet.microsoft.com/download/dotnet/10.0
    pause
    exit /b 1
)

echo Building WillPingApp...
dotnet publish "%~dp0WillPingApp\WillPingApp.csproj" -c Release -o "%~dp0publish"
if errorlevel 1 (
    echo.
    echo ERROR: Build failed. Check the messages above.
    echo The .NET 10 SDK is required.
    pause
    exit /b 1
)

echo.
echo Build completed.
echo Run: %~dp0publish\WillPingApp.exe
echo The .NET 10 Desktop Runtime is required to run the app.
pause
exit /b 0
