// SPDX-License-Identifier: AGPL-3.0-only

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using GameTranslator.App.Views.Steps;
using GameTranslator.Core;
using GameTranslator.Shell;

namespace GameTranslator.App.Views;

/// <summary>Kurulum sihirbazı: üstte adım sayacı ve ilerleme, ortada adım, altta Geri / Devam.</summary>
public partial class OnboardingView : UserControl
{
    private readonly AppController controller;

    public OnboardingView(AppController controller)
    {
        this.controller = controller;
        DataContext = controller;
        InitializeComponent();
        controller.PropertyChanged += OnChanged;
        Unloaded += (_, _) => controller.PropertyChanged -= OnChanged;
        ShowStep();
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppController.Step)) ShowStep();
    }

    private void ShowStep()
    {
        StepHost.Content = controller.Step switch
        {
            OnboardingStep.Welcome => new WelcomeStep(),
            OnboardingStep.SecondAccount => new SecondAccountStep(),
            OnboardingStep.PsnLogin => new PsnLoginStep(),
            OnboardingStep.Console => new ConsoleStep(),
            OnboardingStep.Tv => new TvStep(),
            OnboardingStep.Translation => new TranslationStep(),
            _ => new TestStep(),
        };
        Counter.Text = Localizer.Current.Format("Step %lld of %lld", controller.StepNumber, AppController.StepCount);
        Progress.Value = (double)controller.StepNumber / AppController.StepCount;
        BackButton.Visibility = controller.Step == OnboardingStep.Welcome ? Visibility.Hidden : Visibility.Visible;
        ContinueButton.Visibility = controller.Step == OnboardingStep.Test ? Visibility.Hidden : Visibility.Visible;
    }

    private void OnBack(object sender, RoutedEventArgs e) => controller.Back();

    private void OnContinue(object sender, RoutedEventArgs e) => controller.Advance();
}
