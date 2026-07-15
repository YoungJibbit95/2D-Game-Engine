using Game.Core.Runtime;

namespace Game.Client.Rendering.Entities;

public sealed class EntityVisualProfileCatalog
{
    private readonly Binding[] _bindings;
    private readonly int _bindingMask;
    private readonly int _count;
    private readonly EntityVisualProfile[] _fallbacks;

    internal EntityVisualProfileCatalog(Binding[] bindings, EntityVisualProfile[] fallbacks)
    {
        _count = bindings.Length;
        var capacity = 1;
        while (capacity < Math.Max(4, bindings.Length * 2))
        {
            capacity <<= 1;
        }

        _bindings = new Binding[capacity];
        _bindingMask = capacity - 1;
        for (var index = 0; index < bindings.Length; index++)
        {
            var binding = bindings[index];
            var slot = ResolveSlot(binding.ContentId!, binding.Kind);
            while (_bindings[slot].ContentId is not null)
            {
                slot = (slot + 1) & _bindingMask;
            }

            _bindings[slot] = binding;
        }

        _fallbacks = fallbacks;
    }

    public int Count => _count;

    public EntityVisualProfile Resolve(string contentId, EntityFrameKind kind)
    {
        return Resolve(contentId, kind, out _);
    }

    public EntityVisualProfile Resolve(string contentId, EntityFrameKind kind, out bool usedFallback)
    {
        var slot = ResolveSlot(contentId, kind);
        for (var probe = 0; probe < _bindings.Length; probe++)
        {
            ref readonly var binding = ref _bindings[slot];
            if (binding.ContentId is null)
            {
                break;
            }

            if (binding.Kind == kind &&
                string.Equals(binding.ContentId, contentId, StringComparison.OrdinalIgnoreCase))
            {
                usedFallback = false;
                return binding.Profile!;
            }

            slot = (slot + 1) & _bindingMask;
        }

        usedFallback = true;
        return _fallbacks[(int)kind];
    }

    private int ResolveSlot(string contentId, EntityFrameKind kind)
    {
        var hash = StringComparer.OrdinalIgnoreCase.GetHashCode(contentId);
        hash = unchecked(hash * 397) ^ (int)kind;
        return hash & _bindingMask;
    }

    public static EntityVisualProfileCatalog CreateDefault()
    {
        var builder = new EntityVisualProfileCatalogBuilder();
        var oneFrame = new EntityVisualAnimationRange(0, 1, 8);
        var fourFrameIdle = new EntityVisualAnimationRange(0, 4, 10, EntityVisualPlayback.PingPong);
        var fourFrameWalk = new EntityVisualAnimationRange(0, 4, 5);
        var fourFrameRun = new EntityVisualAnimationRange(0, 4, 3);
        var waveFiveIdle = new EntityVisualAnimationRange(0, 2, 10, EntityVisualPlayback.PingPong);
        var waveFiveWalk = new EntityVisualAnimationRange(2, 4, 4);
        var waveFiveRun = new EntityVisualAnimationRange(4, 4, 3);

        var slime = new EntityVisualProfileBuilder("slime", "entities/slime")
            .WithBob(1f)
            .WithAnimation(EntityVisualState.Idle, oneFrame)
            .WithAnimation(EntityVisualState.Walk, oneFrame)
            .WithAnimation(EntityVisualState.Run, oneFrame)
            .Build();
        builder.Add("slime", slime).Add("entities/slime", slime);
        var eliteSlime = slime with
        {
            Id = "meadow_slime_elite",
            IsElite = true,
            EliteSpriteId = "entities/enemies/meadow_slime_elite"
        };
        builder.Add("meadow_slime_elite", eliteSlime)
            .Add("entities/enemies/meadow_slime_elite", eliteSlime);

        var squirrel = CreateGroundCritter("squirrel", "entities/critters/squirrel", fourFrameIdle, fourFrameWalk, fourFrameRun);
        builder.Add("squirrel", squirrel).Add("entities/critters/squirrel", squirrel);

        var rabbit = CreateGroundCritter("rabbit", "entities/critters/rabbit", fourFrameIdle, fourFrameWalk, fourFrameRun);
        builder.Add("rabbit", rabbit)
            .Add("meadow_rabbit", rabbit)
            .Add("entities/critters/rabbit", rabbit);

        var bird = CreateFlying("bird", "entities/critters/bird", EntityFrameKind.Enemy);
        builder.Add("bird", bird)
            .Add("forest_flock_bird", bird)
            .Add("entities/critters/bird", bird);

        var firefly = CreateFlying("firefly", "entities/critters/firefly", EntityFrameKind.Enemy, 0.8f);
        builder.Add("firefly", firefly).Add("entities/critters/firefly", firefly);

        var bat = CreateFlying("bat", "entities/enemies/bat", EntityFrameKind.Enemy);
        builder.Add("bat", bat)
            .Add("cave_bat_scout", bat)
            .Add("entities/enemies/bat", bat);

        var boar = CreateGroundEnemy("forest_boar", "entities/enemies/forest_boar", fourFrameIdle, fourFrameWalk, fourFrameRun);
        builder.Add("forest_boar", boar).Add("entities/enemies/forest_boar", boar);
        var eliteBoar = boar with
        {
            Id = "forest_boar_elite",
            IsElite = true,
            EliteSpriteId = "entities/enemies/forest_boar_elite"
        };
        builder.Add("forest_boar_elite", eliteBoar)
            .Add("entities/enemies/forest_boar_elite", eliteBoar);

        var spider = CreateGroundEnemy("cave_spider", "entities/enemies/cave_spider", fourFrameIdle, fourFrameWalk, fourFrameRun);
        builder.Add("cave_spider", spider).Add("entities/enemies/cave_spider", spider);
        var eliteSpider = spider with
        {
            Id = "crystal_cave_spider",
            IsElite = true,
            EliteSpriteId = "entities/enemies/cave_spider_elite"
        };
        builder.Add("crystal_cave_spider", eliteSpider)
            .Add("cave_spider_elite", eliteSpider)
            .Add("entities/enemies/cave_spider_elite", eliteSpider);

        var marshFrog = CreateGroundCritter(
            "marsh_frog",
            "entities/wave05/marsh_frog",
            waveFiveIdle,
            waveFiveWalk,
            waveFiveRun) with
        {
            Jump = new EntityVisualAnimationRange(6, 2, 5, EntityVisualPlayback.PingPong),
            DisplayScale = 0.9f
        };
        builder.Add("marsh_frog", marshFrog)
            .Add("entities/wave05/marsh_frog", marshFrog);

        var canopyOwl = new EntityVisualProfileBuilder("canopy_owl", "entities/wave05/canopy_owl")
            .Flying()
            .WithMotion(EntityVisualMotionStyle.Bob | EntityVisualMotionStyle.WingFlap)
            .WithScale(1.05f)
            .WithBob(1.5f)
            .WithAnimation(EntityVisualState.Idle, waveFiveIdle)
            .WithAnimation(EntityVisualState.Fly, new EntityVisualAnimationRange(2, 6, 3))
            .WithAnimation(EntityVisualState.Hurt, new EntityVisualAnimationRange(7, 1, 3, EntityVisualPlayback.Clamp))
            .Build();
        builder.Add("canopy_owl", canopyOwl)
            .Add("entities/wave05/canopy_owl", canopyOwl);

        var amberBeetle = CreateGroundEnemy(
            "amber_beetle",
            "entities/wave05/amber_beetle",
            waveFiveIdle,
            waveFiveWalk,
            waveFiveRun) with
        {
            Attack = new EntityVisualAnimationRange(5, 3, 3, EntityVisualPlayback.Clamp),
            Hurt = new EntityVisualAnimationRange(7, 1, 3, EntityVisualPlayback.Clamp)
        };
        builder.Add("amber_beetle", amberBeetle)
            .Add("entities/wave05/amber_beetle", amberBeetle);

        var prismWisp = new EntityVisualProfileBuilder("prism_wisp", "entities/wave05/prism_wisp")
            .Flying()
            .WithoutShadow()
            .WithMotion(EntityVisualMotionStyle.Bob | EntityVisualMotionStyle.Spin)
            .WithScale(1.1f)
            .WithBob(2.2f)
            .WithStateLocks(5, 10)
            .WithAnimation(EntityVisualState.Idle, waveFiveIdle)
            .WithAnimation(EntityVisualState.Fly, new EntityVisualAnimationRange(2, 6, 3))
            .WithAnimation(EntityVisualState.Attack, new EntityVisualAnimationRange(5, 3, 3, EntityVisualPlayback.Clamp))
            .WithAnimation(EntityVisualState.Hurt, new EntityVisualAnimationRange(7, 1, 3, EntityVisualPlayback.Clamp))
            .Build();
        builder.Add("prism_wisp", prismWisp)
            .Add("entities/wave05/prism_wisp", prismWisp);

        builder.AddFallback(EntityFrameKind.Enemy, slime);
        builder.AddFallback(EntityFrameKind.Entity, slime with { Id = "generic", SpriteId = "missing_sprite" });

        AddProjectile(builder, "wooden_arrow", "projectiles/wooden_arrow");
        AddProjectile(builder, "poison_arrow", "projectiles/poison_arrow");
        AddProjectile(builder, "spark_bolt", "projectiles/spark_bolt");
        AddProjectile(builder, "arcane_mote", "projectiles/arcane_mote");
        builder.AddFallback(
            EntityFrameKind.Projectile,
            CreateProjectile("projectile", "projectiles/wooden_arrow"));

        AddItemProfiles(builder);
        builder.AddFallback(
            EntityFrameKind.DroppedItem,
            CreateDroppedItem("dropped_item", "missing_sprite"));
        return builder.Build();
    }

    private static EntityVisualProfile CreateGroundCritter(
        string id,
        string spriteId,
        EntityVisualAnimationRange idle,
        EntityVisualAnimationRange walk,
        EntityVisualAnimationRange run)
    {
        return new EntityVisualProfileBuilder(id, spriteId)
            .WithSpeedThresholds(2f, 55f, 14f)
            .WithBob(0.8f)
            .WithAnimation(EntityVisualState.Idle, idle)
            .WithAnimation(EntityVisualState.Walk, walk)
            .WithAnimation(EntityVisualState.Run, run)
            .WithAnimation(EntityVisualState.Jump, new EntityVisualAnimationRange(2, 1, 6))
            .Build();
    }

    private static EntityVisualProfile CreateGroundEnemy(
        string id,
        string spriteId,
        EntityVisualAnimationRange idle,
        EntityVisualAnimationRange walk,
        EntityVisualAnimationRange run)
    {
        return new EntityVisualProfileBuilder(id, spriteId)
            .WithSpeedThresholds(3f, 80f, 18f)
            .WithAnimation(EntityVisualState.Idle, idle)
            .WithAnimation(EntityVisualState.Walk, walk)
            .WithAnimation(EntityVisualState.Run, run)
            .WithAnimation(EntityVisualState.Jump, new EntityVisualAnimationRange(2, 1, 6))
            .WithAnimation(EntityVisualState.Attack, new EntityVisualAnimationRange(1, 3, 3, EntityVisualPlayback.Clamp))
            .Build();
    }

    private static EntityVisualProfile CreateFlying(
        string id,
        string spriteId,
        EntityFrameKind kind,
        float scale = 1f)
    {
        return new EntityVisualProfileBuilder(id, spriteId)
            .ForKind(kind)
            .Flying()
            .WithMotion(EntityVisualMotionStyle.Bob | EntityVisualMotionStyle.WingFlap)
            .WithScale(scale)
            .WithBob(1.6f)
            .WithAnimation(EntityVisualState.Idle, new EntityVisualAnimationRange(0, 4, 7, EntityVisualPlayback.PingPong))
            .WithAnimation(EntityVisualState.Fly, new EntityVisualAnimationRange(0, 4, 3))
            .Build();
    }

    private static void AddProjectile(EntityVisualProfileCatalogBuilder builder, string id, string spriteId)
    {
        var profile = CreateProjectile(id, spriteId);
        builder.Add(id, profile).Add(spriteId, profile);
    }

    private static EntityVisualProfile CreateProjectile(string id, string spriteId)
    {
        return new EntityVisualProfileBuilder(id, spriteId)
            .ForKind(EntityFrameKind.Projectile)
            .Flying()
            .WithoutShadow()
            .WithMotion(EntityVisualMotionStyle.RotateToVelocity)
            .WithAnimation(EntityVisualState.Fly, new EntityVisualAnimationRange(0, 1, 4))
            .Build();
    }

    private static EntityVisualProfile CreateDroppedItem(string id, string spriteId)
    {
        return new EntityVisualProfileBuilder(id, spriteId)
            .ForKind(EntityFrameKind.DroppedItem)
            .WithMotion(EntityVisualMotionStyle.Bob | EntityVisualMotionStyle.Spin)
            .WithScale(0.8f)
            .WithBob(1.8f)
            .WithShadow(0.6f, 0.25f)
            .Build();
    }

    private static void AddItemProfiles(EntityVisualProfileCatalogBuilder builder)
    {
        ReadOnlySpan<string> itemIds =
        [
            "copper_coin", "copper_ore", "iron_ore", "dirt_block", "stone_block", "wood", "gel",
            "copper_pickaxe", "iron_pickaxe", "wooden_sword", "copper_sword", "iron_sword",
            "copper_helmet", "copper_chestplate", "copper_greaves", "wooden_bow", "wooden_arrow",
            "poison_arrow", "workbench", "mining_charm", "healing_potion", "mana_crystal",
            "spark_wand", "apprentice_tome", "copper_hoe", "watering_can", "parsnip_seeds",
            "parsnip", "copper_axe", "iron_axe", "copper_hammer", "iron_hammer", "iron_hoe",
            "magic_wand", "staff", "spellbook", "mana_potion", "torch", "anvil", "furnace"
        ];
        for (var index = 0; index < itemIds.Length; index++)
        {
            var id = itemIds[index];
            builder.Add(id, CreateDroppedItem(id, $"items/{id}"));
        }

        AddDroppedItem(builder, "amberstone_block", "world/wave05/amberstone_autotile");
        AddDroppedItem(builder, "amberwood_plank_block", "world/wave05/amberwood_plank_autotile");
        AddDroppedItem(builder, "mangrove_root_block", "world/wave05/mangrove_root_autotile");
        AddDroppedItem(builder, "marsh_moss_block", "world/wave05/marsh_moss_autotile");
        AddDroppedItem(builder, "sunsteel_pickaxe", "items/wave05/sunsteel_pickaxe");
        AddDroppedItem(builder, "prism_axe", "items/wave05/prism_axe");
        AddDroppedItem(builder, "glimmer_rod", "items/wave05/glimmer_rod");
        AddDroppedItem(builder, "thornblade", "items/wave05/thornblade");
        AddDroppedItem(builder, "mirror_shield", "items/wave05/mirror_shield");
        AddDroppedItem(builder, "flare_bow", "items/wave05/flare_bow");
    }

    private static void AddDroppedItem(
        EntityVisualProfileCatalogBuilder builder,
        string itemId,
        string spriteId)
    {
        builder.Add(itemId, CreateDroppedItem(itemId, spriteId));
    }

    internal readonly record struct Binding(string? ContentId, EntityFrameKind Kind, EntityVisualProfile? Profile);
}

public sealed class EntityVisualProfileCatalogBuilder
{
    private readonly List<EntityVisualProfileCatalog.Binding> _bindings = new();
    private readonly EntityVisualProfile?[] _fallbacks = new EntityVisualProfile[4];

    public EntityVisualProfileCatalogBuilder Add(string contentId, EntityVisualProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);
        ArgumentNullException.ThrowIfNull(profile);
        for (var index = 0; index < _bindings.Count; index++)
        {
            if (_bindings[index].Kind == profile.Kind &&
                string.Equals(_bindings[index].ContentId, contentId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Duplicate entity visual binding '{contentId}' for {profile.Kind}.");
            }
        }

        _bindings.Add(new EntityVisualProfileCatalog.Binding(contentId, profile.Kind, profile));
        return this;
    }

    public EntityVisualProfileCatalogBuilder AddFallback(EntityFrameKind kind, EntityVisualProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _fallbacks[(int)kind] = profile;
        return this;
    }

    public EntityVisualProfileCatalog Build()
    {
        var generic = _fallbacks[(int)EntityFrameKind.Entity] ??
            new EntityVisualProfileBuilder("generic", "missing_sprite")
                .ForKind(EntityFrameKind.Entity)
                .Build();
        var fallbacks = new EntityVisualProfile[4];
        for (var index = 0; index < fallbacks.Length; index++)
        {
            fallbacks[index] = _fallbacks[index] ?? generic;
        }

        return new EntityVisualProfileCatalog(_bindings.ToArray(), fallbacks);
    }
}
