@echo off
chcp 65001 >nul
title 🔨 GameTranslator Windows Self-Contained EXE Builder
cd /d "%~dp0"
cls
echo ======================================================================
echo   🔨 GameTranslator Windows Self-Contained Package Builder
echo   Builds a self-contained Windows EXE that runs with one click
echo ======================================================================
echo.

set "DOTNET_CMD=dotnet"
where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
        set "DOTNET_CMD=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
    ) else (
        echo.
        echo [ERROR] Microsoft .NET SDK was not found!
        echo Please download the .NET SDK from the official Microsoft site: https://dotnet.microsoft.com/download
        echo.
        pause
        exit /b 1
    )
)

echo [1/2] Building the project and packaging it into bin\ as a self-contained EXE...
"%DOTNET_CMD%" publish "windows\src\GameTranslator.App\GameTranslator.App.csproj" -c Release -r win-x64 --self-contained -o bin
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] The build failed.
    pause
    exit /b 1
)

echo [2/2] Unblocking the built files...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -Path 'bin' -Recurse | Unblock-File" >nul 2>&1

echo.
echo ======================================================================
echo   ✅ Done! The Windows package was built successfully!
echo   Executable: bin\GameTranslator.exe
echo   Double-click that file to start the app.
echo ======================================================================
echo.
pause
