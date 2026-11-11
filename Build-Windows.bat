@echo off
chcp 65001 >nul
title 🔨 GameTranslator Windows Bağımsız EXE Derleyici
cd /d "%~dp0"
cls
echo ======================================================================
echo   🔨 GameTranslator Windows Bağımsız Paket Derleyici
echo   Tek Tıkla Çalışabilen Bağımsız (Self-Contained) Windows EXE Üretir
echo ======================================================================
echo.

set "DOTNET_CMD=dotnet"
where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
        set "DOTNET_CMD=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
    ) else (
        echo.
        echo [HATA] Microsoft .NET SDK bulunamadı!
        echo Lütfen resmi Microsoft sitesinden .NET SDK indirin: https://dotnet.microsoft.com/download
        echo.
        pause
        exit /b 1
    )
)

echo [1/2] Proje derleniyor ve bin\ klasörüne bağımsız EXE olarak paketleniyor...
"%DOTNET_CMD%" publish "windows\src\GameTranslator.App\GameTranslator.App.csproj" -c Release -r win-x64 --self-contained -o bin
if %ERRORLEVEL% neq 0 (
    echo.
    echo [HATA] Derleme sırasında hata oluştu.
    pause
    exit /b 1
)

echo [2/2] Dosya güvenlik izinleri düzenleniyor...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -Path 'bin' -Recurse | Unblock-File" >nul 2>&1

echo.
echo ======================================================================
echo   ✅ Tebrikler! Windows paketi başarıyla derlendi!
echo   Çalıştırılabilir dosya: bin\GameTranslator.exe
echo   Artık bu dosyaya çift tıklayarak anında açabilirsiniz!
echo ======================================================================
echo.
pause
