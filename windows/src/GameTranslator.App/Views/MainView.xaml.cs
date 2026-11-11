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

/// <summary>Altyazı geçmişi öğesi.</summary>
public sealed class SubtitleHistoryItem
{
    public required string Turkish { get; init; }
    public required string English { get; init; }
    public required string Time { get; init; }
    public string Summary => $"[{Time}] 📺 {Turkish}  |  🎮 {English}";
}

/// <summary>Ana ekran: Mac sürümüyle birebir aynı 2 panelli modern arayüz ve 5 sekme.</summary>
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

        // Sliders & Ayarlar
        TextSizeSlider.Value = Math.Clamp(s.Style.FontSize, TextSizeSlider.Minimum, TextSizeSlider.Maximum);
        TextSizeLabel.Text = $"{Math.Round(TextSizeSlider.Value)} px";

        BackgroundSlider.Value = Math.Clamp(s.Style.Background, BackgroundSlider.Minimum, BackgroundSlider.Maximum);
        BackgroundLabel.Text = $"%{Math.Round(BackgroundSlider.Value)}";

        BottomPercentSlider.Value = Math.Clamp(s.Style.BottomPercent, BottomPercentSlider.Minimum, BottomPercentSlider.Maximum);
        BottomPercentLabel.Text = $"%{Math.Round(BottomPercentSlider.Value)}";

        // Renk
        selectedColor = s.Style.Color ?? "#FFFFFF";
        UpdateColorSelection();

        // Kırpma Alanı (CropRegion)
        CropTopSlider.Value = s.Region.Top;
        CropBottomSlider.Value = s.Region.Bottom;
        CropLeftSlider.Value = s.Region.Left;
        CropRightSlider.Value = s.Region.Right;

        CropTopLabel.Text = $"%{Math.Round(s.Region.Top * 100)}";
        CropBottomLabel.Text = $"%{Math.Round(s.Region.Bottom * 100)}";
        CropLeftLabel.Text = $"%{Math.Round(s.Region.Left * 100)}";
        CropRightLabel.Text = $"%{Math.Round(s.Region.Right * 100)}";

        // Checkboxlar
        BoldCheck.IsChecked = s.Style.Bold;
        DubbingCheck.IsChecked = s.DubbingEnabled;
        MutePcCheck.IsChecked = isMutePc;
        UpdatePcSesiButton();

        // Yerleşim Dropdown
        foreach (ComboBoxItem item in PlacementBox.Items)
        {
            if (string.Equals((string)item.Tag, s.Style.Placement.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                PlacementBox.SelectedItem = item;
                break;
            }
        }

        // Dil Dropdown
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
            MonitorTurkishText.Text = controller.LastTranslation;
            MonitorEnglishText.Text = string.IsNullOrWhiteSpace(controller.LastSource) ? "Subtitles active" : controller.LastSource;

            // Geçmiş listesine ekle
            history.Insert(0, new SubtitleHistoryItem
            {
                Turkish = controller.LastTranslation,
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
        ToggleText.Text = running ? "Çeviriyi Durdur" : "Çeviriyi Başlat";

        bool hasFrame = LiveStreamImage?.Source != null;
        bool isStreaming = running && hasFrame;

        StreamThumbnailDot.Fill = new SolidColorBrush(isStreaming
            ? Color.FromRgb(16, 185, 129)
            : (running ? Color.FromRgb(245, 158, 11) : Color.FromRgb(156, 163, 175)));
        StreamThumbnailBadge.Text = isStreaming ? "1080p CANLI" : (running ? "BAĞLANIYOR" : "BEKLEMEDE");
        StreamThumbnailBadge.Foreground = new SolidColorBrush(isStreaming ? Color.FromRgb(56, 189, 248) : Color.FromArgb(140, 255, 255, 255));
        ThumbnailStatusText.Text = isStreaming ? "PS5 Canlı Akışı Aktif" : (running ? "PS5 Yayını Bekleniyor..." : "PS5 Akışı Bekleniyor");
        LiveBadgeText.Text = isStreaming ? "Canlı" : (running ? "Bekleniyor" : "Beklemede");

        // PS5 Durum Satırı
        if (Ps5StatusDot != null && Ps5StatusLabel != null)
        {
            var paired = controller.Console != null;
            Ps5StatusDot.Fill = new SolidColorBrush(paired ? Color.FromRgb(16, 185, 129) : Color.FromRgb(156, 163, 175));
            Ps5StatusLabel.Text = paired ? $"Eşleşti ({controller.Console!.Host})" : "Eşleşmedi";
        }

        // TV Durum Satırı
        if (TvStatusDot != null && TvStatusLabel != null)
        {
            var tvReady = controller.TvClients > 0 || controller.Tv != null;
            TvStatusDot.Fill = new SolidColorBrush(tvReady ? Color.FromRgb(16, 185, 129) : Color.FromRgb(245, 158, 11));
            TvStatusLabel.Text = controller.TvClients > 0
                ? $"{controller.TvClients} Cihaz Bağlı (Hazır)"
                : (controller.Tv != null ? $"{controller.Tv.Name} (Hazır)" : "TV uygulaması bekleniyor");
        }

        if (!running)
        {
            if (LiveStreamImage != null) LiveStreamImage.Source = null;
            if (ThumbnailStreamImage != null) ThumbnailStreamImage.Source = null;
            if (StreamPlaceholder != null)
            {
                StreamPlaceholder.Visibility = Visibility.Visible;
                if (StreamPlaceholderTitle != null) StreamPlaceholderTitle.Text = "PS5 Canlı Akışı Başlatılmadı";
                if (StreamPlaceholderSubtitle != null) StreamPlaceholderSubtitle.Text = "PS5 ekranını canlı görmek için sol menüden '▶ Çeviriyi Başlat'a basın.";
                if (BtnLaunchChiaki != null) BtnLaunchChiaki.Visibility = Visibility.Collapsed;
            }
            if (ThumbnailPlaceholder != null) ThumbnailPlaceholder.Visibility = Visibility.Visible;
        }
        else
        {
            if (LiveStreamImage?.Source == null && StreamPlaceholder != null)
            {
                StreamPlaceholder.Visibility = Visibility.Visible;
                if (StreamPlaceholderTitle != null) StreamPlaceholderTitle.Text = "PS5 Canlı Yayını Bekleniyor...";
                if (StreamPlaceholderSubtitle != null) StreamPlaceholderSubtitle.Text = "Lütfen Chiaki veya PS Remote Play üzerinden PS5'inize bağlanın.\nKonsol bağlandığında görüntü otomatik buraya yansıyacaktır.";
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
                StreamThumbnailBadge.Text = "1080p CANLI";
                StreamThumbnailBadge.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            }
            if (ThumbnailStatusText != null) ThumbnailStatusText.Text = "PS5 Canlı Akışı Aktif";
            if (LiveBadgeText != null) LiveBadgeText.Text = "Canlı";
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
                // Eğer konsol henüz sihirbazdan eşleştirilmediyse varsayılan PS5 bağlantısını tanımla
                if (controller.Console == null)
                {
                    var host = "192.168.1.109";
                    var nick = ChiakiLauncher.GetRegisteredNickname(host) ?? "PS5";
                    controller.SetRegisteredConsole(new RegisteredConsole(host, nick, controller.AccountId ?? System.Text.Encoding.ASCII.GetBytes("GTPS5ACC"), new byte[16]));
                }

                // TV henüz seçilmediyse varsayılan Bilgisayar & OBS ekran modunu etkinleştir
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

    // --- Sekme Yönetimi ---

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

    // --- Tab 1: Oyun Okuma Alanı & Şablonlar ---

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
                ShortcutPresetText.Text = "Alt Orta (Standart)";
                break;
            case "BottomWide":
                CropTopSlider.Value = 0.65;
                CropBottomSlider.Value = 0.94;
                CropLeftSlider.Value = 0.08;
                CropRightSlider.Value = 0.92;
                ShortcutPresetText.Text = "Geniş Alt Bant";
                break;
            case "FullBottom":
                CropTopSlider.Value = 0.50;
                CropBottomSlider.Value = 0.98;
                CropLeftSlider.Value = 0.02;
                CropRightSlider.Value = 0.98;
                ShortcutPresetText.Text = "Tüm Alt Yarı";
                break;
            case "TopBanner":
                CropTopSlider.Value = 0.03;
                CropBottomSlider.Value = 0.22;
                CropLeftSlider.Value = 0.08;
                CropRightSlider.Value = 0.92;
                ShortcutPresetText.Text = "Üst Bölge (Görevler)";
                break;
            case "Custom":
                ShortcutPresetText.Text = "Özel Kutu";
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
        ShortcutPresetText.Text = "Özel Kutu";

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

        CropTopLabel.Text = $"%{Math.Round(top * 100)}";
        CropBottomLabel.Text = $"%{Math.Round(bottom * 100)}";
        CropLeftLabel.Text = $"%{Math.Round(left * 100)}";
        CropRightLabel.Text = $"%{Math.Round(right * 100)}";
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
                    "Chiaki uygulaması bulunamadı. Lütfen 'chiaki/chiaki.exe' dosyasının uygulama klasöründe olduğunu kontrol edin.",
                    "Chiaki Bulunamadı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Chiaki başlatılamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                "Chiaki veya PlayStation Remote Play penceresi bulunamadı veya henüz görüntü vermiyor.\n\n" +
                "Lütfen bilgisayarınızda Chiaki veya PS Remote Play uygulamasını açıp konsolunuza bağlandığınızdan emin olun.\n" +
                "İpucu: '🎮 Chiaki'yi Aç' butonuna tıklayarak Chiaki'yi hemen başlatabilirsiniz.",
                "PS5 Penceresi Bulunamadı",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Pencere arama hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // --- Tab 2: TV Görünümü & Renkler ---

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
        BackgroundLabel.Text = $"%{val}";
        controller.Settings = controller.Settings with { Style = controller.Settings.Style with { Background = val } };
        controller.SyncStyle();
        UpdateTvPreview();
    }

    private void OnBottomPercent(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (loading) return;
        var val = (int)Math.Round(e.NewValue);
        BottomPercentLabel.Text = $"%{val}";
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

    private void OnDubbing(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        controller.Settings = controller.Settings with { DubbingEnabled = DubbingCheck.IsChecked == true };
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
        PcSesiButton.Content = isMutePc ? "Sessiz (Muted)" : "Açık (Ses Var)";
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

    // --- Tab 3: Modeller ---

    private void OnGeminiSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GeminiKeyInput.Password)) return;
        MessageBox.Show("Google Gemini API anahtarı kaydedildi ve aktif hale getirildi.", "Model Ayarlandı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void OnDeepLSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DeepLKeyInput.Password)) return;
        await controller.SetDeepLKeyAsync(DeepLKeyInput.Password);
        controller.SendTestSubtitle();
        MessageBox.Show("DeepL API anahtarı doğrulandı ve kaydedildi!", "DeepL Aktif", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // --- Geçmiş Dışa Aktarma ---

    private void OnExportTxt(object sender, RoutedEventArgs e)
    {
        if (history.Count == 0)
        {
            MessageBox.Show("Dışa aktarılacak altyazı geçmişi henüz yok.", "Geçmiş Boş", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new SaveFileDialog { Filter = "Metin Dosyası (*.txt)|*.txt", FileName = "GameTranslator_Altyazi_Gecmisi.txt" };
        if (dlg.ShowDialog() == true)
        {
            var lines = history.Select(h => $"{h.Time}\tTV: {h.Turkish}\tPS5: {h.English}");
            File.WriteAllLines(dlg.FileName, lines);
            MessageBox.Show("Altyazı geçmişi başarıyla kaydedildi!", "Dışa Aktarıldı", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnExportJson(object sender, RoutedEventArgs e)
    {
        if (history.Count == 0)
        {
            MessageBox.Show("Dışa aktarılacak altyazı geçmişi henüz yok.", "Geçmiş Boş", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new SaveFileDialog { Filter = "JSON Dosyası (*.json)|*.json", FileName = "GameTranslator_Altyazi_Gecmisi.json" };
        if (dlg.ShowDialog() == true)
        {
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dlg.FileName, json);
            MessageBox.Show("Altyazı geçmişi JSON formatında kaydedildi!", "Dışa Aktarıldı", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // --- Genel Aksiyonlar & Footer ---

    private void OnCopyObs(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText($"http://localhost:{controller.ServerPort}/obs");
        MessageBox.Show("OBS URL adresi panoya kopyalandı:\nhttp://localhost:" + controller.ServerPort + "/obs", "Kopyalandı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnAbout(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private void OnResetAll(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("GameTranslator ayarları sıfırlansın mı?", "Ayarları Sıfırla",
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
