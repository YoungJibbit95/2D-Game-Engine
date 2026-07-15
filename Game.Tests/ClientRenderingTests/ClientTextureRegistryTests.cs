using Game.Client.Rendering;
using Game.Client.Rendering.TextureGroups;
using Game.Core.Assets;
using Game.Core.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class ClientTextureRegistryTests : IDisposable
{
    private const long SpriteDecodedBytes = 16L * 16L * 4L;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yjse-client-texture-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SpriteCategoriesMapToFiveExplicitResourceGroups()
    {
        var assets = new[]
        {
            CreateSprite("ui/icon", "sprites/ui.png", SpriteAssetCategory.Ui),
            CreateSprite("world/tile", "sprites/tile.png", SpriteAssetCategory.Tile),
            CreateSprite("world/wall", "sprites/wall.png", SpriteAssetCategory.Wall),
            CreateSprite("world/object", "sprites/object.png", SpriteAssetCategory.WorldObject),
            CreateSprite("entities/item", "sprites/item.png", SpriteAssetCategory.Item),
            CreateSprite("entities/tool", "sprites/tool.png", SpriteAssetCategory.Tool),
            CreateSprite("entities/weapon", "sprites/weapon.png", SpriteAssetCategory.Weapon),
            CreateSprite("entities/entity", "sprites/entity.png", SpriteAssetCategory.Entity),
            CreateSprite("entities/crop", "sprites/crop.png", SpriteAssetCategory.Crop),
            CreateSprite("backgrounds/layer", "sprites/background.png", SpriteAssetCategory.Background),
            CreateSprite("effects/projectile", "sprites/projectile.png", SpriteAssetCategory.Projectile),
            CreateSprite("effects/particle", "sprites/particle.png", SpriteAssetCategory.Particle),
            CreateSprite("effects/effect", "sprites/effect.png", SpriteAssetCategory.Effect)
        };
        var factory = new FakeTextureResourceFactory(fileWidth: 16, fileHeight: 16);
        using var registry = CreateRegistry(factory, assets);

        Assert.Equal(TextureResourceGroup.Ui, registry.GetGroup("ui/icon"));
        Assert.Equal(TextureResourceGroup.World, registry.GetGroup("world/tile"));
        Assert.Equal(TextureResourceGroup.World, registry.GetGroup("world/wall"));
        Assert.Equal(TextureResourceGroup.World, registry.GetGroup("world/object"));
        Assert.Equal(TextureResourceGroup.Entities, registry.GetGroup("entities/item"));
        Assert.Equal(TextureResourceGroup.Entities, registry.GetGroup("entities/tool"));
        Assert.Equal(TextureResourceGroup.Entities, registry.GetGroup("entities/weapon"));
        Assert.Equal(TextureResourceGroup.Entities, registry.GetGroup("entities/entity"));
        Assert.Equal(TextureResourceGroup.Entities, registry.GetGroup("entities/crop"));
        Assert.Equal(TextureResourceGroup.Backgrounds, registry.GetGroup("backgrounds/layer"));
        Assert.Equal(TextureResourceGroup.Effects, registry.GetGroup("effects/projectile"));
        Assert.Equal(TextureResourceGroup.Effects, registry.GetGroup("effects/particle"));
        Assert.Equal(TextureResourceGroup.Effects, registry.GetGroup("effects/effect"));
        Assert.Equal(TextureResourceGroup.Ui, registry.GetGroup("unknown/fallback"));
    }

    [Fact]
    public void MultipleFramesOfOneSheetShareOneFileResource()
    {
        WriteSourceFile("sprites/sheet.png");
        var factory = new FakeTextureResourceFactory(fileWidth: 64, fileHeight: 16);
        using var registry = CreateRegistry(factory, CreateSheet("tiles/sheet", "sprites/sheet.png"));

        var first = registry.Get("tiles/sheet", 0);
        var second = registry.GetForAutoTileMask("tiles/sheet", 1);
        var secondAgain = registry.Get("TILES/SHEET", 1);

        Assert.NotSame(first, second);
        Assert.Same(second, secondAgain);
        Assert.Equal(new Rectangle(0, 0, 16, 16), first.SourceRectangle);
        Assert.Equal(new Rectangle(16, 0, 16, 16), second.SourceRectangle);
        Assert.False(first.IsPlaceholder);
        Assert.Equal(1, factory.FileLoadCount);

        var telemetry = registry.Telemetry;
        Assert.Equal(1, telemetry.ResourceCount);
        Assert.Equal(2, telemetry.FrameCount);
        Assert.Equal(0, telemetry.PlaceholderResourceCount);
        Assert.Equal(1, telemetry.FileLoadCount);
        Assert.Equal(64L * 16L * 4L, telemetry.EstimatedDecodedTextureBytes);
        Assert.True(telemetry.TotalResourceLoadMilliseconds >= 0);
        Assert.True(telemetry.TotalResourceLoadAllocatedBytes >= 0);
    }

    [Fact]
    public void DifferentSpriteIdsWithCanonicalSamePathShareOneResource()
    {
        WriteSourceFile("sprites/shared.png");
        var factory = new FakeTextureResourceFactory(fileWidth: 16, fileHeight: 16);
        using var registry = CreateRegistry(
            factory,
            CreateSprite("items/first", "sprites/shared.png"),
            CreateSprite("items/second", "sprites/./shared.png"));

        var first = registry.Get("items/first");
        var second = registry.Get("items/second");

        Assert.NotSame(first, second);
        Assert.Equal(1, factory.FileLoadCount);
        Assert.Single(factory.CreatedResources);
        Assert.Equal(1, registry.Telemetry.ResourceCount);
        Assert.Equal(2, registry.Telemetry.FrameCount);
    }

    [Fact]
    public void GroupPreloadFillsBudgetDeterministicallyAndResidentGetDoesNoIo()
    {
        WriteSourceFile("sprites/world-a.png");
        WriteSourceFile("sprites/world-b.png");
        WriteSourceFile("sprites/world-c.png");
        var factory = new FakeTextureResourceFactory(fileWidth: 16, fileHeight: 16);
        var budgets = new TextureResourceGroupBudgets(worldDecodedBytes: SpriteDecodedBytes * 2);
        using var registry = CreateRegistry(
            factory,
            budgets,
            CreateSprite("world/a", "sprites/world-a.png", SpriteAssetCategory.Tile),
            CreateSprite("world/b", "sprites/world-b.png", SpriteAssetCategory.Tile),
            CreateSprite("world/c", "sprites/world-c.png", SpriteAssetCategory.Tile));

        registry.PreloadGroup(TextureResourceGroup.World);

        Assert.True(registry.IsResident("world/a"));
        Assert.True(registry.IsResident("world/b"));
        Assert.False(registry.IsResident("world/c"));
        Assert.Equal(2, factory.FileLoadCount);

        var loadsBeforeResidentOnlyGet = factory.FileLoadCount;
        Assert.True(registry.Get("world/c").IsPlaceholder);
        Assert.Throws<InvalidOperationException>(() => registry.Acquire("world/c"));
        Assert.Equal(loadsBeforeResidentOnlyGet, factory.FileLoadCount);

        var group = registry.GetGroupTelemetry(TextureResourceGroup.World);
        Assert.Equal(SpriteDecodedBytes * 2, group.DecodedByteBudget);
        Assert.Equal(2, group.ResidentResourceCount);
        Assert.Equal(SpriteDecodedBytes * 2, group.ResidentDecodedBytes);
        Assert.Equal(0, group.EvictedResourceCount);
        Assert.Equal(1, group.BudgetRejectedResourceCount);
    }

    [Fact]
    public void MakeResidentEvictsLeastRecentlyUsedResourceWithStableTelemetry()
    {
        WriteSourceFile("sprites/world-a.png");
        WriteSourceFile("sprites/world-b.png");
        WriteSourceFile("sprites/world-c.png");
        var factory = new FakeTextureResourceFactory(fileWidth: 16, fileHeight: 16);
        var budgets = new TextureResourceGroupBudgets(worldDecodedBytes: SpriteDecodedBytes * 2);
        using var registry = CreateRegistry(
            factory,
            budgets,
            CreateSprite("world/a", "sprites/world-a.png", SpriteAssetCategory.Tile),
            CreateSprite("world/b", "sprites/world-b.png", SpriteAssetCategory.Tile),
            CreateSprite("world/c", "sprites/world-c.png", SpriteAssetCategory.Tile));
        registry.PreloadGroup(TextureResourceGroup.World);
        _ = registry.Get("world/a");

        Assert.True(registry.MakeResident("world/c"));

        Assert.True(registry.IsResident("world/a"));
        Assert.False(registry.IsResident("world/b"));
        Assert.True(registry.IsResident("world/c"));
        Assert.Equal(1, factory.GetLoadedResource("sprites/world-b.png").DisposeCount);

        var group = registry.GetGroupTelemetry(TextureResourceGroup.World);
        Assert.Equal(2, group.ResidentResourceCount);
        Assert.Equal(SpriteDecodedBytes * 2, group.ResidentDecodedBytes);
        Assert.Equal(1, group.EvictedResourceCount);
        Assert.Equal(SpriteDecodedBytes, group.EvictedDecodedBytes);
        Assert.Equal(1, group.BudgetRejectedResourceCount);
        Assert.Equal(1, registry.ResidencyTelemetry.EvictedResourceCount);
        Assert.Equal(SpriteDecodedBytes, registry.ResidencyTelemetry.EvictedDecodedBytes);
    }

    [Fact]
    public void AliasLeasePinsSharedResourceUntilLastReferenceIsReleased()
    {
        WriteSourceFile("sprites/shared.png");
        WriteSourceFile("sprites/z-replacement.png");
        var factory = new FakeTextureResourceFactory(fileWidth: 16, fileHeight: 16);
        var budgets = new TextureResourceGroupBudgets(entitiesDecodedBytes: SpriteDecodedBytes);
        using var registry = CreateRegistry(
            factory,
            budgets,
            CreateSprite("items/alias-a", "sprites/shared.png"),
            CreateSprite("items/alias-b", "sprites/./shared.png"),
            CreateSprite("items/replacement", "sprites/z-replacement.png"));
        registry.PreloadGroup(TextureResourceGroup.Entities);

        using (var first = registry.Acquire("items/alias-a"))
        using (var second = registry.Acquire("items/alias-b"))
        {
            Assert.Equal(1, factory.FileLoadCount);
            Assert.Equal(1, registry.ResidencyTelemetry.PinnedResourceCount);
            Assert.Equal(1, registry.GetGroupTelemetry(TextureResourceGroup.Entities).PinnedResourceCount);
            Assert.False(registry.MakeResident("items/replacement"));
            Assert.Equal(0, registry.ResidencyTelemetry.EvictedResourceCount);
        }

        Assert.Equal(0, registry.ResidencyTelemetry.PinnedResourceCount);
        Assert.True(registry.MakeResident("items/replacement"));
        Assert.False(registry.IsResident("items/alias-a"));
        Assert.False(registry.IsResident("items/alias-b"));
        Assert.Equal(1, factory.GetLoadedResource("sprites/shared.png").DisposeCount);
    }

    [Fact]
    public void PreloadedFrameLookupHasNoSteadyAllocationOrFirstUseLoad()
    {
        WriteSourceFile("sprites/sheet.png");
        var factory = new FakeTextureResourceFactory(fileWidth: 64, fileHeight: 16);
        using var registry = CreateRegistry(factory, CreateSheet("tiles/sheet", "sprites/sheet.png"));
        registry.PreloadGroup(TextureResourceGroup.World);
        var loadsAfterPreload = factory.FileLoadCount;
        _ = registry.Get("tiles/sheet", 0);

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        SpriteTexture? last = registry.Get("tiles/sheet", 3);
        var firstUseAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            last = registry.Get("tiles/sheet", iteration & 3);
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        GC.KeepAlive(last);
        Assert.Equal(0, firstUseAllocatedBytes);
        Assert.Equal(0, allocatedBytes);
        Assert.Equal(loadsAfterPreload, factory.FileLoadCount);
    }

    [Fact]
    public void MissingSheetFramesAndAliasesShareOnePlaceholderResource()
    {
        var factory = new FakeTextureResourceFactory(fileWidth: 1, fileHeight: 1);
        using var registry = CreateRegistry(
            factory,
            CreateSheet("tiles/missing-a", "sprites/missing.png"),
            CreateSheet("tiles/missing-b", "sprites/./missing.png"));

        var first = registry.Get("tiles/missing-a", 0);
        var secondFrame = registry.Get("tiles/missing-a", 1);
        var alias = registry.Get("tiles/missing-b", 0);

        Assert.NotSame(first, secondFrame);
        Assert.NotSame(first, alias);
        Assert.True(first.IsPlaceholder);
        Assert.Equal(0, factory.FileLoadCount);
        Assert.Equal(1, factory.PlaceholderCreateCount);

        var telemetry = registry.Telemetry;
        Assert.Equal(1, telemetry.ResourceCount);
        Assert.Equal(3, telemetry.FrameCount);
        Assert.Equal(1, telemetry.PlaceholderResourceCount);
        Assert.Equal(0, telemetry.FileLoadCount);
    }

    [Fact]
    public void DisposeDisposesSharedResourceExactlyOnceAndRejectsFurtherGets()
    {
        WriteSourceFile("sprites/shared.png");
        var factory = new FakeTextureResourceFactory(fileWidth: 64, fileHeight: 16);
        var registry = CreateRegistry(factory, CreateSheet("tiles/sheet", "sprites/shared.png"));
        registry.Get("tiles/sheet", 0);
        registry.Get("tiles/sheet", 1);
        var resource = Assert.Single(factory.CreatedResources);

        registry.Dispose();
        registry.Dispose();

        Assert.Equal(1, resource.DisposeCount);
        Assert.Equal(0, registry.Telemetry.ResourceCount);
        Assert.Equal(0, registry.Telemetry.EstimatedDecodedTextureBytes);
        Assert.Throws<ObjectDisposedException>(() => registry.Get("tiles/sheet"));
    }

    [Fact]
    public void PreloadAllLoadsResourcesBeforeFrameLookupAndIsIdempotent()
    {
        WriteSourceFile("sprites/sheet.png");
        var factory = new FakeTextureResourceFactory(fileWidth: 64, fileHeight: 16);
        using var registry = CreateRegistry(factory, CreateSheet("tiles/sheet", "sprites/sheet.png"));

        registry.PreloadAll();
        var loadsAfterPreload = factory.FileLoadCount;
        registry.PreloadAll();
        for (var frameIndex = 0; frameIndex < 4; frameIndex++)
        {
            _ = registry.Get("tiles/sheet", frameIndex);
        }

        Assert.True(registry.IsPreloaded);
        Assert.Equal(1, loadsAfterPreload);
        Assert.Equal(loadsAfterPreload, factory.FileLoadCount);
        Assert.Equal(5, registry.ExpectedPreloadedFrameCount);
        Assert.Equal(5, registry.Telemetry.FrameCount);
        Assert.Equal(1, registry.Telemetry.PlaceholderResourceCount);
        Assert.Equal((64L * 16L + 16L * 16L) * 4L, registry.Telemetry.EstimatedDecodedTextureBytes);
        Assert.Equal(registry.FallbackAssetId, Assert.Single(registry.PlaceholderAssetIds));

        var telemetryAfterPreload = registry.Telemetry;
        Assert.True(registry.Get("unknown/sprite").IsPlaceholder);
        Assert.Equal(telemetryAfterPreload, registry.Telemetry);
    }

    [Fact]
    public void PreloadAllRejectsWrongPngDimensionsAndDisposesRejectedResource()
    {
        WriteSourceFile("sprites/wrong-size.png");
        var factory = new FakeTextureResourceFactory(fileWidth: 32, fileHeight: 16);
        using var registry = CreateRegistry(factory, CreateSprite("items/wrong-size", "sprites/wrong-size.png"));

        var exception = Assert.Throws<InvalidDataException>(registry.PreloadAll);

        Assert.Contains("expects 16x16", exception.Message, StringComparison.Ordinal);
        Assert.False(registry.IsPreloaded);
        Assert.Equal(1, Assert.Single(factory.CreatedResources, resource => !resource.IsPlaceholder).DisposeCount);
        Assert.Equal(1, registry.Telemetry.InvalidResourceCount);
        Assert.Equal(SpriteDecodedBytes, registry.Telemetry.EstimatedDecodedTextureBytes);
    }

    [Fact]
    public void ModSpriteOverridesAndModOnlySpritesResolveAgainstTheirSourcePackRoots()
    {
        var baseRoot = Path.Combine(_root, "Base");
        var modsRoot = Path.Combine(_root, "Mods");
        var modRoot = Path.Combine(modsRoot, "VisualMod");
        WriteSpriteManifest(
            Path.Combine(baseRoot, "assets", "sprites.json"),
            """
            {
              "sprites": [
                { "id": "ui/overridden", "path": "sprites/shared.png", "category": "Ui", "width": 16, "height": 16 },
                { "id": "ui/base-only", "path": "sprites/base-only.png", "category": "Ui", "width": 16, "height": 16 }
              ]
            }
            """);
        WriteSpriteManifest(
            Path.Combine(modRoot, "assets", "sprites.json"),
            """
            {
              "sprites": [
                { "id": "ui/overridden", "path": "sprites/shared.png", "category": "Ui", "width": 16, "height": 16 },
                { "id": "ui/mod-only", "path": "sprites/mod-only.png", "category": "Ui", "width": 16, "height": 16 }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(modRoot, "mod.json"),
            """{ "id": "visual_mod", "displayName": "Visual Mod" }""");
        WriteFile(Path.Combine(baseRoot, "sprites", "shared.png"));
        WriteFile(Path.Combine(baseRoot, "sprites", "base-only.png"));
        WriteFile(Path.Combine(modRoot, "sprites", "shared.png"));
        WriteFile(Path.Combine(modRoot, "sprites", "mod-only.png"));

        var content = new GameContentLoader().LoadWithMods(baseRoot, modsRoot);
        var overridden = content.Database.SpriteAssets.GetById("ui/overridden");
        var baseOnly = content.Database.SpriteAssets.GetById("ui/base-only");
        var modOnly = content.Database.SpriteAssets.GetById("ui/mod-only");
        Assert.Equal(Path.GetFullPath(modRoot), overridden.SourceRoot);
        Assert.Equal(Path.GetFullPath(baseRoot), baseOnly.SourceRoot);
        Assert.Equal(Path.GetFullPath(modRoot), modOnly.SourceRoot);
        Assert.Contains(
            content.Report.Overrides,
            item => item.ContentKind == "sprite" && item.ContentId == "ui/overridden");

        var factory = new FakeTextureResourceFactory(fileWidth: 16, fileHeight: 16);
        using var registry = new ClientTextureRegistry(baseRoot, content.Database.SpriteAssets, factory);
        Assert.False(registry.Get("ui/overridden").IsPlaceholder);
        Assert.False(registry.Get("ui/base-only").IsPlaceholder);
        Assert.False(registry.Get("ui/mod-only").IsPlaceholder);

        Assert.Contains(Path.GetFullPath(Path.Combine(modRoot, "sprites", "shared.png")), factory.LoadedPaths);
        Assert.Contains(Path.GetFullPath(Path.Combine(baseRoot, "sprites", "base-only.png")), factory.LoadedPaths);
        Assert.Contains(Path.GetFullPath(Path.Combine(modRoot, "sprites", "mod-only.png")), factory.LoadedPaths);
        Assert.DoesNotContain(Path.GetFullPath(Path.Combine(baseRoot, "sprites", "shared.png")), factory.LoadedPaths);
    }

    [Fact]
    public void SpriteSourcePathsRejectAbsoluteAndParentTraversalAuthoredPaths()
    {
        var absolute = CreateSprite("ui/absolute", Path.GetFullPath(Path.Combine(_root, "outside.png")));
        var escaping = CreateSprite("ui/escaping", "../outside.png");

        Assert.Throws<RegistryValidationException>(() => SpriteAssetRegistry.Create([absolute]));
        Assert.Throws<RegistryValidationException>(() => SpriteAssetRegistry.Create([escaping]));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ClientTextureRegistry CreateRegistry(FakeTextureResourceFactory factory, params SpriteAssetDefinition[] assets)
    {
        return new ClientTextureRegistry(_root, SpriteAssetRegistry.Create(assets), factory);
    }

    private ClientTextureRegistry CreateRegistry(
        FakeTextureResourceFactory factory,
        TextureResourceGroupBudgets budgets,
        params SpriteAssetDefinition[] assets)
    {
        return new ClientTextureRegistry(_root, SpriteAssetRegistry.Create(assets), factory, budgets);
    }

    private void WriteSourceFile(string relativePath)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        WriteFile(path);
    }

    private static void WriteFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [0x59, 0x4A, 0x53, 0x45]);
    }

    private static void WriteSpriteManifest(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private static SpriteAssetDefinition CreateSprite(
        string id,
        string path,
        SpriteAssetCategory category = SpriteAssetCategory.Item)
    {
        return new SpriteAssetDefinition
        {
            Id = id,
            Path = path,
            Category = category,
            Width = 16,
            Height = 16
        };
    }

    private static SpriteAssetDefinition CreateSheet(string id, string path)
    {
        return new SpriteAssetDefinition
        {
            Id = id,
            Path = path,
            Category = SpriteAssetCategory.Tile,
            Width = 64,
            Height = 16,
            Frames =
            [
                new SpriteFrameDefinition { Id = "mask-0", X = 0, Y = 0, Width = 16, Height = 16, AutoTileMask = 0 },
                new SpriteFrameDefinition { Id = "mask-1", X = 16, Y = 0, Width = 16, Height = 16, AutoTileMask = 1 },
                new SpriteFrameDefinition { Id = "mask-2", X = 32, Y = 0, Width = 16, Height = 16, AutoTileMask = 2 },
                new SpriteFrameDefinition { Id = "mask-3", X = 48, Y = 0, Width = 16, Height = 16, AutoTileMask = 3 }
            ]
        };
    }

    private sealed class FakeTextureResourceFactory : ITextureResourceFactory
    {
        private readonly int _fileWidth;
        private readonly int _fileHeight;

        public FakeTextureResourceFactory(int fileWidth, int fileHeight)
        {
            _fileWidth = fileWidth;
            _fileHeight = fileHeight;
        }

        public int FileLoadCount { get; private set; }

        public int PlaceholderCreateCount { get; private set; }

        public List<FakeTextureResource> CreatedResources { get; } = new();

        public List<string> LoadedPaths { get; } = new();

        public Dictionary<string, FakeTextureResource> LoadedResourcesByPath { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ITextureResource LoadFromFile(string canonicalPath)
        {
            Assert.True(Path.IsPathFullyQualified(canonicalPath));
            FileLoadCount++;
            LoadedPaths.Add(canonicalPath);
            var resource = new FakeTextureResource(_fileWidth, _fileHeight, isPlaceholder: false);
            CreatedResources.Add(resource);
            LoadedResourcesByPath.Add(canonicalPath, resource);
            return resource;
        }

        public FakeTextureResource GetLoadedResource(string relativePath)
        {
            var suffix = relativePath.Replace('/', Path.DirectorySeparatorChar);
            foreach (var pair in LoadedResourcesByPath)
            {
                if (pair.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }

            throw new Xunit.Sdk.XunitException($"No fake texture resource was loaded for '{relativePath}'.");
        }

        public ITextureResource CreatePlaceholder(SpriteAssetDefinition asset)
        {
            PlaceholderCreateCount++;
            var resource = new FakeTextureResource(asset.Width, asset.Height, isPlaceholder: true);
            CreatedResources.Add(resource);
            return resource;
        }
    }

    private sealed class FakeTextureResource : ITextureResource
    {
        private bool _disposed;

        public FakeTextureResource(int width, int height, bool isPlaceholder)
        {
            Width = width;
            Height = height;
            IsPlaceholder = isPlaceholder;
        }

        public Texture2D Texture => throw new InvalidOperationException("The fake resource has no GraphicsDevice texture.");

        public int Width { get; }

        public int Height { get; }

        public bool IsPlaceholder { get; }

        public long DecodedByteCount => Width * (long)Height * 4L;

        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            DisposeCount++;
            _disposed = true;
        }
    }
}
