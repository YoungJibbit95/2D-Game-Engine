using System.Text;
using Game.Core.Diagnostics.Replay;
using Xunit;

namespace Game.Tests.DeterminismTests.Replay;

public sealed class ReplaySerializationTests
{
    [Fact]
    public void JsonRoundTrip_IsByteDeterministicAndPreservesNegativeCoordinates()
    {
        var recorder = new ReplayRecorder(8);
        recorder.Record(ReplayTestData.Frame(0, 11));
        recorder.Record(ReplayTestData.Frame(1, null, -0.5f, useItem: true));
        recorder.Record(ReplayTestData.Frame(2, 29, 1f));
        var firstPayload = ReplayJsonSerializer.Serialize(recorder.CaptureSnapshot());

        var restored = ReplayJsonSerializer.Deserialize(firstPayload);
        var secondPayload = ReplayJsonSerializer.Serialize(restored);

        Assert.Equal(firstPayload, secondPayload);
        Assert.Equal(-1, restored.Frames[1].ItemUseRequest!.Value.TargetTileY);
    }

    [Fact]
    public void Deserialize_RejectsMalformedFutureVersionAndCorruptOrdering()
    {
        Assert.Throws<InvalidDataException>(
            () => ReplayJsonSerializer.Deserialize("{broken"u8));

        var futureJson = $$"""
            {
              "formatVersion": {{ReplayRecordingSnapshot.CurrentFormatVersion + 1}},
              "capacity": 4,
              "droppedFrameCount": 0,
              "frames": []
            }
            """;
        Assert.Throws<InvalidDataException>(
            () => ReplayJsonSerializer.Deserialize(Encoding.UTF8.GetBytes(futureJson)));

        var corrupt = new ReplayRecordingSnapshot
        {
            Capacity = 4,
            Frames = [ReplayTestData.Frame(2), ReplayTestData.Frame(1)]
        };
        Assert.Throws<InvalidDataException>(() => ReplayRecordingSnapshot.Validate(corrupt));
    }

    [Fact]
    public void Validate_RejectsImpossibleCapacityCountAndDroppedMetadata()
    {
        var tooLarge = new ReplayRecordingSnapshot
        {
            Capacity = ReplayLimits.MaximumFrameCapacity + 1
        };
        var overCapacity = new ReplayRecordingSnapshot
        {
            Capacity = 1,
            Frames = [ReplayTestData.Frame(0), ReplayTestData.Frame(1)]
        };
        var negativeDrops = new ReplayRecordingSnapshot
        {
            Capacity = 1,
            DroppedFrameCount = -1
        };

        Assert.Throws<InvalidDataException>(() => ReplayRecordingSnapshot.Validate(tooLarge));
        Assert.Throws<InvalidDataException>(() => ReplayRecordingSnapshot.Validate(overCapacity));
        Assert.Throws<InvalidDataException>(() => ReplayRecordingSnapshot.Validate(negativeDrops));
    }
}
