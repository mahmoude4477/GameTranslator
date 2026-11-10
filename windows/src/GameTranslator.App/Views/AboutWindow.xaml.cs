// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace GameTranslator.App.Views;

public partial class AboutWindow : Window
{
    internal AboutWindow()
    {
        InitializeComponent();
        Version.Text = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "";
    }

    public static void Open(Window? owner) => new AboutWindow { Owner = owner }.ShowDialog();

    private void OnLink(object sender, RequestNavigateEventArgs e) =>
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
