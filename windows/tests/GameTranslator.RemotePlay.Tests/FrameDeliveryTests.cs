// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core;

namespace GameTranslator.RemotePlay.Tests;

public class FrameDeliveryTests
{
    private static unsafe void Offer(FrameDelivery delivery, byte[] pixels, int width, int height, int stride)
    {
        fixed (byte* pointer = pixels) delivery.Offer((nint)pointer, width, height, stride);
    }

    [Fact]
    public async Task FramesAreCopiedAndSkippedWhileHandlerIsBusy()
    {
        var release = new ManualResetEventSlim();
        var received = new List<Frame>();
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var delivery = new FrameDelivery(frame =>
        {
            lock (received) received.Add(frame);
            if (!first.Task.IsCompleted)
            {
                first.SetResult();
                release.Wait();
            }
            else
            {
                second.TrySetResult();
            }
        });

        // إطار 2x2، 12 بايتا لكل سطر (4 بايتات حشو).
        byte[] pixels = [1, 2, 3, 4, 5, 6, 7, 8, 0, 0, 0, 0, 9, 10, 11, 12, 13, 14, 15, 16, 0, 0, 0, 0];
        Offer(delivery, pixels, 2, 2, 12);
        await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Offer(delivery, pixels, 2, 2, 12); // المعالج مشغول: يُتخطى
        release.Set();
        await Task.Delay(100);
        Offer(delivery, pixels, 2, 2, 12);
        await second.Task.WaitAsync(TimeSpan.FromSeconds(5));

        lock (received)
        {
            Assert.Equal(2, received.Count);
            Assert.Equal(8, received[0].Stride);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }, received[0].Pixels);
        }
    }
}
