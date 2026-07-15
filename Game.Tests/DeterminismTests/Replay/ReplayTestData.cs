using System.Numerics;
using Game.Core.Diagnostics.Replay;
using Game.Core.Entities;
using Game.Core.Runtime;
using Game.Core.World;

namespace Game.Tests.DeterminismTests.Replay;

internal static class ReplayTestData
{
    public static ReplayInputFrame Frame(
        long tick,
        ulong? hash = null,
        float moveAxis = 0f,
        long? sequence = null,
        bool useItem = false)
    {
        var command = new PlayerCommand(
            moveAxis,
            WantsJump: tick % 3 == 0,
            WantsGuard: tick % 5 == 0,
            GuardFacing: new Vector2(1f, 0f));
        var itemUse = useItem
            ? new PlayerItemUseRequest(
                true,
                new TilePos(checked((int)tick), -checked((int)tick)),
                new Vector2(tick * 16f, tick * -16f))
            : PlayerItemUseRequest.Inactive;

        return ReplayInputFrame.Create(tick, sequence ?? tick, command, itemUse, hash);
    }

    public static ReplayRecordingSnapshot Snapshot(params ReplayInputFrame[] frames)
    {
        return new ReplayRecordingSnapshot
        {
            Capacity = Math.Max(1, frames.Length + 4),
            Frames = frames
        };
    }
}
