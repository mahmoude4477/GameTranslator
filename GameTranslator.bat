@echo off
title 🎮 GameTranslator v1.0 Pro - Sefflex Edition (Windows)
cd /d "%~dp0"
echo ======================================================================
echo   🎮 GameTranslator v1.0 Pro - Sefflex Edition
echo   Windows Yerel Masaustu Uygulamasi (.NET 10 / WPF / DirectML)
echo ======================================================================

if exist "bin\GameTranslator.App.exe" (
    start "" "bin\GameTranslator.App.exe"
) else if exist "windows\src\GameTranslator.App\GameTranslator.App.csproj" (
    echo [MOTOR] Windows Masaustu Uygulamasi baslatiliyor...
    dotnet run --project "windows\src\GameTranslator.App\GameTranslator.App.csproj"
) else (
    echo [HATA] Windows projesi bulunamadi. windows\src klasorunu kontrol edin.
    pause
)