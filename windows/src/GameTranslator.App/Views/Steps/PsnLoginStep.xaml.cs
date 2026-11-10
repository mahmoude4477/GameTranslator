// SPDX-License-Identifier: AGPL-3.0-only

using System.Windows;
using System.Windows.Controls;
using GameTranslator.Core;
using GameTranslator.Shell;

namespace GameTranslator.App.Views.Steps;

public partial class PsnLoginStep : UserControl
{
    public PsnLoginStep()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private AppController Controller => (AppController)DataContext;

    private void Refresh()
    {
        SignIn.Content = Localizer.Current[Controller.HasAccount ? "Sign in again" : "Sign in"];
        SignIn.Style = (Style)FindResource(Controller.HasAccount ? "SecondaryButton" : "PrimaryButton");
    }

    private async void OnSignIn(object sender, RoutedEventArgs e)
    {
        var redirect = PsnLoginWindow.Show(Window.GetWindow(this));
        if (redirect is null) return;
        SignIn.IsEnabled = false;
        Error.Text = "";
        try
        {
            await Controller.CompletePsnLoginAsync(redirect);
        }
        catch (Exception error)
        {
            Error.Text = error is AppException ? error.Message : AppException.Describe(AppError.InvalidLogin, Localizer.Current);
        }
        SignIn.IsEnabled = true;
        Refresh();
    }
}
