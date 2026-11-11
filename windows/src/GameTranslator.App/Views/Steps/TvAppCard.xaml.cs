// SPDX-License-Identifier: AGPL-3.0-only

using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GameTranslator.Core;
using GameTranslator.Core.LgTv;
using GameTranslator.Shell;

namespace GameTranslator.App.Views.Steps;

/// <summary>خطوات تثبيت تطبيق التلفزيون عبر وضع المطوّر (LG، Samsung، Android TV). لا تظهر إن لم يكن هناك ما يُفعل.</summary>
public partial class TvAppCard : UserControl
{
    // يتابع تلقائيًا عندما يفعّل المستخدم وضع المطوّر على التلفزيون.
    private readonly DispatcherTimer devModeWatch = new() { Interval = TimeSpan.FromSeconds(3) };
    private AppController? controller;

    public TvAppCard()
    {
        InitializeComponent();
        devModeWatch.Tick += async (_, _) =>
        {
            if (controller is not null) await controller.RefreshTvAppAsync();
        };
        Loaded += (_, _) =>
        {
            controller = DataContext as AppController;
            if (controller is null) return;
            controller.PropertyChanged += OnChanged;
            Show(controller.TvApp);
        };
        Unloaded += (_, _) =>
        {
            devModeWatch.Stop();
            if (controller is not null) controller.PropertyChanged -= OnChanged;
        };
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppController.TvApp) or nameof(AppController.DevModeHoursLeft))
            Dispatcher.Invoke(() => Show(controller!.TvApp));
    }

    private static Visibility Visible(bool shown) => shown ? Visibility.Visible : Visibility.Collapsed;

    private void Show(TvAppState state)
    {
        var text = Localizer.Current;
        var brand = controller?.Tv?.Brand ?? TvBrand.Lg;
        var devModeOff = state.Kind == TvAppKind.DevModeOff;
        Visibility = Visible(state.Kind != TvAppKind.Unknown);
        LgSteps.Visibility = Visible(devModeOff && brand == TvBrand.Lg);
        SamsungSteps.Visibility = Visible(devModeOff && brand == TvBrand.Samsung);
        AndroidSteps.Visibility = Visible(devModeOff && brand == TvBrand.AndroidTv);
        PassphraseArea.Visibility = Visible(state.Kind == TvAppKind.NeedsPassphrase);
        OpenAgain.Visibility = Visible(state.Kind == TvAppKind.SignIn);
        Retry.Visibility = Visible(state.Kind == TvAppKind.Failed);
        SamsungHostStep.Text = text["3. Turn Developer mode On, type this computer's address as Host PC IP: %@ and press OK."]
            .Replace("%@", controller?.ComputerAddress ?? "?");

        Status.Text = state.Kind switch
        {
            TvAppKind.Working => text["Installing the TV app…"],
            TvAppKind.UpToDate => string.Format(text["The TV app is ready (version %@)."].Replace("%@", "{0}"), state.Version),
            TvAppKind.DevModeOff => brand switch
            {
                TvBrand.Lg => text["The TV app is not installed. GameTranslator can install it once Developer Mode is on."],
                TvBrand.Samsung => text["The TV app is not installed. GameTranslator installs it once Developer Mode is on."],
                _ => text["The TV app is not installed. GameTranslator installs it over the network once debugging is on."],
            },
            TvAppKind.NeedsPassphrase => text["Developer Mode is on. One last step to install the TV app:"],
            TvAppKind.SignIn => text["Samsung signs the app for your TV. Sign in with your Samsung account on the page that opened in your browser."],
            TvAppKind.ApproveOnTv => text["Look at the TV: it asks whether to allow debugging from this computer."],
            _ => "",
        };
        Detail.Text = state.Kind switch
        {
            TvAppKind.SignIn => text["No account? You can create a free one on the same page. If the browser asks to allow access to devices on your network, allow it."],
            TvAppKind.ApproveOnTv => text["Tick “Always allow from this computer” and choose Allow. The app then installs by itself."],
            TvAppKind.UpToDate when brand == TvBrand.Lg => controller?.DevModeHoursLeft is { } hours
                ? text["Developer Mode: %lld hours left. GameTranslator extends it by itself each time you use it."].Replace("%lld", hours.ToString())
                : text["GameTranslator extends Developer Mode by itself each time you use it."],
            _ => "",
        };
        Detail.Visibility = Visible(Detail.Text.Length > 0);
        Problem.Text = state.Error is { } error
            ? Message(error, brand) + (state.Reason is { } reason ? $"\n\n{Localizer.Current["TV says:"]} {reason}" : "")
            : "";
        if (devModeOff) devModeWatch.Start();
        else devModeWatch.Stop();
    }

    private static string Message(DevModeError error, TvBrand brand) => Localizer.Current[error switch
    {
        DevModeError.KeyServerOff => "Key Server is off. Turn it on in the Developer Mode app and keep the app on screen.",
        DevModeError.WrongPassphrase => "The passphrase is not correct. Check it on the TV screen.",
        DevModeError.KeyRefused => "The TV refused the key. Open Key Server and type the passphrase again.",
        DevModeError.Unreachable when brand == TvBrand.Samsung =>
            "The TV did not accept this computer. In the TV's Developer Mode window, check that Host PC IP is this computer's address, then restart the TV.",
        DevModeError.Unreachable => "The TV did not respond. Make sure it is on and on the same network.",
        DevModeError.ToolMissing => "Windows' OpenSSH Client is missing. Add it in Settings › System › Optional features and try again.",
        DevModeError.TooOld => "This Samsung TV is too old for the GameTranslator TV app (2019 models and later are supported).",
        DevModeError.SignInFailed => "Samsung sign-in did not complete. Try again and finish signing in on the page that opens.",
        DevModeError.CertificateFailed => "Samsung did not issue the signing certificate. Try again in a few minutes.",
        DevModeError.NotApproved => "Debugging was not allowed on the TV. Try again and choose Allow on the TV.",
        DevModeError.NotEnoughSpace => "The TV is out of free space. Remove an app you do not use from the TV and try again.",
        _ => "The TV app could not be installed. Try again; if it fails again, send us the reason below.",
    }];

    private async void OnInstall(object sender, RoutedEventArgs e)
    {
        if (controller is not null && !string.IsNullOrWhiteSpace(Passphrase.Text))
            await controller.InstallTvAppAsync(Passphrase.Text);
    }

    private async void OnRetry(object sender, RoutedEventArgs e)
    {
        if (controller is not null) await controller.RefreshTvAppAsync();
    }

    private void OnOpenAgain(object sender, RoutedEventArgs e)
    {
        if (controller?.TvApp.Url is { } url) controller.OpenInBrowser(url);
    }

    private void OnGuide(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://github.com/mahmoude4477/GameTranslator/blob/main/docs/tv-setup.ar.md#android-tv--google-tv") { UseShellExecute = true });
}
