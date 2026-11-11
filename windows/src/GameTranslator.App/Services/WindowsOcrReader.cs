// SPDX-License-Identifier: AGPL-3.0-only

using System.Runtime.InteropServices.WindowsRuntime;
using GameTranslator.Core;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace GameTranslator.App.Services;

/// <summary>
/// التعرّف على النص على الجهاز في Windows. إن لم توجد حزمة OCR للغة اللعبة تُستخدم إحدى اللغات المثبّتة
/// لدى المستخدم (اللغات ذات الأبجدية اللاتينية تقرأ الترجمة النصية الإنجليزية أيضا). تُرتَّب الأسطر من الأعلى إلى الأسفل.
/// </summary>
public sealed class WindowsOcrReader : ITextReader
{
    private readonly OcrEngine engine;

    private WindowsOcrReader(OcrEngine engine) => this.engine = engine;

    /// <summary>null إن لم تكن أي لغة OCR مثبّتة.</summary>
    public static WindowsOcrReader? Create(string language)
    {
        var engine = OcrEngine.TryCreateFromLanguage(new Language(language))
                     ?? OcrEngine.TryCreateFromUserProfileLanguages();
        return engine is null ? null : new WindowsOcrReader(engine);
    }

    /// <summary>هل حزمة OCR الخاصة بلغة اللعبة مثبّتة (إن لم تكن يُقترح إضافتها من الإعدادات).</summary>
    public static bool HasLanguage(string language) => OcrEngine.IsLanguageSupported(new Language(language));

    public string Read(Frame image) => Read(image, deep: true);

    public string Read(Frame image, bool deep) => string.Join(' ', ReadLines(image, deep).Lines.Select(line => line.Text));

    public Reading ReadLines(Frame image, bool deep) => deep ? ReadingFallback.FirstLines(image, Recognize) : Recognize(image);

    /// <summary>
    /// يُقرأ الإطار كما هو ثم بعد إبقاء نصه الأبيض فقط؛ وتُختار القراءة الأقرب إلى كلمات حقيقية.
    /// في المشاهد الساطعة تفوز الثانية، وفي الترجمات النصية الملوّنة تفوز الأولى.
    /// </summary>
    private Reading Recognize(Frame image)
    {
        var raw = RecognizeOnce(image);
        if (image.Width < 40 || image.Height < 40) return raw;
        var whiteText = RecognizeOnce(TextImage.WhiteTextOnly(image));
        return OcrNoise.Score(whiteText.Text) > OcrNoise.Score(raw.Text) ? whiteText : raw;
    }

    private Reading RecognizeOnce(Frame image)
    {
        // محرك OCR يرفض الصورة الصغيرة جدا أو الكبيرة جدا.
        if (image.Width < 40 || image.Height < 40 || image.Width > OcrEngine.MaxImageDimension || image.Height > OcrEngine.MaxImageDimension)
            return Reading.Empty;
        using var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, image.Width, image.Height, BitmapAlphaMode.Premultiplied);
        bitmap.CopyFromBuffer(image.Pixels.AsBuffer());
        var result = engine.RecognizeAsync(bitmap).AsTask().GetAwaiter().GetResult();
        double width = image.Width, height = image.Height;
        var lines = result.Lines
            .SelectMany(line => Split(line, width, height))
            .OrderBy(line => line.Box?.Top ?? 0);
        return new Reading(lines);
    }

    /// <summary>
    /// مربّع السطر هو اتحاد مربّعات الكلمات؛ ويُعطى نسبة إلى أبعاد الصورة. يعدّ Windows أحيانا النصوص المتباعدة
    /// على المحاذاة نفسها (قائمة على اليسار، مؤشر على اليمين) سطرا واحدا؛ إن كانت الفجوة بينها أوسع من أن تكون
    /// مسافة بين كلمتين يُقسم السطر هناك إلى أسطر منفصلة (<see cref="Reading.IsSeparateText"/>).
    /// </summary>
    private static IEnumerable<TextLine> Split(OcrLine line, double width, double height)
    {
        var words = line.Words.ToList();
        if (words.Count == 0) yield break;
        var lineHeight = words.Max(word => word.BoundingRect.Height);
        var start = 0;
        for (var index = 1; index <= words.Count; index++)
        {
            var end = index == words.Count;
            if (!end && !Reading.IsSeparateText(words[index].BoundingRect.X - (words[index - 1].BoundingRect.X + words[index - 1].BoundingRect.Width), lineHeight, width))
                continue;
            var part = words.GetRange(start, index - start);
            TextBox? box = null;
            foreach (var word in part)
            {
                var r = word.BoundingRect;
                var wordBox = new TextBox(r.X / width, r.Y / height, (r.X + r.Width) / width, (r.Y + r.Height) / height);
                box = box?.Union(wordBox) ?? wordBox;
            }
            // نص السطر غير المقسوم يبقى كما أعطاه Windows (في اللغات التي تُكتب بلا مسافات تكون الكلمات متصلة).
            var text = start == 0 && end ? line.Text : string.Join(' ', part.Select(word => word.Text));
            yield return new TextLine(text.Trim(), box);
            start = index;
        }
    }
}
