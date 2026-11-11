// SPDX-License-Identifier: AGPL-3.0-only

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using GameTranslator.Core;

namespace GameTranslator.App.Theme;

/// <summary>نص الواجهة في XAML: <c>{t:T 'Start translating'}</c>. يأتي النص من كتالوج النصوص.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class T(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => Localizer.Current[key];
}

/// <summary>true ← ظاهر؛ والعكس مع <c>ConverterParameter=invert</c>.</summary>
public sealed class VisibleIf : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var shown = value switch
        {
            bool flag => flag,
            string text => text.Length > 0,
            null => false,
            _ => true,
        };
        if (parameter as string == "invert") shown = !shown;
        return shown ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>يعكس قيمة Bool (مثلًا IsEnabled="{Binding IsStarting, Converter={StaticResource Not}}").</summary>
public sealed class Not : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}
