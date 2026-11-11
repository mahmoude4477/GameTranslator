// SPDX-License-Identifier: AGPL-3.0-only


namespace GameTranslator.Core;

/// <summary>يترجم سطرًا من لغة اللعبة إلى لغة الترجمة.</summary>
public interface ITextTranslator
{
    Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default);
}

public enum TranslatorStatus
{
    Ready,
    /// <summary>نموذج الترجمة لم يُنزَّل بعد.</summary>
    NeedsDownload,
    /// <summary>زوج اللغات هذا غير مدعوم.</summary>
    Unsupported,
}

/// <summary>خطأ ترجمة يستطيع المستخدم إصلاحه (النموذج ناقص، اللغة غير مدعومة).</summary>
public sealed class TranslatorException(TranslatorStatus status)
    : Exception($"Translator not available: {status}")
{
    public TranslatorStatus Status { get; } = status;
}

/// <summary>نتيجة تجربة مفتاح الترجمة عبر الإنترنت (DeepL).</summary>
public enum OnlineKeyState { Valid, Invalid, QuotaExceeded, Unreachable }

/// <param name="Used">عدد الأحرف المستخدمة هذا الشهر (مع مفتاح صالح).</param>
/// <param name="Limit">الحد الشهري.</param>
public sealed record OnlineKeyCheck(OnlineKeyState State, long Used = 0, long Limit = 0);
