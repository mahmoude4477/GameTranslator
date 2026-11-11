// SPDX-License-Identifier: AGPL-3.0-only

using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameTranslator.Core.LgTv;

public sealed record CommandResult(int Status, string Output);

/// <summary>كل ما يشغّل أمرًا خارجيًا (فعليًا <see cref="ProcessRunner"/>).</summary>
public interface ICommandRunner
{
    Task<CommandResult> RunAsync(string executable, IReadOnlyList<string> arguments, string? inputFile);
}

/// <summary>المشكلات التي تظهر عند تثبيت تطبيق التلفزيون (مشتركة بين LG وSamsung وAndroid TV).</summary>
public enum DevModeError
{
    Unreachable, KeyServerOff, WrongPassphrase, KeyRefused, InstallFailed, ToolMissing,
    /// <summary>Samsung: التلفزيون أقدم من Tizen 5.0 (2019).</summary>
    TooOld,
    /// <summary>لم يكتمل تسجيل الدخول بحساب Samsung.</summary>
    SignInFailed,
    /// <summary>لم تمنح Samsung الشهادة الخاصة بالتلفزيون.</summary>
    CertificateFailed,
    /// <summary>Android: لم يُمنح إذن تصحيح الأخطاء على التلفزيون.</summary>
    NotApproved,
    /// <summary>أفاد التلفزيون عند التثبيت بأنه لم تبقَ مساحة.</summary>
    NotEnoughSpace,
}

/// <param name="detail">تفاصيل تساعد على معرفة المشكلة (مثل مخرجات الأمر)؛ تُكتب في سجل الأخطاء ولا تُعرض للمستخدم.</param>
public sealed class DevModeException(DevModeError error, string? detail = null)
    : Exception(detail is null ? error.ToString() : $"{error}: {detail}")
{
    public DevModeError Error { get; } = error;

    /// <summary>السبب الفعلي الذي أعطاه التلفزيون أو الأداة (مثل مخرجات Samsung: "install failed[118]").</summary>
    public string? Detail { get; } = detail;
}

/// <summary>مسار ssh-keygen الذي يزيل عبارة المرور عن المفتاح القادم من Key Server.</summary>
public sealed record DevModeTools(string SshKeygen)
{
    public static DevModeTools ForThisSystem() => OperatingSystem.IsWindows()
        ? new(Path.Combine(Environment.SystemDirectory, "OpenSSH", "ssh-keygen.exe"))
        : new("/usr/bin/ssh-keygen");
}

/// <summary>
/// يثبّت تطبيق التلفزيون ويحدّثه عبر وضع المطوّر في LG.
/// التلفزيون الذي فُعّل فيه وضع المطوّر ينتظر SSH على المنفذ 9922؛ ويُجلب المفتاح من Key Server مرة واحدة.
/// </summary>
public sealed class DevModeInstaller(IRemoteShell shell, TimeSpan? pollInterval = null, TimeSpan? installTimeout = null,
    TimeSpan? verifyTimeout = null)
{
    /// <summary>مثبِّت يتصل بالتلفزيون عبر SSH من داخل التطبيق. أمر التثبيت ينتظر نتيجة التلفزيون بضع دقائق.</summary>
    public static DevModeInstaller ForTv(string host, string keyFile) =>
        new(new SshNetShell(host, Port, keyFile) { CommandTimeout = TimeSpan.FromMinutes(5) });

    public const int Port = 9922;
    private const string AppInfoPath = "/media/developer/apps/usr/palm/applications/com.gametranslator.tv/appinfo.json";
    public const string TempDir = "/media/developer/temp";
    private const string InstallLog = TempDir + "/gametranslator-install.log";

    /// <summary>كما يفعل ares-install تُرفع الحزمة إلى التلفزيون باسمها (<c>/media/developer/temp/com.gametranslator.tv_0.9.6_all.ipk</c>).</summary>
    public static string RemotePackage(string package) => $"{TempDir}/{Path.GetFileName(package)}";

    /// <summary><c>com.gametranslator.tv_0.6.1_all.ipk</c> → <c>0.6.1</c></summary>
    public static string? VersionOfPackage(string name)
    {
        var parts = name.Split('_');
        return name.EndsWith(".ipk", StringComparison.Ordinal) && parts.Length >= 3 ? parts[1] : null;
    }

    public static bool IsNewer(string a, string b) =>
        Version.TryParse(a, out var left) && Version.TryParse(b, out var right) && left > right;

    public static string? VersionFromAppInfo(string text)
    {
        var match = Regex.Match(text, "\"version\"\\s*:\\s*\"([^\"]+)\"");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>هل وضع المطوّر مفعّل على التلفزيون؟ إن كان معطّلًا فالمنفذ لا يستجيب.</summary>
    public static async Task<bool> IsDevModeOnAsync(string host, TimeSpan? timeout = null)
    {
        using var client = new TcpClient();
        using var cancellation = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(2));
        try
        {
            await client.ConnectAsync(host, Port, cancellation.Token);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static Uri KeyUrl(string host) => new($"http://{host}:9991/webos_rsa");

    /// <summary>يجلب المفتاح من Key Server ويفكّه بعبارة المرور الظاهرة على شاشة التلفزيون.</summary>
    public static async Task<string> FetchKeyAsync(string host, string passphrase, ICommandRunner runner, DevModeTools tools)
    {
        byte[] key;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            key = await http.GetByteArrayAsync(KeyUrl(host));
        }
        catch (Exception)
        {
            throw new DevModeException(DevModeError.KeyServerOff);
        }
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllBytesAsync(file, key);
            var result = await runner.RunAsync(tools.SshKeygen, ["-q", "-p", "-P", passphrase, "-N", "", "-f", file], null);
            if (result.Status != 0) throw new DevModeException(DevModeError.WrongPassphrase);
            return await File.ReadAllTextAsync(file);
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>الإصدار المثبَّت على التلفزيون؛ وnull إذا لم يكن التطبيق مثبَّتًا.</summary>
    public async Task<string?> InstalledVersionAsync()
    {
        var result = await SshAsync($"cat {AppInfoPath}");
        return VersionFromAppInfo(result.Output);
    }

    /// <summary>
    /// يرفع الحزمة إلى التلفزيون ويثبّتها كما يفعل ares-install: يبقى اشتراك التثبيت مفتوحًا حتى يقول التلفزيون "installed"
    /// أو يعلن خطأ، ولا تُحذف الحزمة إلا بعد ذلك. كان <c>luna-send -n 1</c> سابقًا يخرج بعد أول رد؛ وعند إغلاق الاشتراك
    /// كانت بعض إصدارات webOS تترك التثبيت في منتصفه، وكنا نعطي خطأ «المساحة الفارغة» لأن الإصدار لم يظهر.
    /// </summary>
    public async Task InstallAsync(string package, string version)
    {
        var remote = RemotePackage(package);
        // حتى لا تشغل الحزم الباقية من محاولات سابقة مساحة على التلفزيون.
        var upload = await SshAsync(
            $"mkdir -p {TempDir} && rm -f {TempDir}/com.gametranslator.tv_*.ipk {TempDir}/gametranslator.ipk && cat > '{remote}'", package);
        if (upload.Status != 0)
            throw Failure($"رمز خروج الرفع {upload.Status}: {Short(upload.Output)}", upload.Output);
        // هل وصلت الحزمة إلى التلفزيون كاملة؟ (إذا لم يوجد wc أو تعذّرت قراءة المخرجات يُتخطى الفحص.)
        var expected = new FileInfo(package).Length;
        var size = await SshAsync($"wc -c < '{remote}'");
        if (long.TryParse(size.Output.Trim(), out var actual) && actual != expected)
            throw new DevModeException(DevModeError.InstallFailed, $"وصلت الحزمة إلى التلفزيون ناقصة أو تالفة: {actual}/{expected} بايت");

        var wait = installTimeout ?? TimeSpan.FromMinutes(3);
        var response = await SshAsync(InstallCommand(remote, (int)Math.Max(1, Math.Ceiling(wait.TotalSeconds))));
        var outcome = ParseInstallReplies(response.Output);
        if (outcome.Failed)
        {
            await RemoveAsync(remote);
            throw Failure(string.IsNullOrWhiteSpace(outcome.Reason) ? $"فشل التثبيت: {Short(response.Output)}" : outcome.Reason,
                response.Output);
        }

        // حتى لو قال التلفزيون "installed" (أو تعذّرت قراءة النتيجة) يُتحقق من الإصدار عبر appinfo.json.
        var timer = Stopwatch.StartNew();
        var limit = verifyTimeout ?? TimeSpan.FromSeconds(outcome.Installed ? 30 : 60);
        while (true)
        {
            if (await InstalledVersionAsync() == version)
            {
                await RemoveAsync(remote);
                return;
            }
            if (timer.Elapsed >= limit) break;
            await Task.Delay(pollInterval ?? TimeSpan.FromSeconds(2));
        }
        // إذا لم تصل نتيجة لا تُحذف الحزمة: قد يكون التثبيت جاريًا على التلفزيون؛ والمحاولة التالية تنظّفها.
        if (outcome.Installed) await RemoveAsync(remote);
        throw new DevModeException(DevModeError.InstallFailed,
            $"لم يظهر الإصدار {version} على التلفزيون؛ ردّ التثبيت ({response.Status}): {Short(response.Output)}");
    }

    /// <summary>
    /// أمر الصدفة الذي يبدأ التثبيت على التلفزيون. <c>luna-send-pub -i</c> (الصيغة التي يستخدمها ares-install) يُبقي
    /// الاشتراك ما دام الإدخال مفتوحًا؛ والإدخال يأتي من <c>sleep</c>. تُكتب الردود في ملف؛ وعندما يقول التلفزيون "installed"
    /// أو يعلن خطأ، أو يخرج luna-send من تلقاء نفسه، أو تنتهي المهلة، تُطبع الردود ويُغلق luna-send. مخارج sleep
    /// غير مربوطة بقناة SSH؛ فلا تنتظره الجلسة.
    /// </summary>
    public static string InstallCommand(string remotePackage, int waitSeconds)
    {
        var parameters = $"{{\"id\":\"com.ares.defaultName\",\"ipkUrl\":\"{remotePackage}\",\"subscribe\":true}}";
        return string.Join("; ",
            $"rm -f {InstallLog}",
            $"sleep {waitSeconds + 30} </dev/null 2>/dev/null | /usr/bin/luna-send-pub -i 'luna://com.webos.appInstallService/dev/install' '{parameters}' >{InstallLog} 2>&1 & L=$!",
            "i=0",
            $"while [ $i -lt {waitSeconds} ]; do grep -q -i -e '\"state\" *: *\"installed' -e 'failed' -e '\"returnValue\" *: *false' -e '\"state\" *: *\"success' {InstallLog} 2>/dev/null && break; kill -0 $L 2>/dev/null || break; sleep 1; i=$((i+1)); done",
            $"cat {InstallLog}",
            "kill $L 2>/dev/null",
            $"rm -f {InstallLog}");
    }

    /// <summary>خلاصة ردود luna-send؛ و<see cref="Reason"/> هو السبب الذي أعطاه التلفزيون.</summary>
    public sealed record InstallOutcome(bool Installed, bool Failed, string? Reason);

    /// <summary>
    /// يقرأ ردود JSON التي يعطيها luna-send سطرًا سطرًا كما يفعل ares-install: <c>returnValue: false</c> أو
    /// FAILED داخل <c>details.state</c> خطأ، و<c>installed</c>/<c>SUCCESS</c> نجاح.
    /// </summary>
    public static InstallOutcome ParseInstallReplies(string output)
    {
        var pending = "";
        foreach (var line in output.Split('\n'))
        {
            pending += line.Trim();
            if (pending.Length == 0) continue;
            if (!pending.StartsWith('{'))
            {
                // نص من luna-send نفسه (مثل "Error: ...")؛ وares أيضًا يتخطى ما ليس JSON.
                pending = "";
                continue;
            }
            JsonElement reply;
            try
            {
                using var document = JsonDocument.Parse(pending);
                reply = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                continue; // السطر ناقص؛ يُضم إلى الذي يليه.
            }
            pending = "";

            JsonElement? details = reply.TryGetProperty("details", out var d) && d.ValueKind == JsonValueKind.Object ? d : null;
            var state = Text(details, "state") ?? "";
            if (reply.TryGetProperty("returnValue", out var ok) && ok.ValueKind == JsonValueKind.False)
                return new(false, true, Reason(Text(reply, "errorText") ?? Text(reply, "errorMessage"), Text(reply, "errorCode")));
            if (state.Contains("failed", StringComparison.OrdinalIgnoreCase))
                return new(false, true, Reason(Text(details, "reason") ?? Text(details, "errorText") ?? state,
                    Text(details, "errorCode") ?? Text(reply, "errorCode")));
            if (state.Contains("installed", StringComparison.OrdinalIgnoreCase) || state.StartsWith("success", StringComparison.OrdinalIgnoreCase))
                return new(true, false, null);
        }
        return new(false, false, null);
    }

    /// <summary>هل يقول التلفزيون أو الصدفة إنه لم تبقَ مساحة؟</summary>
    public static bool IsStorageProblem(string text) => Regex.IsMatch(text,
        @"no space|not enough (free )?(space|storage)|insufficient (free )?(space|storage)|out of space|storage (is )?full|disk (is )?full|ENOSPC|lack of (space|storage)",
        RegexOptions.IgnoreCase);

    private static DevModeException Failure(string detail, string evidence) =>
        new(IsStorageProblem(evidence) || IsStorageProblem(detail) ? DevModeError.NotEnoughSpace : DevModeError.InstallFailed, detail);

    private static string Reason(string? text, string? code)
    {
        var reason = (text ?? "").Trim();
        return string.IsNullOrWhiteSpace(code) ? reason : $"{reason} (errorCode {code})".Trim();
    }

    private static string? Text(JsonElement? element, string name) =>
        element is { } e && e.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            }
            : null;

    private async Task RemoveAsync(string remote)
    {
        try { await SshAsync($"rm -f '{remote}'"); } catch (DevModeException) { /* قد يبقى الملف المؤقت */ }
    }

    private static string Short(string text) => text.Length <= 500 ? text.Trim() : text[..500].Trim() + "…";

    private const string SessionTokenPath = "/var/luna/preferences/devmode_enabled";
    public static Uri RenewUrl(string token) => new($"https://developer.lge.com/secure/ResetDevModeSession.dev?sessionToken={Uri.EscapeDataString(token)}");
    public static Uri CheckUrl(string token) => new($"https://developer.lge.com/secure/CheckDevModeSession.dev?sessionToken={Uri.EscapeDataString(token)}");

    /// <summary>
    /// يعيد مدة وضع المطوّر البالغة 1000 ساعة إلى بدايتها (مثل زر EXTEND في تطبيق Developer Mode على التلفزيون).
    /// يُقرأ رمز الجلسة من التلفزيون ويُرسل إلى خادم LG؛ وهو ما تفعله أداة dev-manager-desktop من webOS Homebrew
    /// تمامًا. إذا فشل يعود تحذير فقط: يستطيع المستخدم التمديد من التلفزيون أيضًا قبل انتهاء المدة.
    /// </summary>
    /// <returns>المدة المتبقية (بالساعات)؛ وnull إن لم تُعرف.</returns>
    public async Task<int?> RenewSessionAsync(HttpClient http)
    {
        var token = (await SshAsync($"cat {SessionTokenPath}")).Output.Trim();
        if (token.Length == 0 || token.Contains(' ')) return null;
        try
        {
            var renewed = await http.GetStringAsync(RenewUrl(token));
            if (!renewed.Contains("success", StringComparison.OrdinalIgnoreCase)) return null;
            return RemainingHours(await http.GetStringAsync(CheckUrl(token)));
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>المدة المتبقية في رد LG تأتي بالصيغة <c>999:58:40</c> (ساعة:دقيقة:ثانية).</summary>
    public static int? RemainingHours(string response)
    {
        var match = Regex.Match(response, @"(\d{1,4}):(\d{2}):(\d{2})");
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    private Task<CommandResult> SshAsync(string command, string? input = null) => shell.RunAsync(command, input);
}

/// <summary>يشغّل الأوامر عبر <see cref="Process"/>؛ ولا يفتح نافذة.</summary>
public sealed class ProcessRunner : ICommandRunner
{
    public async Task<CommandResult> RunAsync(string executable, IReadOnlyList<string> arguments, string? inputFile)
    {
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardInput = inputFile is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            // قد يكون عميل OpenSSH في Windows (ssh-keygen) محذوفًا.
            throw new DevModeException(DevModeError.ToolMissing);
        }
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (inputFile is not null)
        {
            await using (var input = File.OpenRead(inputFile))
                await input.CopyToAsync(process.StandardInput.BaseStream);
            process.StandardInput.Close();
        }
        await process.WaitForExitAsync();
        return new CommandResult(process.ExitCode, await output + await error);
    }
}
