// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.RemotePlay.Tests;

/// <summary>Yerel kütüphane (gametranslator_native) yüklenemiyorsa testi atlar: Mac'te ve DLL'siz derlemede.</summary>
public sealed class NativeFactAttribute : FactAttribute
{
    public NativeFactAttribute()
    {
        if (!NativeMethods.IsAvailable) Skip = "gametranslator_native is not available on this machine";
    }
}
