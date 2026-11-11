// SPDX-License-Identifier: AGPL-3.0-only

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GameTranslator.RemotePlay;

/// <summary>استدعاءات gametranslator_native.dll (windows/native/gametranslator_native.h). دوال رد النداء
/// دوال ساكنة من نوع <c>UnmanagedCallersOnly</c>؛ ويُوصل إلى الكائن عبر <c>GCHandle</c>.</summary>
internal static unsafe partial class NativeMethods
{
    internal const string Library = "gametranslator_native";

    internal const int EventConnected = 0;
    internal const int EventQuit = 1;
    internal const int EventLoginPin = 2;

    internal const int QuitStopped = 0;
    internal const int QuitInUse = 1;
    internal const int QuitRefused = 2;
    internal const int QuitNetwork = 3;
    internal const int QuitOther = 4;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RegisteredConsoleNative
    {
        public fixed byte Nickname[0x20];
        public fixed byte RegistKey[0x10];
        public fixed byte Morning[0x10];
    }

    [LibraryImport(Library)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int cs_init();

    /// <param name="callback">void (*)(const char *host, const char *name, int awake, void *user)</param>
    [LibraryImport(Library)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int cs_discover(nint* targets, int targetCount, int timeoutMs, nint callback, nint user);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int cs_wake(string host, ulong credential);

    /// <param name="log">void (*)(int level, const char *message, void *user)</param>
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int cs_register(string host, byte* accountId, uint pin, RegisteredConsoleNative* output,
                                            uint* reason, nint log, nint user);

    /// <param name="frame">void (*)(const uint8_t *bgra, int width, int height, int stride, void *user)</param>
    /// <param name="eventCallback">void (*)(int event, int quit_reason, const char *reason_text, void *user)</param>
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint cs_session_start(string host, byte* registKey, byte* morning, byte* accountId, int maxFps,
                                                  nint frame, nint eventCallback, nint log, nint user);

    [LibraryImport(Library)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void cs_session_stop(nint session);

    private static readonly Lock InitLock = new();
    private static bool initialized;

    /// <summary>يُجهَّز مكدس الشبكة مرة واحدة عند أول استخدام. إن غابت المكتبة يُرمى DllNotFoundException.</summary>
    internal static void EnsureInitialized()
    {
        lock (InitLock)
        {
            if (initialized) return;
            var result = cs_init();
            if (result != 0) throw new InvalidOperationException($"gametranslator_native could not be initialized ({result})");
            initialized = true;
        }
    }

    /// <summary>هل يمكن تحميل المكتبة على هذا الجهاز (لا على Mac ولا في البنيات الخالية من DLL).</summary>
    internal static bool IsAvailable
    {
        get
        {
            if (!NativeLibrary.TryLoad(Library, typeof(NativeMethods).Assembly, null, out var handle)) return false;
            NativeLibrary.Free(handle);
            return true;
        }
    }

    internal static string? Utf8(byte* text) => text == null ? null : Marshal.PtrToStringUTF8((nint)text);

    /// <summary>يحوّل مصفوفة C إلى سلسلة نصية بالجزء السابق للصفر.</summary>
    internal static string FixedString(byte* bytes, int length)
    {
        var end = 0;
        while (end < length && bytes[end] != 0) end++;
        return System.Text.Encoding.UTF8.GetString(bytes, end);
    }

    /// <summary>ينسخ البايتات إلى مصفوفة ثابتة الطول مكمَّلة بالأصفار (ويُقطع الزائد).</summary>
    internal static byte[] Padded(byte[] data, int length)
    {
        var result = new byte[length];
        Array.Copy(data, result, Math.Min(data.Length, length));
        return result;
    }

    internal static string LevelName(int level) => level switch { 0 => "error", 1 => "warning", _ => "info" };
}

/// <summary>أسطر سجل libchiaki (خطأ، تحذير، معلومة). للتشخيص؛ تصل أثناء التسجيل والجلسة.</summary>
public static class RemotePlayLog
{
    /// <summary>يُستدعى في خيط المكتبة الأصلية؛ ويجب أن يعود سريعًا.</summary>
    public static event Action<string>? MessageLogged;

    internal static void Write(int level, string? message)
    {
        if (message is null) return;
        try { MessageLogged?.Invoke($"[chiaki {NativeMethods.LevelName(level)}] {message}"); }
        catch { /* خطأ مستمع السجل يجب ألا يُسقط الخيط الأصلي */ }
    }
}
