@echo off
chcp 65001 >nul
title 🎮 GameTranslator v1.0 Pro - Sefflex Edition (Windows)
cd /d "%~dp0"
cls
echo ======================================================================
echo   🎮 GameTranslator v1.0 Pro - Sefflex Edition (Windows)
echo   Real-Time Arabic Subtitle System for Console Games
echo ======================================================================
echo.

:: 1. التحقق من وجود ملف EXE مبني مسبقًا
if exist "bin\GameTranslator.exe" (
    echo [INFO] Starting the prebuilt GameTranslator binary...
    start "" "bin\GameTranslator.exe"
    exit /b 0
)

if exist "bin\GameTranslator.App.exe" (
    echo [INFO] Starting the prebuilt GameTranslator binary...
    start "" "bin\GameTranslator.App.exe"
    exit /b 0
)

:: 2. التحقق من وجود .NET SDK
set "DOTNET_CMD=dotnet"
where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
        set "DOTNET_CMD=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
    ) else (
        echo.
        echo ======================================================================
        echo   [WARNING] Microsoft .NET SDK was not found on this computer!
        echo ======================================================================
        echo.
        echo The GameTranslator Windows app needs the free .NET SDK
        echo to be installed on this computer.
        echo.
        echo Official Microsoft download page:
        echo https://dotnet.microsoft.com/download
        echo.
        set /p OPEN_BROWSER="Open the official Microsoft download page in your browser? (Y/N): "
        if /i "%OPEN_BROWSER%"=="Y" (
            start https://dotnet.microsoft.com/download
        )
        echo.
        echo After installing it, double-click GameTranslator.bat again.
        echo.
        pause
        exit /b 1
    )
)

:: 3. التحقق من ملف المشروع ثم التشغيل
if exist "windows\src\GameTranslator.App\GameTranslator.App.csproj" (
    echo [ENGINE] Starting the Windows desktop app - .NET 10 / WPF...
    echo On the first run, please wait a few seconds while the packages are restored...
    echo.
    "%DOTNET_CMD%" run --project "windows\src\GameTranslator.App\GameTranslator.App.csproj"
    if %ERRORLEVEL% neq 0 (
        echo.
        echo [ERROR] The app stopped with a problem. See the error details above.
        pause
    )
) else (
    echo.
    echo [ERROR] windows\src\GameTranslator.App\GameTranslator.App.csproj was not found!
    echo Please make sure the whole ZIP file was extracted into this folder.
    echo.
    pause
)
