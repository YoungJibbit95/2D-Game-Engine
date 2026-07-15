using Game.Client.Rendering.Entities;
using Game.Core.Runtime;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class EntityVisualProfileTests
{
    [Fact]
    public void DefaultCatalog_ResolvesRuntimeAliasesAndWave03Sprites()
    {
        var catalog = EntityVisualProfileCatalog.CreateDefault();

        Assert.Equal("entities/critters/bird", catalog.Resolve("forest_flock_bird", EntityFrameKind.Enemy).SpriteId);
        Assert.Equal("entities/critters/rabbit", catalog.Resolve("meadow_rabbit", EntityFrameKind.Enemy).SpriteId);
        Assert.Equal("entities/enemies/bat", catalog.Resolve("cave_bat_scout", EntityFrameKind.Enemy).SpriteId);
        Assert.Equal("entities/enemies/forest_boar", catalog.Resolve("forest_boar", EntityFrameKind.Enemy).SpriteId);
        Assert.Equal("items/wood", catalog.Resolve("wood", EntityFrameKind.DroppedItem).SpriteId);
        Assert.Equal("projectiles/spark_bolt", catalog.Resolve("spark_bolt", EntityFrameKind.Projectile).SpriteId);
        Assert.Equal("entities/critters/squirrel", catalog.Resolve("entities/critters/squirrel", EntityFrameKind.Enemy).SpriteId);
        Assert.Equal("projectiles/wooden_arrow", catalog.Resolve("projectiles/wooden_arrow", EntityFrameKind.Projectile).SpriteId);

        var elite = catalog.Resolve("crystal_cave_spider", EntityFrameKind.Enemy);
        Assert.True(elite.IsElite);
        Assert.Equal("entities/enemies/cave_spider_elite", elite.EliteSpriteId);
        Assert.True(catalog.Resolve("forest_boar_elite", EntityFrameKind.Enemy).IsElite);
        Assert.True(catalog.Resolve("meadow_slime_elite", EntityFrameKind.Enemy).IsElite);
        Assert.Equal(
            "entities/wave05/marsh_frog",
            catalog.Resolve("marsh_frog", EntityFrameKind.Enemy).SpriteId);
        Assert.Equal(6, catalog.Resolve("canopy_owl", EntityFrameKind.Enemy).Fly.FrameCount);
        Assert.Equal(3, catalog.Resolve("amber_beetle", EntityFrameKind.Enemy).Attack.FrameCount);
        Assert.False(catalog.Resolve("prism_wisp", EntityFrameKind.Enemy).CastsShadow);
        Assert.Equal(
            "items/wave05/mirror_shield",
            catalog.Resolve("mirror_shield", EntityFrameKind.DroppedItem).SpriteId);
    }

    [Fact]
    public void Builder_AllowsGameSpecificImmutableProfilesAndAliases()
    {
        var profile = new EntityVisualProfileBuilder("clockwork_bee", "mod/entities/clockwork_bee")
            .Flying()
            .WithoutShadow()
            .WithScale(1.5f)
            .WithAnimation(EntityVisualState.Fly, new EntityVisualAnimationRange(2, 6, 3))
            .Build();
        var catalog = new EntityVisualProfileCatalogBuilder()
            .Add("clockwork_bee", profile)
            .Add("clockwork_bee_elite", profile)
            .Build();

        var resolved = catalog.Resolve("clockwork_bee_elite", EntityFrameKind.Enemy);

        Assert.Same(profile, resolved);
        Assert.True(resolved.IsFlying);
        Assert.False(resolved.CastsShadow);
        Assert.Equal(1.5f, resolved.DisplayScale);
        Assert.Equal(6, resolved.Fly.FrameCount);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 1)]
    [InlineData(4, 2)]
    [InlineData(6, 1)]
    [InlineData(8, 0)]
    public void AnimationRange_PingPongIsDeterministic(long tick, int expectedFrame)
    {
        var animation = new EntityVisualAnimationRange(0, 3, 2, EntityVisualPlayback.PingPong);

        Assert.Equal(expectedFrame, animation.ResolveFrame(tick));
    }
}
