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
        // إذا لم يبدأ تجهيز محرك الترجمة في بداية المعالج (مثلًا عند العودة من الإعدادات) يبدأ هنا.
        Loaded += (_, _) => ((AppController)DataContext).StartModelDownload();
    }

    private void OnRetry(object sender, RoutedEventArgs e) => ((AppController)DataContext).StartModelDownload();
}
