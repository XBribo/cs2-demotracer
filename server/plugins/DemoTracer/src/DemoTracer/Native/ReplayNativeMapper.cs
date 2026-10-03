/*---------------------------------------------------------------------------------------------
 * Copyright (c) 2026 unicbm. All rights reserved.
 * Licensed under the GNU Affero General Public License v3.0 only.
 * See LICENSE in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System.Numerics;
using System.Runtime.CompilerServices;
using BotControllerApi;

namespace DemoTracer;

internal static class ReplayNativeMapper
{
    // Convert disk DTOs once into the shared model without changing cached source data.
    public static ReplayData BuildPlaybackData(DtrReplayFile replay, bool writeLeftHandDesired)
    {
        // The reader validates legacy extras as evidence only, matching the old runtime.
        // Their mask/clock is undefined; only sparse source state supplies restorable history.
        var frames = new ReplayFrame[replay.Ticks.Length];
        var values = new uint?[67];
        int changeIndex = 0, offset = 0;
        uint playerTickStart = 0, playerTickRun = 0;
        ulong previousHeld = 0;
        for (int i = 0; i < frames.Length; ++i)
        {
            var disk = replay.Ticks[i];
            while (changeIndex < replay.SourceState.Length && replay.SourceState[changeIndex].TickIndex <= (uint)i)
            {
                var change = replay.SourceState[changeIndex++];
                if (change.FieldId >= values.Length) continue;
                values[change.FieldId] = (change.Present & 1) != 0 ? change.ValueBits : null;
                if (change.FieldId == 1)
                {
                    playerTickStart = change.TickIndex;
                    playerTickRun = change.Present >> 1;
                }
            }
            var pre = ToSnapshot(disk.Pre);
            // Carry source inputs for boundary initialization, not per-tick state overwrites.
            if (values[6] is uint stamina) pre.Stamina = BitConverter.UInt32BitsToSingle(stamina);
            if (values[31] is uint velocityModifier) pre.VelocityModifier = BitConverter.UInt32BitsToSingle(velocityModifier);
            if (values[32] is uint friction) pre.Friction = BitConverter.UInt32BitsToSingle(friction);
            if (values[33] is uint gravityScale) pre.GravityScale = BitConverter.UInt32BitsToSingle(gravityScale);
            if (values[34] is uint gravityDisabled) pre.GravityDisabled = gravityDisabled != 0;
            if (values[37] is uint baseX && values[38] is uint baseY && values[39] is uint baseZ)
                pre.BaseVelocity = new Vector3(BitConverter.UInt32BitsToSingle(baseX), BitConverter.UInt32BitsToSingle(baseY), BitConverter.UInt32BitsToSingle(baseZ));
            bool hasHistory = ApplyHistory(ref pre, values);
            hasHistory |= ApplySourceState(ref pre, values, disk.WeaponDefIndex);
            int? sourceTick = null;
            if (values[1] is uint basePlayerTick)
            {
                ulong playerTick = (ulong)basePlayerTick + Math.Min((uint)i - playerTickStart, playerTickRun);
                if (playerTick > int.MaxValue)
                    throw new InvalidDataException($"source player clock at tick {i} is out of range");
                sourceTick = (int)playerTick;
            }
            if (hasHistory && sourceTick is null)
                throw new InvalidDataException($"source state at tick {i} lacks the source player clock");

            var command = replay.CommandFrames.Length == 0 ? default : replay.CommandFrames[i];
            if ((command.Fields & 16) == 0)
            {
                command.Buttons = disk.Pre.Buttons;
                command.Buttons1 = disk.Pre.Buttons1;
                command.Buttons2 = disk.Pre.Buttons2;
                // Legacy held-only samples lack transitions; preserve complete all-zero planes.
                if (command.Buttons1 == 0 && command.Buttons2 == 0)
                    command.Buttons1 = command.Buttons ^ previousHeld;
                command.Fields |= 16;
            }
            previousHeld = command.Buttons;
            if ((command.Fields & 64) != 0 && command.WeaponSelect > 0 && (command.Fields & 256) == 0)
            {
                if (disk.WeaponDefIndex <= 0)
                    throw new InvalidDataException($"weapon selection at tick {i} has no item definition");
                command.WeaponSelect = disk.WeaponDefIndex;
            }
            uint count = disk.NumSubtick;
            if (count > 36 || count > replay.Subticks.Length - offset)
                throw new InvalidDataException($"subtick count at tick {i} is invalid");
            var subticks = new SubtickMove[(int)count];
            for (int j = 0; j < subticks.Length; ++j)
                subticks[j] = Unsafe.BitCast<NativeSubtickMove, SubtickMove>(replay.Subticks[offset + j]);
            offset += (int)count;
            frames[i] = new ReplayFrame
            {
                SourcePlayerTick = sourceTick, Pre = pre, Post = ToSnapshot(disk.Post),
                WeaponDefIndex = disk.WeaponDefIndex, Subticks = subticks,
                Input = new ReplayInput
                {
                    ForwardMove = (command.Fields & 1) != 0 ? command.ForwardMove : null,
                    LeftMove = (command.Fields & 2) != 0 ? command.LeftMove : null,
                    UpMove = (command.Fields & 4) != 0 ? command.UpMove : null,
                    ViewAngles = (command.Fields & 8) != 0 ? new Vector3(command.Pitch, command.Yaw, command.Roll) : null,
                    Buttons = new ReplayButtons(command.Buttons, command.Buttons1, command.Buttons2),
                    Mouse = (command.Fields & 32) != 0 ? new ReplayMouse(command.MouseDx, command.MouseDy) : null,
                    WeaponSelectDefIndex = (command.Fields & 64) != 0 ? command.WeaponSelect : null,
                    LeftHandDesired = writeLeftHandDesired && (command.Fields & 128) != 0 ? command.LeftHandDesired != 0 : null
                }
            };
        }
        if (offset != replay.Subticks.Length)
            throw new InvalidDataException("replay has unclaimed subticks");
        return new ReplayData { TickRate = replay.TickRate, Frames = frames };
    }

    // Carry complete presence-aware history groups into the pre-command snapshot.
    private static bool ApplyHistory(ref MovementSnapshot pre, uint?[] values)
    {
        if (values[4] is uint duck) pre.LastDuckTime = BitConverter.UInt32BitsToSingle(duck);
        if (values[22] is uint actualTick && values[23] is uint actualFrac)
            pre.LastActualJumpPress = new(unchecked((int)actualTick), BitConverter.UInt32BitsToSingle(actualFrac));
        if (values[24] is uint usableTick && values[25] is uint usableFrac)
            pre.LastUsableJumpPress = new(unchecked((int)usableTick), BitConverter.UInt32BitsToSingle(usableFrac));
        if (values[26] is uint landedTick && values[27] is uint landedFrac)
            pre.LastLanded = new(unchecked((int)landedTick), BitConverter.UInt32BitsToSingle(landedFrac));
        if (values[28] is uint vx && values[29] is uint vy && values[30] is uint vz)
            pre.LastLandedVelocity = new Vector3(BitConverter.UInt32BitsToSingle(vx), BitConverter.UInt32BitsToSingle(vy), BitConverter.UInt32BitsToSingle(vz));
        return pre.LastDuckTime.HasValue || pre.LastActualJumpPress.HasValue ||
               pre.LastUsableJumpPress.HasValue || pre.LastLanded.HasValue || pre.LastLandedVelocity.HasValue;
    }

    // Translate remaining disk fields here; BotController never interprets historical DTR IDs.
    private static bool ApplySourceState(ref MovementSnapshot pre, uint?[] values, int weaponDef)
    {
        pre.DuckRoot = SourceFloat(values, 2); pre.DuckView = SourceFloat(values, 3);
        pre.DuckOverride = SourceBool(values, 5);
        pre.DuckAmount = SourceFloat(values, 7) ?? pre.DuckAmount;
        pre.DuckSpeed = SourceFloat(values, 8) ?? pre.DuckSpeed;
        if (SourceBool(values, 9) is { } ducked) pre.Ducked = ducked ? (byte)1 : (byte)0;
        if (SourceBool(values, 10) is { } ducking) pre.Ducking = ducking ? (byte)1 : (byte)0;
        if (SourceBool(values, 11) is { } desiresDuck) pre.DesiresDuck = desiresDuck ? (byte)1 : (byte)0;
        pre.LastJump = SourceTime(values, 12); pre.LastJumpVelocityZ = SourceFloat(values, 14);
        pre.UsingGroundTopology = SourceBool(values, 15); pre.GroundTopologySmoothing = SourceFloat(values, 16);
        pre.FrictionStashedSpeed = SourceFloat(values, 17); pre.UseFrictionStashedSpeed = SourceBool(values, 18);
        pre.FrictionStashedUntilFraction = SourceFloat(values, 19); pre.LadderSurface = SourceInt(values, 20);
        pre.FallVelocity = SourceFloat(values, 21); pre.ShotsFired = SourceInt(values, 35); pre.Scoped = SourceBool(values, 36);
        pre.PredictableAngle = SourceVector(values, 40); pre.PredictableAngleVelocity = SourceVector(values, 43);
        pre.UnpredictableAngle = SourceVector(values, 46); pre.PredictableAngleTime = SourceTime(values, 49);
        pre.UnpredictableAngleTick = SourceInt(values, 51);
        // The source handle is an opaque instance key, never a live-server handle to restore.
        // Match DemoTracer's one-shot weapon initialization only when identity is available.
        if (values[64] is { } instance && instance != 0 && instance != uint.MaxValue &&
            (values[55].HasValue || values[56].HasValue || values[57].HasValue || values[58].HasValue ||
             values[59].HasValue || values[60].HasValue || values[61].HasValue || values[62].HasValue || values[63].HasValue))
        {
            if (weaponDef <= 0) throw new InvalidDataException("source weapon state lacks an item definition");
            pre.Weapon = new ReplayWeaponState
            {
                DefIndex = weaponDef, InstanceId = instance,
                NextPrimaryAttack = SourceTime(values, 55), NextSecondaryAttack = SourceTime(values, 57),
                RecoilIndex = SourceFloat(values, 59), AccuracyPenalty = SourceFloat(values, 60),
                LastShotTime = SourceFloat(values, 61), BurstShotsRemaining = SourceInt(values, 62),
                NextAttack = SourceFloat(values, 63)
            };
        }
        // ServerTick, clip/reserve ammunition and reload fields remain engine-owned.
        return pre.LastJump.HasValue || pre.PredictableAngleTime.HasValue || pre.UnpredictableAngleTick.HasValue ||
               pre.Weapon is { } weapon && (weapon.NextPrimaryAttack.HasValue || weapon.NextSecondaryAttack.HasValue ||
                   weapon.LastShotTime.HasValue || weapon.NextAttack.HasValue);
    }

    // Sparse float bits retain explicit zeros and sentinel values.
    private static float? SourceFloat(uint?[] values, int id)
        => values[id] is { } bits ? BitConverter.UInt32BitsToSingle(bits) : null;
    // Preserve signed ticks and surface indices without reinterpreting them as file ticks.
    private static int? SourceInt(uint?[] values, int id)
        => values[id] is { } bits ? unchecked((int)bits) : null;
    // Boolean presence is independent of its value.
    private static bool? SourceBool(uint?[] values, int id)
        => values[id] is { } bits ? bits != 0 : null;
    // Supply only complete tick/fraction pairs to the shared API.
    private static ReplayTimestamp? SourceTime(uint?[] values, int id)
        => SourceInt(values, id) is { } tick && SourceFloat(values, id + 1) is { } fraction ? new(tick, fraction) : null;
    // Supply vectors as complete presence-aware groups.
    private static Vector3? SourceVector(uint?[] values, int id)
        => SourceFloat(values, id) is { } x && SourceFloat(values, id + 1) is { } y && SourceFloat(values, id + 2) is { } z
            ? new(x, y, z) : null;

    // Native padding and disk event-tail bytes are not part of the public snapshot.
    private static MovementSnapshot ToSnapshot(NativeMovementSnapshot value) => new()
    {
        OriginX = value.OriginX,
        OriginY = value.OriginY,
        OriginZ = value.OriginZ,
        VelX = value.VelX,
        VelY = value.VelY,
        VelZ = value.VelZ,
        Pitch = value.Pitch,
        Yaw = value.Yaw,
        Roll = value.Roll,
        EntityFlags = value.EntityFlags,
        MoveType = value.MoveType,
        Buttons = value.Buttons,
        Buttons1 = value.Buttons1,
        Buttons2 = value.Buttons2,
        DuckAmount = value.DuckAmount,
        DuckSpeed = value.DuckSpeed,
        LadderNormalX = value.LadderNormalX,
        LadderNormalY = value.LadderNormalY,
        LadderNormalZ = value.LadderNormalZ,
        Ducked = value.Ducked,
        Ducking = value.Ducking,
        DesiresDuck = value.DesiresDuck,
        ActualMoveType = value.ActualMoveType,
    };

    // Keep existing DemoTracer consumers on their disk DTO until separately migrated.
    public static NativeReplayTick ToDiskTick(ReplayFrame frame) => new()
    {
        Pre = ToDiskSnapshot(frame.Pre), Post = ToDiskSnapshot(frame.Post),
        WeaponDefIndex = frame.WeaponDefIndex, NumSubtick = (uint)frame.Subticks.Length
    };

    // Only restore the unchanged 92-byte disk prefix, never serialize nullable history here.
    private static NativeMovementSnapshot ToDiskSnapshot(MovementSnapshot value) => new()
    {
        OriginX = value.OriginX,
        OriginY = value.OriginY,
        OriginZ = value.OriginZ,
        VelX = value.VelX,
        VelY = value.VelY,
        VelZ = value.VelZ,
        Pitch = value.Pitch,
        Yaw = value.Yaw,
        Roll = value.Roll,
        EntityFlags = value.EntityFlags,
        MoveType = value.MoveType,
        Buttons = value.Buttons,
        Buttons1 = value.Buttons1,
        Buttons2 = value.Buttons2,
        DuckAmount = value.DuckAmount,
        DuckSpeed = value.DuckSpeed,
        LadderNormalX = value.LadderNormalX,
        LadderNormalY = value.LadderNormalY,
        LadderNormalZ = value.LadderNormalZ,
        Ducked = value.Ducked,
        Ducking = value.Ducking,
        DesiresDuck = value.DesiresDuck,
        ActualMoveType = value.ActualMoveType,
    };

    // Retain the managed event/inventory metadata independently of native movement.
    public static ReplayFileMetadata BuildMetadata(DtrReplayFile replay)
    {
        if (replay.PreparedMetadata is { } prepared)
            return prepared;
        // Consumers need the first weapon and the inventory set, not a second
        // per-tick array that they immediately scan and deduplicate again.
        var seenWeapons = new HashSet<int>();
        var weaponDefIndices = new List<int>();
        for (var i = 0; i < replay.Ticks.Length; i++)
            if (seenWeapons.Add(replay.Ticks[i].WeaponDefIndex))
                weaponDefIndices.Add(replay.Ticks[i].WeaponDefIndex);
        ReplayVector3? roundStartOrigin = null;
        if (replay.Ticks.Length > 0)
        {
            var snapshot = replay.Ticks[0].Pre;
            roundStartOrigin = new ReplayVector3(
                snapshot.OriginX,
                snapshot.OriginY,
                snapshot.OriginZ);
        }
        return new ReplayFileMetadata(
            replay.TickRate,
            replay.PlayStartTickIndex,
            replay.Ticks.Length,
            replay.Projectiles,
            replay.HighFidelity,
            weaponDefIndices.ToArray(),
            roundStartOrigin);
    }
}
