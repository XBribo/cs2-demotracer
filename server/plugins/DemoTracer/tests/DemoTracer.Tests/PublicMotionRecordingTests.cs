/*---------------------------------------------------------------------------------------------
 * Copyright (c) 2026 unicbm. All rights reserved.
 * Licensed under the GNU Affero General Public License v3.0 only.
 * See LICENSE in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using BotControllerImpl;
using BotControllerApi;
using System.IO.Compression;
using System.Text;

namespace DemoTracer.Tests;

public sealed class PublicMotionRecordingTests
{
    [Fact]
    public void LegacyMotionJsonPreservesItsDataInUnifiedFrames()
    {
        var recording = LoadJson("""
            {"Tickrate":64,"Ticks":[{"WeaponDefIndex":7,"Pre":{"OriginX":128.25}}],
             "Subticks":[],"Commands":[{"ForwardMove":0.5,"Fields":257}]}
            """);
        var frame = Assert.Single(recording.Frames);
        Assert.Equal(64, recording.TickRate);
        Assert.Equal(7, frame.WeaponDefIndex);
        Assert.Equal(128.25f, frame.Pre.OriginX);
        Assert.Equal(0.5f, frame.Input.ForwardMove);
        Assert.Null(frame.Drop);
    }

    [Fact]
    public void LegacyDropWithoutReleasePoseIsRejectedBeforeNativeLoad()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            LoadJson("""
                {"Tickrate":64,"Ticks":[{"EventFlags":1}],"Subticks":[],"Commands":[{"Fields":256}]}
                """));
        Assert.Contains("drop release pose", exception.Message);
    }

    // Presence must preserve supplied zero modifiers without inventing a player clock.
    [Fact]
    public void UnifiedMotionPreservesOptionalSourceState()
    {
        var recording = LoadJson("""
            {"TickRate":64,"Frames":[{"Pre":{"Stamina":0,"GravityDisabled":false},
             "Input":{"ForwardMove":0},"WeaponDefIndex":7}]}
            """);
        var frame = Assert.Single(recording.Frames);
        Assert.Equal(0f, frame.Pre.Stamina);
        Assert.False(frame.Pre.GravityDisabled);
        Assert.Null(frame.Pre.VelocityModifier);
        Assert.Null(frame.SourcePlayerTick);
        Assert.Equal(0f, frame.Input.ForwardMove);
    }

    // Clock-dependent history is invalid without a source player tickbase.
    [Fact]
    public void UnifiedHistoryWithoutSourceClockIsRejected()
        => Assert.Throws<InvalidDataException>(() => LoadJson("""
            {"TickRate":64,"Frames":[{"Pre":{"LastDuckTime":0}}]}
            """));

    [Theory]
    [InlineData("null")]
    [InlineData("{\"Ticks\":null,\"Subticks\":[]}")]
    [InlineData("{\"Ticks\":[],\"Subticks\":null}")]
    public void NullRecordingDataIsRejectedBeforeNativeLoad(string json)
        => Assert.Throws<InvalidDataException>(() => LoadJson(json));

    // Match the maintained provider's Brotli container rather than the old plain JSON format.
    private static ReplayData LoadJson(string json)
    {
        string path = Path.GetTempFileName();
        try
        {
            using (var file = File.Create(path))
            using (var brotli = new BrotliStream(file, CompressionLevel.Optimal))
                brotli.Write(Encoding.UTF8.GetBytes(json));
            return MotionStore.LoadFromFile(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
