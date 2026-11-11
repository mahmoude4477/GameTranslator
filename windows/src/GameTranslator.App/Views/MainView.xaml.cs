// SPDX-License-Identifier: AGPL-3.0-only

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using GameTranslator.App.Controls;
using GameTranslator.App.Services;
using GameTranslator.Core;
using GameTranslator.Core.LgTv;
using GameTranslator.RemotePlay;
using GameTranslator.Shell;
using Microsoft.Win32;

namespace GameTranslator.App.Views;

/// <summary>عنصر في سجل الترجمة.</summary>
public sealed class SubtitleHistoryItem
{
    public required string Translation { get; init; }
    public required string English { get; init; }
    public required string Time { get; init; }
    public string Summary => $"[{Time}] 📺 {Translation}  |  🎮 {English}";
}

/// <summary>الشاشة الرئيسية: واجهة عصرية بلوحتين و5 تبويبات.</summary>
public partial class MainView : UserControl
{
    private readonly AppController controller;
    private readonly DispatcherTimer connectionWatch = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly ObservableCollection<SubtitleHistoryItem> history = new();
    private bool loading = true;
    private string selectedColor = "#FFFFFF";
    private bool isMutePc = true;
    private string selectedPreset = "BottomCenter";

    public MainView(AppController controller)
    {
        this.controller = controller;
        DataContext = controller;
        InitializeComponent();

        HistoryListBox.ItemsSource = history;
        controller.PropertyChanged += OnChanged;
        connectionWatch.Tick += async (_, _) => await controller.CheckConnectionsAsync();

        Loaded += (_, _) =>
        {
            connectionWatch.Start();
            LoadSettings();
            UpdateOcrBox();
            UpdateTvPreview();
        };

        Unloaded += (_, _) =>
        {
            controller.PropertyChanged -= OnChanged;
            connectionWatch.Stop();
        };

        Refresh();
        _ = controller.CheckForUpdateAsync();
    }

    private void LoadSettings()
    {
        loading = true;
        var s = controller.Settings;

        // المنزلقات والإعدادات
        TextSizeSlider.Value = Math.Clamp(s.Style.FontSize, TextSizeSlider.Minimum, TextSizeSlider.Maximum);
        TextSizeLabel.Text = $"{Math.Round(TextSizeSlider.Value)} px";

        BackgroundSlider.Value = Math.Clamp(s.Style.Background, BackgroundSlider.Minimum, BackgroundSlider.Maximum);
        BackgroundLabel.Text = $"{Math.Round(BackgroundSlider.Value)}%";

        BottomPercentSlider.Value = Math.Clamp(s.Style.BottomPercent, BottomPercentSlider.Minimum, BottomPercentSlider.Maximum);
        BottomPercentLabel.Text = $"{Math.Round(BottomPercentSlider.Value)}%";

        // اللون
        selectedColor = s.Style.Color ?? "#FFFFFF";
        UpdateColorSelection();

        // منطقة الاقتصاص (CropRegion)
        CropTopSlider.Value = s.Region.Top;
        CropBottomSlider.Value = s.Region.Bottom;
        CropLeftSlider.Value = s.Region.Left;
        CropRightSlider.Value = s.Region.Right;

        CropTopLabel.Text = $"{Math.Round(s.Region.Top * 100)}%";
        CropBottomLabel.Text = $"{Math.Round(s.Region.Bottom * 100)}%";
        CropLeftLabel.Text = $"{Math.Round(s.Region.Left * 100)}%";
        CropRightLabel.Text = $"{Math.Round(s.Region.Right * 100)}%";

        // مربّعات الاختيار
        BoldCheck.IsChecked = s.Style.Bold;
        MutePcCheck.IsChecked = isMutePc;
        UpdatePcSesiButton();

        // قائمة موضع الترجمة
        foreach (ComboBoxItem item in PlacementBox.Items)
        {
            if (string.Equals((string)item.Tag, s.Style.Placement.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                PlacementBox.SelectedItem = item;
                break;
            }
        }

        // قائمة اللغة
        foreach (ComboBoxItem item in LanguageBox.Items)
        {
            if (string.Equals((string)item.Tag, s.SourceLanguage, StringComparison.OrdinalIgnoreCase))
            {
                LanguageBox.SelectedItem = item;
                break;
            }
        }

        ObsText.Text = $"OBS overlay: http://localhost:{controller.ServerPort}/obs";
        loading = false;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppController.IsRunning) 
            or nameof(AppController.TranslatorStatus) 
            or nameof(AppController.ModelInstalled)
            or nameof(AppController.Console)
            or nameof(AppController.Tv)
            or nameof(AppController.TvClients)
            or nameof(AppController.Ps5StatusText)
            or nameof(AppController.TvStatusText))
        {
            Refresh();
        }

        if (e.PropertyName == nameof(AppController.LastFrame) && controller.LastFrame != null)
        {
            UpdateLiveFrame(controller.LastFrame);
        }

        if (e.PropertyName == nameof(AppController.LastTranslation) && !string.IsNullOrWhiteSpace(controller.LastTranslation))
        {
            MonitorTranslationText.Text = controller.LastTranslation;
            MonitorEnglishText.Text = string.IsNullOrWhiteSpace(controller.LastSource) ? "Subtitles active" : controller.LastSource;

            // أضف إلى قائمة السجل
            history.Insert(0, new SubtitleHistoryItem
            {
                Translation = controller.LastTranslation,
                English = controller.LastSource ?? string.Empty,
                Time = DateTime.Now.ToString("HH:mm:ss")
            });
            if (history.Count > 100) history.RemoveAt(history.Count - 1);
        }
    }

    private void Refresh()
    {
        var running = controller.IsRunning;
        Toggle.Background = new SolidColorBrush(
            running ? Color.FromRgb(220, 38, 38) : Color.FromRgb(10, 132, 255));
        ToggleGlyph.Text = running ? Glyphs.Stop : Glyphs.Play;
        ToggleText.Text = running ? "أوقف الترجمة" : "ابدأ الترجمة";

        bool hasFrame = LiveStreamImage?.Source != null;
        bool isStreaming = running && hasFrame;

        StreamThumbnailDot.Fill = new SolidColorBrush(isStreaming
            ? Color.FromRgb(16, 185, 129)
            : (running ? Color.FromRgb(245, 158, 11) : Color.FromRgb(156, 163, 175)));
        StreamThumbnailBadge.Text = isStreaming ? "1080p مباشر" : (running ? "جارٍ الاتصال" : "في الانتظار");
        StreamThumbnailBadge.Foreground = new SolidColorBrush(isStreaming ? Color.FromRgb(56, 189, 248) : Color.FromArgb(140, 255, 255, 255));
        ThumbnailStatusText.Text = isStreaming ? "بث PS5 المباشر يعمل" : (running ? "في انتظار بث PS5..." : "في انتظار بث PS5");
        LiveBadgeText.Text = isStreaming ? "مباشر" : (running ? "في الانتظار" : "متوقف");

        // سطر حالة PS5
        if (Ps5StatusDot != null && Ps5StatusLabel != null)
        {
            var paired = controller.Console != null;
            Ps5StatusDot.Fill = new SolidColorBrush(paired ? Color.FromRgb(16, 185, 129) : Color.FromRgb(156, 163, 175));
            Ps5StatusLabel.Text = paired ? $"مقترن ({controller.Console!.Host})" : "غير مقترن";
        }

        // سطر حالة التلفزيون
        if (TvStatusDot != null && TvStatusLabel != null)
        {
            var tvReady = controller.TvClients > 0 || controller.Tv != null;
            TvStatusDot.Fill = new SolidColorBrush(tvReady ? Color.FromRgb(16, 185, 129) : Color.FromRgb(245, 158, 11));
            TvStatusLabel.Text = controller.TvClients > 0
                ? $"الأجهزة المتصلة: {controller.TvClients} (جاهز)"
                : (controller.Tv != null ? $"{controller.Tv.Name} (جاهز)" : "في انتظار تطبيق التلفزيون");
        }

        if (!running)
        {
            if (LiveStreamImage != null) LiveStreamImage.Source = null;
            if (ThumbnailStreamImage != null) ThumbnailStreamImage.Source = null;
            if (StreamPlaceholder != null)
            {
                StreamPlaceholder.Visibility = Visibility.Visible;
                if (StreamPlaceholderTitle != null) StreamPlaceholderTitle.Text = "لم يبدأ بث PS5 المباشر";
                if (StreamPlaceholderSubtitle != null) StreamPlaceholderSubtitle.Text = "لمشاهدة شاشة PS5 مباشرة اضغط «▶ ابدأ الترجمة» في القائمة اليسرى.";
                if (BtnLaunchChiaki != null) BtnLaunchChiaki.Visibility = Visibility.Collapsed;
            }
            if (ThumbnailPlaceholder != null) ThumbnailPlaceholder.Visibility = Visibility.Visible;
        }
        else
        {
            if (LiveStreamImage?.Source == null && StreamPlaceholder != null)
            {
                StreamPlaceholder.Visibility = Visibility.Visible;
                if (StreamPlaceholderTitle != null) StreamPlaceholderTitle.Text = "في انتظار بث PS5 المباشر...";
                if (StreamPlaceholderSubtitle != null) StreamPlaceholderSubtitle.Text = "اتصل بجهاز PS5 عبر Chiaki أو PS Remote Play.\nعند اتصال جهاز الألعاب ستظهر الصورة هنا تلقائيًا.";
                if (BtnLaunchChiaki != null) BtnLaunchChiaki.Visibility = Visibility.Visible;
            }
        }
    }

    private void UpdateLiveFrame(GameTranslator.Core.Frame frame)
    {
        try
        {
            var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(
                frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null, frame.Pixels, frame.Stride);
            bitmap.Freeze();
            if (LiveStreamImage != null) LiveStreamImage.Source = bitmap;
            if (ThumbnailStreamImage != null) ThumbnailStreamImage.Source = bitmap;
            if (StreamPlaceholder != null) StreamPlaceholder.Visibility = Visibility.Collapsed;
            if (ThumbnailPlaceholder != null) ThumbnailPlaceholder.Visibility = Visibility.Collapsed;

            if (StreamThumbnailDot != null) StreamThumbnailDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            if (StreamThumbnailBadge != null)
            {
                StreamThumbnailBadge.Text = "1080p مباشر";
                StreamThumbnailBadge.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            }
            if (ThumbnailStatusText != null) ThumbnailStatusText.Text = "بث PS5 المباشر يعمل";
            if (LiveBadgeText != null) LiveBadgeText.Text = "مباشر";
        }
        catch { }
    }

    private async void OnToggle(object sender, RoutedEventArgs e)
    {
        if (controller.IsRunning)
        {
            controller.Stop();
        }
        else
        {
            if (controller.Console == null || controller.Tv == null)
            {
                // إذا لم يُقترن جهاز الألعاب عبر المعالج بعد فعرّف اتصال PS5 الافتراضي
                if (controller.Console == null)
                {
                    var host = "192.168.1.109";
                    var nick = ChiakiLauncher.GetRegisteredNickname(host) ?? "PS5";
                    controller.SetRegisteredConsole(new RegisteredConsole(host, nick, controller.AccountId ?? System.Text.Encoding.ASCII.GetBytes("GTPS5ACC"), new byte[16]));
                }

                // إذا لم يُختر تلفزيون بعد ففعّل وضع العرض الافتراضي على الكمبيوتر و OBS
                if (controller.Tv == null)
                {
                    try
                    {
                        await controller.PairAsync(new LgTv("PC & OBS", "127.0.0.1", TvBrand.AndroidTv));
                    }
                    catch { }
                }
            }
            await controller.StartAsync();
        }
    }

    // --- إدارة التبويبات ---

    private void OnTabChanged(object sender, RoutedEventArgs e)
    {
        if (PanelOcrArea == null || PanelDisplay == null || PanelModels == null || PanelSystem == null || PanelGuide == null) return;

        PanelOcrArea.Visibility = TabOcrArea.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelDisplay.Visibility = TabDisplay.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelModels.Visibility = TabModels.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelSystem.Visibility = TabSystem.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelGuide.Visibility = TabGuide.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

        if (TabOcrArea.IsChecked == true) UpdateOcrBox();
        if (TabDisplay.IsChecked == true) UpdateTvPreview();
    }

    private void OnOcrShortcut(object sender, RoutedEventArgs e)
    {
        TabOcrArea.IsChecked = true;
    }

    private void OnSettingsButton(object sender, RoutedEventArgs e)
    {
        TabSystem.IsChecked = true;
    }

    // --- التبويب 1: منطقة قراءة اللعبة والقوالب ---

    private void OnPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;
        selectedPreset = tag;

        loading = true;
        switch (tag)
        {
            case "BottomCenter":
                CropTopSlider.Value = 0.72;
                CropBottomSlider.Value = 0.85;
                CropLeftSlider.Value = 0.16;
                CropRightSlider.Value = 0.84;
                ShortcutPresetText.Text = "أسفل الوسط (قياسي)";
                break;
            case "BottomWide":
                CropTopSlider.Value = 0.65;
                CropBottomSlider.Value = 0.94;
                CropLeftSlider.Value = 0.08;
                CropRightSlider.Value = 0.92;
                ShortcutPresetText.Text = "شريط سفلي عريض";
                break;
            case "FullBottom":
                CropTopSlider.Value = 0.50;
                CropBottomSlider.Value = 0.98;
                CropLeftSlider.Value = 0.02;
                CropRightSlider.Value = 0.98;
                ShortcutPresetText.Text = "النصف السفلي كله";
                break;
            case "TopBanner":
                CropTopSlider.Value = 0.03;
                CropBottomSlider.Value = 0.22;
                CropLeftSlider.Value = 0.08;
                CropRightSlider.Value = 0.92;
                ShortcutPresetText.Text = "المنطقة العليا (المهام)";
                break;
            case "Custom":
                ShortcutPresetText.Text = "مربّع مخصّص";
                break;
        }

        controller.Settings = controller.Settings with
        {
            Region = new CropRegion(CropTopSlider.Value, CropBottomSlider.Value, CropLeftSlider.Value, CropRightSlider.Value)
        };
        UpdateOcrBox();
        loading = false;
    }

    private void OnCropSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (loading) return;
        selectedPreset = "Custom";
        ShortcutPresetText.Text = "مربّع مخصّص";

        controller.Settings = controller.Settings with
        {
            Region = new CropRegion(CropTopSlider.Value, CropBottomSlider.Value, CropLeftSlider.Value, CropRightSlider.Value)
        };
        UpdateOcrBox();
    }

    private void OnOcrCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateOcrBox();
    }

    private void UpdateOcrBox()
    {
        if (OcrCanvas == null || OcrBoxBorder == null) return;
        var w = OcrCanvas.ActualWidth;
        var h = OcrCanvas.ActualHeight;
        if (w <= 10 || h <= 10)
        {
            w = 520;
            h = 230;
        }

        var top = CropTopSlider.Value;
        var bottom = CropBottomSlider.Value;
        var left = CropLeftSlider.Value;
        var right = CropRightSlider.Value;

        var boxX = left * w;
        var boxY = top * h;
        var boxW = Math.Max(40, (right - left) * w);
        var boxH = Math.Max(20, (bottom - top) * h);

        Canvas.SetLeft(OcrBoxBorder, boxX);
        Canvas.SetTop(OcrBoxBorder, boxY);
        OcrBoxBorder.Width = boxW;
        OcrBoxBorder.Height = boxH;

        CropTopLabel.Text = $"{Math.Round(top * 100)}%";
        CropBottomLabel.Text = $"{Math.Round(bottom * 100)}%";
        CropLeftLabel.Text = $"{Math.Round(left * 100)}%";
        CropRightLabel.Text = $"{Math.Round(right * 100)}%";
    }

    private void OnLaunchChiakiManual(object sender, RoutedEventArgs e)
    {
        try
        {
            var host = controller.Console?.Host;
            var name = controller.Console?.Name;
            if (!string.IsNullOrWhiteSpace(host))
            {
                ChiakiLauncher.SyncConfig(host);
            }
            if (!ChiakiLauncher.LaunchOrBringToFront(host, name))
            {
                MessageBox.Show(
                    "لم يُعثر على تطبيق Chiaki. تأكد من وجود الملف 'chiaki/chiaki.exe' في مجلد التطبيق.",
                    "لم يُعثر على Chiaki",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"تعذّر تشغيل Chiaki: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnRefreshCapture(object sender, RoutedEventArgs e)
    {
        try
        {
            var hWnd = WindowCaptureService.FindTargetWindow(out var title);
            if (hWnd != IntPtr.Zero)
            {
                var frame = WindowCaptureService.Capture(hWnd);
                if (frame != null)
                {
                    controller.FeedFrame(frame);
                    UpdateLiveFrame(frame);
                    return;
                }
            }

            MessageBox.Show(
                "لم يُعثر على نافذة Chiaki أو PlayStation Remote Play، أو أنها لا تعرض صورة بعد.\n\n" +
                "تأكد من فتح تطبيق Chiaki أو PS Remote Play على الكمبيوتر ومن الاتصال بجهاز الألعاب.\n" +
                "تلميح: يمكنك تشغيل Chiaki فورًا بالنقر على زر «🎮 افتح Chiaki».",
                "لم يُعثر على نافذة PS5",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"خطأ في البحث عن النافذة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // --- التبويب 2: مظهر التلفزيون والألوان ---

    private void OnColorWhite(object sender, RoutedEventArgs e) => SelectColor("#FFFFFF");
    private void OnColorYellow(object sender, RoutedEventArgs e) => SelectColor("#FFE600");
    private void OnColorCyan(object sender, RoutedEventArgs e) => SelectColor("#38BDF8");
    private void OnColorGreen(object sender, RoutedEventArgs e) => SelectColor("#10B981");
    private void OnColorOrange(object sender, RoutedEventArgs e) => SelectColor("#FF9F0A");

    private void SelectColor(string hex)
    {
        selectedColor = hex;
        UpdateColorSelection();

        controller.Settings = controller.Settings with
        {
            Style = controller.Settings.Style with { Color = hex }
        };
        controller.SyncStyle();
        UpdateTvPreview();
    }

    private void UpdateColorSelection()
    {
        ColorWhiteBorder.BorderBrush = selectedColor == "#FFFFFF" ? new SolidColorBrush(Color.FromRgb(10, 132, 255)) : Brushes.Transparent;
        ColorYellowBorder.BorderBrush = selectedColor == "#FFE600" ? new SolidColorBrush(Color.FromRgb(10, 132, 255)) : Brushes.Transparent;
        ColorCyanBorder.BorderBrush = selectedColor == "#38BDF8" ? new SolidColorBrush(Color.FromRgb(10, 132, 255)) : Brushes.Transparent;
        ColorGreenBorder.BorderBrush = selectedColor == "#10B981" ? new SolidColorBrush(Color.FromRgb(10, 132, 255)) : Brushes.Transparent;
        ColorOrangeBorder.BorderBrush = selectedColor == "#FF9F0A" ? new SolidColorBrush(Color.FromRgb(10, 132, 255)) : Brushes.Transparent;
    }

    private void OnTextSize(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (loading) return;
        var val = (int)Math.Round(e.NewValue);
        TextSizeLabel.Text = $"{val} px";
        controller.Settings = controller.Settings with { Style = controller.Settings.Style with { FontSize = val } };
        controller.SyncStyle();
        UpdateTvPreview();
    }

    private void OnBackground(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (loading) return;
        var val = (int)Math.Round(e.NewValue);
        BackgroundLabel.Text = $"{val}%";
        controller.Settings = controller.Settings with { Style = controller.Settings.Style with { Background = val } };
        controller.SyncStyle();
        UpdateTvPreview();
    }

    private void OnBottomPercent(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (loading) return;
        var val = (int)Math.Round(e.NewValue);
        BottomPercentLabel.Text = $"{val}%";
        controller.Settings = controller.Settings with { Style = controller.Settings.Style with { BottomPercent = val } };
        controller.SyncStyle();
        UpdateTvPreview();
    }

    private void OnPlacement(object sender, SelectionChangedEventArgs e)
    {
        if (loading || PlacementBox.SelectedItem is not ComboBoxItem item) return;
        if (Enum.TryParse<SubtitlePlacement>((string)item.Tag, true, out var placement))
        {
            controller.Settings = controller.Settings with { Style = controller.Settings.Style with { Placement = placement } };
            controller.SyncStyle();
            UpdateTvPreview();
        }
    }

    private void OnLanguage(object sender, SelectionChangedEventArgs e)
    {
        if (loading || LanguageBox.SelectedItem is not ComboBoxItem item) return;
        var lang = (string)item.Tag;
        controller.Settings = controller.Settings with { SourceLanguage = lang };
    }

    private void OnBold(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        controller.Settings = controller.Settings with { Style = controller.Settings.Style with { Bold = BoldCheck.IsChecked == true } };
        controller.SyncStyle();
        UpdateTvPreview();
    }

    private void OnMutePc(object sender, RoutedEventArgs e)
    {
        isMutePc = MutePcCheck.IsChecked == true;
        UpdatePcSesiButton();
    }

    private void OnPcSesiToggle(object sender, RoutedEventArgs e)
    {
        isMutePc = !isMutePc;
        MutePcCheck.IsChecked = isMutePc;
        UpdatePcSesiButton();
    }

    private void UpdatePcSesiButton()
    {
        if (PcSesiButton == null) return;
        PcSesiButton.Content = isMutePc ? "صامت (Muted)" : "مفعّل (الصوت يعمل)";
        var color = isMutePc ? Color.FromRgb(239, 68, 68) : Color.FromRgb(16, 185, 129);
        PcSesiButton.Foreground = new SolidColorBrush(color);
        PcSesiButton.Background = new SolidColorBrush(Color.FromArgb(31, color.R, color.G, color.B));
        PcSesiButton.BorderBrush = new SolidColorBrush(color);
    }

    private void UpdateTvPreview()
    {
        if (TvPreviewSubtitleText == null || TvPreviewSubtitleBox == null) return;
        try
        {
            var converter = new BrushConverter();
            if (converter.ConvertFromString(selectedColor) is Brush brush)
                TvPreviewSubtitleText.Foreground = brush;
        }
        catch { }

        var size = TextSizeSlider.Value;
        TvPreviewSubtitleText.FontSize = Math.Max(11, size * 0.35);
        TvPreviewSubtitleText.FontWeight = BoldCheck.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;

        var opacity = BackgroundSlider.Value / 100.0;
        var byteAlpha = (byte)Math.Clamp(Math.Round(opacity * 255), 0, 255);
        TvPreviewSubtitleBox.Background = new SolidColorBrush(Color.FromArgb(byteAlpha, 0, 0, 0));

        var bottomPx = BottomPercentSlider.Value * 1.5;
        TvPreviewSubtitleBox.Margin = new Thickness(0, 0, 0, bottomPx);
    }

    private void OnTestSubtitle(object sender, RoutedEventArgs e) => controller.SendTestSubtitle();

    // --- التبويب 3: محرّكات الترجمة ---

    private void OnGeminiSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GeminiKeyInput.Password)) return;
        MessageBox.Show("Google Gemini غير مدعوم في هذا الإصدار، ولم يُحفظ المفتاح.", "غير مدعوم", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void OnDeepLSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DeepLKeyInput.Password)) return;
        await controller.SetDeepLKeyAsync(DeepLKeyInput.Password);
        controller.SendTestSubtitle();
        MessageBox.Show("تم التحقق من مفتاح DeepL API وحفظه!", "DeepL مفعّل", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // --- تصدير السجل ---

    private void OnExportTxt(object sender, RoutedEventArgs e)
    {
        if (history.Count == 0)
        {
            MessageBox.Show("لا يوجد سجل ترجمة للتصدير بعد.", "السجل فارغ", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new SaveFileDialog { Filter = "ملف نصي (*.txt)|*.txt", FileName = "GameTranslator_Subtitle_History.txt" };
        if (dlg.ShowDialog() == true)
        {
            var lines = history.Select(h => $"{h.Time}\tTV: {h.Translation}\tPS5: {h.English}");
            File.WriteAllLines(dlg.FileName, lines);
            MessageBox.Show("تم حفظ سجل الترجمة بنجاح!", "تم التصدير", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnExportJson(object sender, RoutedEventArgs e)
    {
        if (history.Count == 0)
        {
            MessageBox.Show("لا يوجد سجل ترجمة للتصدير بعد.", "السجل فارغ", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new SaveFileDialog { Filter = "ملف JSON (*.json)|*.json", FileName = "GameTranslator_Subtitle_History.json" };
        if (dlg.ShowDialog() == true)
        {
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dlg.FileName, json);
            MessageBox.Show("تم حفظ سجل الترجمة بصيغة JSON!", "تم التصدير", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // --- الإجراءات العامة والشريط السفلي ---

    private void OnCopyObs(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText($"http://localhost:{controller.ServerPort}/obs");
        MessageBox.Show("تم نسخ عنوان OBS إلى الحافظة:\nhttp://localhost:" + controller.ServerPort + "/obs", "تم النسخ", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnAbout(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private void OnResetAll(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("هل تريد إعادة ضبط إعدادات GameTranslator؟", "إعادة ضبط الإعدادات",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            controller.ResetSettings();
            LoadSettings();
            UpdateOcrBox();
            UpdateTvPreview();
        }
    }

    private void OnReinstall(object sender, RoutedEventArgs e)
    {
        controller.Step = OnboardingStep.Welcome;
    }

    private void OnDone(object sender, RoutedEventArgs e)
    {
        var win = Window.GetWindow(this);
        if (win != null) win.WindowState = WindowState.Minimized;
    }

    private void OnHyperlink(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
    }
}
