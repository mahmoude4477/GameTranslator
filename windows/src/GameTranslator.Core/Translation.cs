// SPDX-License-Identifier: AGPL-3.0-only


namespace GameTranslator.Core;

/// <summary>Bir satırı oyun dilinden hedef dile çevirir (cihaz üstünde).</summary>
public interface ITextTranslator
{
    Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default);
}

public enum TranslatorStatus
{
    Ready,
    /// <summary>Çeviri modeli henüz indirilmedi.</summary>
    NeedsDownload,
    /// <summary>Bu dil çifti desteklenmiyor.</summary>
    Unsupported,
}

/// <summary>Kullanıcının düzeltebileceği çeviri hatası (model eksik, dil desteklenmiyor).</summary>
public sealed class TranslatorException(TranslatorStatus status)
    : Exception($"Translator not available: {status}")
{
    public TranslatorStatus Status { get; } = status;
}

/// <summary>Çevrimiçi çeviri anahtarı (DeepL) denendiğinde ne çıktı.</summary>
public enum OnlineKeyState { Valid, Invalid, QuotaExceeded, Unreachable }

/// <param name="Used">Bu ay kullanılan karakter (geçerli anahtarda).</param>
/// <param name="Limit">Aylık sınır.</param>
public sealed record OnlineKeyCheck(OnlineKeyState State, long Used = 0, long Limit = 0);
