// SPDX-License-Identifier: AGPL-3.0-only

using System.Runtime.InteropServices;

namespace GameTranslator.App.Services;

/// <summary>Çeviri sürerken bilgisayarın boşta uykuya geçmesini engeller (kullanıcı TV'de oynuyor, bilgisayara dokunmuyor).</summary>
public static partial class SleepGuard
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;

    [LibraryImport("kernel32.dll")]
    private static partial uint SetThreadExecutionState(uint flags);

    public static bool IsActive { get; private set; }

    public static void Begin()
    {
        SetThreadExecutionState(EsContinuous | EsSystemRequired);
        IsActive = true;
    }

    public static void End()
    {
        SetThreadExecutionState(EsContinuous);
        IsActive = false;
    }
}
