// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.RemotePlay.Tests;

/// <summary>يتخطى الاختبار إذا تعذّر تحميل المكتبة الأصلية (gametranslator_native): على macOS وفي البناء بدون DLL.</summary>
public sealed class NativeFactAttribute : FactAttribute
{
    public NativeFactAttribute()
    {
        if (!NativeMethods.IsAvailable) Skip = "gametranslator_native is not available on this machine";
    }
}
