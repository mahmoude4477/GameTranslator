// SPDX-License-Identifier: AGPL-3.0-only

using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Threading;
using GameTranslator.App.Controls;
using GameTranslator.Core;
using GameTranslator.Shell;

namespace GameTranslator.App.Views;

/// <summary>Ana ekran: başlat/durdur, PS5 ve TV durumu, uyarılar, son altyazı ve entegre ayarlar.</summary>
public partial class MainView : UserControl
{
    private readonly AppController controller;
    private readonly DispatcherTimer connectionWatch = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool loading = true;

    public MainView(AppController controller)
    {
        this.controller = controller;
        DataContext = controller;
        InitializeComponent();
        controller.PropertyChanged += OnChanged;
        connectionWatch.Tick += async (_, _) => await controller.CheckConnectionsAsync();
        Loaded += (_, _) =>
        {
            connectionWatch.Start();
            LoadSettings();
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
        TextSizeSlider.Value = Math.Clamp(s.Style.FontSize, TextSizeSlider.Minimum, TextSizeSlider.Maximum);
        BackgroundSlider.Value = s.Style.Background;
        BoldCheck.IsChecked = s.Style.Bold;
        DubbingCheck.IsChecked = s.DubbingEnabled;
        ObsText.Text = $"OBS overlay: http://localhost:{controller.ServerPort}/obs";
        loading = false;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppController.IsRunning) or nameof(AppController.TranslatorStatus) or nameof(AppController.ModelInstalled))
            Refresh();
    }

    private void Refresh()
    {
        var running = controller.IsRunning;
        Toggle.Background = new System.Windows.Media.SolidColorBrush(
            running ? System.Windows.Media.Color.FromRgb(220, 38, 38) : System.Windows.Media.Color.FromRgb(10, 132, 255));
        ToggleGlyph.Text = running ? Glyphs.Stop : Glyphs.Play;
        ToggleText.Text = running ? "Çeviriyi Durdur" : "Çeviriyi Başlat";
    }

    private async void OnToggle(object sender, RoutedEventArgs e)
    {
        if (controller.IsRunning) controller.Stop(); else await controller.StartAsync();
    }

    private void OnTabChanged(object sender, RoutedEventArgs e)
    {
        if (PanelDisplay == null || PanelAudio == null || PanelSystem == null) return;
        PanelDisplay.Visibility = TabDisplay.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelAudio.Visibility = TabAudio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelSystem.Visibility = TabSystem.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnTextSize(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (loading) return;
        var val = (int)Math.Round(e.NewValue);
        controller.Settings = controller.Settings with { Style = controller.Settings.Style with { FontSize = val } };
    }

    private void OnBackground(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (loading) return;
        var val = (int)Math.Round(e.NewValue);
        controller.Settings = controller.Settings with { Style = controller.Settings.Style with { Background = val } };
    }

    private void OnPlacement(object sender, SelectionChangedEventArgs e)
    {
        if (loading || PlacementBox.SelectedItem is not ComboBoxItem item) return;
        if (Enum.TryParse<SubtitlePlacement>((string)item.Tag, out var placement))
            controller.Settings = controller.Settings with { Style = controller.Settings.Style with { Placement = placement } };
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
    }

    private void OnDubbing(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        controller.Settings = controller.Settings with { DubbingEnabled = DubbingCheck.IsChecked == true };
    }

    private void OnTestSubtitle(object sender, RoutedEventArgs e) => controller.SendTestSubtitle();

    private void OnCropChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (loading) return;
        controller.Settings = controller.Settings with
        {
            Region = new CropRegion(CropTop.Value, CropBottom.Value, CropLeft.Value, CropRight.Value)
        };
    }

    private void OnResetArea(object sender, RoutedEventArgs e)
    {
        loading = true;
        CropTop.Value = 0;
        CropBottom.Value = 1;
        CropLeft.Value = 0;
        CropRight.Value = 1;
        controller.Settings = controller.Settings with { Region = new CropRegion() };
        loading = false;
    }

    private async void OnDeepLSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DeepLKeyInput.Password)) return;
        await controller.SetDeepLKeyAsync(DeepLKeyInput.Password);
        controller.SendTestSubtitle();
    }

    private void OnCopyObs(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText($"http://localhost:{controller.ServerPort}/obs");
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
        }
    }

    private void OnDone(object sender, RoutedEventArgs e)
    {
        // Kaydedildi
    }

    private void OnSettingsButton(object sender, RoutedEventArgs e)
    {
        TabDisplay.IsChecked = true;
    }

    private void OnHyperlink(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
    }
}
