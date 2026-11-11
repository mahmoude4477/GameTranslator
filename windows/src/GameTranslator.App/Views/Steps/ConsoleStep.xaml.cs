// SPDX-License-Identifier: AGPL-3.0-only

using System.Windows;
using System.Windows.Controls;
using GameTranslator.Core;
using GameTranslator.RemotePlay;
using GameTranslator.Shell;

namespace GameTranslator.App.Views.Steps;

public partial class ConsoleStep : UserControl
{
    private string? selected;

    public ConsoleStep()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            ShowDone();
            await SearchAsync();
        };
    }

    private AppController Controller => (AppController)DataContext;

    private string Host => selected ?? ManualHost.Text.Trim();

    private void ShowDone()
    {
        Done.Text = Controller.Console?.Name ?? "";
        Done.Visibility = Controller.Console is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task SearchAsync()
    {
        Search.IsEnabled = false;
        Consoles.Children.Clear();
        try
        {
            var found = await Controller.DiscoverConsolesAsync();
            selected ??= found.FirstOrDefault()?.Host;
            foreach (var console in found)
            {
                var option = new RadioButton { Content = $"{console.Name} — {console.Host}", IsChecked = console.Host == selected, GroupName = "ps5" };
                option.Checked += (_, _) => { selected = console.Host; UpdateConnect(); };
                Consoles.Children.Add(option);
            }
            var none = found.Count == 0;
            NotFound.Visibility = ManualHost.Visibility = none ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception)
        {
            NotFound.Visibility = ManualHost.Visibility = Visibility.Visible;
            Error.Text = "لم يُعثر على جهاز الألعاب عند البحث في الشبكة. أدخل عنوان IP لجهاز PS5 يدويًا.";
        }
        Search.IsEnabled = true;
        UpdateConnect();
    }

    private void UpdateConnect() =>
        Connect.IsEnabled = Host.Length > 0 && Pin.Text.Count(char.IsDigit) == 8;

    private void OnPinChanged(object sender, TextChangedEventArgs e) => UpdateConnect();

    private async void OnSearch(object sender, RoutedEventArgs e) => await SearchAsync();

    private async void OnConnect(object sender, RoutedEventArgs e)
    {
        Connect.IsEnabled = false;
        Error.Text = "";
        try
        {
            await Controller.RegisterAsync(Host, Pin.Text);
            ShowDone();
        }
        catch (AppException error)
        {
            Error.Text = error.Message;
        }
        catch (Exception)
        {
            Error.Text = AppException.Describe(AppError.RegistrationFailed, Localizer.Current);
        }
        UpdateConnect();
    }
}
