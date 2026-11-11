// SPDX-License-Identifier: AGPL-3.0-only

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using GameTranslator.Core;
using GameTranslator.Shell;

namespace GameTranslator.App.Views;

/// <summary>
/// إذن جدار حماية Windows: يظهر إذا كانت القاعدة ناقصة عند فشل اقتران PS5 أو عند تعذّر اتصال PS5 والتلفزيون
/// أثناء الترجمة. تُستخدم البطاقة نفسها في الشاشة الرئيسية وفي خطوة PS5.
/// </summary>
public partial class FirewallCard : UserControl
{
    private AppController? controller;

    public FirewallCard()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            controller = DataContext as AppController;
            if (controller is null) return;
            controller.PropertyChanged += OnChanged;
            Show();
        };
        Unloaded += (_, _) =>
        {
            if (controller is not null) controller.PropertyChanged -= OnChanged;
        };
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppController.FirewallPrompt)) Dispatcher.Invoke(Show);
    }

    private void Show()
    {
        if (controller is null) return;
        var text = Localizer.Current;
        var prompt = controller.FirewallPrompt;
        // إذا لم تكن الترجمة جارية فالبطاقة خاصة باقتران PS5.
        var running = controller.IsRunning;
        Visibility = prompt == FirewallPrompt.None ? Visibility.Collapsed : Visibility.Visible;
        Allow.Visibility = prompt is FirewallPrompt.Offer or FirewallPrompt.Failed or FirewallPrompt.Working ? Visibility.Visible : Visibility.Collapsed;
        Allow.IsEnabled = prompt != FirewallPrompt.Working;
        Headline.Text = prompt == FirewallPrompt.Allowed
            ? text["Windows Firewall now allows GameTranslator."]
            : text["Windows Firewall may be blocking the connection."];
        Hint.Text = prompt switch
        {
            FirewallPrompt.Working => text["Waiting for Windows…"],
            FirewallPrompt.Failed => text["The firewall rule was not added. Try again and choose Yes when Windows asks."],
            FirewallPrompt.Allowed when running => text["Reconnecting…"],
            FirewallPrompt.Allowed => text["Open Pair Device again on the PS5 and enter the new code."],
            _ when running => text["GameTranslator needs incoming connections from the PS5 and the TV. Windows asks for permission once."],
            _ => text["Allow GameTranslator, then open Pair Device again on the PS5 and enter the new code."],
        };
    }

    private async void OnAllow(object sender, RoutedEventArgs e)
    {
        if (controller is not null) await controller.AllowThroughFirewallAsync();
    }
}
