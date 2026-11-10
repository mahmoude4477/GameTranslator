// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core;
using Xunit;

namespace GameTranslator.Core.Tests;

public class ReadSchedulerTests
{
    private static byte[] Still() => Enumerable.Repeat((byte)40, 16).ToArray();

    [Fact]
    public void FirstFrameIsAlwaysRead() => Assert.Equal(ReadPlan.Read(true), new ReadScheduler().Plan(Still()));

    [Fact]
    public void UnchangedRegionReusesTheLastReading()
    {
        var scheduler = new ReadScheduler();
        scheduler.Plan(Still());
        scheduler.Record("Hello there");
        Assert.Equal(ReadPlan.Reused("Hello there"), scheduler.Plan(Still()));
    }

    [Fact]
    public void VideoNoiseCountsAsUnchanged()
    {
        var scheduler = new ReadScheduler();
        scheduler.Plan(Still());
        scheduler.Record("");
        var noisy = Still();
        noisy[3] += 8;
        Assert.Equal(ReadPlan.Reused(""), scheduler.Plan(noisy));
    }

    [Fact]
    public void OneChangedSampleTriggersAReading()
    {
        var scheduler = new ReadScheduler();
        scheduler.Plan(Still());
        scheduler.Record("Hel");
        var typed = Still();
        typed[3] += 40;
        Assert.Equal(ReadPlan.Read(false), scheduler.Plan(typed));
    }

    [Fact]
    public void ReusedReadingIsRefreshedAfterALimit()
    {
        var scheduler = new ReadScheduler(maxReuse: 2, deepEvery: 2);
        scheduler.Plan(Still());
        scheduler.Record("a line");
        Assert.True(scheduler.Plan(Still()).Reuse);
        Assert.True(scheduler.Plan(Still()).Reuse);
        Assert.Equal(ReadPlan.Read(false), scheduler.Plan(Still()));
    }

    [Fact]
    public void SlowerSearchRunsOnlyOnEveryThirdEmptyFrame()
    {
        var scheduler = new ReadScheduler(deepEvery: 3);
        var deep = new List<bool>();
        for (var value = 0; value < 6; value++)
        {
            deep.Add(scheduler.Plan(Enumerable.Repeat((byte)(value * 40), 16).ToArray()).Deep);
            scheduler.Record("");
        }
        Assert.Equal([true, false, false, true, false, false], deep);
    }

    [Fact]
    public void ResetForgetsThePreviousFrame()
    {
        var scheduler = new ReadScheduler();
        scheduler.Plan(Still());
        scheduler.Record("old");
        scheduler.Reset();
        Assert.Equal(ReadPlan.Read(true), scheduler.Plan(Still()));
    }

    [Fact]
    public void AlwaysDisablesReuse()
    {
        var scheduler = ReadScheduler.Always();
        scheduler.Plan(Still());
        scheduler.Record("");
        Assert.Equal(ReadPlan.Read(true), scheduler.Plan(Still()));
    }

    [Fact]
    public void SignatureHasAFixedSizeAndFollowsBrightness()
    {
        static Frame Filled(byte value)
        {
            var frame = Frame.Blank(640, 120);
            Array.Fill(frame.Pixels, value);
            return frame;
        }
        Assert.Equal(64 * 12, RegionCropper.Signature(Filled(50)).Length);
        Assert.Equal(RegionCropper.Signature(Filled(50)), RegionCropper.Signature(Filled(50)));
        Assert.NotEqual(RegionCropper.Signature(Filled(50)), RegionCropper.Signature(Filled(200)));
    }
}
