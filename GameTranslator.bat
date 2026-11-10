@echo off
chcp 65001 >nul
title 🎮 GameTranslator v1.0 Pro - Sefflex Edition (Windows)
cd /d "%~dp0"
cls
echo ======================================================================
echo   🎮 GameTranslator v1.0 Pro - Sefflex Edition (Windows)
echo   Konsol Oyunları İçin Gerçek Zamanlı Türkçe Altyazı Sistemi
echo ======================================================================
echo.

:: 1. Önceden derlenmiş EXE kontrolü
if exist "bin\GameTranslator.exe" (
    echo [BİLGİ] Hazır GameTranslator ikilisi başlatılıyor...
    start "" "bin\GameTranslator.exe"
    exit /b 0
)

if exist "bin\GameTranslator.App.exe" (
    echo [BİLGİ] Hazır GameTranslator ikilisi başlatılıyor...
    start "" "bin\GameTranslator.App.exe"
    exit /b 0
)

:: 2. .NET SDK Kontrolü
where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo.
    echo ======================================================================
    echo   [UYARI] Bilgisayarınızda Microsoft .NET SDK bulunamadı!
    echo ======================================================================
    echo.
    echo GameTranslator Windows uygulamasının çalışması için ücretsiz
    echo .NET SDK paketinin bilgisayarınızda kurulu olması gerekmektedir.
    echo.
    echo Resmi Microsoft İndirme Adresi:
    echo https://dotnet.microsoft.com/download
    echo.
    set /p OPEN_BROWSER="Resmi Microsoft indirme sayfasını tarayıcınızda açmak ister misiniz? (E/H): "
    if /i "%OPEN_BROWSER%"=="E" (
        start https://dotnet.microsoft.com/download
    )
    echo.
    echo Kurulumu tamamladıktan sonra GameTranslator.bat dosyasını tekrar çift tıklayın.
    echo.
    pause
    exit /b 1
)

:: 3. Proje Dosyası Kontrolü ve Başlatma
if exist "windows\src\GameTranslator.App\GameTranslator.App.csproj" (
    echo [MOTOR] Windows Masaüstü Arayüzü başlatılıyor (.NET 10 / WPF / DirectML)...
    echo Lütfen ilk açılışta paketlerin yüklenmesi için birkaç saniye bekleyin...
    echo.
    dotnet run --project "windows\src\GameTranslator.App\GameTranslator.App.csproj"
    if %ERRORLEVEL% neq 0 (
        echo.
        echo [HATA] Uygulama çalışırken bir sorun oluştu. Hata kodunu yukarıdan inceleyebilirsiniz.
        pause
    )
) else (
    echo.
    echo [HATA] windows\src\GameTranslator.App\GameTranslator.App.csproj bulunamadı!
    echo Lütfen ZIP dosyasını klasöre eksiksiz çıkarttığınızdan emin olun.
    echo.
    pause
)
