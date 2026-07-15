using System.Diagnostics;
using Game.Core.Saving;
using Game.Core.World.Generation;

namespace Game.Core.World.Streaming;

public sealed record ChunkStreamingLoadJobRequest(
    long WorldSessionGeneration,
    long RequestSequence,
    ChunkPos Position,
    int WorldWidthTiles,
    int WorldHeightTiles,
    bool IsHorizontallyInfinite,
    WorldMetadata WorldMetadata,
    WorldGenerationProfile GenerationProfile,
    string? WorldDirectory);

public sealed record ChunkStreamingSaveJobRequest(
    long WorldSessionGeneration,
    long RequestSequence,
    int WorldWidthTiles,
    int WorldHeightTiles,
    bool IsHorizontallyInfinite,
    WorldMetadata WorldMetadata,
    string WorldDirectory,
    ChunkStreamingChunkSnapshot Chunk);

public sealed record ChunkStreamingLoadJobResult(
    ChunkStreamingLoadJobRequest Request,
    ChunkStreamingChunkSnapshot Chunk,
    bool LoadedFromSave,
    TimeSpan LoadTime,
    TimeSpan GenerateTime)
{
    public long DecodedBytes => Chunk.DecodedBytes;
}

public sealed record ChunkStreamingSaveJobResult(
    ChunkStreamingSaveJobRequest Request,
    bool Succeeded,
    TimeSpan SaveTime)
{
    public long DecodedBytes => Request.Chunk.DecodedBytes;
}

public interface IChunkStreamingJobRunner
{
    Task<ChunkStreamingLoadJobResult> RunLoadOrGenerateAsync(
        ChunkStreamingLoadJobRequest request,
        CancellationToken cancellationToken);

    Task<ChunkStreamingSaveJobResult> RunSaveAsync(
        ChunkStreamingSaveJobRequest request,
        CancellationToken cancellationToken);
}

public sealed class ChunkStreamingJobRunner : IChunkStreamingJobRunner, IDisposable
{
    private readonly InfiniteWorldChunkGenerator _generator;
    private readonly WorldSaveService _saves;
    private readonly SemaphoreSlim _storageGate = new(1, 1);

    public ChunkStreamingJobRunner(
        InfiniteWorldChunkGenerator? generator = null,
        WorldSaveService? saves = null)
    {
        _generator = generator ?? new InfiniteWorldChunkGenerator();
        _saves = saves ?? new WorldSaveService(WorldChunkStorageMode.RegionFiles);
    }

    public Task<ChunkStreamingLoadJobResult> RunLoadOrGenerateAsync(
        ChunkStreamingLoadJobRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(() => LoadOrGenerate(request, cancellationToken), cancellationToken);
    }

    public Task<ChunkStreamingSaveJobResult> RunSaveAsync(
        ChunkStreamingSaveJobRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(() => Save(request, cancellationToken), cancellationToken);
    }

    public void Dispose()
    {
        _storageGate.Dispose();
    }

    private ChunkStreamingLoadJobResult LoadOrGenerate(
        ChunkStreamingLoadJobRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var loadTime = TimeSpan.Zero;

        if (!string.IsNullOrWhiteSpace(request.WorldDirectory))
        {
            var loadStarted = Stopwatch.GetTimestamp();
            _storageGate.Wait(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var isolatedWorld = CreateIsolatedWorld(request);
                if (_saves.TryLoadChunk(isolatedWorld, request.WorldDirectory, request.Position) &&
                    isolatedWorld.TryGetChunk(request.Position, out var loaded) &&
                    loaded is not null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new ChunkStreamingLoadJobResult(
                        request,
                        ChunkStreamingChunkSnapshot.Capture(loaded),
                        LoadedFromSave: true,
                        Stopwatch.GetElapsedTime(loadStarted),
                        TimeSpan.Zero);
                }
            }
            finally
            {
                _storageGate.Release();
            }

            loadTime = Stopwatch.GetElapsedTime(loadStarted);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var generateStarted = Stopwatch.GetTimestamp();
        var generated = _generator.GenerateChunk(
            request.GenerationProfile,
            request.WorldMetadata.Seed,
            request.Position);
        cancellationToken.ThrowIfCancellationRequested();
        return new ChunkStreamingLoadJobResult(
            request,
            ChunkStreamingChunkSnapshot.Capture(generated),
            LoadedFromSave: false,
            loadTime,
            Stopwatch.GetElapsedTime(generateStarted));
    }

    private ChunkStreamingSaveJobResult Save(
        ChunkStreamingSaveJobRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var saveStarted = Stopwatch.GetTimestamp();
        _storageGate.Wait(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isolatedWorld = new World(
                request.WorldWidthTiles,
                request.WorldHeightTiles,
                request.WorldMetadata,
                request.IsHorizontallyInfinite);
            request.Chunk.ApplyTo(isolatedWorld.GetOrCreateChunk(request.Chunk.Position));
            var saved = _saves.SaveChunk(
                isolatedWorld,
                request.WorldDirectory,
                request.Chunk.Position);
            cancellationToken.ThrowIfCancellationRequested();
            return new ChunkStreamingSaveJobResult(request, saved, Stopwatch.GetElapsedTime(saveStarted));
        }
        finally
        {
            _storageGate.Release();
        }
    }

    private static World CreateIsolatedWorld(ChunkStreamingLoadJobRequest request)
    {
        return new World(
            request.WorldWidthTiles,
            request.WorldHeightTiles,
            request.WorldMetadata,
            request.IsHorizontallyInfinite);
    }
}
