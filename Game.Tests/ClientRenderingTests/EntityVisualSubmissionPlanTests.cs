using System.Numerics;
using Game.Client.Rendering.Entities;
using Game.Core.Combat;
using Game.Core.Inventory;
using Game.Core.Runtime;
using Game.Core.World;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class EntityVisualSubmissionPlanTests
{
    private static readonly RectI WideViewport = new(-10_000, -10_000, 20_000, 20_000);

    [Fact]
    public void Prepare_BucketsShadowsBeforeActorsAndPreservesStableLayerOrder()
    {
        var pipeline = new EntityVisualPipeline();
        var entities = new[]
        {
            Enemy(11, "forest_boar", x: 0),
            Enemy(22, "forest_boar", x: 40)
        };

        var telemetry = pipeline.Prepare(entities, WideViewport, fixedTick: 10);
        var commands = pipeline.CommandBuffer;
        var plan = commands.SubmissionPlan;

        Assert.True(telemetry.Submission.IsComplete);
        Assert.Equal(4, plan.Count);
        Assert.Equal(2, plan.BucketCount);
        Assert.Equal([0, 2, 1, 3], plan.CommandIndices.ToArray());
        Assert.Equal(EntityVisualSubmissionLayer.Shadows, plan.Buckets[0].Layer);
        Assert.Equal(EntityVisualSubmissionMaterial.ShadowMask, plan.Buckets[0].Material);
        Assert.Equal(2, plan.Buckets[0].CommandCount);
        Assert.Equal(EntityVisualSubmissionLayer.Actors, plan.Buckets[1].Layer);
        Assert.Equal("entities/enemies/forest_boar", plan.Buckets[1].TextureKey);
        Assert.Equal(2, plan.Buckets[1].CommandCount);

        Assert.Equal(11, commands[plan[0]].EntityId);
        Assert.Equal(22, commands[plan[1]].EntityId);
        Assert.Equal(11, commands[plan[2]].EntityId);
        Assert.Equal(22, commands[plan[3]].EntityId);
        Assert.Equal(4, telemetry.Submission.EstimatedTextureSwitchesBefore);
        Assert.Equal(2, telemetry.Submission.EstimatedTextureSwitchesAfter);
    }

    [Fact]
    public void Prepare_TwoHundredAlternatingTexturesNearlyHalvesEstimatedSwitches()
    {
        var pipeline = new EntityVisualPipeline(
            maximumTrackedEntities: 256,
            maximumDrawCommands: 512,
            maximumSubmissionBuckets: 256);
        var entities = new EntityFrameSnapshot[200];
        for (var index = 0; index < entities.Length; index++)
        {
            entities[index] = Enemy(
                index + 1,
                index % 2 == 0 ? "forest_boar" : "meadow_rabbit",
                x: index * 20);
        }

        var telemetry = pipeline.Prepare(entities, WideViewport, fixedTick: 10);

        Assert.True(telemetry.Submission.IsComplete);
        Assert.Equal(400, telemetry.PreparedCommands);
        Assert.Equal(400, telemetry.Submission.EstimatedTextureSwitchesBefore);
        Assert.Equal(201, telemetry.Submission.EstimatedTextureSwitchesAfter);
        Assert.Equal(199, telemetry.Submission.EstimatedTextureSwitchReduction);
        Assert.Equal(201, telemetry.Submission.PreparedBuckets);
    }

    [Fact]
    public void Prepare_SubmissionBucketOverflowIsHardAndFallsBackWithoutDroppingCommands()
    {
        var pipeline = new EntityVisualPipeline(
            maximumTrackedEntities: 4,
            maximumDrawCommands: 8,
            maximumSubmissionBuckets: 1);
        var entities = new[]
        {
            Enemy(1, "forest_boar", x: 0),
            Enemy(2, "forest_boar", x: 40)
        };

        var telemetry = pipeline.Prepare(entities, WideViewport, fixedTick: 10);

        Assert.Equal(4, telemetry.PreparedCommands);
        Assert.False(telemetry.WasBudgetClamped);
        Assert.True(telemetry.UsedSubmissionFallback);
        Assert.False(telemetry.Submission.IsComplete);
        Assert.Equal(2, telemetry.Submission.RequiredBuckets);
        Assert.Equal(1, telemetry.Submission.BucketCapacityOverflow);
        Assert.Equal(4, telemetry.Submission.EstimatedTextureSwitchesAfter);
        Assert.Equal(0, telemetry.Submission.EstimatedTextureSwitchReduction);
        Assert.Equal(0, pipeline.CommandBuffer.SubmissionPlan.Count);
        Assert.Equal(0, pipeline.CommandBuffer.SubmissionPlan.BucketCount);
    }

    private static EntityFrameSnapshot Enemy(int id, string contentId, int x)
    {
        return new EntityFrameSnapshot(
            id,
            EntityFrameKind.Enemy,
            contentId,
            new Vector2(x, 40),
            Vector2.Zero,
            new RectI(x, 40, 24, 20),
            true,
            20,
            20,
            ItemStack.Empty,
            false,
            DamageType.Generic);
    }
}
