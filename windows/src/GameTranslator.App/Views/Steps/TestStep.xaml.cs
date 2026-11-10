// SPDX-License-Identifier: AGPL-3.0-only

using System.Windows;
using System.Windows.Controls;
using GameTranslator.Shell;

namespace GameTranslator.App.Views.Steps;

public partial class TestStep : UserControl
{
    public TestStep() => InitializeComponent();

    private AppController Controller => (AppController)DataContext;

    private void OnSend(object sender, RoutedEventArgs e)
    {
        Controller.SendTestSubtitle();
        Send.Style = (Style)FindResource("SecondaryButton");
        Question.Visibility = Visibility.Visible;
    }

    private void OnYes(object sender, RoutedEventArgs e) => Controller.Advance();

    private void OnNo(object sender, RoutedEventArgs e) => Controller.Step = OnboardingStep.Tv;
}
