// SPDX-License-Identifier: AGPL-3.0-only

using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace GameTranslator.App.Services;

/// <summary>العمل الذي تؤديه الاختصارات العامة.</summary>
public enum HotkeyAction
{
    /// <summary>Num 1: ترجِم المنطقة الإضافية 1 مرة واحدة.</summary>
    ExtraRegion1,
    /// <summary>Num 2: ترجِم المنطقة الإضافية 2 مرة واحدة.</summary>
    ExtraRegion2,
    /// <summary>Num 3: ترجِم المنطقة الإضافية 3 مرة واحدة.</summary>
    ExtraRegion3,
    /// <summary>Num 5: جمّد الصورة ودَع المستخدم يحدد منطقة حرة.</summary>
    FreeArea,
    /// <summary>Num 8: حدّث الصورة في شاشة الإعدادات بصورة اللعبة الحالية.</summary>
    Snapshot,
    /// <summary>Num 0: أوقف ترجمة النص الحية مؤقتا / استأنفها.</summary>
    PauseSubtitles,
    /// <summary>Num 9: ترجِم الشاشة كاملة مرة واحدة؛ والضغط مجددا يزيل الترجمات.</summary>
    Screen,
}

/// <summary>
/// الاختصارات العامة (Num 0, 1, 2, 3, 5, 8, 9): تعمل حتى لو كانت نافذة الكمبيوتر في الخلفية والمستخدم يلعب بذراع التحكم.
/// تُحجز الاختصارات فقط عند تفعيل <see cref="Enabled"/>؛ وهكذا تعمل مفاتيح الأرقام طبيعيا في التطبيقات الأخرى
/// (مثل Excel) حين تكون الترجمة متوقفة. إن كان المفتاح مستخدما في تطبيق آخر يُتجاوز بصمت.
/// </summary>
public sealed partial class GlobalHotkeys : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private const int FirstId = 0x4353;

    /// <summary>Num 0, 1, 2, 3, 5, 8, 9 (و NumLock مفعّل).</summary>
    private static readonly (HotkeyAction Action, uint VirtualKey)[] Keys =
    [
        (HotkeyAction.ExtraRegion1, 0x61),
        (HotkeyAction.ExtraRegion2, 0x62),
        (HotkeyAction.ExtraRegion3, 0x63),
        (HotkeyAction.FreeArea, 0x65),
        (HotkeyAction.Snapshot, 0x68),
        (HotkeyAction.PauseSubtitles, 0x60),
        (HotkeyAction.Screen, 0x69),
    ];

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr window, int id);

    private readonly IntPtr window;
    private readonly HwndSource? source;
    private readonly Action<HotkeyAction> pressed;
    private readonly List<int> registered = [];
    private bool enabled;

    /// <param name="window">مقبض النافذة (HWND).</param>
    /// <param name="pressed">يُستدعى عند الضغط على الاختصار.</param>
    public GlobalHotkeys(IntPtr window, Action<HotkeyAction> pressed)
    {
        this.window = window;
        this.pressed = pressed;
        source = HwndSource.FromHwnd(window);
        source?.AddHook(Hook);
    }

    /// <summary>هل الاختصارات محجوزة.</summary>
    public bool Enabled
    {
        get => enabled;
        set
        {
            if (value == enabled || source is null) return;
            enabled = value;
            if (value) Register();
            else Unregister();
        }
    }

    private void Register()
    {
        for (var index = 0; index < Keys.Length; index++)
        {
            var id = FirstId + index;
            if (RegisterHotKey(window, id, ModNoRepeat, Keys[index].VirtualKey)) registered.Add(id);
        }
    }

    private void Unregister()
    {
        foreach (var id in registered) UnregisterHotKey(window, id);
        registered.Clear();
    }

    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmHotkey) return IntPtr.Zero;
        var id = wParam.ToInt32();
        if (!registered.Contains(id)) return IntPtr.Zero;
        handled = true;
        pressed(Keys[id - FirstId].Action);
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        source?.RemoveHook(Hook);
        Unregister();
        enabled = false;
    }
}
