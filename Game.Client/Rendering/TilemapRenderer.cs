using Game.Core;
using Game.Core.Tiles;
using Game.Core.World;
using Game.Client.Rendering.Atlas;
using Game.Client.Rendering.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering;

public sealed class TilemapRenderer : IDisposable
{
    private const int AtlasPadding = 1;
    private const int MinimumAtlasPageSize = 256;
    private const int MaximumAtlasPageSize = 2_048;
    private const int MaximumLiquidPresentationCommands = 8_192;
    private static readonly string[] LegacyTrunkVariants =
    [
        "tiles/oak_trunk_autotile",
        "tiles/pine_trunk_autotile",
        "tiles/birch_trunk_autotile"
    ];
    private static readonly string[] LegacyCanopyVariants =
    [
        "tiles/oak_leaves_autotile",
        "tiles/pine_leaves_tree_autotile",
        "tiles/birch_leaves_autotile"
    ];
    private static readonly string[] OakCanopyVariants =
    [
        "tiles/loose_oak_leaves_v2a_autotile",
        "tiles/loose_oak_leaves_v2b_autotile",
        "tiles/loose_oak_leaves_v2c_autotile"
    ];
    private static readonly string[] AutumnCanopyVariants =
    [
        "tiles/loose_autumn_leaves_v2a_autotile",
        "tiles/loose_autumn_leaves_v2b_autotile",
        "tiles/loose_autumn_leaves_v2c_autotile"
    ];
    private static readonly string[] MarshCanopyVariants =
    [
        "tiles/loose_marsh_leaves_v2a_autotile",
        "tiles/loose_marsh_leaves_v2b_autotile",
        "tiles/loose_marsh_leaves_v2c_autotile"
    ];

    private static readonly TileRegistry EmptyTiles = TileRegistry.Create(Array.Empty<TileDefinition>());
    private readonly ChunkRenderCache _cache = new();
    private readonly List<Texture2D> _atlasPages = new();
    private readonly LiquidPresentationCommand[] _liquidPresentationCommands =
        new LiquidPresentationCommand[MaximumLiquidPresentationCommands];
    private VisibleChunkRenderData[] _visibleChunks = new VisibleChunkRenderData[1024];
    private PreparedTileSprite?[][] _tileSprites = Array.Empty<PreparedTileSprite?[]>();
    private int _preparedRebuilds;
    private int _liquidPresentationCommandCount;
    private int _textureBucketCount = 1;

    public bool ShowGrid { get; set; }

    public bool DrawLiquids { get; set; } = true;

    public float LiquidOpacity { get; set; } = 0.72f;

    public int MaxCachedChunks { get; set; } = 512;

    public bool UseTextureAtlas { get; set; } = true;

    public ClientTextureRegistry? Textures { get; set; }

    public Func<ushort, string?>? TileSpriteResolver { get; set; }

    public TileRegistry? Tiles { get; set; }

    public TilemapRenderMetrics LastMetrics { get; private set; }

    public int LastTerrainDetailCommands { get; private set; }

    public TileAtlasTelemetry AtlasTelemetry { get; private set; }

    public LiquidPresentationTelemetry LiquidPresentationTelemetry { get; private set; }

    public void ConfigureContent(ClientTextureRegistry textures, TileRegistry tiles)
    {
        Textures = textures ?? throw new ArgumentNullException(nameof(textures));
        Tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
        ReleaseAtlasPages();

        var maximumNumericId = 0;
        foreach (var definition in tiles.Definitions)
        {
            if (definition.NumericId != ushort.MaxValue)
            {
                maximumNumericId = Math.Max(maximumNumericId, definition.NumericId);
            }
        }

        var sourceSprites = new SpriteTexture?[maximumNumericId + 1][];
        var textureBucketByTileMask = new int[maximumNumericId + 1][];
        var sourceTextures = new HashSet<Texture2D>(ReferenceEqualityComparer.Instance);
        var sourceFrameCount = 0;
        for (var numericId = 0; numericId <= maximumNumericId; numericId++)
        {
            if (!tiles.TryGetByNumericId((ushort)numericId, out var definition))
            {
                continue;
            }

            var variantSpriteIds = ResolveTreeVariantSpriteIds((ushort)numericId);
            var variantCount = variantSpriteIds?.Length ?? 1;
            var frames = new SpriteTexture?[16 * variantCount];
            var buckets = new int[frames.Length];
            for (var mask = 0; mask < frames.Length; mask++)
            {
                var spriteId = variantSpriteIds is null
                    ? definition.TexturePath
                    : variantSpriteIds[mask / 16];
                if (textures.TryGetRealTextureForAutoTileMask(spriteId, mask & 15, out var sprite))
                {
                    frames[mask] = sprite;
                    sourceTextures.Add(sprite.Texture);
                    sourceFrameCount++;
                }
            }

            sourceSprites[numericId] = frames;
            textureBucketByTileMask[numericId] = buckets;
        }

        var tileSprites = UseTextureAtlas
            ? BuildTileAtlas(sourceSprites, sourceFrameCount, out var atlasPlan)
            : BuildUnatlased(sourceSprites, out atlasPlan);
        var textureBuckets = new Dictionary<Texture2D, int>(ReferenceEqualityComparer.Instance);
        var textureBucketCount = 1;
        for (var numericId = 0; numericId < tileSprites.Length; numericId++)
        {
            var frames = tileSprites[numericId];
            var buckets = textureBucketByTileMask[numericId];
            if (frames is null || buckets is null)
            {
                continue;
            }

            for (var mask = 0; mask < frames.Length; mask++)
            {
                if (frames[mask] is not { } sprite)
                {
                    continue;
                }

                if (!textureBuckets.TryGetValue(sprite.Texture, out var bucketIndex))
                {
                    bucketIndex = textureBucketCount++;
                    textureBuckets.Add(sprite.Texture, bucketIndex);
                }

                buckets[mask] = bucketIndex;
            }
        }

        _tileSprites = tileSprites;
        _textureBucketCount = textureBucketCount;
        _cache.ConfigureTextureBuckets(textureBucketByTileMask, textureBucketCount);
        AtlasTelemetry = new TileAtlasTelemetry(
            sourceFrameCount,
            sourceTextures.Count,
            atlasPlan.PageCount,
            textureBucketCount,
            Math.Max(0, sourceTextures.Count - Math.Max(0, textureBucketCount - 1)),
            atlasPlan.EstimatedPageBytes);
    }

    public void Draw(RenderContext context, World world, Camera2D camera)
    {
        var tileCommands = 0;
        var terrainDetailCommands = 0;
        var liquidCommands = 0;
        camera.GetVisibleChunkBounds(out var minimum, out var maximum);
        var visibleChunkCount = CollectVisibleChunks(context, world, camera, minimum, maximum);
        var scaledTileSize = GameConstants.TileSize * camera.Zoom;
        var roundedTileSize = (int)MathF.Round(scaledTileSize);
        var useIntegerTileGrid = MathF.Abs(scaledTileSize - roundedTileSize) < 0.0001f;

        context.SpriteBatch.End();
        context.SpriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);

        for (var textureBucketIndex = 0; textureBucketIndex < _textureBucketCount; textureBucketIndex++)
        {
            for (var visibleIndex = 0; visibleIndex < visibleChunkCount; visibleIndex++)
            {
                DrawChunkTiles(
                    context,
                    world,
                    in _visibleChunks[visibleIndex],
                    scaledTileSize,
                    roundedTileSize,
                    useIntegerTileGrid,
                    textureBucketIndex,
                    ref tileCommands);
            }
        }

        for (var visibleIndex = 0; visibleIndex < visibleChunkCount; visibleIndex++)
        {
            DrawChunkTerrainDetails(
                context,
                world,
                in _visibleChunks[visibleIndex],
                scaledTileSize,
                roundedTileSize,
                useIntegerTileGrid,
                ref terrainDetailCommands);
        }

        context.SpriteBatch.End();
        context.SpriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);

        if (DrawLiquids)
        {
            DrawPreparedLiquids(context);
            liquidCommands = _liquidPresentationCommandCount;
        }

        if (ShowGrid)
        {
            DrawVisibleGrid(context, camera);
        }

        var evicted = _cache.TrimToLoadedChunks(world.Chunks);
        evicted += _cache.TrimToBudget(MaxCachedChunks);
        LastMetrics = new TilemapRenderMetrics(
            visibleChunkCount,
            _cache.CachedChunkCount,
            _preparedRebuilds,
            evicted,
            tileCommands,
            liquidCommands);
        LastTerrainDetailCommands = terrainDetailCommands;
        _preparedRebuilds = 0;
    }

    public int PrepareVisible(World world, Camera2D camera, int maxRebuilds = 2)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(camera);
        if (maxRebuilds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRebuilds));
        }

        var tiles = Tiles ?? EmptyTiles;
        var budget = _cache.CachedChunkCount == 0 ? Math.Max(maxRebuilds, 16) : maxRebuilds;
        var rebuilt = 0;
        camera.GetVisibleChunkBounds(out var minimum, out var maximum);
        EnsureVisibleChunkCapacity(minimum, maximum);
        for (var chunkY = minimum.Y; chunkY <= maximum.Y && rebuilt < budget; chunkY++)
        {
            for (var chunkX = minimum.X; chunkX <= maximum.X && rebuilt < budget; chunkX++)
            {
                if (!world.TryGetChunk(new ChunkPos(chunkX, chunkY), out var chunk) ||
                    chunk is null ||
                    !_cache.NeedsBuild(chunk))
                {
                    continue;
                }

                if (_cache.GetOrBuild(world, tiles, chunk).Rebuilt)
                {
                    rebuilt++;
                }
            }
        }

        _preparedRebuilds += rebuilt;
        return rebuilt;
    }

    public LiquidPresentationTelemetry PrepareLiquidPresentation(
        World world,
        Camera2D camera,
        Rectangle viewport,
        in WaterPresentationPalette palette,
        float opacity,
        long tickNumber)
    {
        LiquidPresentationTelemetry = LiquidPresentationPlanner.Build(
            world,
            camera,
            viewport,
            palette,
            opacity,
            tickNumber,
            _liquidPresentationCommands);
        _liquidPresentationCommandCount = LiquidPresentationTelemetry.CommandCount;
        return LiquidPresentationTelemetry;
    }

    public void ClearCache()
    {
        _cache.Clear();
        _preparedRebuilds = 0;
        _liquidPresentationCommandCount = 0;
        LastTerrainDetailCommands = 0;
        LiquidPresentationTelemetry = default;
        LastMetrics = default;
    }

    public void Dispose()
    {
        ReleaseAtlasPages();
        _tileSprites = Array.Empty<PreparedTileSprite?[]>();
        _cache.Clear();
        GC.SuppressFinalize(this);
    }

    private int CollectVisibleChunks(
        RenderContext context,
        World world,
        Camera2D camera,
        ChunkPos minimum,
        ChunkPos maximum)
    {
        var visibleCount = 0;
        for (var chunkY = minimum.Y; chunkY <= maximum.Y; chunkY++)
        {
            for (var chunkX = minimum.X; chunkX <= maximum.X; chunkX++)
            {
                if (visibleCount == _visibleChunks.Length)
                {
                    return visibleCount;
                }

                var chunkPosition = new ChunkPos(chunkX, chunkY);
                if (!world.TryGetChunk(chunkPosition, out var chunk) ||
                    chunk is null ||
                    !_cache.TryGetPrepared(chunkPosition, out var commands))
                {
                    continue;
                }

                var chunkBounds = CoordinateUtils.ChunkTileBounds(chunkPosition);
                var chunkWorld = new Vector2(
                    chunkBounds.Left * (float)GameConstants.TileSize,
                    chunkBounds.Top * (float)GameConstants.TileSize);
                var screenPosition = camera.WorldToScreen(chunkWorld, context.ViewportBounds);
                var fullyInBounds = chunkBounds.Top >= 0 &&
                    chunkBounds.Bottom <= world.HeightTiles &&
                    (world.IsHorizontallyInfinite ||
                        chunkBounds.Left >= 0 && chunkBounds.Right <= world.WidthTiles);
                _visibleChunks[visibleCount++] = new VisibleChunkRenderData(
                    chunkBounds.Left,
                    chunkBounds.Top,
                    screenPosition,
                    (int)MathF.Floor(screenPosition.X),
                    (int)MathF.Floor(screenPosition.Y),
                    (int)MathF.Ceiling(screenPosition.X),
                    (int)MathF.Ceiling(screenPosition.Y),
                    fullyInBounds,
                    commands);
            }
        }

        return visibleCount;
    }

    private void EnsureVisibleChunkCapacity(ChunkPos minimum, ChunkPos maximum)
    {
        var width = Math.Max(0L, (long)maximum.X - minimum.X + 1L);
        var height = Math.Max(0L, (long)maximum.Y - minimum.Y + 1L);
        var required = Math.Min(int.MaxValue, width * height);
        if (required <= _visibleChunks.Length)
        {
            return;
        }

        Array.Resize(ref _visibleChunks, checked((int)required));
    }

    private void DrawChunkTiles(
        RenderContext context,
        World world,
        in VisibleChunkRenderData visibleChunk,
        float scaledTileSize,
        int roundedTileSize,
        bool useIntegerTileGrid,
        int textureBucketIndex,
        ref int tileCommands)
    {
        var commands = visibleChunk.Commands;
        var bucket = commands.TextureBuckets[textureBucketIndex];
        if (bucket.Count == 0)
        {
            return;
        }

        var chunkScreen = visibleChunk.ScreenPosition;
        var preparedTiles = commands.TileCommands;
        for (var index = bucket.StartIndex; index < bucket.EndIndex; index++)
        {
            var command = preparedTiles[index];
            var tileX = visibleChunk.TileLeft + command.LocalX;
            var tileY = visibleChunk.TileTop + command.LocalY;
            if (!visibleChunk.FullyInBounds && !world.IsInBounds(tileX, tileY))
            {
                continue;
            }

            int left;
            int top;
            int right;
            int bottom;
            if (useIntegerTileGrid)
            {
                left = visibleChunk.ScreenLeft + command.LocalX * roundedTileSize;
                top = visibleChunk.ScreenTop + command.LocalY * roundedTileSize;
                right = visibleChunk.ScreenRight + (command.LocalX + 1) * roundedTileSize;
                bottom = visibleChunk.ScreenBottom + (command.LocalY + 1) * roundedTileSize;
            }
            else
            {
                left = (int)MathF.Floor(chunkScreen.X + command.LocalX * scaledTileSize);
                top = (int)MathF.Floor(chunkScreen.Y + command.LocalY * scaledTileSize);
                right = (int)MathF.Ceiling(chunkScreen.X + (command.LocalX + 1) * scaledTileSize);
                bottom = (int)MathF.Ceiling(chunkScreen.Y + (command.LocalY + 1) * scaledTileSize);
            }

            var destination = new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));

            DrawTile(
                context,
                destination,
                command.Tile,
                command.AutoTileMask,
                command.VisualVariant,
                command.VisualTransform);
            tileCommands++;
        }
    }

    private static void DrawChunkTerrainDetails(
        RenderContext context,
        World world,
        in VisibleChunkRenderData visibleChunk,
        float scaledTileSize,
        int roundedTileSize,
        bool useIntegerTileGrid,
        ref int terrainDetailCommands)
    {
        var chunkScreen = visibleChunk.ScreenPosition;
        var preparedTiles = visibleChunk.Commands.TileCommands;
        for (var index = 0; index < preparedTiles.Length; index++)
        {
            var command = preparedTiles[index];
            var tileX = visibleChunk.TileLeft + command.LocalX;
            var tileY = visibleChunk.TileTop + command.LocalY;
            if (!visibleChunk.FullyInBounds && !world.IsInBounds(tileX, tileY))
            {
                continue;
            }

            int left;
            int top;
            int right;
            int bottom;
            if (useIntegerTileGrid)
            {
                left = visibleChunk.ScreenLeft + command.LocalX * roundedTileSize;
                top = visibleChunk.ScreenTop + command.LocalY * roundedTileSize;
                right = visibleChunk.ScreenRight + (command.LocalX + 1) * roundedTileSize;
                bottom = visibleChunk.ScreenBottom + (command.LocalY + 1) * roundedTileSize;
            }
            else
            {
                left = (int)MathF.Floor(chunkScreen.X + command.LocalX * scaledTileSize);
                top = (int)MathF.Floor(chunkScreen.Y + command.LocalY * scaledTileSize);
                right = (int)MathF.Ceiling(chunkScreen.X + (command.LocalX + 1) * scaledTileSize);
                bottom = (int)MathF.Ceiling(chunkScreen.Y + (command.LocalY + 1) * scaledTileSize);
            }

            terrainDetailCommands += TerrainSurfaceDetailRenderer.Draw(
                context,
                new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top)),
                command.Tile.TileId,
                command.AutoTileMask,
                tileX,
                tileY);
        }
    }

    private void DrawPreparedLiquids(RenderContext context)
    {
        for (var index = 0; index < _liquidPresentationCommandCount; index++)
        {
            ref readonly var command = ref _liquidPresentationCommands[index];
            context.SpriteBatch.Draw(context.Pixel, command.BodyBounds, command.BodyColor);
            context.SpriteBatch.Draw(context.Pixel, command.DepthBandBounds, command.DepthColor);
            if (!command.SurfaceHighlightBounds.IsEmpty)
            {
                context.SpriteBatch.Draw(context.Pixel, command.SurfaceHighlightBounds, command.SurfaceColor);
            }

            if (!command.LeftShoreBounds.IsEmpty)
            {
                context.SpriteBatch.Draw(context.Pixel, command.LeftShoreBounds, command.ShoreColor);
            }

            if (!command.RightShoreBounds.IsEmpty)
            {
                context.SpriteBatch.Draw(context.Pixel, command.RightShoreBounds, command.ShoreColor);
            }
        }
    }

    private void DrawTile(
        RenderContext context,
        Rectangle destination,
        TileInstance tile,
        AutoTileMask autoTileMask,
        byte visualVariant,
        TileVisualTransform visualTransform)
    {
        var frames = tile.TileId < _tileSprites.Length ? _tileSprites[tile.TileId] : null;
        var sourceMask = TreeTileVisualSelector.ResolveSourceMask(autoTileMask, visualTransform);
        var frameIndex = frames is null
            ? 0
            : Math.Min(visualVariant, frames.Length / 16 - 1) * 16 + ((int)sourceMask & 15);
        if (frames is not null && frames[frameIndex] is { } preparedSprite)
        {
            context.SpriteBatch.Draw(
                preparedSprite.Texture,
                destination,
                preparedSprite.Source,
                Color.White,
                0f,
                Vector2.Zero,
                (visualTransform & TileVisualTransform.FlipHorizontal) != 0
                    ? SpriteEffects.FlipHorizontally
                    : SpriteEffects.None,
                0f);
            return;
        }

        context.SpriteBatch.Draw(context.Pixel, destination, GetTileColor(tile.TileId));
    }

    private static void DrawVisibleGrid(RenderContext context, Camera2D camera)
    {
        var color = new Color(0, 0, 0, 35);
        var visible = camera.VisibleWorldRect;
        var minimumTileX = (int)Math.Floor(visible.Left / (double)GameConstants.TileSize);
        var maximumTileX = (int)Math.Ceiling(visible.Right / (double)GameConstants.TileSize);
        var minimumTileY = Math.Max(0, (int)Math.Floor(visible.Top / (double)GameConstants.TileSize));
        var maximumTileY = (int)Math.Ceiling(visible.Bottom / (double)GameConstants.TileSize);
        var viewport = context.ViewportBounds;

        for (var tileX = minimumTileX; tileX <= maximumTileX; tileX++)
        {
            var screenX = camera.WorldToScreen(
                new Vector2(tileX * GameConstants.TileSize, 0f),
                viewport).X;
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle((int)MathF.Floor(screenX), viewport.Top, 1, viewport.Height),
                color);
        }

        for (var tileY = minimumTileY; tileY <= maximumTileY; tileY++)
        {
            var screenY = camera.WorldToScreen(
                new Vector2(0f, tileY * GameConstants.TileSize),
                viewport).Y;
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(viewport.Left, (int)MathF.Floor(screenY), viewport.Width, 1),
                color);
        }
    }

    private static Color GetTileColor(ushort tileId)
    {
        return tileId switch
        {
            KnownTileIds.Dirt => new Color(112, 76, 48),
            KnownTileIds.Grass => new Color(70, 142, 62),
            KnownTileIds.Stone => new Color(92, 94, 104),
            KnownTileIds.CopperOre => new Color(173, 99, 62),
            KnownTileIds.IronOre => new Color(158, 136, 112),
            KnownTileIds.Wood => new Color(118, 80, 43),
            KnownTileIds.Leaves => new Color(52, 132, 62),
            KnownTileIds.OakTrunk => new Color(111, 72, 40),
            KnownTileIds.OakLeaves => new Color(62, 137, 63),
            KnownTileIds.LivingWood => new Color(132, 82, 43),
            KnownTileIds.AutumnLeaves => new Color(202, 113, 42),
            KnownTileIds.MarshLeaves => new Color(50, 116, 89),
            KnownTileIds.Snow => new Color(220, 232, 239),
            KnownTileIds.Ice => new Color(126, 177, 207),
            KnownTileIds.Workbench => new Color(141, 92, 48),
            _ => Color.Magenta
        };
    }

    internal static string[]? ResolveTreeVariantSpriteIds(ushort tileId)
    {
        return tileId switch
        {
            KnownTileIds.Wood => LegacyTrunkVariants,
            KnownTileIds.Leaves => LegacyCanopyVariants,
            KnownTileIds.OakLeaves => OakCanopyVariants,
            KnownTileIds.AutumnLeaves => AutumnCanopyVariants,
            KnownTileIds.MarshLeaves => MarshCanopyVariants,
            _ => null
        };
    }

    private PreparedTileSprite?[][] BuildTileAtlas(
        SpriteTexture?[][] sources,
        int sourceFrameCount,
        out TextureAtlasPlan plan)
    {
        var result = new PreparedTileSprite?[sources.Length][];
        if (sourceFrameCount == 0)
        {
            plan = default;
            return result;
        }

        var inputs = new TileAtlasInput[sourceFrameCount];
        var sizes = new TextureAtlasSourceSize[sourceFrameCount];
        var inputCount = 0;
        var totalPaddedArea = 0L;
        var maximumPaddedDimension = 0;
        for (var numericId = 0; numericId < sources.Length; numericId++)
        {
            var frames = sources[numericId];
            if (frames is null)
            {
                continue;
            }

            result[numericId] = new PreparedTileSprite?[frames.Length];
            for (var mask = 0; mask < frames.Length; mask++)
            {
                if (frames[mask] is not { } sprite)
                {
                    continue;
                }

                var source = sprite.SourceRectangle;
                inputs[inputCount] = new TileAtlasInput(numericId, mask, sprite);
                sizes[inputCount] = new TextureAtlasSourceSize(source.Width, source.Height);
                var paddedWidth = source.Width + AtlasPadding * 2;
                var paddedHeight = source.Height + AtlasPadding * 2;
                totalPaddedArea += (long)paddedWidth * paddedHeight;
                maximumPaddedDimension = Math.Max(maximumPaddedDimension, Math.Max(paddedWidth, paddedHeight));
                inputCount++;
            }
        }

        var pageSize = ResolveAtlasPageSize(totalPaddedArea, maximumPaddedDimension);
        var placements = new TextureAtlasPlacement[inputCount];
        plan = TextureAtlasPlanner.Build(sizes.AsSpan(0, inputCount), pageSize, pageSize, AtlasPadding, placements);
        if (plan.PageCount == 0)
        {
            CopyUnatlasedInputs(inputs.AsSpan(0, inputCount), result);
            return result;
        }

        var graphicsDevice = inputs[0].Sprite.Texture.GraphicsDevice;
        var pagePixels = new Color[plan.PageCount][];
        for (var pageIndex = 0; pageIndex < plan.PageCount; pageIndex++)
        {
            pagePixels[pageIndex] = new Color[pageSize * pageSize];
        }

        var sourcePixelsByTexture = new Dictionary<Texture2D, Color[]>(ReferenceEqualityComparer.Instance);
        for (var inputIndex = 0; inputIndex < inputCount; inputIndex++)
        {
            ref readonly var placement = ref placements[inputIndex];
            if (!placement.IsPlaced)
            {
                continue;
            }

            ref readonly var input = ref inputs[inputIndex];
            var sourceTexture = input.Sprite.Texture;
            if (!sourcePixelsByTexture.TryGetValue(sourceTexture, out var sourcePixels))
            {
                sourcePixels = new Color[sourceTexture.Width * sourceTexture.Height];
                sourceTexture.GetData(sourcePixels);
                sourcePixelsByTexture.Add(sourceTexture, sourcePixels);
            }

            CopyFrameWithPadding(
                sourcePixels,
                sourceTexture.Width,
                input.Sprite.SourceRectangle,
                pagePixels[placement.PageIndex],
                pageSize,
                placement.ContentBounds,
                AtlasPadding);
        }

        for (var pageIndex = 0; pageIndex < plan.PageCount; pageIndex++)
        {
            var page = new Texture2D(graphicsDevice, pageSize, pageSize, false, SurfaceFormat.Color);
            page.SetData(pagePixels[pageIndex]);
            _atlasPages.Add(page);
        }

        for (var inputIndex = 0; inputIndex < inputCount; inputIndex++)
        {
            ref readonly var input = ref inputs[inputIndex];
            ref readonly var placement = ref placements[inputIndex];
            result[input.NumericId]![input.Mask] = placement.IsPlaced
                ? new PreparedTileSprite(_atlasPages[placement.PageIndex], placement.ContentBounds)
                : new PreparedTileSprite(input.Sprite.Texture, input.Sprite.SourceRectangle);
        }

        return result;
    }

    private static void CopyFrameWithPadding(
        Color[] sourcePixels,
        int sourceStride,
        Rectangle source,
        Color[] destinationPixels,
        int destinationStride,
        Rectangle destination,
        int padding)
    {
        for (var localY = -padding; localY < source.Height + padding; localY++)
        {
            var sampledY = Math.Clamp(localY, 0, source.Height - 1);
            var sourceRow = (source.Y + sampledY) * sourceStride;
            var destinationRow = destination.Y + localY;
            for (var localX = -padding; localX < source.Width + padding; localX++)
            {
                var sampledX = Math.Clamp(localX, 0, source.Width - 1);
                destinationPixels[destinationRow * destinationStride + destination.X + localX] =
                    sourcePixels[sourceRow + source.X + sampledX];
            }
        }
    }

    private static PreparedTileSprite?[][] BuildUnatlased(
        SpriteTexture?[][] sources,
        out TextureAtlasPlan plan)
    {
        var result = new PreparedTileSprite?[sources.Length][];
        for (var numericId = 0; numericId < sources.Length; numericId++)
        {
            var frames = sources[numericId];
            if (frames is null)
            {
                continue;
            }

            result[numericId] = new PreparedTileSprite?[frames.Length];
            for (var mask = 0; mask < frames.Length; mask++)
            {
                if (frames[mask] is { } sprite)
                {
                    result[numericId]![mask] = new PreparedTileSprite(sprite.Texture, sprite.SourceRectangle);
                }
            }
        }

        plan = default;
        return result;
    }

    private static void CopyUnatlasedInputs(
        ReadOnlySpan<TileAtlasInput> inputs,
        PreparedTileSprite?[][] destination)
    {
        for (var index = 0; index < inputs.Length; index++)
        {
            ref readonly var input = ref inputs[index];
            destination[input.NumericId]![input.Mask] = new PreparedTileSprite(
                input.Sprite.Texture,
                input.Sprite.SourceRectangle);
        }
    }

    private static int ResolveAtlasPageSize(long totalPaddedArea, int maximumPaddedDimension)
    {
        var size = MinimumAtlasPageSize;
        while (size < MaximumAtlasPageSize &&
               (size < maximumPaddedDimension || (long)size * size < totalPaddedArea))
        {
            size *= 2;
        }

        return size;
    }

    private void ReleaseAtlasPages()
    {
        for (var index = 0; index < _atlasPages.Count; index++)
        {
            _atlasPages[index].Dispose();
        }

        _atlasPages.Clear();
        AtlasTelemetry = default;
    }

    private readonly record struct VisibleChunkRenderData(
        int TileLeft,
        int TileTop,
        Vector2 ScreenPosition,
        int ScreenLeft,
        int ScreenTop,
        int ScreenRight,
        int ScreenBottom,
        bool FullyInBounds,
        PreparedChunkRenderCommands Commands);

    private readonly record struct TileAtlasInput(int NumericId, int Mask, SpriteTexture Sprite);

    private readonly record struct PreparedTileSprite(Texture2D Texture, Rectangle Source);
}
