/*---------------------------------------------------------------------------------------------
 * Copyright (c) 2026 unicbm. All rights reserved.
 * Licensed under the GNU Affero General Public License v3.0 only.
 * See LICENSE in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace DemoTracer.Tests;

public sealed class ReplayNativeMapperTests
{
    // Reject the old same-major transports before starting or reporting healthy playback.
    [Theory]
    [InlineData(576, 256, true)]
    [InlineData(416, 256, false)]
    [InlineData(380, 256, false)]
    [InlineData(0, 228, false)]
    public void CompatibilityRequiresTheCompleteNativeFrameLayout(int frameSize, int tickSize, bool expected)
    {
        var info = BotControllerAbiInfo.Unavailable;
        info.AbiMajor = 23;
        info.ReplayFrameSize = frameSize;
        info.ReplayTickSize = tickSize;
        info.ReplayCommandSize = 68;
        Assert.Equal(expected, BotControllerNative.HasCompatibleLayout(info));
        Assert.False(BotControllerNative.HasCompatibleLayout(BotControllerAbiInfo.Unavailable));
    }

    // Sparse changes carry forward, and an explicit absence restores null rather than zero.
    [Fact]
    public void SourceModifiersPreservePresenceAcrossFrames()
    {
        var replay = new DtrReplayFile(12, new NativeReplayTick[3], [],
            ReplayHighFidelityMetadata.Empty, [], [], [], [], [], 64, 0)
        {
            SourceState = [
                new() { TickIndex = 0, FieldId = 6, ValueBits = 0, Present = 1 },
                new() { TickIndex = 0, FieldId = 34, ValueBits = 0, Present = 1 },
                new() { TickIndex = 2, FieldId = 6, ValueBits = 0, Present = 0 }
            ]
        };
        var frames = ReplayNativeMapper.BuildPlaybackData(replay, true).Frames;
        Assert.Equal(0f, frames[0].Pre.Stamina);
        Assert.Equal(0f, frames[1].Pre.Stamina);
        Assert.Null(frames[2].Pre.Stamina);
        Assert.All(frames, frame =>
        {
            Assert.False(frame.Pre.GravityDisabled);
            Assert.Null(frame.Pre.VelocityModifier);
            Assert.Null(frame.SourcePlayerTick);
        });
    }

    // Compact player-clock runs increase only within the declared run, then retain their last tick.
    [Fact]
    public void SourcePlayerClockUsesItsCompactRunInsteadOfDemoTick()
    {
        var replay = new DtrReplayFile(12, new NativeReplayTick[3], [],
            ReplayHighFidelityMetadata.Empty, [], [], [], [], [], 64, 0)
        {
            SourceState = [
                new() { TickIndex = 0, FieldId = 1, ValueBits = 100, Present = 3 },
                new() { TickIndex = 0, FieldId = 4, ValueBits = 0, Present = 1 }
            ]
        };
        var frames = ReplayNativeMapper.BuildPlaybackData(replay, true).Frames;
        Assert.Equal([100, 101, 101], frames.Select(frame => frame.SourcePlayerTick!.Value));
        Assert.All(frames, frame => Assert.Equal(0f, frame.Pre.LastDuckTime));
    }

    [Fact]
    public void InventoryPlanningKeepsFirstAppearanceOrderWithoutPerTickDuplicates()
    {
        var replay = new DtrReplayFile(11,
            new[] { -1, 7, 7, 43, 7, 43, 9 }.Select(def => new NativeReplayTick { WeaponDefIndex = def }).ToArray(),
            [], ReplayHighFidelityMetadata.Empty, [], [], [], [], [], 64, 2);
        var metadata = ReplayNativeMapper.BuildMetadata(replay);
        Assert.Equal(new[] { -1, 7, 43, 9 }, metadata.WeaponDefIndices);
        Assert.Equal(7, metadata.TickCount);
        Assert.Equal(2U, metadata.PlayStartTickIndex);
        var prepared = replay with { PreparedMetadata = metadata };
        Assert.Same(metadata.WeaponDefIndices, ReplayNativeMapper.BuildMetadata(prepared).WeaponDefIndices);
    }

    [Fact]
    public void PrefetchBudgetIncludesSourceStateAndRetainedInputHistory()
    {
        var empty = new DtrReplayFile(11, [], [], ReplayHighFidelityMetadata.Empty, [], [], [], [], [], 64, 0);
        var replay = empty with
        {
            SourceState = new NativeReplaySourceStateChange[10],
            InputHistoryTicks = new NativeReplayInputHistoryTick[2],
            InputHistoryEntries = new NativeReplayInputHistoryEntry[3],
        };
        Assert.Equal(10 * 16 + 2 * 16 + 3 * 128, DtrReplayPrefetch.EstimateReplayBytes(replay));
    }
}
