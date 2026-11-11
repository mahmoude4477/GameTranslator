// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core;

namespace GameTranslator.RemotePlay;

// الأنواع التي تتيحها طبقة Remote Play للتطبيق.

public sealed record ConsoleInfo(string Host, string Name, bool IsAwake);

/// <summary>بيانات اتصال هذا الكمبيوتر المسجَّل لدى PS5. تُحفظ مشفّرة.</summary>
/// <param name="RegistKey">مفتاح التسجيل بطول 16 بايت (ست عشري بترميز ASCII، مكمَّل بالأصفار).</param>
/// <param name="Morning">مفتاح الجلسة بطول 16 بايت.</param>
public sealed record RegisteredConsole(string Host, string Name, byte[] RegistKey, byte[] Morning)
{
    /// <summary>القيمة الست عشرية لمفتاح التسجيل، تُستخدم للإيقاظ.</summary>
    public ulong? WakeCredential
    {
        get
        {
            var end = Array.IndexOf(RegistKey, (byte)0);
            var text = System.Text.Encoding.ASCII.GetString(RegistKey, 0, end < 0 ? RegistKey.Length : end);
            return ulong.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var value) ? value : null;
        }
    }

    public RegisteredConsole WithHost(string host) => this with { Host = host };
}

/// <summary>سبب انقطاع الاتصال؛ بصيغة يمكن شرحها للمستخدم.</summary>
public enum DisconnectKind
{
    Stopped,
    /// <summary>PS5 مطفأ أو تعذر إيقاظه.</summary>
    ConsoleAsleep,
    /// <summary>Remote Play مستخدم على جهاز آخر.</summary>
    InUse,
    /// <summary>PS5 رفض الاتصال (مثلًا التسجيل غير صالح أو Remote Play معطّل).</summary>
    Refused,
    Network,
    /// <summary>للمستخدم الثاني رمز دخول على PS5؛ و Remote Play يطلب الرمز.</summary>
    LoginPasscode,
    Other,
}

public sealed record DisconnectReason(DisconnectKind Kind, string? Detail = null);

public enum SessionPhase { Connecting, Streaming, Disconnected }

public sealed record SessionState(SessionPhase Phase, DisconnectReason? Reason = null)
{
    public static readonly SessionState Connecting = new(SessionPhase.Connecting);
    public static readonly SessionState Streaming = new(SessionPhase.Streaming);
    public static SessionState Disconnected(DisconnectKind kind, string? detail = null) => new(SessionPhase.Disconnected, new DisconnectReason(kind, detail));
}

public enum RegistrationFailure
{
    Failed,
    Canceled,
    /// <summary>PS5 لم يتعرف على الحساب: الرمز لم يؤخذ من مستخدم PS5 صاحب الحساب المسجَّل دخوله في التطبيق.</summary>
    WrongAccount,
    /// <summary>PS5 لم يرد على حزمة البحث: الرمز لم يصل إليه أصلًا (مطفأ، أو على شبكة أخرى، أو Remote Play معطّل).</summary>
    NoAnswer,
    InUse,
    /// <summary>خدمة Remote Play في PS5 أعطت خطأ.</summary>
    ConsoleCrashed,
    /// <summary>إصدار نظام PS5 يختلف عن الإصدارات التي تعرفها libchiaki.</summary>
    VersionMismatch,
}

/// <param name="reason">قيمة RP-Application-Reason التي أبلغ بها PS5 (مثل 0x80108b09)؛ و null إن لم يرد PS5.</param>
/// <param name="detail">التفصيل التقني المعروض للمستخدم والمكتوب في السجل (رمز PS5 أو سطر خطأ libchiaki).</param>
public sealed class RegistrationException(RegistrationFailure failure, uint? reason = null, string? detail = null)
    : Exception($"PS5 registration failed: {failure} {detail}")
{
    public RegistrationFailure Failure { get; } = failure;
    public uint? Reason { get; } = reason;
    public string? Detail { get; } = detail;
}

/// <summary>اتصال Remote Play مع PS5 (فعليًا StreamSession). الصوت مغلق ولا تُرسل مدخلات ذراع التحكم.</summary>
public interface IStreamSession : IDisposable
{
    /// <summary>إطار مفكوك الترميز (15 في الثانية على الأكثر). يُستدعى في خيط الجلسة نفسها.</summary>
    event Action<Frame>? FrameReceived;
    event Action<SessionState>? StateChanged;
    /// <summary>يُستدعى عند العثور على PS5 بعنوان جديد في الشبكة؛ ويجب حفظ العنوان الجديد.</summary>
    event Action<RegisteredConsole>? ConsoleMoved;
    void Start();
    /// <summary>لا يحجب: تُغلق الجلسة في الخلفية.</summary>
    void Stop();
    /// <summary>يُستدعى عند استيقاظ الكمبيوتر من السكون أو عند تغيّر الشبكة.</summary>
    void ReconnectNow();
}
