// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GameTranslator.Shell;

namespace GameTranslator.App.Views;

/// <summary>Kurulum sihirbazı: Mac sürümüyle birebir aynı 5 adımlı modern kurulum arayüzü.</summary>
public partial class OnboardingView : UserControl
{
    private readonly AppController controller;
    private int currentStep = 1;

    public OnboardingView(AppController controller)
    {
        this.controller = controller;
        DataContext = controller;
        InitializeComponent();
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
        controller.Step = OnboardingStep.Done;
    }

    private void OnPill1(object sender, RoutedEventArgs e) => SetStep(1);
    private void OnPill2(object sender, RoutedEventArgs e) => SetStep(2);
    private void OnPill3(object sender, RoutedEventArgs e) => SetStep(3);
    private void OnPill4(object sender, RoutedEventArgs e) => SetStep(4);
    private void OnPill5(object sender, RoutedEventArgs e) => SetStep(5);

    // Adım 2: PSN
    private void OnOpenSonyLogin(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://ca.account.sony.com/api/authz/v3/oauth/authorize?access_type=offline&client_id=09515139-db77-4948-a192-e352e302e3aa&response_type=code&scope=psn:clientapp&redirect_uri=https://remoteplay.dl.playstation.net/remoteplay/redirect") { UseShellExecute = true });
    }

    private async void OnVerifyPsn(object sender, RoutedEventArgs e)
    {
        var input = PsnRedirectInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(input)) return;
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            try
            {
                await controller.CompletePsnLoginAsync(uri);
                PsnStatusText.Text = "✓ PlayStation Network hesabı başarıyla bağlandı!";
                PsnStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
            catch (Exception ex)
            {
                PsnStatusText.Text = $"Hata: {ex.Message}";
                PsnStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
        }
    }

    // Adım 3: PS5
    private async void OnScanPs5(object sender, RoutedEventArgs e)
    {
        PairStatusText.Text = "Yerel ağda PS5 konsolu taranıyor...";
        PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        try
        {
            var found = await controller.DiscoverConsoleAsync();
            if (found != null)
            {
                Ps5HostInput.Text = found.Host;
                PairStatusText.Text = $"✓ PS5 Bulundu: {found.Host} ({found.Name})";
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
            await controller.RegisterConsoleAsync(pin, string.IsNullOrEmpty(host) ? null : host);
            PairStatusText.Text = "✓ PS5 Konsolu Başarıyla Eşleşti!";
            PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            SetStep(4);
        }
        catch (Exception ex)
        {
            PairStatusText.Text = $"Eşleştirme uyarısı: {ex.Message}";
            PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }
    }

    // Adım 4 & 5: TV Test
    private void OnTestTv(object sender, RoutedEventArgs e)
    {
        controller.SendTestSubtitle();
        MessageBox.Show("TV'nize test altyazısı gönderildi! Ekranınızda görünüyorsa eşleştirme başarılıdır.", "Test Gönderildi", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
