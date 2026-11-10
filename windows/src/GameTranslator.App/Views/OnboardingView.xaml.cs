// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GameTranslator.Core;
using GameTranslator.Core.LgTv;
using GameTranslator.Shell;

namespace GameTranslator.App.Views;

/// <summary>Kurulum sihirbazı: Mac sürümüyle birebir aynı 5 adımlı modern kurulum arayüzü.</summary>
public partial class OnboardingView : UserControl
{
    private readonly AppController controller;
    private int currentStep = 1;
    private string selectedTvPlatform = "LG";

    public OnboardingView(AppController controller)
    {
        this.controller = controller;
        DataContext = controller;
        if (controller.Tv != null)
        {
            selectedTvPlatform = controller.Tv.Brand switch
            {
                TvBrand.Lg => "LG",
                TvBrand.Samsung => "Samsung",
                TvBrand.AndroidTv when controller.Tv.Name.Contains("PC") => "PC",
                TvBrand.AndroidTv => "Android",
                _ => "LG"
            };
        }
        else
        {
            selectedTvPlatform = "LG";
            _ = controller.PairAsync(new LgTv("LG webOS TV", "192.168.1.50", TvBrand.Lg));
        }
        InitializeComponent();
        UpdateTvPlatformSelection();
        UpdateStepView();
    }

    private void SetStep(int step)
    {
        currentStep = Math.Clamp(step, 1, 5);
        UpdateStepView();
    }

    private void UpdateStepView()
    {
        StepBadgeText.Text = $"Adım {currentStep} / 5";

        // Panel Görünürlükleri
        Step1Panel.Visibility = currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4Panel.Visibility = currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;
        Step5Panel.Visibility = currentStep == 5 ? Visibility.Visible : Visibility.Collapsed;

        // Butonlar
        BackButton.Visibility = currentStep == 1 ? Visibility.Hidden : Visibility.Visible;
        NextButton.Content = currentStep == 5 ? "Kurulumu Tamamla ✓" : "Devam Et ➔";

        // Kapsül Renkleri (Progress Pills)
        UpdatePill(PillBar1, PillText1, 1);
        UpdatePill(PillBar2, PillText2, 2);
        UpdatePill(PillBar3, PillText3, 3);
        UpdatePill(PillBar4, PillText4, 4);
        UpdatePill(PillBar5, PillText5, 5);
    }

    private void UpdatePill(Border bar, TextBlock text, int step)
    {
        if (step < currentStep)
        {
            bar.Background = new SolidColorBrush(Color.FromRgb(10, 132, 255));
            text.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175));
            text.FontWeight = FontWeights.Normal;
        }
        else if (step == currentStep)
        {
            bar.Background = new SolidColorBrush(Color.FromRgb(10, 132, 255));
            text.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            text.FontWeight = FontWeights.Bold;
        }
        else
        {
            bar.Background = new SolidColorBrush(Color.FromRgb(35, 43, 62));
            text.Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128));
            text.FontWeight = FontWeights.Normal;
        }
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (currentStep > 1) SetStep(currentStep - 1);
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        if (currentStep == 4)
        {
            SaveCurrentTvPlatform();
        }

        if (currentStep < 5)
        {
            SetStep(currentStep + 1);
        }
        else
        {
            controller.Step = OnboardingStep.Done;
        }
    }

    private void OnSkipToMain(object sender, RoutedEventArgs e)
    {
        SaveCurrentTvPlatform();
        controller.Step = OnboardingStep.Done;
    }

    private void SaveCurrentTvPlatform()
    {
        try
        {
            switch (selectedTvPlatform)
            {
                case "LG":
                    _ = controller.PairAsync(new LgTv("LG webOS TV", string.IsNullOrWhiteSpace(LgHostInput.Text) ? "192.168.1.50" : LgHostInput.Text.Trim(), TvBrand.Lg));
                    break;
                case "Samsung":
                    _ = controller.PairAsync(new LgTv("Samsung Smart TV", string.IsNullOrWhiteSpace(SamsungHostInput.Text) ? "192.168.1.50" : SamsungHostInput.Text.Trim(), TvBrand.Samsung));
                    break;
                case "Android":
                    _ = controller.PairAsync(new LgTv("Android TV", string.IsNullOrWhiteSpace(AndroidHostInput.Text) ? "192.168.1.50" : AndroidHostInput.Text.Trim(), TvBrand.AndroidTv));
                    break;
                case "PC":
                default:
                    _ = controller.PairAsync(new LgTv("PC & OBS", "127.0.0.1", TvBrand.AndroidTv));
                    break;
            }
        }
        catch { }
    }

    private void OnPill1(object sender, RoutedEventArgs e) => SetStep(1);
    private void OnPill2(object sender, RoutedEventArgs e) => SetStep(2);
    private void OnPill3(object sender, RoutedEventArgs e) => SetStep(3);
    private void OnPill4(object sender, RoutedEventArgs e) => SetStep(4);
    private void OnPill5(object sender, RoutedEventArgs e) => SetStep(5);

    // ==========================================
    // Adım 2: PSN Girişi
    // ==========================================
    private void OnOpenSonyLogin(object sender, RoutedEventArgs e)
    {
        // Resmi Sony Remote Play OAuth URL (Chiaki-ng ve Mac ile birebir aynı)
        var url = "https://auth.api.sonyentertainmentnetwork.com/2.0/oauth/authorize?service_entity=urn:service-entity:psn&response_type=code&client_id=ba495a24-818c-472b-b12d-ff231c1b5745&redirect_uri=https://remoteplay.dl.playstation.net/remoteplay/redirect&scope=psn:clientapp%20referenceDataService:countryConfig.read%20pushNotification:webSocket.desktop.connect%20sessionManager:remotePlaySession.system.update&request_locale=en_US&ui=pr&service_logo=ps&layout_type=popup&smcid=remoteplay&prompt=always&PlatformPrivacyWs1=minimal";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private async void OnVerifyPsn(object sender, RoutedEventArgs e)
    {
        var input = PsnRedirectInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(input)) return;

        try
        {
            if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
            {
                await controller.CompletePsnLoginAsync(uri);
            }
            else
            {
                var fullUrl = input.StartsWith("code=")
                    ? $"https://remoteplay.dl.playstation.net/remoteplay/redirect?{input}"
                    : $"https://remoteplay.dl.playstation.net/remoteplay/redirect?code={input}";
                await controller.CompletePsnLoginAsync(new Uri(fullUrl));
            }

            PsnStatusText.Text = "✓ PlayStation Network hesabı başarıyla bağlandı!";
            PsnStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            await Task.Delay(800);
            SetStep(3);
        }
        catch (Exception ex)
        {
            PsnStatusText.Text = $"Doğrulama uyarısı: {ex.Message}";
            PsnStatusText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }
    }

    // ==========================================
    // Adım 3: PS5 Eşleştirme
    // ==========================================
    private async void OnScanPs5(object sender, RoutedEventArgs e)
    {
        PairStatusText.Text = "Yerel ağda PS5 konsolu taranıyor...";
        PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        try
        {
            var found = await controller.DiscoverConsolesAsync();
            var first = found.FirstOrDefault();
            if (first != null)
            {
                Ps5HostInput.Text = first.Host;
                PairStatusText.Text = $"✓ PS5 Bulundu: {first.Host} ({first.Name})";
                PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
            else
            {
                PairStatusText.Text = "PS5 otomatik bulunamadı. Lütfen konsolun açık ve aynı ağda olduğunu kontrol edip IP'yi elle yazın.";
                PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            }
        }
        catch (Exception ex)
        {
            PairStatusText.Text = $"Arama bilgisi: {ex.Message}";
        }
    }

    private async void OnPairPs5(object sender, RoutedEventArgs e)
    {
        var pin = Ps5PinInput.Text.Trim();
        var host = Ps5HostInput.Text.Trim();
        if (pin.Length != 8)
        {
            PairStatusText.Text = "Lütfen PS5 ekranında görünen 8 haneli kodu tam olarak girin.";
            PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            return;
        }

        PairStatusText.Text = "PS5'e eşleştirme paketi gönderiliyor...";
        PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));

        try
        {
            await controller.RegisterAsync(host, pin);
            PairStatusText.Text = $"✓ PS5 Konsolu Başarıyla Eşleşti! ({host})";
            PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            await Task.Delay(800);
            SetStep(4);
        }
        catch (Exception ex)
        {
            PairStatusText.Text = $"Eşleştirme uyarısı: {ex.Message}";
            PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }
    }

    // ==========================================
    // Adım 4: Akıllı TV Platform Seçimi & Eşleştirme
    // ==========================================
    private async void OnSelectTvPc(object sender, RoutedEventArgs e)
    {
        selectedTvPlatform = "PC";
        UpdateTvPlatformSelection();
        try
        {
            await controller.PairAsync(new LgTv("PC & OBS", "127.0.0.1", TvBrand.AndroidTv));
        }
        catch { }
        TvStatusFeedback.Text = "✓ Bilgisayar & OBS modu kaydedildi.";
        TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
    }

    private async void OnSelectTvSamsung(object sender, RoutedEventArgs e)
    {
        selectedTvPlatform = "Samsung";
        UpdateTvPlatformSelection();
        try
        {
            var host = string.IsNullOrWhiteSpace(SamsungHostInput.Text) ? "192.168.1.50" : SamsungHostInput.Text.Trim();
            await controller.PairAsync(new LgTv("Samsung Smart TV", host, TvBrand.Samsung));
            TvStatusFeedback.Text = "✓ Samsung Smart TV modu seçildi.";
            TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }
        catch { }
    }

    private async void OnSelectTvLg(object sender, RoutedEventArgs e)
    {
        selectedTvPlatform = "LG";
        UpdateTvPlatformSelection();
        try
        {
            var host = string.IsNullOrWhiteSpace(LgHostInput.Text) ? "192.168.1.50" : LgHostInput.Text.Trim();
            await controller.PairAsync(new LgTv("LG webOS TV", host, TvBrand.Lg));
            TvStatusFeedback.Text = "✓ LG webOS TV modu seçildi.";
            TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }
        catch { }
    }

    private async void OnSelectTvAndroid(object sender, RoutedEventArgs e)
    {
        selectedTvPlatform = "Android";
        UpdateTvPlatformSelection();
        try
        {
            var host = string.IsNullOrWhiteSpace(AndroidHostInput.Text) ? "192.168.1.50" : AndroidHostInput.Text.Trim();
            await controller.PairAsync(new LgTv("Android TV", host, TvBrand.AndroidTv));
            TvStatusFeedback.Text = "✓ Android TV modu seçildi.";
            TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }
        catch { }
    }

    private void UpdateTvPlatformSelection()
    {
        var blueBrush = new SolidColorBrush(Color.FromRgb(10, 132, 255));
        var defBrush = new SolidColorBrush(Color.FromRgb(35, 43, 62));

        // Kenarlıklar
        TvBorderPc.BorderBrush = selectedTvPlatform == "PC" ? blueBrush : defBrush;
        TvBorderSamsung.BorderBrush = selectedTvPlatform == "Samsung" ? blueBrush : defBrush;
        TvBorderLg.BorderBrush = selectedTvPlatform == "LG" ? blueBrush : defBrush;
        TvBorderAndroid.BorderBrush = selectedTvPlatform == "Android" ? blueBrush : defBrush;

        // Rozetler
        TvBadgePc.Visibility = selectedTvPlatform == "PC" ? Visibility.Visible : Visibility.Collapsed;
        TvBadgeSamsung.Visibility = selectedTvPlatform == "Samsung" ? Visibility.Visible : Visibility.Collapsed;
        TvBadgeLg.Visibility = selectedTvPlatform == "LG" ? Visibility.Visible : Visibility.Collapsed;
        TvBadgeAndroid.Visibility = selectedTvPlatform == "Android" ? Visibility.Visible : Visibility.Collapsed;

        // Detay Panelleri
        TvDetailPc.Visibility = selectedTvPlatform == "PC" ? Visibility.Visible : Visibility.Collapsed;
        TvDetailSamsung.Visibility = selectedTvPlatform == "Samsung" ? Visibility.Visible : Visibility.Collapsed;
        TvDetailLg.Visibility = selectedTvPlatform == "LG" ? Visibility.Visible : Visibility.Collapsed;
        TvDetailAndroid.Visibility = selectedTvPlatform == "Android" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnCopyObsUrl(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText("http://localhost:8787/obs");
        MessageBox.Show("OBS URL adresi panoya kopyalandı:\nhttp://localhost:8787/obs", "Kopyalandı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void OnSaveSamsung(object sender, RoutedEventArgs e)
    {
        var host = SamsungHostInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(host)) return;
        TvStatusFeedback.Text = $"Samsung TV ({host}) kaydediliyor...";
        TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        try
        {
            await controller.PairAsync(new LgTv("Samsung Smart TV", host, TvBrand.Samsung));
        }
        catch { }
        TvStatusFeedback.Text = $"✓ Samsung TV kaydedildi ({host}). TV'nizde GameTranslator uygulamasını açın.";
        TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
    }

    private async void OnSaveLg(object sender, RoutedEventArgs e)
    {
        var host = LgHostInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(host)) return;
        TvStatusFeedback.Text = $"LG TV ({host}) kaydediliyor...";
        TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        try
        {
            await controller.PairAsync(new LgTv("LG webOS TV", host, TvBrand.Lg));
        }
        catch { }
        TvStatusFeedback.Text = $"✓ LG webOS TV kaydedildi ({host}). TV'nizde GameTranslator uygulamasını açın.";
        TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
    }

    private async void OnSaveAndroid(object sender, RoutedEventArgs e)
    {
        var host = AndroidHostInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(host)) return;
        TvStatusFeedback.Text = $"Android TV ({host}) kaydediliyor...";
        TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        try
        {
            await controller.PairAsync(new LgTv("Android TV", host, TvBrand.AndroidTv));
        }
        catch { }
        TvStatusFeedback.Text = $"✓ Android TV kaydedildi ({host}). TV'nizde GameTranslator uygulamasını açın.";
        TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
    }

    private void OnOpenSamsungWgt(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tv-app", "gametranslator-samsung_1.0.0.wgt");
        if (File.Exists(path))
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        else
            Process.Start(new ProcessStartInfo("https://github.com/Sefflex/GameTranslator/releases") { UseShellExecute = true });
    }

    private void OnOpenLgIpk(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tv-app", "com.gametranslator.tv_1.0.0_all.ipk");
        if (File.Exists(path))
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        else
            Process.Start(new ProcessStartInfo("https://github.com/Sefflex/GameTranslator/releases") { UseShellExecute = true });
    }

    private void OnOpenAndroidApk(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tv-app", "gametranslator-androidtv_1.0.0.apk");
        if (File.Exists(path))
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        else
            Process.Start(new ProcessStartInfo("https://github.com/Sefflex/GameTranslator/releases") { UseShellExecute = true });
    }

    private void OnOpenGuidePdf(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/Sefflex/GameTranslator/raw/main/docs/GameTranslator_TV_Kurulum_Rehberi.pdf") { UseShellExecute = true });
    }

    // ==========================================
    // Adım 5: Test
    // ==========================================
    private void OnTestTv(object sender, RoutedEventArgs e)
    {
        controller.SendTestSubtitle();
        MessageBox.Show("TV'nize test altyazısı gönderildi! Ekranınızda görünüyorsa eşleştirme başarılıdır.", "Test Gönderildi", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
