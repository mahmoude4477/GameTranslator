// SPDX-License-Identifier: AGPL-3.0-only

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using GameTranslator.Core;

namespace GameTranslator.RemotePlay;

/// <summary>فواصل إعادة المحاولة عند انقطاع الاتصال: 2، 4، 8، 16، وبحد أقصى 30 ثانية.</summary>
internal sealed class ReconnectPolicy
{
    private int attempt;

    public int NextDelay()
    {
        var delay = Math.Min(30, 2 << attempt);
        attempt = Math.Min(attempt + 1, 5);
        return delay;
    }

    public void Reset() => attempt = 0;
}

internal static class DisconnectReasons
{
    /// <summary>يحوّل قيمة CS_QUIT_* من المكتبة الأصلية إلى سبب يمكن شرحه للمستخدم.</summary>
    public static DisconnectReason FromNative(int quit, string? text) => quit switch
    {
        NativeMethods.QuitStopped => new(DisconnectKind.Stopped),
        NativeMethods.QuitInUse => new(DisconnectKind.InUse),
        NativeMethods.QuitRefused => new(DisconnectKind.Refused),
        NativeMethods.QuitNetwork => new(DisconnectKind.Network),
        _ => new(DisconnectKind.Other, text),
    };
}

/// <summary>طابور بخيط واحد ينفّذ المهام بالتتابع.</summary>
internal sealed class SerialQueue : IDisposable
{
    private readonly Channel<Action> channel = Channel.CreateUnbounded<Action>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Thread thread;

    public SerialQueue(string name)
    {
        thread = new Thread(Run) { IsBackground = true, Name = name };
        thread.Start();
    }

    public void Post(Action action) => channel.Writer.TryWrite(action);

    public void PostAfter(TimeSpan delay, Action action) =>
        _ = Task.Delay(delay).ContinueWith(_ => Post(action), TaskScheduler.Default);

    public bool IsCurrent => Thread.CurrentThread == thread;

    private void Run()
    {
        var reader = channel.Reader;
        while (reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
        {
            while (reader.TryRead(out var action))
            {
                try { action(); }
                catch (Exception error) { RemotePlayLog.Write(0, $"session queue: {error}"); }
            }
        }
    }

    public void Dispose() => channel.Writer.TryComplete();
}

/// <summary>ينشئ اتصال Remote Play مع PS5 (بالحساب الثاني) ويسلّم الصورة إطارًا إطارًا.
/// الصوت مغلق ولا تُرسل مدخلات ذراع التحكم. إذا انقطع الاتصال يعيد الاتصال تلقائيًا.</summary>
public sealed class StreamSession : IStreamSession
{
    /// <summary>هل يمكن تحميل مكتبة CChiaki / gametranslator_native الأصلية (DLL) على النظام.</summary>
    public static bool IsNativeAvailable => NativeMethods.IsAvailable;

    /// <summary>أقصى عدد من الإطارات يُحوَّل إلى BGRA في الثانية.</summary>
    internal const int MaxFps = 20;

    /// <summary>إطار مفكوك الترميز. يُستدعى في خيط إطارات الجلسة؛ والإطارات الواردة أثناء انشغال المعالج تُتخطى.</summary>
    public event Action<Frame>? FrameReceived;
    /// <summary>يُستدعى في خيط طابور الجلسة.</summary>
    public event Action<SessionState>? StateChanged;
    /// <summary>يُستدعى في خيط طابور الجلسة.</summary>
    public event Action<RegisteredConsole>? ConsoleMoved;

    /// <summary>مسح الشبكة كلها مكلف؛ فلا يُجرى في كل إعادة محاولة و PS5 مطفأ.</summary>
    private static readonly TimeSpan RelocateInterval = TimeSpan.FromSeconds(30);

    /// <summary>يتغيّر في الطابور وحده (إذا تغيّر عنوان PS5).</summary>
    private RegisteredConsole console;
    private DateTime nextRelocate = DateTime.MinValue;
    private readonly byte[] accountId;
    private readonly SerialQueue queue = new("GameTranslator stream session");
    private readonly FrameDelivery delivery;
    private readonly ReconnectPolicy policy = new();
    /// <summary>يُقرأ ويُكتب في الطابور وحده.</summary>
    private SessionBox? box;

    /// <summary>طلب الإيقاف ورقم الجيل؛ كل بدء/إيقاف يفتح جيلًا جديدًا فتلغي الكتل القديمة
    /// (إعادة المحاولة، حدث إغلاق الجلسة القديمة) نفسها.</summary>
    private readonly Lock stateLock = new();
    private bool running;
    private int generation;
    private bool disposed;

    public StreamSession(RegisteredConsole console, byte[] accountId)
    {
        if (accountId.Length != 8) throw new ArgumentException("PSN account id must be 8 bytes", nameof(accountId));
        this.console = console;
        this.accountId = (byte[])accountId.Clone();
        delivery = new FrameDelivery(frame => FrameReceived?.Invoke(frame));
    }

    public void Start()
    {
        var current = Bump(running: true);
        queue.Post(() =>
        {
            policy.Reset();
            Connect(current);
        });
    }

    /// <summary>لا يحجب: تُغلق الجلسة في الخلفية.</summary>
    public void Stop()
    {
        Bump(running: false);
        queue.Post(CloseBox);
    }

    /// <summary>يستدعيه التطبيق عند استيقاظ الكمبيوتر من السكون أو عند تغيّر الشبكة.</summary>
    public void ReconnectNow()
    {
        if (!IsRunning) return;
        var current = Bump(running: true);
        queue.Post(() =>
        {
            CloseBox();
            policy.Reset();
            Connect(current);
        });
    }

    public void Dispose()
    {
        lock (stateLock)
        {
            if (disposed) return;
            disposed = true;
        }
        Stop();
        queue.Post(() =>
        {
            queue.Dispose();
            delivery.Dispose();
        });
    }

    // الحالة

    private bool IsRunning
    {
        get { lock (stateLock) return running; }
    }

    private int Bump(bool running)
    {
        lock (stateLock)
        {
            this.running = running;
            return ++generation;
        }
    }

    private bool IsCurrent(int value)
    {
        lock (stateLock) return running && generation == value;
    }

    // الاتصال (كله في الطابور)

    private void Connect(int current)
    {
        if (!IsCurrent(current) || box is not null) return;
        Emit(SessionState.Connecting);

        // إن كان PS5 نائمًا فأيقظه؛ وانتظر استيقاظه بفحوص متباعدة دون حجب الطابور.
        var info = ConsoleDiscovery.Discover(console.Host, TimeSpan.FromSeconds(2)).FirstOrDefault() ?? Relocate();
        if (info is { IsAwake: false })
        {
            try { ConsoleDiscovery.Wake(console); }
            catch (Exception error) { RemotePlayLog.Write(1, $"wake: {error.Message}"); }
            WaitForWake(current, DateTime.UtcNow.AddSeconds(20));
            return;
        }
        OpenSession(current);
    }

    /// <summary>إن لم يوجد PS5 على العنوان المسجَّل (ربما غيّر الراوتر عنوانه) يبحث في الشبكة؛ وإن وجده حدّث العنوان.</summary>
    private ConsoleInfo? Relocate()
    {
        if (DateTime.UtcNow < nextRelocate) return null;
        nextRelocate = DateTime.UtcNow + RelocateInterval;
        var moved = ConsoleDiscovery.PickMoved(ConsoleDiscovery.Discover(null, TimeSpan.FromSeconds(3)), console.Name);
        if (moved is null || moved.Host == console.Host) return null;
        RemotePlayLog.Write(1, $"PS5 على عنوان جديد: {console.Host} -> {moved.Host}");
        console = console.WithHost(moved.Host);
        try { ConsoleMoved?.Invoke(console); }
        catch (Exception error) { RemotePlayLog.Write(0, $"ConsoleMoved handler: {error}"); }
        return moved;
    }

    private void WaitForWake(int current, DateTime deadline)
    {
        queue.PostAfter(TimeSpan.FromSeconds(2), () =>
        {
            if (!IsCurrent(current)) return;
            if (ConsoleDiscovery.Discover(console.Host, TimeSpan.FromSeconds(2)).Any(c => c.IsAwake))
                OpenSession(current);
            else if (DateTime.UtcNow < deadline)
                WaitForWake(current, deadline);
            else
                ScheduleReconnect(current, new DisconnectReason(DisconnectKind.ConsoleAsleep));
        });
    }

    private void OpenSession(int current)
    {
        if (!IsCurrent(current) || box is not null) return;
        var started = SessionBox.Start(this, current, console, accountId, out var failure);
        if (started is null)
        {
            ScheduleReconnect(current, new DisconnectReason(DisconnectKind.Other, failure ?? "session could not be started"));
            return;
        }
        box = started;
    }

    /// <summary>يغلق الجلسة الحالية. الإغلاق يتضمن join، لذا يُجرى في الخلفية حتى لا ينتظر الطابور.</summary>
    private void CloseBox()
    {
        if (box is not { } closing) return;
        box = null;
        Task.Run(closing.TearDown);
    }

    /// <summary>يُستدعى من خيط المكتبة الأصلية.</summary>
    private void Handle(int eventType, int quitReason, string? text, int eventGeneration)
    {
        switch (eventType)
        {
            case NativeMethods.EventConnected:
                queue.Post(() =>
                {
                    if (box?.Generation != eventGeneration) return;
                    policy.Reset();
                    Emit(SessionState.Streaming);
                });
                break;
            case NativeMethods.EventLoginPin:
                // لا نرسل مدخلات ذراع التحكم فلا يمكننا إدخال الرمز؛ وإعادة المحاولة تنتهي إلى النتيجة نفسها.
                queue.Post(() =>
                {
                    if (box?.Generation != eventGeneration) return;
                    Bump(running: false);
                    CloseBox();
                    Emit(SessionState.Disconnected(DisconnectKind.LoginPasscode));
                });
                break;
            case NativeMethods.EventQuit:
                var reason = DisconnectReasons.FromNative(quitReason, text);
                RemotePlayLog.Write(1, $"أُغلقت الجلسة: السبب {quitReason} ({reason.Kind}) {text}");
                queue.Post(() =>
                {
                    // حدث إغلاق جلسة قديمة (مثل التي أُغلقت بـ ReconnectNow) لا يؤثر في الجلسة الجديدة.
                    if (box?.Generation != eventGeneration) return;
                    CloseBox();
                    ScheduleReconnect(eventGeneration, reason);
                });
                break;
        }
    }

    private void ScheduleReconnect(int current, DisconnectReason reason)
    {
        Emit(new SessionState(SessionPhase.Disconnected, reason));
        if (!IsCurrent(current)) return;
        var delay = policy.NextDelay();
        queue.PostAfter(TimeSpan.FromSeconds(delay), () => Connect(current));
    }

    private void Emit(SessionState state)
    {
        try { StateChanged?.Invoke(state); }
        catch (Exception error) { RemotePlayLog.Write(0, $"StateChanged handler: {error}"); }
    }

    /// <summary>مالك جلسة أصلية واحدة. تصل ردود النداء إلى هذا الصندوق؛ ويبقى الصندوق (مع GCHandle الخاص به) حيًّا
    /// حتى تُوقَف الجلسة ويُنتظر انتهاء خيوطها، فلا يقع رد نداء على كائن محرَّر.</summary>
    private sealed class SessionBox
    {
        public int Generation { get; }
        private readonly StreamSession owner;
        private readonly Lock tearLock = new();
        private GCHandle handle;
        private nint native;
        private string? lastError;

        private SessionBox(StreamSession owner, int generation)
        {
            this.owner = owner;
            Generation = generation;
            handle = GCHandle.Alloc(this);
        }

        public static unsafe SessionBox? Start(StreamSession owner, int generation, RegisteredConsole console, byte[] accountId, out string? failure)
        {
            var box = new SessionBox(owner, generation);
            failure = null;
            if (!NativeMethods.IsAvailable)
            {
                failure = "تعذر العثور على مكتبة gametranslator_native (DLL). تحقق من Chiaki أو المكونات المرتبطة به.";
                return null;
            }
            try
            {
                NativeMethods.EnsureInitialized();
                var registKey = NativeMethods.Padded(console.RegistKey, 0x10);
                var morning = NativeMethods.Padded(console.Morning, 0x10);
                delegate* unmanaged[Cdecl]<byte*, int, int, int, nint, void> frame = &OnFrame;
                delegate* unmanaged[Cdecl]<int, int, byte*, nint, void> eventCallback = &OnEvent;
                delegate* unmanaged[Cdecl]<int, byte*, nint, void> log = &OnLog;
                fixed (byte* key = registKey)
                fixed (byte* morningPointer = morning)
                fixed (byte* account = accountId)
                    box.native = NativeMethods.cs_session_start(console.Host, key, morningPointer, account, MaxFps,
                                                                (nint)frame, (nint)eventCallback, (nint)log,
                                                                GCHandle.ToIntPtr(box.handle));
            }
            catch (Exception error)
            {
                box.lastError = error.Message;
            }
            if (box.native != 0) return box;
            // الجلسة التي لم تبدأ لا خيوط لها تُنتظر؛ يُحرَّر المقبض فقط.
            failure = box.lastError;
            box.handle.Free();
            return null;
        }

        /// <summary>يوقف الجلسة وينتظر خيوطها. يجب ألا يُستدعى من رد نداء أصلي.</summary>
        public void TearDown()
        {
            lock (tearLock)
            {
                if (native == 0) return;
                NativeMethods.cs_session_stop(native);
                native = 0;
                handle.Free();
            }
        }

        private static SessionBox? From(nint user) => user == 0 ? null : GCHandle.FromIntPtr(user).Target as SessionBox;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static unsafe void OnFrame(byte* bgra, int width, int height, int stride, nint user)
        {
            try
            {
                if (From(user) is { } box && bgra != null && width > 0 && height > 0)
                    box.owner.delivery.Offer((nint)bgra, width, height, stride);
            }
            catch (Exception error) { RemotePlayLog.Write(0, $"frame: {error.Message}"); }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static unsafe void OnEvent(int eventType, int quitReason, byte* reasonText, nint user)
        {
            try
            {
                if (From(user) is { } box)
                    box.owner.Handle(eventType, quitReason, NativeMethods.Utf8(reasonText), box.Generation);
            }
            catch (Exception error) { RemotePlayLog.Write(0, $"event: {error.Message}"); }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static unsafe void OnLog(int level, byte* message, nint user)
        {
            var text = NativeMethods.Utf8(message);
            // إذا تعذر بدء الجلسة فالسبب هو آخر سطر خطأ (cs_session_start يكتبه في الخيط نفسه).
            if (level == 0 && text is not null && From(user) is { } box) box.lastError = text;
            RemotePlayLog.Write(level, text);
        }
    }
}

/// <summary>يأخذ الإطارات من الخيط الأصلي ويوزّعها في خيطه الخاص. الإطارات الواردة أثناء انشغال المعالج
/// تُتخطى دون نسخ: فلا ينتظر خيط الشبكة القراءة ولا تُحجز ذاكرة بلا داعٍ.</summary>
internal sealed class FrameDelivery : IDisposable
{
    private readonly Action<Frame> deliver;
    // كائن عادي لا Lock لأن Monitor.Wait/Pulse مطلوبان.
    private readonly object gate = new();
    private readonly Thread thread;
    private Frame? pending;
    private bool busy;
    private bool closed;

    public FrameDelivery(Action<Frame> deliver)
    {
        this.deliver = deliver;
        thread = new Thread(Run) { IsBackground = true, Name = "GameTranslator frames" };
        thread.Start();
    }

    /// <summary>المخزن المؤقت صالح طوال هذا الاستدعاء فقط.</summary>
    public void Offer(nint bgra, int width, int height, int stride)
    {
        lock (gate)
        {
            if (closed || busy || pending is not null) return;
        }
        var frame = Frame.Copy(bgra, width, height, stride);
        lock (gate)
        {
            if (closed || pending is not null) return;
            pending = frame;
            Monitor.Pulse(gate);
        }
    }

    private void Run()
    {
        while (true)
        {
            Frame frame;
            lock (gate)
            {
                while (pending is null && !closed) Monitor.Wait(gate);
                if (closed) return;
                frame = pending!;
                pending = null;
                busy = true;
            }
            try { deliver(frame); }
            catch (Exception error) { RemotePlayLog.Write(0, $"FrameReceived handler: {error}"); }
            lock (gate) busy = false;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            closed = true;
            pending = null;
            Monitor.Pulse(gate);
        }
    }
}
