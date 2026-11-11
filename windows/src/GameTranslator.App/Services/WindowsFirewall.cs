// SPDX-License-Identifier: AGPL-3.0-only

using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using GameTranslator.Shell;

namespace GameTranslator.App.Services;

/// <summary>
/// إذن الاتصالات الواردة إلى GameTranslator.exe في جدار حماية Windows. تُقرأ القواعد عبر COM (HNetCfg.FwPolicy2) دون
/// صلاحيات المسؤول؛ وتُضاف القاعدة عبر netsh بإذن المسؤول (UAC).
/// </summary>
/// <remarks>
/// يثبّت Velopack التطبيق في المسار %LOCALAPPDATA%\GameTranslatorApp\current\GameTranslator.exe ويجدّد المجلد نفسه عند
/// التحديث؛ ولأن المسار لا يتغيّر مع الإصدار تبقى القاعدة سارية بعد التحديثات أيضا.
/// </remarks>
internal sealed class WindowsFirewall : IFirewall
{
    private const string RuleName = "GameTranslator";
    // NET_FW_PROFILE2_DOMAIN, _PRIVATE, _PUBLIC
    private static readonly int[] Profiles = [1, 2, 4];
    // NET_FW_IP_PROTOCOL_TCP, _UDP; 256 = أي بروتوكول
    private static readonly int[] Protocols = [6, 17];
    private const int AnyProtocol = 256;
    private const int Inbound = 1;
    private const int Allow = 1;

    private static string? ExePath => Environment.ProcessPath;

    public bool HasRule()
    {
        if (ExePath is not { } exe) return true;
        var type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
        if (type is null) return true;
        dynamic policy = Activator.CreateInstance(type)!;
        // لكل (ملف تعريف، بروتوكول): هل توجد قاعدة سماح، وهل توجد قاعدة حظر. في Windows قاعدة الحظر تغلب السماح؛
        // إن أغلق المستخدم السؤال عند أول تشغيل فربما كتب Windows قاعدة حظر لهذا الملف التنفيذي.
        var allowed = new bool[Profiles.Length, Protocols.Length];
        var blocked = new bool[Profiles.Length, Protocols.Length];
        foreach (dynamic rule in (IEnumerable)policy.Rules)
        {
            if (!(bool)rule.Enabled || (int)rule.Direction != Inbound) continue;
            if (!SamePath((string?)rule.ApplicationName, exe)) continue;
            var profiles = (int)rule.Profiles;
            var protocol = (int)rule.Protocol;
            var isAllow = (int)rule.Action == Allow;
            for (var p = 0; p < Profiles.Length; p++)
            {
                if ((profiles & Profiles[p]) == 0) continue;
                for (var q = 0; q < Protocols.Length; q++)
                {
                    if (protocol != AnyProtocol && protocol != Protocols[q]) continue;
                    if (isAllow) allowed[p, q] = true; else blocked[p, q] = true;
                }
            }
        }
        for (var p = 0; p < Profiles.Length; p++)
        {
            // إن كان جدار الحماية معطّلا في ملف التعريف هذا فلا حاجة إلى قاعدة.
            if (!FirewallEnabled(policy, Profiles[p])) continue;
            for (var q = 0; q < Protocols.Length; q++)
                if (!allowed[p, q] || blocked[p, q]) return false;
        }
        return true;
    }

    /// <summary>
    /// بسؤال UAC واحد: تُحذف كل قواعد الاتصالات الواردة لهذا الملف التنفيذي (ومنها قواعد الحظر التي كتبها Windows)،
    /// وتُضاف مكانها قاعدة واحدة تسمح في كل ملف تعريف وكل بروتوكول.
    /// </summary>
    public async Task<bool> AddRuleAsync()
    {
        if (ExePath is not { } exe) return false;
        var program = $"program=\"{exe}\"";
        var command = $"netsh advfirewall firewall delete rule name=all dir=in {program} >nul"
                      + $" & netsh advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow {program} enable=yes profile=any protocol=any";
        try
        {
            // /s: يحذف cmd علامتي الاقتباس الأولى والأخيرة فقط؛ وتبقى علامات الاقتباس في المسار.
            using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/s /c \"{command}\"")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            if (process is null) return false;
            await process.WaitForExitAsync();
        }
        catch (Win32Exception)
        {
            // أجاب المستخدم بـ "لا" عن سؤال الإذن من Windows.
            return false;
        }
        try { return await Task.Run(HasRule); }
        catch (Exception) { return true; }
    }

    private static bool FirewallEnabled(dynamic policy, int profile)
    {
        try { return (bool)policy.FirewallEnabled[profile]; }
        catch (Exception) { return true; }
    }

    private static bool SamePath(string? ruleApp, string exe)
    {
        if (string.IsNullOrEmpty(ruleApp)) return false;
        try
        {
            var expanded = Path.GetFullPath(Environment.ExpandEnvironmentVariables(ruleApp));
            return string.Equals(expanded, Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception) { return false; }
    }
}
