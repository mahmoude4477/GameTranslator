// SPDX-License-Identifier: AGPL-3.0-only

using System.Collections.Concurrent;
using System.IO;
using GameTranslator.RemotePlay;

namespace GameTranslator.App.Services;

/// <summary>
/// يكتب سجل اتصال PS5 (libchiaki) في الملف %LOCALAPPDATA%\GameTranslator\remoteplay.log. حين يبقى PS5 على
/// "جارٍ إعادة الاتصال…" يُعرف السبب من هنا. تُكتب الأسطر في الخلفية دون تعطيل خيط المكتبة الأصلية؛
/// وحين يتجاوز الملف 1 MB يُحذف نصفه القديم.
/// </summary>
internal static class RemotePlayDiagnostics
{
    private const long MaxBytes = 1 << 20;
    private static readonly BlockingCollection<string> Lines = new(boundedCapacity: 2000);

    internal static string LogPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameTranslator", "remoteplay.log");

    public static void Start()
    {
        RemotePlayLog.MessageLogged += line => Lines.TryAdd($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}");
        new Thread(WriteLoop) { IsBackground = true, Name = "remoteplay.log" }.Start();
    }

    public static void Note(string line) => Lines.TryAdd($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}");

    private static void WriteLoop()
    {
        foreach (var line in Lines.GetConsumingEnumerable())
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                var info = new FileInfo(LogPath);
                if (info.Exists && info.Length > MaxBytes)
                {
                    var text = File.ReadAllText(LogPath);
                    File.WriteAllText(LogPath, text[(text.Length / 2)..]);
                }
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // يستمر التطبيق حتى لو تعذّرت كتابة السجل.
            }
        }
    }
}
