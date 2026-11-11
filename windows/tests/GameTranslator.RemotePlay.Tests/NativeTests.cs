// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;

namespace GameTranslator.RemotePlay.Tests;

/// <summary>اختبارات تعمل بالمكتبة الأصلية (على Windows في CI؛ تُتخطى إن لم توجد DLL).</summary>
public class NativeTests
{
    [NativeFact]
    public async Task DiscoveryOfALoopbackAddressFindsNothingAndReturns()
    {
        var watch = Stopwatch.StartNew();
        var found = await ConsoleDiscovery.DiscoverAsync("127.0.0.1", TimeSpan.FromMilliseconds(600));
        Assert.Empty(found);
        Assert.InRange(watch.Elapsed.TotalSeconds, 0.4, 5);
    }

    [NativeFact]
    public async Task RegistrationWithUnreachableConsoleFails()
    {
        var error = await Assert.ThrowsAsync<RegistrationException>(() => Registration.RegisterAsync("127.0.0.1", new byte[8], 12345678));
        Assert.Equal(RegistrationFailure.NoAnswer, error.Failure);
    }

    /// <summary>الإيقاف والتحرير أثناء انتظار إعادة المحاولة على عنوان يتعذّر الاتصال به يجب ألا يسبب انهيارا ولا تعليقا.</summary>
    [NativeFact]
    public async Task StopWhileReconnectingDoesNotCrashOrBlock()
    {
        var console = new RegisteredConsole("127.0.0.1", "test", Enumerable.Repeat((byte)0x31, 16).ToArray(), Enumerable.Repeat((byte)1, 16).ToArray());
        var session = new StreamSession(console, Enumerable.Repeat((byte)1, 8).ToArray());
        var disconnected = new TaskCompletionSource<SessionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.StateChanged += state =>
        {
            if (state.Phase == SessionPhase.Disconnected) disconnected.TrySetResult(state);
        };
        session.Start();
        var state = await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.NotEqual(DisconnectKind.Stopped, state.Reason!.Kind);

        var watch = Stopwatch.StartNew();
        session.Stop();
        Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(200));
        session.Dispose();
        // انتظر حتى يمر الوقت الذي كانت ستعمل فيه إعادة المحاولة المعلّقة؛ إن بقيت العملية حية فلا انهيار.
        await Task.Delay(TimeSpan.FromSeconds(3));
    }

    [NativeFact]
    public void WakeWithoutCredentialIsRejected()
    {
        var console = new RegisteredConsole("127.0.0.1", "test", new byte[16], new byte[16]);
        Assert.Throws<InvalidOperationException>(() => ConsoleDiscovery.Wake(console));
    }
}
