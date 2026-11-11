// SPDX-License-Identifier: AGPL-3.0-only

using System.IO;
using System.Windows;
using GameTranslator.App.Services;
using GameTranslator.RemotePlay;
using Microsoft.Web.WebView2.Core;

namespace GameTranslator.App.Views;

/// <summary>يفتح صفحة تسجيل الدخول لدى Sony؛ ويلتقط عنوان إعادة التوجيه عند انتهاء الدخول. كلمة المرور تذهب إلى Sony فقط.</summary>
public partial class PsnLoginWindow : Window
{
    private Uri? redirect;

    private PsnLoginWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await OpenAsync();
    }

    /// <summary>يعيد عنوان إعادة التوجيه إذا اكتمل الدخول، و null عند الإلغاء.</summary>
    public static Uri? Show(Window? owner)
    {
        var window = new PsnLoginWindow { Owner = owner };
        window.ShowDialog();
        return window.redirect;
    }

    // يجب أن تبقى البيئة حيّة حتى تنتهي عملية المتصفح؛ وإلا لا يصل BrowserProcessExited.
    private static CoreWebView2Environment? lastEnvironment;

    private async Task OpenAsync()
    {
        // جلسة نظيفة عند كل دخول: حتى لا تتداخل ملفات تعريف الارتباط السابقة (مثل حسابك الشخصي).
        var root = Path.Combine(AppPaths.Data, "webview");
        DeleteLeftovers(root);
        var folder = Path.Combine(root, Guid.NewGuid().ToString("N"));
        var environment = await CoreWebView2Environment.CreateAsync(null, folder);
        lastEnvironment = environment;
        // يُبقي WebView2 ملفاته (مثل BrowserMetrics-*.pma) مفتوحة مدة عند الإغلاق. لا يمكن حذف المجلد إلا بعد
        // انتهاء عملية المتصفح؛ والمجلد الذي تعذّر حذفه ينظّفه الدخول التالي.
        environment.BrowserProcessExited += (_, _) => TryDelete(folder);
        await Browser.EnsureCoreWebView2Async(environment);
        Browser.CoreWebView2.NavigationStarting += OnNavigation;
        Browser.CoreWebView2.Navigate(PsnAccount.AuthorizeUrl.AbsoluteUri);
        Closed += (_, _) => Browser.Dispose();
    }

    private static void DeleteLeftovers(string root)
    {
        if (!Directory.Exists(root)) return;
        foreach (var folder in Directory.EnumerateDirectories(root)) TryDelete(folder);
    }

    private static void TryDelete(string folder)
    {
        try { Directory.Delete(folder, recursive: true); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    private void OnNavigation(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!e.Uri.StartsWith(PsnAccount.RedirectPrefix, StringComparison.Ordinal)) return;
        e.Cancel = true;
        redirect = new Uri(e.Uri);
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
