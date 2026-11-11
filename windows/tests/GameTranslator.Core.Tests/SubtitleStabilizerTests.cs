// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core;

namespace GameTranslator.Core.Tests;

public class SubtitleStabilizerTests
{
    /// السطر الذي لا ينتهي بعلامة ترقيم قد يكون قيد الكتابة؛ يُرسل عندما يُقرأ إطاران بالنص نفسه.
    [Fact]
    public void UnfinishedLineIsSentAfterTwoStableFrames()
    {
        var s = new SubtitleStabilizer();
        Assert.Equal(StabilizerAction.None, s.Feed("We do not have time"));
        Assert.Equal(StabilizerAction.Translate("We do not have time"), s.Feed("We do not have time"));
        Assert.Equal(StabilizerAction.None, s.Feed("We do not have time"));
    }

    /// في الخط المزخرف تُقرأ الجملة نفسها في كل إطار برموز عشوائية مختلفة؛ يجب ألا تظهر على التلفزيون كترجمتين.
    [Fact]
    public void GarbledRereadOfSentLineDoesNotReemit()
    {
        var s = new SubtitleStabilizer();
        const string first = "Across the deep waters, Pbo went,\nhoping a boat would carry him home.";
        Assert.Equal(StabilizerAction.Translate(first), s.Feed(first));
        Assert.Equal(StabilizerAction.None, s.Feed("Across the deep waters O went,"));
        Assert.Equal(StabilizerAction.None, s.Feed("Across the deep waters O went,"));
        Assert.Equal(StabilizerAction.Translate("We should keep moving."), s.Feed("We should keep moving."));
    }

    /// إذا اكتملت الجملة فلا حاجة للانتظار؛ في الحوار السريع كل إطار مهم.
    [Theory]
    [InlineData("We do not have time.")]
    [InlineData("Wait!")]
    [InlineData("Who are you?")]
    [InlineData("I know…")]
    [InlineData("\"Run.\"")]
    public void FinishedSentenceIsSentOnFirstFrame(string line)
    {
        var s = new SubtitleStabilizer();
        Assert.Equal(StabilizerAction.Translate(line), s.Feed(line));
        Assert.Equal(StabilizerAction.None, s.Feed(line));
    }

    [Fact]
    public void OcrNoiseDoesNotReemit()
    {
        var s = new SubtitleStabilizer();
        s.Feed("The gate will not hold"); s.Feed("The gate will not hold");
        Assert.Equal(StabilizerAction.None, s.Feed("The gale will not hold"));
        Assert.Equal(StabilizerAction.None, s.Feed("The gale will not hold"));
    }

    [Fact]
    public void FlickeringHudDoesNotSpam()
    {
        var s = new SubtitleStabilizer();
        var sent = 0;
        for (var i = 0; i < 20; i++)
            if (s.Feed(i % 2 == 0 ? "24 / 120" : "").Kind == StabilizerKind.Translate) sent++;
        Assert.Equal(0, sent);
    }

    [Fact]
    public void TypewriterSendsCompletedLine()
    {
        var s = new SubtitleStabilizer();
        s.Feed("We do"); s.Feed("We do not have");
        Assert.Equal(StabilizerAction.Translate("We do not have time for this, boy."), s.Feed("We do not have time for this, boy."));
        Assert.Equal(StabilizerAction.None, s.Feed("We do not have time for this, boy."));
    }

    [Fact]
    public void TwoEmptyFramesClearAfterSend()
    {
        var s = new SubtitleStabilizer();
        s.Feed("Hello there"); s.Feed("Hello there");
        Assert.Equal(StabilizerAction.None, s.Feed(""));
        Assert.Equal(StabilizerAction.Clear, s.Feed(""));
    }

    [Fact]
    public void AlreadyTranslatedIsSkipped()
    {
        var s = new SubtitleStabilizer();
        Assert.False(s.ShouldSend("إضافة مستخدم تم تسجيل الدخول", "إضافة مستخدم تم تسجيل الدخول"));
        Assert.True(s.ShouldSend("Add user", "إضافة مستخدم"));
    }

    [Fact]
    public void ResetClearsOnlyAfterSomethingWasSent()
    {
        var s = new SubtitleStabilizer();
        Assert.Equal(StabilizerAction.None, s.Reset());
        s.Feed("Hello there"); s.Feed("Hello there");
        Assert.Equal(StabilizerAction.Clear, s.Reset());
        Assert.Equal(StabilizerAction.None, s.Reset());
    }

    /// السطر الذي فشلت ترجمته يجب أن تُعاد محاولته ما دام على الشاشة.
    [Fact]
    public void ForgottenLineIsTranslatedAgain()
    {
        var s = new SubtitleStabilizer();
        s.Feed("Hold the gate");
        Assert.Equal(StabilizerAction.Translate("Hold the gate"), s.Feed("Hold the gate"));
        s.ForgetSent();
        Assert.Equal(StabilizerAction.None, s.Feed("Hold the gate"));
        Assert.Equal(StabilizerAction.Translate("Hold the gate"), s.Feed("Hold the gate"));
    }
}
