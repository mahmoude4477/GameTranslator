// SPDX-License-Identifier: AGPL-3.0-only

using System.Windows;
using System.Windows.Controls;
using GameTranslator.Shell;

namespace GameTranslator.App.Views.Steps;

public partial class TranslationStep : UserControl
{
    public TranslationStep()
    {
        InitializeComponent();
        // Sihirbaz başında indirme başlamamışsa (ör. ayarlardan geri gelindi) burada başlar.
        Loaded += (_, _) => ((AppController)DataContext).StartModelDownload();
    }

    private void OnRetry(object sender, RoutedEventArgs e) => ((AppController)DataContext).StartModelDownload();
}
