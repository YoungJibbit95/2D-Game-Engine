using System.Numerics;
using Game.Core.Diagnostics.Replay;
using Game.Core.Entities;
using Game.Core.Runtime;
using Game.Core.World;
using Xunit;

namespace Game.Tests.DeterminismTests.Replay;

public sealed class ReplayInputContractTests
{
    [Fact]
    public void Create_RoundTripsExistingRuntimeInputsWithoutRuntimeLayoutPersistence()
    {
        var command = new PlayerCommand(0.75f, true, true, new Vector2(-0.5f, 0.25f));
        var itemUse = new PlayerItemUseRequest(
            true,
            new TilePos(-42, 17),
            new Vector2(-671.5f, 280.25f));

        var frame = ReplayInputFrame.Create(91, 112, command, itemUse, 0xCAFE_BABEUL);

        Assert.Equal(command, frame.Command.ToRuntime());
        Assert.Equal(itemUse, frame.ItemUseRequest!.Value.ToRuntime());
        Assert.Equal(0xCAFE_BABEUL, frame.CheckpointStateHash);
    }

    [Fact]
    public void Create_OmitsInactiveItemUseRequest()
    {
        var frame = ReplayInputFrame.Create(
            0,
            0,
            PlayerCommand.None,
            PlayerItemUseRequest.Inactive);

        Assert.Null(frame.ItemUseRequest);
    }

    [Fact]
    public void Validate_RejectsFutureVersionInvalidNumbersAndInactiveOptionalUse()
    {
        var valid = ReplayTestData.Frame(1);
        var future = valid with { FormatVersion = ReplayInputFrame.CurrentFormatVersion + 1 };
        var invalidMove = valid with
        {
            Command = valid.Command with { MoveAxis = float.NaN }
        };
        var inactiveUse = valid with
        {
            ItemUseRequest = new ReplayPlayerItemUseRequest(false, 0, 0, default)
        };

        Assert.Throws<InvalidDataException>(() => ReplayInputFrame.Validate(future));
        Assert.Throws<InvalidDataException>(() => ReplayInputFrame.Validate(invalidMove));
        Assert.Throws<InvalidDataException>(() => ReplayInputFrame.Validate(inactiveUse));
    }
}
