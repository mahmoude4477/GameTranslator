// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.Shell;

/// <summary>
/// إذن الاتصالات الواردة إلى GameTranslator في جدار حماية Windows. Remote Play (PS5) وخادم الترجمة النصية (التلفزيون) يستقبلان
/// اتصالات من الخارج؛ إن أغلق المستخدم سؤال الإذن عند أول تشغيل أو كانت الشبكة "عامة" تُحظر هذه الاتصالات.
/// </summary>
public interface IFirewall
{
    /// <summary>هل توجد لهذا الملف التنفيذي قاعدة سماح غير محظورة تشمل TCP و UDP في كل ملف تعريف (المجال، خاص، عام). لا يتطلب صلاحيات المسؤول.</summary>
    bool HasRule();

    /// <summary>يضيف القاعدة بإذن المسؤول (UAC)؛ false إن لم يأذن المستخدم أو بقيت القاعدة غير موجودة.</summary>
    Task<bool> AddRuleAsync();
}

/// <summary>
/// حالة بطاقة جدار الحماية. Offer: القاعدة ناقصة والاتصال متعثّر، فيظهر الزر. Working: في انتظار سؤال الإذن
/// من Windows. Allowed: أُضيفت القاعدة (تأكيد قصير). Failed: تعذّرت إضافة القاعدة، فيظهر الزر من جديد.
/// </summary>
public enum FirewallPrompt { None, Offer, Working, Allowed, Failed }
