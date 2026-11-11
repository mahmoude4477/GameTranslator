// SPDX-License-Identifier: AGPL-3.0-only

using System.Net;
using System.Text;
using System.Text.Json;
using GameTranslator.Core;
using GameTranslator.Translation;
using Xunit;

namespace GameTranslator.Translation.Tests;

public class DeepLTranslatorTests
{
    /// <summary>خادم وهمي يقوم مقام DeepL: يحفظ الطلبات الواردة ويعيد الرد المعطى.</summary>
    private sealed class FakeDeepL(Func<HttpRequestMessage, string, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request, body));
            return reply(request, body);
        }
    }

    private sealed class FakeLocal : ITextTranslator
    {
        public List<string> Calls { get; } = [];
        public Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default)
        {
            Calls.Add(text);
            return Task.FromResult("محلي: " + text);
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>يترجم كل جملة إلى "[ar] الجملة".</summary>
    private static HttpResponseMessage Echo(HttpRequestMessage request, string body)
    {
        var texts = JsonDocument.Parse(body).RootElement.GetProperty("text").EnumerateArray().Select(t => t.GetString());
        return Json(JsonSerializer.Serialize(new { translations = texts.Select(t => new { text = "[ar] " + t }) }));
    }

    [Fact]
    public async Task SpeakerLabelStaysAndPreviousSubtitleIsContext()
    {
        var deepL = new FakeDeepL(Echo);
        var translator = new DeepLTranslator("abc:fx", new FakeLocal(), "ar", new HttpClient(deepL));

        Assert.Equal("KRATOS: [ar] We move on.", await translator.TranslateAsync("KRATOS: We move on."));
        await translator.TranslateAsync("Stay close.");

        var (first, firstBody) = deepL.Requests[0];
        Assert.Equal("https://api-free.deepl.com/v2/translate", first.RequestUri!.AbsoluteUri);
        Assert.Equal("DeepL-Auth-Key abc:fx", first.Headers.Authorization!.ToString());
        var json = JsonDocument.Parse(firstBody).RootElement;
        Assert.Equal("We move on.", json.GetProperty("text")[0].GetString());
        Assert.Equal("AR", json.GetProperty("target_lang").GetString());
        Assert.False(json.TryGetProperty("context", out _));
        Assert.Equal("KRATOS: We move on.", JsonDocument.Parse(deepL.Requests[1].Body).RootElement.GetProperty("context").GetString());
    }

    /// <summary>لغة الترجمة تُرسل إلى DeepL برمزها بأحرف كبيرة (العربية: "AR").</summary>
    [Fact]
    public async Task TargetLanguageIsSentToDeepL()
    {
        var deepL = new FakeDeepL(Echo);
        var translator = new DeepLTranslator("abc:fx", new FakeLocal(), "ar", new HttpClient(deepL));
        await translator.TranslateAsync("Stay close.");
        Assert.Equal("AR", JsonDocument.Parse(deepL.Requests[0].Body).RootElement.GetProperty("target_lang").GetString());
    }

    [Fact]
    public async Task SameSubtitleIsNotSentTwice()
    {
        var deepL = new FakeDeepL(Echo);
        var translator = new DeepLTranslator("abc", new FakeLocal(), "ar", new HttpClient(deepL));
        await translator.TranslateAsync("Who are you?");
        await translator.TranslateAsync("Who are you?");
        Assert.Single(deepL.Requests);
        Assert.StartsWith("https://api.deepl.com/", deepL.Requests[0].Request.RequestUri!.AbsoluteUri);
    }

    /// <summary>إذا انتهت الحصة أو تعذّر الوصول إلى DeepL لا تبقى اللعبة بلا ترجمة نصية؛ ولا يُجرَّب DeepL لفترة.</summary>
    [Fact]
    public async Task FallsBackToLocalModelWhenDeepLFails()
    {
        var deepL = new FakeDeepL((_, _) => new HttpResponseMessage((HttpStatusCode)456));
        var local = new FakeLocal();
        var translator = new DeepLTranslator("abc:fx", local, "ar", new HttpClient(deepL));

        Assert.Equal("محلي:Run!", await translator.TranslateAsync("Run!"));
        Assert.Equal(OnlineKeyState.QuotaExceeded, translator.LastError);
        Assert.Equal("محلي:Hide!", await translator.TranslateAsync("Hide!"));
        Assert.Single(deepL.Requests);
        Assert.Equal(["Run!", "Hide!"], local.Calls);
    }

    [Fact]
    public async Task KeyCheckReportsUsageAndRejectedKeys()
    {
        var usage = new FakeDeepL((_, _) => Json("""{"character_count":1200,"character_limit":500000}"""));
        Assert.Equal(new OnlineKeyCheck(OnlineKeyState.Valid, 1200, 500000), await DeepLTranslator.CheckAsync("k:fx", new HttpClient(usage)));
        Assert.EndsWith("/v2/usage", usage.Requests[0].Request.RequestUri!.AbsoluteUri);

        var rejected = new FakeDeepL((_, _) => new HttpResponseMessage(HttpStatusCode.Forbidden));
        Assert.Equal(OnlineKeyState.Invalid, (await DeepLTranslator.CheckAsync("k", new HttpClient(rejected))).State);

        var full = new FakeDeepL((_, _) => Json("""{"character_count":500000,"character_limit":500000}"""));
        Assert.Equal(OnlineKeyState.QuotaExceeded, (await DeepLTranslator.CheckAsync("k", new HttpClient(full))).State);

        Assert.Equal(OnlineKeyState.Invalid, (await DeepLTranslator.CheckAsync("  ")).State);
    }
}
