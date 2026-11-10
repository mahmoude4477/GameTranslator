// SPDX-License-Identifier: AGPL-3.0-only

using System.Net;
using System.Text;

namespace GameTranslator.RemotePlay.Tests;

public class PsnAccountTests
{
    [Fact]
    public void CodeIsReadFromRedirect()
    {
        Assert.Equal("AbC123", PsnAccount.CodeFromRedirect(new Uri(PsnAccount.RedirectPrefix + "?code=AbC123&cid=x")));
    }

    [Fact]
    public void NonRedirectUrlHasNoCode()
    {
        Assert.Null(PsnAccount.CodeFromRedirect(new Uri("https://example.com/redirect?code=AbC123")));
    }

    [Fact]
    public void UserIdBecomesEightLittleEndianBytes()
    {
        Assert.Equal("AQAAAAAAAAA=", Convert.ToBase64String(PsnAccount.AccountIdBytes("1")!));
        Assert.Equal(new byte[] { 2, 1, 0, 0, 0, 0, 0, 0 }, PsnAccount.AccountIdBytes("258"));
    }

    [Fact]
    public void InvalidUserIdIsRejected()
    {
        Assert.Null(PsnAccount.AccountIdBytes("abc"));
        Assert.Null(PsnAccount.AccountIdBytes(""));
        Assert.Null(PsnAccount.AccountIdBytes("-1"));
    }

    [Fact]
    public void AuthorizeUrlPointsToSonyWithRemotePlayRedirect()
    {
        var url = PsnAccount.AuthorizeUrl.AbsoluteUri;
        Assert.StartsWith("https://auth.api.sonyentertainmentnetwork.com/2.0/oauth/authorize?", url);
        Assert.Contains("redirect_uri=https://remoteplay.dl.playstation.net/remoteplay/redirect", url);
        Assert.Contains("scope=psn:clientapp%20referenceDataService", url);
        Assert.Contains("client_id=ba495a24-818c-472b-b12d-ff231c1b5745", url);
    }

    private sealed class FakeSony(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public readonly List<(HttpRequestMessage Request, string? Body)> Requests = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request, request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
            return respond(request);
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task CodeIsExchangedForAccountId()
    {
        var sony = new FakeSony(request => request.Method == HttpMethod.Post
            ? Json("""{"access_token":"tok123"}""")
            : Json("""{"user_id":"258"}"""));
        var id = await PsnAccount.AccountIdAsync("the-code", new HttpClient(sony), CancellationToken.None);

        Assert.Equal(new byte[] { 2, 1, 0, 0, 0, 0, 0, 0 }, id);
        var (token, body) = sony.Requests[0];
        Assert.Equal(PsnAccount.TokenUrl, token.RequestUri!.AbsoluteUri);
        Assert.Equal("Basic", token.Headers.Authorization!.Scheme);
        Assert.Equal("application/x-www-form-urlencoded", token.Content!.Headers.ContentType!.MediaType);
        Assert.StartsWith("grant_type=authorization_code&code=the-code&scope=psn:clientapp%20", body);
        Assert.Equal(PsnAccount.TokenUrl + "/tok123", sony.Requests[1].Request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task HttpErrorsAreReported()
    {
        var sony = new FakeSony(_ => Json("{}", HttpStatusCode.BadRequest));
        var error = await Assert.ThrowsAsync<PsnAccountException>(() => PsnAccount.AccountIdAsync("x", new HttpClient(sony), CancellationToken.None));
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task MissingTokenIsBadResponse()
    {
        var sony = new FakeSony(_ => Json("""{"error":"invalid_grant"}"""));
        var error = await Assert.ThrowsAsync<PsnAccountException>(() => PsnAccount.AccountIdAsync("x", new HttpClient(sony), CancellationToken.None));
        Assert.Null(error.StatusCode);
    }
}
