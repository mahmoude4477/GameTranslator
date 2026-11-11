// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.Core;

/// <summary>صورة BGRA (4 بايتات لكل بكسل). الإطارات القادمة من بث PS5 والمناطق المقصوصة منها.</summary>
public sealed class Frame(byte[] pixels, int width, int height, int stride)
{
    public byte[] Pixels { get; } = pixels;
    public int Width { get; } = width;
    public int Height { get; } = height;
    public int Stride { get; } = stride;

    public static Frame Blank(int width, int height) => new(new byte[width * height * 4], width, height, width * 4);

    /// <summary>نسخة من المخزن المؤقت الذي تعطيه الشيفرة الأصلية (المخزن صالح أثناء الاستدعاء الراجع فقط).</summary>
    public static unsafe Frame Copy(IntPtr bgra, int width, int height, int stride)
    {
        var pixels = new byte[width * height * 4];
        var source = (byte*)bgra;
        fixed (byte* target = pixels)
            for (var y = 0; y < height; y++)
                Buffer.MemoryCopy(source + ((long)y * stride), target + ((long)y * width * 4), width * 4, width * 4);
        return new Frame(pixels, width, height, width * 4);
    }

    public Frame Crop(int x, int y, int width, int height)
    {
        x = Math.Clamp(x, 0, Width);
        y = Math.Clamp(y, 0, Height);
        width = Math.Clamp(width, 0, Width - x);
        height = Math.Clamp(height, 0, Height - y);
        var result = Blank(width, height);
        for (var row = 0; row < height; row++)
            Buffer.BlockCopy(Pixels, ((y + row) * Stride) + (x * 4), result.Pixels, row * result.Stride, width * 4);
        return result;
    }

    /// <summary>تكبير ثنائي الخطية (bilinear)؛ يساعد على قراءة الأسطر القصيرة.</summary>
    public Frame Scaled(int factor)
    {
        var result = Blank(Width * factor, Height * factor);
        for (var ty = 0; ty < result.Height; ty++)
        {
            var sy = Math.Min(((ty + 0.5) / factor) - 0.5, Height - 1.0);
            if (sy < 0) sy = 0;
            var y0 = (int)sy;
            var y1 = Math.Min(y0 + 1, Height - 1);
            var fy = sy - y0;
            for (var tx = 0; tx < result.Width; tx++)
            {
                var sx = Math.Min(((tx + 0.5) / factor) - 0.5, Width - 1.0);
                if (sx < 0) sx = 0;
                var x0 = (int)sx;
                var x1 = Math.Min(x0 + 1, Width - 1);
                var fx = sx - x0;
                for (var c = 0; c < 4; c++)
                {
                    double P(int x, int y) => Pixels[(y * Stride) + (x * 4) + c];
                    var top = P(x0, y0) + ((P(x1, y0) - P(x0, y0)) * fx);
                    var bottom = P(x0, y1) + ((P(x1, y1) - P(x0, y1)) * fx);
                    result.Pixels[(ty * result.Stride) + (tx * 4) + c] = (byte)Math.Round(top + ((bottom - top) * fy));
                }
            }
        }
        return result;
    }
}

/// <summary>المنطقة التي يُبحث فيها عن الترجمة النصية في الصورة، نسبةً إلى الحواف (0–1، من الأعلى ومن اليسار).</summary>
/// <summary>تجهيز الصورة قبل التعرّف على النص.</summary>
public static class TextImage
{
    /// <summary>
    /// الترجمات النصية في الألعاب غالبًا نص أبيض بحدّ داكن أو ظل. على خلفية ساطعة (رمل، سماء) لم يكن التعرّف على النص
    /// في Windows يميّز الحروف عن الخلفية فيقرؤها مثل "CESAREiVJütt'h". هنا تصير البكسلات القريبة من الأبيض التي
    /// بجوارها بكسل داكن نصًّا أسود والباقي خلفية بيضاء؛ وهذه أفضل صيغة يقرؤها التعرّف على النص.
    /// </summary>
    public static Frame WhiteTextOnly(Frame image, int radius = 5)
    {
        int width = image.Width, height = image.Height;
        var bright = new bool[width * height];
        // جدول المجاميع التراكمية للبكسلات الداكنة: يُعرف بخطوة واحدة هل في النافذة بكسل داكن.
        var dark = new int[(width + 1) * (height + 1)];
        for (var y = 0; y < height; y++)
        {
            var rowSum = 0;
            for (var x = 0; x < width; x++)
            {
                var i = (y * image.Stride) + (x * 4);
                int b = image.Pixels[i], g = image.Pixels[i + 1], r = image.Pixels[i + 2];
                int min = Math.Min(r, Math.Min(g, b)), max = Math.Max(r, Math.Max(g, b));
                bright[(y * width) + x] = min >= 200 && max - min <= 45;
                rowSum += (r * 3 + g * 6 + b) / 10 < 110 ? 1 : 0;
                dark[((y + 1) * (width + 1)) + x + 1] = dark[(y * (width + 1)) + x + 1] + rowSum;
            }
        }
        var result = new Frame(new byte[width * height * 4], width, height, width * 4);
        for (var y = 0; y < height; y++)
        {
            int top = Math.Max(0, y - radius), bottom = Math.Min(height, y + radius + 1);
            for (var x = 0; x < width; x++)
            {
                var text = false;
                if (bright[(y * width) + x])
                {
                    int left = Math.Max(0, x - radius), right = Math.Min(width, x + radius + 1);
                    var darkCount = dark[(bottom * (width + 1)) + right] - dark[(top * (width + 1)) + right]
                                    - dark[(bottom * (width + 1)) + left] + dark[(top * (width + 1)) + left];
                    text = darkCount > 0;
                }
                var value = (byte)(text ? 0 : 255);
                var o = (y * result.Stride) + (x * 4);
                result.Pixels[o] = result.Pixels[o + 1] = result.Pixels[o + 2] = value;
                result.Pixels[o + 3] = 255;
            }
        }
        return result;
    }
}

public sealed record CropRegion(double Top = 0.62, double Bottom = 0.97, double Left = 0.14, double Right = 0.86);

public static class RegionCropper
{
    public static Frame Crop(Frame image, CropRegion region)
    {
        // تُقرَّب الحواف إلى الخارج.
        var left = (int)Math.Floor(image.Width * region.Left);
        var top = (int)Math.Floor(image.Height * region.Top);
        var right = (int)Math.Ceiling(image.Width * region.Right);
        var bottom = (int)Math.Ceiling(image.Height * region.Bottom);
        return image.Crop(left, top, right - left, bottom - top);
    }

    /// <summary>نسخة رمادية مصغَّرة من المنطقة: تكفي لمقارنة إطارين دون حاجة إلى التعرّف على النص.</summary>
    public static byte[] Signature(Frame image)
    {
        const int width = 64, height = 12;
        var samples = new byte[width * height];
        if (image.Width == 0 || image.Height == 0) return samples;
        for (var y = 0; y < height; y++)
        {
            var row = (y * image.Height / height) * image.Stride;
            for (var x = 0; x < width; x++)
            {
                var pixel = row + (x * image.Width / width) * 4;
                samples[y * width + x] = (byte)((image.Pixels[pixel] + image.Pixels[pixel + 1] * 2 + image.Pixels[pixel + 2]) / 4);
            }
        }
        return samples;
    }
}

/// <summary>يقرأ النص في صورة (على Windows: Windows.Media.Ocr).</summary>
public interface ITextReader
{
    string Read(Frame image);
    /// <summary>إذا كان <paramref name="deep"/> false لا تُجرَّب القراءة المكبَّرة في المنطقة الفارغة (أخفّ كثيرًا على المعالج).</summary>
    string Read(Frame image, bool deep) => Read(image);
    /// <summary>الأسطر منفصلة ومع مواضعها في الصورة.</summary>
    Reading ReadLines(Frame image, bool deep) => Reading.FromText(Read(image, deep));
}

/// <summary>
/// كثيرًا ما لا يجد OCR في المنطقة الواسعة الأسطر القصيرة مثل "Go!" إطلاقًا. إذا لم يظهر في المنطقة نص
/// يُعاد في الوقت نفسه قراءة الشريط الأوسط (الترجمات النصية موسَّطة) والمنطقة كلها بعد تكبيرهما؛
/// وإن وجد كلاهما نصًّا تُختار قراءة الشريط. القراءة المتزامنة تمنع التأخر في ملاحظة سطر جديد في إطار فارغ.
/// </summary>
public static class ReadingFallback
{
    /// <summary>الصيغة القديمة بلا أسطر: يعود النص الموجود نصًّا واحدًا.</summary>
    public static string FirstReading(Frame image, Func<Frame, string> recognize) =>
        FirstLines(image, frame => Reading.FromText(recognize(frame))).Text;

    public static Reading FirstLines(Frame image, Func<Frame, Reading> recognize)
    {
        var reading = recognize(image);
        if (reading.Lines.Count > 0) return reading;

        // موضع السطر الموجود في الشريط يُعطى بنسب المنطقة كلها لا بنسب الشريط.
        var (x, width) = CenterStrip(image, 0.4);
        var stripArea = new TextBox((double)x / image.Width, 0, (double)(x + width) / image.Width, 1);
        Func<Reading>[] fallbacks =
        [
            () => recognize(image.Crop(x, 0, width, image.Height).Scaled(2)).PlacedIn(stripArea),
            () => recognize(image.Scaled(2)),
        ];
        var results = new Reading[fallbacks.Length];
        Parallel.For(0, fallbacks.Length, index => results[index] = fallbacks[index]());
        return results.FirstOrDefault(r => r.Lines.Count > 0) ?? Reading.Empty;
    }

    private static (int X, int Width) CenterStrip(Frame image, double widthFraction)
    {
        var width = image.Width * widthFraction;
        var left = (image.Width - width) / 2;
        var x = (int)Math.Floor(left);
        return (x, (int)Math.Ceiling(left + width) - x);
    }
}
