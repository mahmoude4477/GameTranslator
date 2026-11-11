// SPDX-License-Identifier: AGPL-3.0-only

using System.Net.Sockets;
using GameTranslator.Core.LgTv;
using GameTranslator.RemotePlay;
using GameTranslator.Shell;

namespace GameTranslator.Shell.Tests;

internal sealed class FakeFirewall : IFirewall
{
    public bool Rule { get; set; }
    /// <summary>تُضاف القاعدة إذا اختار المستخدم «نعم» في سؤال الإذن من Windows.</summary>
    public bool UserAccepts { get; set; } = true;
    public int Checks { get; private set; }
    public int Adds { get; private set; }

    public bool HasRule()
    {
        Checks++;
        return Rule;
    }

    public Task<bool> AddRuleAsync()
    {
        Adds++;
        if (UserAccepts) Rule = true;
        return Task.FromResult(Rule);
    }
}

/// لا يُقترح إذن جدار الحماية إلا عند وجود مشكلة وغياب القاعدة؛ فلا يُزعَج المستخدم عند كل تشغيل.
public class FirewallPairingTests
{
    private static async Task<(AppController Controller, FakeFirewall Firewall)> Pairing(Exception error, bool rule = false)
    {
        var firewall = new FakeFirewall { Rule = rule };
        var services = new FakeServices { RegisterError = error, Firewall = firewall };
        var controller = AppControllerTests.Make(services);
        await controller.CompletePsnLoginAsync(new Uri("https://x/?code=x"));
        controller.Step = OnboardingStep.Console;
        return (controller, firewall);
    }

    [Fact]
    public async Task FailedRegistrationOffersFirewallWhenRuleIsMissing()
    {
        var (controller, _) = await Pairing(new RegistrationException(RegistrationFailure.Failed));
        Assert.Equal(FirewallPrompt.None, controller.FirewallPrompt);
        var error = await Assert.ThrowsAsync<AppException>(() => controller.RegisterAsync("192.168.1.253", "12345678"));
        Assert.Equal(AppError.RegistrationFailed, error.Error);
        Assert.Equal(FirewallPrompt.Offer, controller.FirewallPrompt);
    }

    [Fact]
    public async Task FailedRegistrationWithRuleOffersNothing()
    {
        var (controller, _) = await Pairing(new RegistrationException(RegistrationFailure.Failed), rule: true);
        await Assert.ThrowsAsync<AppException>(() => controller.RegisterAsync("192.168.1.253", "12345678"));
        Assert.Equal(FirewallPrompt.None, controller.FirewallPrompt);
    }

    /// إذا كان الحساب خاطئًا فالمشكلة ليست جدار الحماية.
    [Fact]
    public async Task WrongAccountOffersNothing()
    {
        var (controller, firewall) = await Pairing(new RegistrationException(RegistrationFailure.WrongAccount));
        await Assert.ThrowsAsync<AppException>(() => controller.RegisterAsync("192.168.1.253", "12345678"));
        Assert.Equal(FirewallPrompt.None, controller.FirewallPrompt);
        Assert.Equal(0, firewall.Checks);
    }

    /// عندما لا يرد PS5 يفشل التسجيل بانتهاء المهلة؛ وهذا أيضًا من أعراض جدار الحماية.
    [Fact]
    public async Task NetworkErrorOffersFirewallAndKeepsTheError()
    {
        var (controller, _) = await Pairing(new SocketException());
        await Assert.ThrowsAsync<SocketException>(() => controller.RegisterAsync("192.168.1.253", "12345678"));
        Assert.Equal(FirewallPrompt.Offer, controller.FirewallPrompt);
    }

    [Fact]
    public async Task AllowingShowsConfirmationAndNextPairingClearsIt()
    {
        var (controller, firewall) = await Pairing(new RegistrationException(RegistrationFailure.Failed));
        await Assert.ThrowsAsync<AppException>(() => controller.RegisterAsync("192.168.1.253", "12345678"));
        Assert.True(await controller.AllowThroughFirewallAsync());
        Assert.Equal(1, firewall.Adds);
        Assert.Equal(FirewallPrompt.Allowed, controller.FirewallPrompt);
        controller.Step = OnboardingStep.Tv;
        Assert.Equal(FirewallPrompt.None, controller.FirewallPrompt);
    }

    [Fact]
    public async Task DeclinedPermissionKeepsTheButton()
    {
        var (controller, firewall) = await Pairing(new RegistrationException(RegistrationFailure.Failed));
        firewall.UserAccepts = false;
        await Assert.ThrowsAsync<AppException>(() => controller.RegisterAsync("192.168.1.253", "12345678"));
        Assert.False(await controller.AllowThroughFirewallAsync());
        Assert.Equal(FirewallPrompt.Failed, controller.FirewallPrompt);
    }

    [Fact]
    public async Task WithoutFirewallServiceNothingIsOffered()
    {
        var services = new FakeServices { RegisterError = new RegistrationException(RegistrationFailure.Failed) };
        var controller = AppControllerTests.Make(services);
        await controller.CompletePsnLoginAsync(new Uri("https://x/?code=x"));
        await Assert.ThrowsAsync<AppException>(() => controller.RegisterAsync("192.168.1.253", "12345678"));
        Assert.Equal(FirewallPrompt.None, controller.FirewallPrompt);
        Assert.False(await controller.AllowThroughFirewallAsync());
    }
}

public class FirewallRunningTests
{
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    private sealed class Clock
    {
        public DateTime Now { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public void Advance(double seconds) => Now = Now.AddSeconds(seconds);
    }

    private static async Task<(AppController Controller, FakeServices Services, FakeFirewall Firewall, Clock Clock)> Running(bool rule = false)
    {
        var firewall = new FakeFirewall { Rule = rule };
        var services = new FakeServices { Firewall = firewall };
        var store = new MemoryStore();
        store.Save(StoreKeys.AccountId, Enumerable.Repeat((byte)1, 8).ToArray());
        store.Save(StoreKeys.Console, new RegisteredConsole("192.168.1.253", "PS5", Enumerable.Repeat((byte)0x31, 16).ToArray(), Enumerable.Repeat((byte)2, 16).ToArray()));
        store.Save(StoreKeys.Tv, new LgTv("LG TV", "127.0.0.1"));
        store.Save(StoreKeys.TvKey, "tv-key");
        var clock = new Clock();
        var controller = AppControllerTests.Make(services, store);
        controller.Clock = () => clock.Now;
        await controller.StartAsync();
        return (controller, services, firewall, clock);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "لم يتحقق الشرط في الوقت المحدد");
            await Task.Delay(20);
        }
    }

    /// يتصل بالخادم بدلًا من تطبيق التلفزيون.
    private static async Task<HttpResponseMessage> ConnectTv(AppController controller)
    {
        var response = await Http.GetAsync($"http://127.0.0.1:{controller.ServerPort}/events", HttpCompletionOption.ResponseHeadersRead);
        await WaitUntil(() => controller.TvClients == 1);
        return response;
    }

    [Fact]
    public async Task ReconnectingPs5OffersFirewallAfterTwentySeconds()
    {
        var (controller, services, _, clock) = await Running();
        using var tv = await ConnectTv(controller);
        services.Streams[0].Emit(SessionState.Disconnected(DisconnectKind.Network));
        clock.Advance(19);
        await controller.CheckConnectionsAsync();
        Assert.Equal(FirewallPrompt.None, controller.FirewallPrompt);
        clock.Advance(2);
        await controller.CheckConnectionsAsync();
        Assert.Equal(FirewallPrompt.Offer, controller.FirewallPrompt);
        controller.Dispose();
    }

    [Fact]
    public async Task CardDisappearsWhenPs5AndTvConnect()
    {
        var (controller, services, _, clock) = await Running();
        using var tv = await ConnectTv(controller);
        services.Streams[0].Emit(SessionState.Connecting);
        clock.Advance(30);
        await controller.CheckConnectionsAsync();
        Assert.Equal(FirewallPrompt.Offer, controller.FirewallPrompt);
        services.Streams[0].Emit(SessionState.Streaming);
        await controller.CheckConnectionsAsync();
        Assert.Equal(FirewallPrompt.None, controller.FirewallPrompt);
        controller.Dispose();
    }

    [Fact]
    public async Task TvWaitingOffersFirewallAfterSixtySeconds()
    {
        var (controller, services, _, clock) = await Running();
        services.Streams[0].Emit(SessionState.Streaming);
        clock.Advance(50);
        await controller.CheckConnectionsAsync();
        Assert.Equal(FirewallPrompt.None, controller.FirewallPrompt);
        clock.Advance(11);
        await controller.CheckConnectionsAsync();
        Assert.Equal(FirewallPrompt.Offer, controller.FirewallPrompt);
        controller.Dispose();
    }

    /// إذا كان PS5 في وضع الراحة أو مستخدمًا على جهاز آخر فالمشكلة ليست جدار الحماية.
    [Fact]
    public async Task AsleepPs5OffersNothing()
    {
        var (controller, services, firewall, clock) = await Running();
        using var tv = await ConnectTv(controller);
        services.Streams[0].Emit(SessionState.Disconnected(DisconnectKind.ConsoleAsleep));
        clock.Advance(120);
        await controller.CheckConnectionsAsync();
        Assert.Equal(FirewallPrompt.None, controller.FirewallPrompt);
        Assert.Equal(0, firewall.Checks);
        controller.Dispose();
    }

    [Fact]
    public async Task ExistingRuleOffersNothingAndIsCheckedOnce()
    {
        var (controller, _, firewall, clock) = await Running(rule: true);
        clock.Advance(120);
        await controller.CheckConnectionsAsync();
        await controller.CheckConnectionsAsync();
        Assert.Equal(FirewallPrompt.None, controller.FirewallPrompt);
        Assert.Equal(1, firewall.Checks);
        controller.Dispose();
    }

    [Fact]
    public async Task AllowingReconnectsPs5AndTv()
    {
        var (controller, services, _, clock) = await Running();
        clock.Advance(61);
        await controller.CheckConnectionsAsync();
        Assert.Equal(FirewallPrompt.Offer, controller.FirewallPrompt);
        Assert.True(await controller.AllowThroughFirewallAsync());
        Assert.Equal(FirewallPrompt.Allowed, controller.FirewallPrompt);
        Assert.Equal(1, services.Streams[0].Reconnects);
        Assert.Equal(1, services.Keepers[0].Reconnects);
        // إن لم يتصل بعد يبقى التأكيد، ولا تُقترح البطاقة من جديد.
        clock.Advance(120);
        await controller.CheckConnectionsAsync();
        Assert.Equal(FirewallPrompt.Allowed, controller.FirewallPrompt);
        controller.Dispose();
    }

    [Fact]
    public async Task StoppingHidesTheCard()
    {
        var (controller, _, _, clock) = await Running();
        clock.Advance(61);
        await controller.CheckConnectionsAsync();
        Assert.Equal(FirewallPrompt.Offer, controller.FirewallPrompt);
        controller.Stop();
        Assert.Equal(FirewallPrompt.None, controller.FirewallPrompt);
        clock.Advance(120);
        await controller.CheckConnectionsAsync();
        Assert.Equal(FirewallPrompt.None, controller.FirewallPrompt);
        controller.Dispose();
    }
}
