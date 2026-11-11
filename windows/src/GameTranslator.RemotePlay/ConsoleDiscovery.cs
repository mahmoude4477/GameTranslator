// SPDX-License-Identifier: AGPL-3.0-only

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GameTranslator.RemotePlay;

/// <summary>يعثر على أجهزة PS5 في الشبكة المحلية ويوقظ الجهاز الموضوع في وضع السكون.</summary>
public static class ConsoleDiscovery
{
    internal const string Broadcast = "255.255.255.255";

    /// <summary>إذا أُعطي <paramref name="host"/> يُسأل ذلك العنوان وحده (البث العام قد يُحجب في بعض الشبكات).
    /// الإلغاء يترك الانتظار؛ والبحث في الخلفية ينتهي تلقائيًا عند انقضاء مدته.</summary>
    public static Task<IReadOnlyList<ConsoleInfo>> DiscoverAsync(string? host = null, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var duration = timeout ?? TimeSpan.FromSeconds(3);
        // البحث ينتظر نائمًا طوال المدة؛ لذا يعمل في خيط مستقل حتى لا يشغل مجمع الخيوط.
        return Task.Factory.StartNew(() => Discover(host, duration), CancellationToken.None,
                                     TaskCreationOptions.LongRunning, TaskScheduler.Default).WaitAsync(ct);
    }

    /// <summary>يختار PS5 المسجَّل من نتائج البحث: إن وُجد جهاز واحد فهو المقصود، وإن تعددت فالوحيد المطابق للاسم.
    /// عند عدم التأكد يعيد null (لا يُغيَّر العنوان بدل محاولة الاتصال بجهاز خاطئ).</summary>
    internal static ConsoleInfo? PickMoved(IReadOnlyList<ConsoleInfo> found, string name)
    {
        if (found.Count == 1) return found[0];
        var named = found.Where(c => c.Name == name).ToList();
        return named.Count == 1 ? named[0] : null;
    }

    /// <summary>يوقظ PS5 المسجَّل من وضع الراحة.</summary>
    public static void Wake(RegisteredConsole console)
    {
        var credential = console.WakeCredential ?? throw new InvalidOperationException("Registration key is not a wake credential");
        if (NativeMethods.IsAvailable)
        {
            try
            {
                NativeMethods.EnsureInitialized();
                NativeMethods.cs_wake(console.Host, credential);
            }
            catch { }
        }
    }

    private sealed class Collector
    {
        public readonly Dictionary<string, ConsoleInfo> Hosts = [];
    }

    /// <summary>بحث حاجب (يستخدمه طابور الجلسة و DiscoverAsync).</summary>
    internal static unsafe IReadOnlyList<ConsoleInfo> Discover(string? host, TimeSpan timeout)
    {
        // 1. ابحث أولًا بمقبس UDP من .NET وحده (لا يحتاج DLL، ولا خطر انهيار!)
        var managed = DiscoverPureManaged(host, timeout);
        if (managed.Count > 0 || !NativeMethods.IsAvailable)
            return managed;

        // 2. إن وُجدت مكتبة DLL الأصلية فجرّبها
        try
        {
            var targets = host is null ? LocalTargets() : [host];
            var collector = new Collector();
            var handle = GCHandle.Alloc(collector);
            var strings = new nint[targets.Count];
            try
            {
                for (var i = 0; i < targets.Count; i++) strings[i] = Marshal.StringToCoTaskMemUTF8(targets[i]);
                delegate* unmanaged[Cdecl]<byte*, byte*, int, nint, void> callback = &OnConsole;
                fixed (nint* pointer = strings)
                    NativeMethods.cs_discover(pointer, strings.Length, (int)timeout.TotalMilliseconds, (nint)callback, GCHandle.ToIntPtr(handle));
            }
            finally
            {
                foreach (var pointer in strings) Marshal.FreeCoTaskMem(pointer);
                handle.Free();
            }
            lock (collector)
            {
                if (collector.Hosts.Count > 0)
                    return [.. collector.Hosts.Values.OrderBy(c => c.Host, StringComparer.Ordinal)];
            }
        }
        catch
        {
            // ارجع بصمت إلى نتيجة البحث المُدار
        }

        return managed;
    }

    internal static IReadOnlyList<ConsoleInfo> DiscoverPureManaged(string? host, TimeSpan timeout)
    {
        var found = new Dictionary<string, ConsoleInfo>();
        try
        {
            using var udp = new UdpClient();
            udp.EnableBroadcast = true;
            udp.Client.ReceiveTimeout = Math.Max(500, (int)timeout.TotalMilliseconds);

            var payload = System.Text.Encoding.UTF8.GetBytes("SRCH * HTTP/1.1\r\ndevice-discovery-protocol-version:00030010\r\n\r\n");
            
            var targets = host is null ? LocalTargets() : [host];
            foreach (var target in targets)
            {
                if (IPAddress.TryParse(target, out var targetIp))
                {
                    try { udp.Send(payload, payload.Length, new IPEndPoint(targetIp, 9302)); } catch { }
                }
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    var remoteEp = new IPEndPoint(IPAddress.Any, 0);
                    var bytes = udp.Receive(ref remoteEp);
                    var text = System.Text.Encoding.UTF8.GetString(bytes);
                    if (text.Contains("host-name", StringComparison.OrdinalIgnoreCase) ||
                        text.Contains("host-id", StringComparison.OrdinalIgnoreCase) ||
                        text.Contains("HTTP/1.1 200", StringComparison.OrdinalIgnoreCase))
                    {
                        var name = "PlayStation 5";
                        var isAwake = true;
                        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            var colon = line.IndexOf(':');
                            if (colon > 0)
                            {
                                var k = line[..colon].Trim().ToLowerInvariant();
                                var v = line[(colon + 1)..].Trim();
                                if (k == "host-name") name = v;
                                if (k == "status-code" && v.Equals("standby", StringComparison.OrdinalIgnoreCase)) isAwake = false;
                            }
                        }
                        var ipStr = remoteEp.Address.ToString();
                        found[ipStr] = new ConsoleInfo(ipStr, name, isAwake);
                    }
                }
                catch (SocketException)
                {
                    break;
                }
                catch
                {
                    // لا تقطع الحلقة عند الأخطاء الأخرى
                }
            }
        }
        catch
        {
            // خطأ في فتح المقبس
        }
        return [.. found.Values.OrderBy(c => c.Host, StringComparer.Ordinal)];
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnConsole(byte* host, byte* name, int awake, nint user)
    {
        try
        {
            if (GCHandle.FromIntPtr(user).Target is not Collector collector || NativeMethods.Utf8(host) is not { } address) return;
            var info = new ConsoleInfo(address, NativeMethods.Utf8(name) ?? "PS5", awake != 0);
            lock (collector) collector.Hosts[info.Host] = info;
        }
        catch
        {
            // يجب ألا يتسرب استثناء إلى الخيط الأصلي.
        }
    }

    /// <summary>كل العناوين في شبكات IPv4 المتصلة (حتى /22، أي 1022 عنوانًا) مع عنوان البث العام.</summary>
    internal static IReadOnlyList<string> LocalTargets()
    {
        var result = new List<string>();
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(unicast.Address)) continue;
                    var mask = unicast.IPv4Mask;
                    if (mask is null || mask.Equals(IPAddress.Any)) continue;
                    result.AddRange(Hosts(ToUInt32(unicast.Address), ToUInt32(mask)).Select(ToText));
                }
            }
        }
        catch (NetworkInformationException)
        {
            // إذا تعذرت قراءة معلومات الشبكة يبقى البث العام وحده.
        }
        result.Add(Broadcast);
        return [.. result.Distinct()];
    }

    /// <summary>العناوين الأخرى في الشبكة (عدا عنواني الشبكة والبث)؛ فارغة في الشبكات الأكبر من /22.</summary>
    internal static IReadOnlyList<uint> Hosts(uint address, uint netmask)
    {
        var hostCount = ~netmask;
        if (hostCount <= 1 || hostCount > 1023) return [];
        var network = address & netmask;
        var result = new List<uint>((int)hostCount);
        for (uint i = 1; i < hostCount; i++)
            if (network + i != address) result.Add(network + i);
        return result;
    }

    internal static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    internal static string ToText(uint address) => $"{address >> 24}.{(address >> 16) & 0xFF}.{(address >> 8) & 0xFF}.{address & 0xFF}";
}
