// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core.LgTv;
using Xunit;
using Tv = GameTranslator.Core.LgTv.LgTv;

namespace GameTranslator.Core.Tests;

public class TvLocatorTests
{
    private sealed class Control : ITvControl
    {
        public Task LaunchAsync(string appId, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> ForegroundAppIdAsync(CancellationToken cancellationToken = default) => Task.FromResult("");
        public void Dispose() { }
    }

    private static readonly Tv Saved = new("Salon", "192.168.1.153");

    [Fact]
    public async Task KeepsSavedAddressWhileItWorks()
    {
        var searches = 0;
        var locator = new TvLocator(Saved, _ => Task.FromResult<ITvControl>(new Control()),
            () => { searches++; return Task.FromResult<IReadOnlyList<Tv>>([]); }, _ => { });
        await locator.ConnectAsync();
        Assert.Equal(0, searches);
    }

    [Fact]
    public async Task FollowsTvToItsNewAddressAndReportsIt()
    {
        var moved = new Tv("Salon", "192.168.1.230");
        var tried = new List<string>();
        Tv? reported = null;
        var locator = new TvLocator(Saved, tv =>
        {
            tried.Add(tv.Host);
            return tv.Host == Saved.Host ? throw new IOException() : Task.FromResult<ITvControl>(new Control());
        }, () => Task.FromResult<IReadOnlyList<Tv>>([moved]), tv => reported = tv);

        await locator.ConnectAsync();
        Assert.Equal(["192.168.1.153", "192.168.1.230"], tried);
        Assert.Equal(moved, reported);

        await locator.ConnectAsync();
        Assert.Equal(3, tried.Count);
        Assert.Equal("192.168.1.230", tried[^1]);
    }

    [Fact]
    public async Task DoesNotSearchTheNetworkOnEveryFailure()
    {
        var searches = 0;
        var locator = new TvLocator(Saved, _ => throw new IOException(),
            () => { searches++; return Task.FromResult<IReadOnlyList<Tv>>([]); }, _ => { }, TimeSpan.FromSeconds(30));
        for (var i = 0; i < 5; i++) await Assert.ThrowsAsync<IOException>(locator.ConnectAsync);
        Assert.Equal(1, searches);
    }

    [Fact]
    public void PicksTheTvWithTheSameNameAmongSeveral()
    {
        var moved = new Tv("Salon", "192.168.1.230");
        Assert.Equal(moved, TvLocator.Moved(Saved, [new Tv("Yatak odası", "192.168.1.40"), moved]));
    }

    [Fact]
    public void AcceptsTheOnlyTvWhenItsNameDiffers()
    {
        var renamed = new Tv("LG TV", "192.168.1.230");
        Assert.Equal(renamed, TvLocator.Moved(Saved, [renamed]));
    }

    [Fact]
    public void DoesNotGuessAmongSeveralUnknownTvs() =>
        Assert.Null(TvLocator.Moved(Saved, [new Tv("A", "192.168.1.40"), new Tv("B", "192.168.1.41")]));

    [Fact]
    public void StaysPutWhenSavedAddressStillAnswersDiscovery() =>
        Assert.Null(TvLocator.Moved(Saved, [new Tv("Salon", Saved.Host)]));
}
