using System.Runtime.InteropServices;
using Game.Client.Rendering.Effects;
using Game.Client.Rendering.Lighting;
using Game.Core;
using Game.Core.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class LightingPreparationCacheTests
{
    private static LightingFrameParameters Frame(long tick = 1) =>
        new(0.5f, 0.35f, 1f, 1f, 0.3f, 0.1f, 0.9f, 0.65f, 0, tick);

    [Fact]
    public void RawAliasesSettersAndLoadInvalidateAfterDirtyFlagsAreCleared()
    {
        var fixture = new Fixture();
        fixture.Compare(Frame(), ambientHit: 0, portalHit: 0);
        fixture.Compare(Frame(2), ambientHit: 1, portalHit: 1);
        var position = new TilePos(-2, 3);
        var chunk = fixture.World.GetOrCreateChunk(CoordinateUtils.TileToChunk(position));
        var local = CoordinateUtils.LocalTileInChunk(position);
        var index = local.Y * GameConstants.ChunkSize + local.X;
        var alias = chunk.Tiles;
        foreach (var flags in new[]
        {
            TileFlags.Solid, TileFlags.Solid | TileFlags.HalfBlock,
            TileFlags.Solid | TileFlags.SlopeAscendingLeft,
            TileFlags.Solid | TileFlags.SlopeAscendingRight,
            TileFlags.Solid | TileFlags.IsActuated
        })
        {
            alias[index].Flags = flags;
            fixture.World.ClearAllDirtyFlags();
            fixture.Compare(Frame(), ambientHit: 0, portalHit: 0);
        }

        alias[index].Light++;
        fixture.Compare(Frame(), ambientHit: 0, portalHit: 0);
        fixture.World.SetTileLight(position.X, position.Y, 121);
        fixture.World.ClearAllDirtyFlags();
        fixture.Compare(Frame(), ambientHit: 0, portalHit: 0);
        var tile = fixture.World.GetTile(position.X, position.Y);
        fixture.World.SetTile(position.X, position.Y, tile with { Flags = TileFlags.Solid });
        fixture.Compare(Frame(), ambientHit: 0, portalHit: 0);
        var replacement = (TileInstance[])alias.Clone();
        replacement[index].Light = 17;
        chunk.LoadTiles(replacement);
        chunk.ClearDirtyFlags();
        fixture.Compare(Frame(), ambientHit: 0, portalHit: 0);
        fixture.Compare(Frame(), ambientHit: 1, portalHit: 1);
        // Clear/mark operations without a changed sampled payload are not mutations.
        chunk.MarkLightDirty();
        chunk.ClearLightUpdateFlag();
        chunk.ClearMeshRebuildFlag();
        chunk.ClearDirtyFlags();
        fixture.Compare(Frame(), ambientHit: 1, portalHit: 1);
    }

    [Fact]
    public void NegativeChunkEdgesResidencyReplacementAndWorldIdentityStayExact()
    {
        var fixture = new Fixture(cachePixels: 1024, columns: 68)
        {
            Bounds = new Rectangle(-544, 0, 1088, 128),
            Profile = Profile(new Point(68, 8))
        };
        fixture.Compare(Frame(), ambientHit: 0);
        fixture.Compare(Frame(), ambientHit: 1);
        foreach (var x in new[] { -33, -32, -1, 0, 31, 32 })
        {
            fixture.World.SetTileLight(x, 2, 153);
            fixture.World.ClearAllDirtyFlags();
            fixture.Compare(Frame(), ambientHit: 0, portalHit: 0);
        }

        var changed = new ChunkPos(-1, 0);
        Assert.True(fixture.World.UnloadChunk(changed, requireClean: false));
        fixture.Compare(Frame(), ambientHit: 0);
        fixture.World.GetOrCreateChunk(changed);
        fixture.Compare(Frame(), ambientHit: 0);
        // Identical empty payload and coordinates still represent a new residency.
        var dictionary = Assert.IsType<Dictionary<ChunkPos, Chunk>>(fixture.World.Chunks);
        dictionary[changed] = new Chunk(changed);
        fixture.Compare(Frame(), ambientHit: 0);
        fixture.Compare(Frame(), ambientHit: 1);
        fixture.World.GetOrCreateChunk(new ChunkPos(20, 0));
        fixture.Compare(Frame(), ambientHit: 1);
        fixture.World = CreateWorld();
        fixture.Compare(Frame(), ambientHit: 0);
    }

    [Fact]
    public void FinitePartialEdgesAndAbsentAirMaterializationAreDependencies()
    {
        var fixture = new Fixture
        {
            World = CreateWorld(finite: true), Bounds = new Rectangle(992, 0, 128, 128)
        };
        fixture.Compare(Frame(), ambientHit: 0);
        fixture.Compare(Frame(), ambientHit: 1);
        Assert.False(fixture.World.TrySetTile(64, 2, TileInstance.Air));
        fixture.Compare(Frame(), ambientHit: 1);
        fixture.World.SetTileLight(63, 2, 152);
        fixture.Compare(Frame(), ambientHit: 0);

        fixture.World = new World(1, 128, WorldMetadata.CreateDefault(9), isHorizontallyInfinite: true);
        fixture.Bounds = new Rectangle(-64, 0, 128, 128);
        fixture.Compare(Frame(), ambientHit: 0);
        fixture.Compare(Frame(), ambientHit: 1);
        fixture.World.SetTileLight(-1, 2, 0);
        fixture.Compare(Frame(), ambientHit: 0);
    }

    [Fact]
    public void ResolvedSurfaceValuesInvalidatePortalsWithoutDiscardingUnchangedAmbient()
    {
        var fixture = new Fixture();
        var height = 2;
        Func<int, int> resolver = _ => height;
        fixture.Compare(Frame(), resolver: resolver, ambientHit: 0, portalHit: 0);
        fixture.Compare(Frame(), resolver: resolver, ambientHit: 1, portalHit: 1);
        height = 4;
        fixture.Compare(Frame(), resolver: resolver, ambientHit: 1, portalHit: 0);
        fixture.Compare(Frame(), resolver: resolver, ambientHit: 1, portalHit: 1);
        fixture.Compare(Frame(), surface: null, ambientHit: 1, portalHit: 0);
        fixture.Compare(Frame(), surface: 4, ambientHit: 1, portalHit: 0);
        fixture.Compare(Frame(), resolver: resolver, ambientHit: 1, portalHit: 1);
    }

    [Fact]
    public void ContextQualityResetAndReenableNeverRestoreOldPreparations()
    {
        var fixture = new Fixture();
        var viewport = new Rectangle(0, 0, 640, 360);
        var camera = new Vector2(-3, 63);
        fixture.Cache.SetContext(viewport, camera, 2, 1);
        fixture.Compare(Frame(), ambientHit: 0);
        fixture.Cache.SetContext(viewport, camera, 2, 1);
        fixture.Compare(Frame(), ambientHit: 1);
        fixture.Cache.SetContext(viewport, camera + new Vector2(0.125f, 0), 2, 1);
        fixture.Compare(Frame(), ambientHit: 0);
        fixture.Cache.SetContext(viewport, camera, 2.25f, 1);
        fixture.Compare(Frame(), ambientHit: 0);
        fixture.Cache.SetContext(viewport with { Width = 800 }, camera, 2.25f, 1);
        fixture.Compare(Frame(), ambientHit: 0);
        fixture.Cache.SetContext(viewport, camera, 2.25f, 2);
        fixture.Compare(Frame(), ambientHit: 0);
        fixture.Bounds = fixture.Bounds with { X = fixture.Bounds.X - 1 };
        fixture.Compare(Frame(), ambientHit: 0);
        fixture.Profile = fixture.Profile with { AmbientOcclusionRadius = 0 };
        fixture.Compare(Frame(), ambientHit: 0);
        fixture.Compare(Frame(), ambientHit: 1);
        fixture.Compare(Frame(), useCache: false);
        fixture.Cache.Reset();
        fixture.Compare(Frame(), ambientHit: 0);
        fixture.Compare(Frame(), ambientHit: 1);
    }

    [Fact]
    public void SourcesFlickerSolarWeatherAndAmbientAlwaysExecuteAfterSubstageHits()
    {
        var fixture = new Fixture();
        var lights = new[]
        {
            ScreenSpaceLight.Torch(new Vector2(-16, 48), 7, 64, 0.85f),
            ScreenSpaceLight.Torch(new Vector2(40, 56), 29, 72, 0.7f) with { Color = Color.CornflowerBlue }
        };
        fixture.Compare(Frame(3), lights, ambientHit: 0);
        var beforeFlicker = (float[])fixture.Candidate.Planes[1].Clone();
        fixture.Compare(Frame(4), lights, ambientHit: 1, portalHit: 1);
        Assert.False(BitwiseEqual(beforeFlicker, fixture.Candidate.Planes[1]));
        lights[0] = lights[0] with { WorldPosition = new Vector2(-8, 44), Intensity = 0.45f };
        fixture.Compare(Frame(8), lights, ambientHit: 1, portalHit: 1);
        (lights[0], lights[1]) = (lights[1], lights[0]);
        fixture.Compare(Frame(9) with { WeatherOcclusion = 0.9f, AmbientLight = 0.1f }, lights,
            ambientHit: 1, portalHit: 1);
        fixture.Compare(Frame(10) with { NormalizedTimeOfDay = 0, CaveBlend = 0.85f, CaveResidualLight = 0.3f },
            lights, lightCount: 1, ambientHit: 1, portalHit: 1);
        fixture.Compare(Frame(11) with { SkyLightMultiplier = 0.2f, ShadowStrength = 0.5f },
            lights, lightCount: 0, ambientHit: 1, portalHit: 1);
    }

    [Fact]
    public void IncomingBloomIsPreservedWhenDirectionalPassDoesNotRun()
    {
        var fixture = new Fixture();
        var frame = Frame() with { SkyLightMultiplier = 0.0001f };
        for (var step = 0; step < 4; step++)
        {
            Array.Fill(fixture.Reference.Planes[4], step % 2 == 0 ? 0.75f : 0);
            Array.Fill(fixture.Candidate.Planes[4], step % 2 == 0 ? 0.75f : 0);
            var telemetry = fixture.Compare(frame, ambientHit: step == 0 ? 0 : 1,
                portalHit: step == 0 ? 0 : 1);
            Assert.Equal(0, telemetry.RaysCast);
        }
    }

    [Fact]
    public void TemporalSettlingAndHistoryGapStayBitwiseEqualWhileRawStagesHit()
    {
        var fixture = new Fixture();
        fixture.Compare(Frame(1), temporal: true, ambientHit: 0);
        fixture.Compare(Frame(2) with { ShadowStrength = 0.8f }, temporal: true, ambientHit: 1);
        Assert.True(fixture.LastTemporal.ReprojectedPixels > 0);
        var settling = (float[])fixture.Candidate.Planes[0].Clone();
        fixture.Compare(Frame(3) with { ShadowStrength = 0.8f }, temporal: true, ambientHit: 1);
        Assert.False(BitwiseEqual(settling, fixture.Candidate.Planes[0]));
        fixture.Compare(Frame(15) with { ShadowStrength = 0.8f }, temporal: true, ambientHit: 1);
        Assert.True(fixture.LastTemporal.HistoryRejected);
        fixture.Compare(Frame(16), temporal: true, ambientHit: 1);
        Assert.False(fixture.LastTemporal.HistoryRejected);
    }

    [Fact]
    public void PixelAndDependencyCapacitiesBypassWithoutGrowingOrChangingOutput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LightingPreparationCache(16_385, 128));
        var fixture = new Fixture(cachePixels: 256, columns: 256)
        {
            Profile = Profile(new Point(17, 16))
        };
        var payload = fixture.Cache.PayloadBytes;
        fixture.Compare(Frame(), ambientHit: 0, portalHit: 0);
        Assert.Equal(1, fixture.Cache.LastTelemetry.Bypasses);
        fixture.Profile = Profile(new Point(129, 1));
        fixture.Bounds = new Rectangle(0, 0, GameConstants.PixelsPerChunk * 129, 32);
        fixture.Compare(Frame(), ambientHit: 0, portalHit: 0);
        Assert.Equal(1, fixture.Cache.LastTelemetry.Bypasses);
        Assert.Equal(payload, fixture.Cache.PayloadBytes);
        fixture.Profile = Profile(new Point(16, 16));
        fixture.Bounds = new Rectangle(-64, 0, 128, 128);
        fixture.Compare(Frame(), ambientHit: 0);
        fixture.Compare(Frame(), ambientHit: 1, portalHit: 1);
    }

    [Fact]
    public void MaximumPixelAndChunkIdentityBoundariesStillPermitExactReuse()
    {
        var fixture = new Fixture(cachePixels: 16_384, columns: 128, bufferPixels: 16_384)
        {
            Profile = Profile(new Point(128, 128)), Bounds = new Rectangle(-512, 0, 1024, 128)
        };
        var payload = fixture.Cache.PayloadBytes;
        fixture.Compare(Frame(), ambientHit: 0, portalHit: 0);
        fixture.Compare(Frame(), ambientHit: 1, portalHit: 1);
        Assert.Equal(16_384, fixture.Cache.LastTelemetry.DependencySamples);
        fixture.Profile = Profile(new Point(128, 1));
        fixture.Bounds = new Rectangle(0, 0, GameConstants.PixelsPerChunk * 128, 32);
        fixture.Compare(Frame(), ambientHit: 0, portalHit: 0);
        fixture.Compare(Frame(), ambientHit: 1, portalHit: 1);
        Assert.Equal(0, fixture.Cache.LastTelemetry.Bypasses);
        Assert.Equal(payload, fixture.Cache.PayloadBytes);
    }

    [Fact]
    [Trait("Category", "StrictAllocation")]
    public void WarmHitsAndSampleMissesRemainAllocationFree()
    {
        var fixture = new Fixture();
        var frame = Frame();
        for (var iteration = 0; iteration < 1000; iteration++) { fixture.BuildCandidate(frame); }
        var beforeHits = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1000; iteration++) { fixture.BuildCandidate(frame); }
        var hitBytes = GC.GetAllocatedBytesForCurrentThread() - beforeHits;
        Assert.Equal(1, fixture.Cache.LastTelemetry.AmbientHits);
        Assert.Equal(1, fixture.Cache.LastTelemetry.PortalHits);

        var chunk = fixture.World.GetOrCreateChunk(new ChunkPos(-1, 0));
        var local = CoordinateUtils.LocalTileInChunk(-2, 3);
        var index = local.Y * GameConstants.ChunkSize + local.X;
        var tiles = chunk.Tiles;
        for (var iteration = 0; iteration < 1000; iteration++)
        {
            tiles[index].Light = unchecked((byte)(tiles[index].Light + 1));
            fixture.BuildCandidate(frame);
        }
        var beforeMisses = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1000; iteration++)
        {
            tiles[index].Light = unchecked((byte)(tiles[index].Light + 1));
            fixture.BuildCandidate(frame);
        }
        var missBytes = GC.GetAllocatedBytesForCurrentThread() - beforeMisses;
        Assert.Equal(0, fixture.Cache.LastTelemetry.AmbientHits);
        Assert.Equal(0, fixture.Cache.LastTelemetry.PortalHits);
        Assert.Equal(0, hitBytes);
        Assert.Equal(0, missBytes);
    }

    private static PresentationQualityProfile Profile(Point size) =>
        PresentationQualityProfile.Create(PresentationQualityTier.High, new Rectangle(0, 0, 640, 360)) with { MaskSize = size };

    private static World CreateWorld(bool finite = false)
    {
        var world = new World(64, 128, new WorldMetadata("cache parity", 1337, DateTimeOffset.UnixEpoch), !finite);
        for (var y = 0; y < 8; y++)
        {
            for (var x = finite ? 0 : -40; x < (finite ? 64 : 40); x++)
            {
                var solid = y >= 6 || x == 1 && y >= 2;
                world.SetTile(x, y, new TileInstance
                {
                    TileId = solid ? (ushort)1 : (ushort)0,
                    Flags = solid ? TileFlags.Solid : TileFlags.None,
                    Light = (byte)(70 + (x & 3))
                });
            }
        }
        world.ClearAllDirtyFlags();
        return world;
    }

    private static bool BitwiseEqual(float[] first, float[] second) =>
        MemoryMarshal.AsBytes(first.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(second.AsSpan()));

    private sealed class FieldBuffers(int capacity)
    {
        public float[][] Planes { get; } = Enumerable.Range(0, 6).Select(_ => new float[capacity]).ToArray();
        public float[][] History { get; } = Enumerable.Range(0, 4).Select(_ => new float[capacity]).ToArray();
    }

    private sealed class Fixture
    {
        private bool _hasHistory;
        private Rectangle _previousBounds;
        private long _previousFrame;

        public Fixture(int cachePixels = 256, int columns = 16, int bufferPixels = 1024)
        {
            Cache = new LightingPreparationCache(cachePixels, columns);
            Reference = new FieldBuffers(bufferPixels);
            Candidate = new FieldBuffers(bufferPixels);
        }

        public World World { get; set; } = CreateWorld();
        public Rectangle Bounds { get; set; } = new(-64, 0, 128, 128);
        public PresentationQualityProfile Profile { get; set; } = LightingPreparationCacheTests.Profile(new Point(16, 16));
        public LightingPreparationCache Cache { get; }
        public FieldBuffers Reference { get; }
        public FieldBuffers Candidate { get; }
        public LightingTemporalStabilizationTelemetry LastTemporal { get; private set; }

        public LightingBuildTelemetry BuildCandidate(LightingFrameParameters frame) =>
            Build(Candidate, frame, ReadOnlySpan<ScreenSpaceLight>.Empty, 3, null, Cache);

        public LightingBuildTelemetry Compare(LightingFrameParameters frame, ScreenSpaceLight[]? lights = null,
            int? lightCount = null, int? surface = 3, Func<int, int>? resolver = null,
            bool temporal = false, bool useCache = true, int? ambientHit = null, int? portalHit = null)
        {
            var sources = lights is null ? ReadOnlySpan<ScreenSpaceLight>.Empty : lights.AsSpan(0, lightCount ?? lights.Length);
            var reference = Build(Reference, frame, sources, surface, resolver, null);
            var candidate = Build(Candidate, frame, sources, surface, resolver, useCache ? Cache : null);
            Assert.Equal(reference.PointLightsUsed, candidate.PointLightsUsed);
            Assert.Equal(reference.RaysCast, candidate.RaysCast);
            Assert.Equal(reference.PointShadowSamples, candidate.PointShadowSamples);
            AssertPlanesEqual();
            if (temporal)
            {
                if (_hasHistory && frame.FrameIndex > _previousFrame && frame.FrameIndex - _previousFrame <= 8)
                {
                    var referenceTemporal = Stabilize(Reference);
                    LastTemporal = Stabilize(Candidate);
                    Assert.Equal(referenceTemporal, LastTemporal);
                }
                else { LastTemporal = new LightingTemporalStabilizationTelemetry(0, 0, true); }
                AssertPlanesEqual();
                for (var plane = 0; plane < 4; plane++)
                {
                    Reference.Planes[plane].AsSpan(0, Profile.MaskPixelCount).CopyTo(Reference.History[plane]);
                    Candidate.Planes[plane].AsSpan(0, Profile.MaskPixelCount).CopyTo(Candidate.History[plane]);
                }
                _hasHistory = true;
                _previousBounds = Bounds;
                _previousFrame = frame.FrameIndex;
            }
            if (ambientHit.HasValue) { Assert.Equal(ambientHit.Value, Cache.LastTelemetry.AmbientHits); }
            if (portalHit.HasValue) { Assert.Equal(portalHit.Value, Cache.LastTelemetry.PortalHits); }
            return candidate;
        }

        private LightingTemporalStabilizationTelemetry Stabilize(FieldBuffers planes) =>
            LightingTemporalStabilizer.Apply(_previousBounds, Bounds, Profile.MaskSize,
                planes.History[0], planes.History[1], planes.History[2], planes.History[3],
                planes.Planes[0], planes.Planes[1], planes.Planes[2], planes.Planes[3]);

        private LightingBuildTelemetry Build(FieldBuffers planes, LightingFrameParameters frame,
            ReadOnlySpan<ScreenSpaceLight> lights, int? surface, Func<int, int>? resolver, LightingPreparationCache? cache) =>
            TileRayCastShadowMaskBuilder.Build(World, Bounds, Profile, frame, lights,
                planes.Planes[0], planes.Planes[1], planes.Planes[2], planes.Planes[3], planes.Planes[4], planes.Planes[5],
                surface, resolver, cache);

        private void AssertPlanesEqual()
        {
            for (var plane = 0; plane < 5; plane++)
            {
                Assert.True(BitwiseEqual(Reference.Planes[plane], Candidate.Planes[plane]), $"Plane {plane} differs bitwise.");
            }
        }
    }
}
