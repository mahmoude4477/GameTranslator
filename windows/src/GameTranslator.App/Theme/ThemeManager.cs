// SPDX-License-Identifier: AGPL-3.0-only

using System.Windows;
using System.Windows.Media;
using GameTranslator.Core;
using Microsoft.Win32;

namespace GameTranslator.App.Theme;

/// <summary>
/// يحوّل قيم design/tokens.json إلى موارد التطبيق. تُختار الألوان بحسب السمة (فاتحة/داكنة)؛
/// ويستخدمها XAML عبر DynamicResource فتتحدّث فورًا عند إعادة تطبيق السمة.
/// </summary>
public static class ThemeManager
{
    private static readonly DesignTokens Tokens = DesignTokens.Shared;

    public static void Apply(ResourceDictionary resources, bool? forceDark = null)
    {
        // GameTranslator تطبيق ألعاب؛ يستخدم دائمًا السمة الداكنة (Dark Theme) العصرية والأنيقة.
        var dark = forceDark ?? true;
        foreach (var (name, pair) in Tokens.Color)
        {
            var color = ToColor(pair.For(dark));
            resources[$"{name}Color"] = color;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[$"{name}Brush"] = brush;
        }
        foreach (var (name, font) in Tokens.Font)
        {
            resources[$"{name}FontSize"] = font.Size;
            resources[$"{name}FontWeight"] = font.Weight switch
            {
                "bold" => FontWeights.Bold,
                "semibold" => FontWeights.SemiBold,
                "medium" => FontWeights.Medium,
                _ => FontWeights.Normal,
            };
        }
        foreach (var (name, value) in Tokens.Space)
        {
            resources[$"space.{name}"] = value;
            resources[$"pad.{name}"] = new Thickness(value);
        }
        foreach (var (name, value) in Tokens.Radius) resources[$"radius.{name}"] = new CornerRadius(value);
        foreach (var (name, value) in Tokens.Size) resources[$"size.{name}"] = value;
    }

    /// <summary>يجدّد الموارد عند تغيّر السمة.</summary>
    public static void Follow(ResourceDictionary resources)
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General)
                Application.Current.Dispatcher.Invoke(() => Apply(resources, true));
        };
    }

    public static bool IsDarkTheme()
    {
        return true;
    }

    private static Color ToColor(string hex)
    {
        var (r, g, b) = DesignTokens.Rgb(hex) ?? (0, 0, 0);
        return Color.FromRgb((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }
}
