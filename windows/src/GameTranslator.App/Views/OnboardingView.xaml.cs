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

/// <summary>معالج الإعداد: واجهة إعداد عصرية متعددة الخطوات.</summary>
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
        StepBadgeText.Text = $"الخطوة {currentStep} / 4";

        // إظهار اللوحات
        Step1Panel.Visibility = currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4Panel.Visibility = currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;

        // الأزرار
        BackButton.Visibility = currentStep == 1 ? Visibility.Hidden : Visibility.Visible;
        NextButton.Content = currentStep == 4 ? "أكمل الإعداد ✓" : "متابعة ➔";

        // ألوان الكبسولات (Progress Pills)
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
                    PairStatusText.Text = "✓ جهاز PS5 مقترن مسبقًا في Chiaki! يمكنك الانتقال مباشرة إلى الخطوة التالية.";
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
    // الخطوة 2: تسجيل الدخول إلى PSN
    // ==========================================
    private void OnCopyPsnId(object sender, RoutedEventArgs e)
    {
        if (PsnIdDisplay != null && !string.IsNullOrWhiteSpace(PsnIdDisplay.Text))
        {
            Clipboard.SetText(PsnIdDisplay.Text.Trim());
            MessageBox.Show("تم نسخ رمز حساب PSN الخاص بـ Chiaki إلى الحافظة!\n\nيمكنك لصقه في خانة 'PSN Account-ID' عند تسجيل جهاز الألعاب في Chiaki.", "تم النسخ", MessageBoxButton.OK, MessageBoxImage.Information);
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
                PsnStatusText.Text = "✓ تم ربط حساب PlayStation Network بنجاح!";
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
            // إذا لم يتوفر WebView2 فافتح في المتصفح الافتراضي
            try
            {
                var url = PsnAccount.AuthorizeUrl.AbsoluteUri;
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
            PsnStatusText.Text = $"معلومات تسجيل الدخول: {ex.Message}";
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
            MessageBox.Show($"تعذّر فتح المتصفح: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string? ExtractCode(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        input = input.Trim();

        // 1. جرّب تحليله مباشرة كـ Uri
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

        // 2. ابحث عن code= داخل النص
        var match = System.Text.RegularExpressions.Regex.Match(input, @"(?:^|[?&])code=([^&\s]+)");
        if (match.Success)
        {
            return Uri.UnescapeDataString(match.Groups[1].Value);
        }

        // 3. إذا أُعطي الرمز الخام مباشرة (v3.xxx أو قطعة واحدة من أحرف وأرقام)
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
            MessageBox.Show("الصق رابط إعادة التوجيه من Sony أو الرمز.", "الرمز مطلوب", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var code = ExtractCode(raw);
        if (string.IsNullOrWhiteSpace(code))
        {
            PsnStatusText.Text = "❌ صيغة الرابط أو الرمز غير صالحة. تحقّق من الجزء 'code=' في العنوان.";
            PsnStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            return;
        }

        PsnVerifyButton.IsEnabled = false;
        PsnStatusText.Text = "⏳ جارٍ التحقق من الرمز لدى Sony...";
        PsnStatusText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));

        try
        {
            await controller.CompletePsnLoginWithCodeAsync(code);
            UpdatePsnIdDisplay();
            PsnCodeInput.Text = "";
        }
        catch (Exception ex)
        {
            PsnStatusText.Text = $"❌ خطأ في التحقق: {ex.Message}";
            PsnStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
        finally
        {
            PsnVerifyButton.IsEnabled = true;
        }
    }



    // ==========================================
    // الخطوة 3: اقتران PS5
    // ==========================================
    private async void OnScanPs5(object sender, RoutedEventArgs e)
    {
        PairStatusText.Text = "جارٍ البحث عن جهاز PS5 في الشبكة المحلية...";
        PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        try
        {
            var found = await controller.DiscoverConsolesAsync();
            var first = found.FirstOrDefault();
            if (first != null)
            {
                Ps5HostInput.Text = first.Host;
                PairStatusText.Text = $"✓ عُثر على PS5: {first.Host} ({first.Name})";
                PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
            else
            {
                PairStatusText.Text = "لم يُعثر على PS5 تلقائيًا. تأكد أن جهاز الألعاب مشغّل وعلى الشبكة نفسها ثم اكتب عنوان IP يدويًا.";
                PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            }
        }
        catch (Exception ex)
        {
            PairStatusText.Text = $"معلومات البحث: {ex.Message}";
        }
    }

    private async void OnPairPs5(object sender, RoutedEventArgs e)
    {
        var host = Ps5HostInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            PairStatusText.Text = "أدخل عنوان IP لجهاز PS5.";
            PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            return;
        }

        var pin = Ps5PinInput.Text.Trim();
        var digits = new string([.. pin.Where(char.IsDigit)]);
        if (digits.Length != 8)
        {
            PairStatusText.Text = "أدخل رمز PIN المكوّن من 8 أرقام الظاهر على شاشة PS5 كاملًا (PS5 › الإعدادات › النظام › Remote Play › Pair Device).";
            PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            return;
        }

        PairButton.IsEnabled = false;
        PairButton.Content = "⏳ جارٍ الاقتران مع PS5...";
        PairStatusText.Text = $"جارٍ إنشاء اتصال مشفّر مع PS5 على العنوان {host} والتحقق من رمز PIN...";
        PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));

        try
        {
            ChiakiLauncher.SyncConfig(host);
            await controller.RegisterAsync(host, digits);

            PairStatusText.Text = $"✓ تم اقتران جهاز PS5 بنجاح! ({host})";
            PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            PairButton.Content = "✓ تم الاقتران!";

            await Task.Delay(800);
            SetStep(3);
        }
        catch (Exception ex)
        {
            PairButton.IsEnabled = true;
            PairButton.Content = "🎮 اقرن مع جهاز PS5 ➔";
            PairStatusText.Text = $"فشل الاقتران: {ex.Message}";
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
        PairStatusText.Text = "فُتحت واجهة الاقتران في Chiaki. انقر على جهاز PS5 ثم أدخل رمز PSN أعلاه ورمز PIN الظاهر على التلفزيون لإتمام الاقتران.";
        PairStatusText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
    }

    // ==========================================
    // الخطوة 4: اختيار منصة التلفزيون الذكي والاقتران
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
        TvStatusFeedback.Text = "✓ تم حفظ وضع الكمبيوتر و OBS.";
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
            TvStatusFeedback.Text = "✓ تم اختيار وضع Samsung Smart TV.";
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
            TvStatusFeedback.Text = "✓ تم اختيار وضع LG webOS TV.";
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
            TvStatusFeedback.Text = "✓ تم اختيار وضع Android TV.";
            TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }
        catch { }
    }

    private void UpdateTvPlatformSelection()
    {
        var blueBrush = new SolidColorBrush(Color.FromRgb(10, 132, 255));
        var defBrush = new SolidColorBrush(Color.FromRgb(35, 43, 62));

        // الحدود
        TvBorderPc.BorderBrush = selectedTvPlatform == "PC" ? blueBrush : defBrush;
        TvBorderSamsung.BorderBrush = selectedTvPlatform == "Samsung" ? blueBrush : defBrush;
        TvBorderLg.BorderBrush = selectedTvPlatform == "LG" ? blueBrush : defBrush;
        TvBorderAndroid.BorderBrush = selectedTvPlatform == "Android" ? blueBrush : defBrush;

        // الشارات
        TvBadgePc.Visibility = selectedTvPlatform == "PC" ? Visibility.Visible : Visibility.Collapsed;
        TvBadgeSamsung.Visibility = selectedTvPlatform == "Samsung" ? Visibility.Visible : Visibility.Collapsed;
        TvBadgeLg.Visibility = selectedTvPlatform == "LG" ? Visibility.Visible : Visibility.Collapsed;
        TvBadgeAndroid.Visibility = selectedTvPlatform == "Android" ? Visibility.Visible : Visibility.Collapsed;

        // لوحات التفاصيل
        TvDetailPc.Visibility = selectedTvPlatform == "PC" ? Visibility.Visible : Visibility.Collapsed;
        TvDetailSamsung.Visibility = selectedTvPlatform == "Samsung" ? Visibility.Visible : Visibility.Collapsed;
        TvDetailLg.Visibility = selectedTvPlatform == "LG" ? Visibility.Visible : Visibility.Collapsed;
        TvDetailAndroid.Visibility = selectedTvPlatform == "Android" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnCopyObsUrl(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText("http://localhost:8787/obs");
        MessageBox.Show("تم نسخ عنوان OBS إلى الحافظة:\nhttp://localhost:8787/obs", "تم النسخ", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void OnSaveSamsung(object sender, RoutedEventArgs e)
    {
        var host = SamsungHostInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(host)) return;
        TvStatusFeedback.Text = $"جارٍ حفظ تلفزيون Samsung ({host})...";
        TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        try
        {
            await controller.PairAsync(new LgTv("Samsung Smart TV", host, TvBrand.Samsung));
        }
        catch { }
        TvStatusFeedback.Text = $"✓ تم حفظ تلفزيون Samsung ({host}). افتح تطبيق GameTranslator على التلفزيون.";
        TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
    }

    private async void OnSaveLg(object sender, RoutedEventArgs e)
    {
        var host = LgHostInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(host)) return;
        TvStatusFeedback.Text = $"جارٍ حفظ تلفزيون LG ({host})...";
        TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        try
        {
            await controller.PairAsync(new LgTv("LG webOS TV", host, TvBrand.Lg));
        }
        catch { }
        TvStatusFeedback.Text = $"✓ تم حفظ LG webOS TV ({host}). افتح تطبيق GameTranslator على التلفزيون.";
        TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
    }

    private async void OnScanAndroidTv(object sender, RoutedEventArgs e)
    {
        ScanAndroidTvButton.IsEnabled = false;
        AndroidInstallStatus.Visibility = Visibility.Visible;
        AndroidInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        AndroidInstallStatus.Text = "🔍 جارٍ البحث عن أجهزة Android TV في الشبكة المحلية (port 5555)...";
        try
        {
            var found = await controller.Services.ScanAndroidTvsAsync();
            var first = found.FirstOrDefault();
            if (first != null)
            {
                AndroidHostInput.Text = first;
                AndroidInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                AndroidInstallStatus.Text = $"✓ عُثر على Android TV: {first}";
            }
            else
            {
                AndroidInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                AndroidInstallStatus.Text = "لم يُعثر في الشبكة على Android TV مفعّل فيه ADB. تحقّق من عنوان IP للتلفزيون واكتبه يدويًا.";
            }
        }
        catch (Exception ex)
        {
            AndroidInstallStatus.Text = $"معلومات البحث: {ex.Message}";
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
            MessageBox.Show("أدخل عنوان IP لجهاز Android TV.", "عنوان IP مطلوب", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AndroidInstallButton.IsEnabled = false;
        AndroidInstallStatus.Visibility = Visibility.Visible;
        AndroidInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        AndroidInstallStatus.Text = $"⏳ جارٍ الاتصال عبر ADB بجهاز Android TV على العنوان {host}...";

        try
        {
            var version = await controller.InstallAndroidTvAppAsync(host, ev =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (ev.Kind == TvInstallEventKind.ApproveOnTv)
                    {
                        AndroidInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                        AndroidInstallStatus.Text = "⚠️ انظر إلى التلفزيون: عند سؤال السماح بتصحيح الأخطاء (USB / Network debugging) على الشاشة، اختر بالريموت «السماح دائمًا» (Always allow) ثم اضغط OK!";
                    }
                    else
                    {
                        AndroidInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                        AndroidInstallStatus.Text = "⏳ جارٍ إرسال حزمة APK إلى التلفزيون وتثبيتها...";
                    }
                });
            });

            AndroidInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            AndroidInstallStatus.Text = $"✓ تم بنجاح! ثُبّت GameTranslator v{version} على التلفزيون وفُتح.";
            TvStatusFeedback.Text = $"✓ تم الاتصال بجهاز Android TV ({host}) والتطبيق جاهز.";
            TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }
        catch (Exception ex)
        {
            AndroidInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            AndroidInstallStatus.Text = $"❌ خطأ في التثبيت: {ex.Message}\n(يجب أن يكون Developer options > USB / Network debugging (ADB) مفعّلًا على التلفزيون وأن تكون شاشته مشغّلة)";
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
        TvStatusFeedback.Text = $"جارٍ حفظ Android TV ({host})...";
        TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        try
        {
            await controller.PairAsync(new LgTv("Android TV", host, TvBrand.AndroidTv));
        }
        catch { }
        TvStatusFeedback.Text = $"✓ تم حفظ Android TV ({host}). افتح تطبيق GameTranslator على التلفزيون.";
        TvStatusFeedback.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
    }

    private void OnOpenSamsungWgt(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tv-app", "gametranslator-samsung_1.0.0.wgt");
        if (File.Exists(path))
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        else
            Process.Start(new ProcessStartInfo("https://github.com/mahmoude4477/GameTranslator/releases") { UseShellExecute = true });
    }

    private void OnOpenLgIpk(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tv-app", "com.gametranslator.tv_1.0.0_all.ipk");
        if (File.Exists(path))
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        else
            Process.Start(new ProcessStartInfo("https://github.com/mahmoude4477/GameTranslator/releases") { UseShellExecute = true });
    }

    private void OnOpenAndroidApk(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tv-app", "gametranslator-androidtv_1.0.0.apk");
        if (File.Exists(path))
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        else
            Process.Start(new ProcessStartInfo("https://github.com/mahmoude4477/GameTranslator/releases") { UseShellExecute = true });
    }

    private void OnOpenGuidePdf(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/mahmoude4477/GameTranslator/raw/main/docs/GameTranslator_TV_Setup_Guide.pdf") { UseShellExecute = true });
    }

    // ==========================================
    // الخطوة 5: الاختبار
    // ==========================================
    private void OnTestTv(object sender, RoutedEventArgs e)
    {
        controller.SendTestSubtitle();
        MessageBox.Show("أُرسل سطر ترجمة تجريبي إلى التلفزيون! إذا ظهر على الشاشة فالاتصال ناجح.", "أُرسل الاختبار", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
