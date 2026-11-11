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
using GameTranslator.RemotePlay;
using GameTranslator.Shell;
using GameTranslator.App.Services;

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
        currentStep = Math.Clamp(step, 1, 4);
        UpdateStepView();
    }

    private void UpdateStepView()
    {
        StepBadgeText.Text = $"Adım {currentStep} / 4";

        // Panel Görünürlükleri
        Step1Panel.Visibility = currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4Panel.Visibility = currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;

        // Butonlar
        BackButton.Visibility = currentStep == 1 ? Visibility.Hidden : Visibility.Visible;
        NextButton.Content = currentStep == 4 ? "Kurulumu Tamamla ✓" : "Devam Et ➔";

        // Kapsül Renkleri (Progress Pills)
        UpdatePill(PillBar1, PillText1, 1);
        UpdatePill(PillBar2, PillText2, 2);
        UpdatePill(PillBar3, PillText3, 3);
        UpdatePill(PillBar4, PillText4, 4);

        if (currentStep == 2)
        {
            if (controller.HasAccount)
            {
                UpdatePsnIdDisplay();
            }

            if (PairStatusText != null)
            {
                var savedHost = ChiakiLauncher.GetSavedHost();
                if (!string.IsNullOrWhiteSpace(savedHost) && Ps5HostInput != null)
                {
                    Ps5HostInput.Text = savedHost;
                }

                var host = controller.Console?.Host ?? Ps5HostInput?.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(host) && ChiakiLauncher.IsRegistered(host))
                {
                    PairStatusText.Text = "✓ PS5 Konsolu Chiaki'de zaten eşleşmiş durumda! Doğrudan sonraki adıma geçebilirsiniz.";
                    PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                }
            }
        }
        else if (currentStep == 3)
        {
            if (controller.Tv != null && controller.Tv.Brand == TvBrand.AndroidTv && AndroidHostInput != null)
            {
                if (!string.IsNullOrWhiteSpace(controller.Tv.Host) && controller.Tv.Host != "192.168.1.50")
                {
                    AndroidHostInput.Text = controller.Tv.Host;
                }
            }
        }
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
        if (currentStep == 3)
        {
            SaveCurrentTvPlatform();
        }

        if (currentStep < 4)
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

    // ==========================================
    // Adım 2: PSN Girişi
    // ==========================================
    private void OnCopyPsnId(object sender, RoutedEventArgs e)
    {
        if (PsnIdDisplay != null && !string.IsNullOrWhiteSpace(PsnIdDisplay.Text))
        {
            Clipboard.SetText(PsnIdDisplay.Text.Trim());
            MessageBox.Show("Chiaki PSN Hesap Kodu panoya kopyalandı!\n\nChiaki'de konsolu kaydederken 'PSN Account-ID' kutusuna yapıştırabilirsiniz.", "Kopyalandı", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void UpdatePsnIdDisplay()
    {
        var b64 = controller.AccountIdBase64;
        if (!string.IsNullOrWhiteSpace(b64) && PsnIdDisplay != null && PsnIdCard != null)
        {
            PsnIdDisplay.Text = b64;
            PsnIdCard.Visibility = Visibility.Visible;
            if (PsnStatusText != null)
            {
                PsnStatusText.Text = "✓ PlayStation Network hesabı başarıyla bağlandı!";
                PsnStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
        }
    }

    private async void OnOpenSonyLogin(object sender, RoutedEventArgs e)
    {
        try
        {
            var redirect = PsnLoginWindow.Show(Window.GetWindow(this));
            if (redirect != null)
            {
                await controller.CompletePsnLoginAsync(redirect);
                UpdatePsnIdDisplay();
            }
        }
        catch (Exception ex)
        {
            // WebView2 yoksa varsayılan tarayıcıda aç
            try
            {
                var url = PsnAccount.AuthorizeUrl.AbsoluteUri;
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
            PsnStatusText.Text = $"Giriş bilgisi: {ex.Message}";
            PsnStatusText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }
    }

    private void OnOpenExternalSonyBrowser(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(PsnAccount.AuthorizeUrl.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Tarayıcı açılamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string? ExtractCode(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        input = input.Trim();

        // 1. Doğrudan Uri olarak parse etmeyi dene
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            var code = PsnAccount.CodeFromRedirect(uri);
            if (!string.IsNullOrEmpty(code)) return code;

            if (!string.IsNullOrEmpty(uri.Query))
            {
                var queryMatch = System.Text.RegularExpressions.Regex.Match(uri.Query, @"[?&]code=([^&]+)");
                if (queryMatch.Success) return Uri.UnescapeDataString(queryMatch.Groups[1].Value);
            }
        }

        // 2. Metin içinde code= ara
        var match = System.Text.RegularExpressions.Regex.Match(input, @"(?:^|[?&])code=([^&\s]+)");
        if (match.Success)
        {
            return Uri.UnescapeDataString(match.Groups[1].Value);
        }

        // 3. Doğrudan ham kod verilmişse (v3.xxx veya tek parça alfanümerik)
        if (!input.Contains(' ') && !input.Contains('/') && input.Length >= 8)
        {
            return input;
        }

        return null;
    }

    private async void OnVerifyPsnCode(object sender, RoutedEventArgs e)
    {
        var raw = PsnCodeInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            MessageBox.Show("Lütfen Sony yönlendirme linkini veya kodunu yapıştırın.", "Kod Gerekli", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var code = ExtractCode(raw);
        if (string.IsNullOrWhiteSpace(code))
        {
            PsnStatusText.Text = "❌ Geçersiz link veya kod formatı. Lütfen adresteki 'code=' kısmını kontrol edin.";
            PsnStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            return;
        }

        PsnVerifyButton.IsEnabled = false;
        PsnStatusText.Text = "⏳ Kod Sony ile doğrulanıyor...";
        PsnStatusText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));

        try
        {
            await controller.CompletePsnLoginWithCodeAsync(code);
            UpdatePsnIdDisplay();
            PsnCodeInput.Text = "";
        }
        catch (Exception ex)
        {
            PsnStatusText.Text = $"❌ Doğrulama hatası: {ex.Message}";
            PsnStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
        finally
        {
            PsnVerifyButton.IsEnabled = true;
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
        var host = Ps5HostInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            PairStatusText.Text = "Lütfen PS5 konsolunuzun IP adresini girin.";
            PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            return;
        }

        var pin = Ps5PinInput.Text.Trim();
        var digits = new string([.. pin.Where(char.IsDigit)]);
        if (digits.Length != 8)
        {
            PairStatusText.Text = "Lütfen PS5 ekranındaki 8 haneli PIN kodunu eksiksiz girin (PS5 › Ayarlar › Sistem › Uzaktan Oynatma › Cihaz Eşleştir).";
            PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            return;
        }

        PairButton.IsEnabled = false;
        PairButton.Content = "⏳ PS5 ile Eşleştiriliyor...";
        PairStatusText.Text = $"{host} adresindeki PS5 ile şifreli bağlantı kuruluyor ve PIN doğrulanıyor...";
        PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));

        try
        {
            ChiakiLauncher.SyncConfig(host);
            await controller.RegisterAsync(host, digits);

            PairStatusText.Text = $"✓ PS5 Konsolu Başarıyla Eşleşti! ({host})";
            PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            PairButton.Content = "✓ Eşleşti!";

            await Task.Delay(800);
            SetStep(3);
        }
        catch (Exception ex)
        {
            PairButton.IsEnabled = true;
            PairButton.Content = "🎮 PS5 Konsolu ile Eşleştir ➔";
            PairStatusText.Text = $"Eşleştirme başarısız: {ex.Message}";
            PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
    }

    private void OnOpenChiakiPairingGui(object sender, RoutedEventArgs e)
    {
        var host = string.IsNullOrWhiteSpace(Ps5HostInput?.Text) ? "192.168.1.109" : Ps5HostInput.Text.Trim();
        var accountId = controller.AccountId;
        ChiakiLauncher.SyncConfig(host, accountId);
        ChiakiLauncher.LaunchPairingGui(host);
        var registered = new RegisteredConsole(host, "PS5", accountId ?? System.Text.Encoding.ASCII.GetBytes("GTPS5ACC"), new byte[16]);
        controller.SetRegisteredConsole(registered);
        PairStatusText.Text = "Chiaki eşleştirme arayüzü açıldı. PS5 konsolunuza tıklayarak yukarıdaki PSN kodunu ve TV'deki PIN'i girip eşleştirebilirsiniz.";
        PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
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

    private async void OnScanAndroidTv(object sender, RoutedEventArgs e)
    {
        ScanAndroidTvButton.IsEnabled = false;
        AndroidInstallStatus.Visibility = Visibility.Visible;
        AndroidInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        AndroidInstallStatus.Text = "🔍 Yerel ağdaki Android TV'ler taranıyor (port 5555)...";
        try
        {
            var found = await controller.Services.ScanAndroidTvsAsync();
            var first = found.FirstOrDefault();
            if (first != null)
            {
                AndroidHostInput.Text = first;
                AndroidInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                AndroidInstallStatus.Text = $"✓ Android TV bulundu: {first}";
            }
            else
            {
                AndroidInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                AndroidInstallStatus.Text = "Ağda ADB'si açık Android TV bulunamadı. Lütfen TV IP adresini kontrol edip elle yazın.";
            }
        }
        catch (Exception ex)
        {
            AndroidInstallStatus.Text = $"Tarama bilgisi: {ex.Message}";
        }
        finally
        {
            ScanAndroidTvButton.IsEnabled = true;
        }
    }

    private async void OnInstallAndroidApp(object sender, RoutedEventArgs e)
    {
        var host = AndroidHostInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            MessageBox.Show("Lütfen Android TV'nizin IP adresini girin.", "IP Adresi Gerekli", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AndroidInstallButton.IsEnabled = false;
        AndroidInstallStatus.Visibility = Visibility.Visible;
        AndroidInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        AndroidInstallStatus.Text = $"⏳ {host} adresindeki Android TV'ye ADB ile bağlanılıyor...";

        try
        {
            var version = await controller.InstallAndroidTvAppAsync(host, ev =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (ev.Kind == TvInstallEventKind.ApproveOnTv)
                    {
                        AndroidInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                        AndroidInstallStatus.Text = "⚠️ LÜTFEN TV'YE BAKIN: Ekrandaki 'Ağ / USB Hata Ayıklamaya izin verilsin mi?' sorusuna kumandayla 'Her zaman izin ver' deyip Tamam'a basın!";
                    }
                    else
                    {
                        AndroidInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                        AndroidInstallStatus.Text = "⏳ APK paketi TV'ye yükleniyor ve kuruluyor...";
                    }
                });
            });

            AndroidInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            AndroidInstallStatus.Text = $"✓ Başarılı! GameTranslator v{version} TV'ye kuruldu ve açıldı.";
            TvStatusFeedback.Text = $"✓ Android TV ({host}) bağlandı ve uygulama hazır.";
            TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }
        catch (Exception ex)
        {
            AndroidInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            AndroidInstallStatus.Text = $"❌ Kurulum hatası: {ex.Message}\n(TV'de Geliştirici Seçenekleri > Ağ/USB Hata Ayıklama (ADB) açık ve TV ekranı açık olmalıdır)";
        }
        finally
        {
            AndroidInstallButton.IsEnabled = true;
        }
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
