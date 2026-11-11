// SPDX-License-Identifier: AGPL-3.0-only

using System.Windows;
using System.Windows.Controls;
using GameTranslator.Shell;

namespace GameTranslator.App.Controls;

// المكوّنات المشتركة (docs/DESIGN.md). مظهرها في قوالب Theme/Styles.xaml؛ والمقاسات والألوان
// من الموارد القادمة من tokens.json.

/// <summary>التخطيط المشترك لخطوات المعالج: شارة الأيقونة، العنوان، الوصف، المحتوى.</summary>
public class StepLayout : ContentControl
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(StepLayout));
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(StepLayout));
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(nameof(Message), typeof(string), typeof(StepLayout));

    static StepLayout() => DefaultStyleKeyProperty.OverrideMetadata(typeof(StepLayout), new FrameworkPropertyMetadata(typeof(StepLayout)));

    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
}

/// <summary>بطاقة محتوى بعنوان أو بدونه.</summary>
public class Card : ContentControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(Card));

    static Card() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Card), new FrameworkPropertyMetadata(typeof(Card)));

    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
}

/// <summary>سطر تعليمات مرقّم.</summary>
public class NumberedRow : Control
{
    public static readonly DependencyProperty NumberProperty = DependencyProperty.Register(nameof(Number), typeof(int), typeof(NumberedRow));
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(NumberedRow));

    static NumberedRow() => DefaultStyleKeyProperty.OverrideMetadata(typeof(NumberedRow), new FrameworkPropertyMetadata(typeof(NumberedRow)));

    public int Number { get => (int)GetValue(NumberProperty); set => SetValue(NumberProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
}

/// <summary>نقطة حالة ملوّنة مع اسمها وقيمتها.</summary>
public class StatusRow : Control
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(StatusRow));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatusRow));
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(StatusTone), typeof(StatusRow));

    static StatusRow() => DefaultStyleKeyProperty.OverrideMetadata(typeof(StatusRow), new FrameworkPropertyMetadata(typeof(StatusRow)));

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
}

/// <summary>أسماء أيقونات Segoe Fluent Icons.</summary>
public static class Glyphs
{
    public const string Captions = "";
    public const string People = "";
    public const string Person = "";
    public const string Controller = "";
    public const string Tv = "";
    public const string Seal = "";
    public const string Download = "";
    public const string Play = "";
    public const string Stop = "";
    public const string Settings = "";
    public const string Check = "";
    public const string Warning = "";
}
