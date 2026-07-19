namespace Game.Client.Rendering.Entities;

public enum EntityVisualSubmissionLayer : byte
{
    Shadows,
    Actors
}

public enum EntityVisualSubmissionMaterial : byte
{
    ShadowMask,
    SpriteTexture
}

public readonly record struct EntityVisualSubmissionBucket(
    EntityVisualSubmissionLayer Layer,
    EntityVisualSubmissionMaterial Material,
    string TextureKey,
    int StartIndex,
    int CommandCount);

public readonly record struct EntityVisualSubmissionTelemetry(
    int InputCommands,
    int PlannedCommands,
    int RequiredBuckets,
    int PreparedBuckets,
    int EstimatedTextureSwitchesBefore,
    int EstimatedTextureSwitchesAfter,
    int CommandCapacityOverflow,
    int BucketCapacityOverflow)
{
    public bool IsComplete =>
        PlannedCommands == InputCommands &&
        CommandCapacityOverflow == 0 &&
        BucketCapacityOverflow == 0;

    public bool WasCapacityExceeded => CommandCapacityOverflow > 0 || BucketCapacityOverflow > 0;

    public int EstimatedTextureSwitchReduction =>
        Math.Max(0, EstimatedTextureSwitchesBefore - EstimatedTextureSwitchesAfter);
}

/// <summary>
/// Fixed-capacity submission order prepared outside Draw. Shadow commands form one stable
/// layer; actor commands retain their original relative order and coalesce adjacent texture keys.
/// </summary>
public sealed class EntityVisualSubmissionPlan
{
    private readonly int[] _commandIndices;
    private readonly EntityVisualSubmissionBucket[] _buckets;

    public EntityVisualSubmissionPlan(int commandCapacity, int bucketCapacity)
    {
        if (commandCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(commandCapacity));
        }

        if (bucketCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketCapacity));
        }

        _commandIndices = new int[commandCapacity];
        _buckets = new EntityVisualSubmissionBucket[bucketCapacity];
    }

    public int CommandCapacity => _commandIndices.Length;

    public int BucketCapacity => _buckets.Length;

    public int Count { get; private set; }

    public int BucketCount { get; private set; }

    public EntityVisualSubmissionTelemetry Telemetry { get; private set; }

    public ReadOnlySpan<int> CommandIndices => _commandIndices.AsSpan(0, Count);

    public ReadOnlySpan<EntityVisualSubmissionBucket> Buckets => _buckets.AsSpan(0, BucketCount);

    public int this[int index] =>
        (uint)index < (uint)Count ? _commandIndices[index] : throw new ArgumentOutOfRangeException(nameof(index));

    internal void Clear()
    {
        Count = 0;
        BucketCount = 0;
        Telemetry = default;
    }

    internal EntityVisualSubmissionTelemetry Build(ReadOnlySpan<EntityVisualDrawCommand> commands)
    {
        Clear();
        var switchesBefore = CountTextureRuns(commands, plannedOrder: false);
        var requiredBuckets = CountTextureRuns(commands, plannedOrder: true);
        var commandOverflow = Math.Max(0, commands.Length - _commandIndices.Length);
        var bucketOverflow = Math.Max(0, requiredBuckets - _buckets.Length);
        if (commandOverflow > 0 || bucketOverflow > 0)
        {
            Telemetry = new EntityVisualSubmissionTelemetry(
                commands.Length,
                0,
                requiredBuckets,
                0,
                switchesBefore,
                switchesBefore,
                commandOverflow,
                bucketOverflow);
            return Telemetry;
        }

        AppendLayer(commands, EntityVisualSubmissionLayer.Shadows);
        AppendLayer(commands, EntityVisualSubmissionLayer.Actors);
        Telemetry = new EntityVisualSubmissionTelemetry(
            commands.Length,
            Count,
            requiredBuckets,
            BucketCount,
            switchesBefore,
            requiredBuckets,
            0,
            0);
        return Telemetry;
    }

    private void AppendLayer(
        ReadOnlySpan<EntityVisualDrawCommand> commands,
        EntityVisualSubmissionLayer requestedLayer)
    {
        for (var commandIndex = 0; commandIndex < commands.Length; commandIndex++)
        {
            ref readonly var command = ref commands[commandIndex];
            var layer = ResolveLayer(command.Kind);
            if (layer != requestedLayer)
            {
                continue;
            }

            var material = ResolveMaterial(layer);
            var textureKey = ResolveTextureKey(command, layer);
            _commandIndices[Count] = commandIndex;
            if (BucketCount == 0 || !Matches(_buckets[BucketCount - 1], layer, material, textureKey))
            {
                _buckets[BucketCount++] = new EntityVisualSubmissionBucket(
                    layer,
                    material,
                    textureKey,
                    Count,
                    1);
            }
            else
            {
                ref var bucket = ref _buckets[BucketCount - 1];
                bucket = bucket with { CommandCount = bucket.CommandCount + 1 };
            }

            Count++;
        }
    }

    private static int CountTextureRuns(
        ReadOnlySpan<EntityVisualDrawCommand> commands,
        bool plannedOrder)
    {
        var runs = 0;
        var hasPrevious = false;
        var previousLayer = default(EntityVisualSubmissionLayer);
        var previousMaterial = default(EntityVisualSubmissionMaterial);
        string? previousTextureKey = null;

        var passCount = plannedOrder ? 2 : 1;
        for (var pass = 0; pass < passCount; pass++)
        {
            var requestedLayer = pass == 0
                ? EntityVisualSubmissionLayer.Shadows
                : EntityVisualSubmissionLayer.Actors;
            for (var index = 0; index < commands.Length; index++)
            {
                ref readonly var command = ref commands[index];
                var layer = ResolveLayer(command.Kind);
                if (plannedOrder && layer != requestedLayer)
                {
                    continue;
                }

                var material = ResolveMaterial(layer);
                var textureKey = ResolveTextureKey(command, layer);
                if (!hasPrevious ||
                    previousLayer != layer ||
                    previousMaterial != material ||
                    !string.Equals(previousTextureKey, textureKey, StringComparison.OrdinalIgnoreCase))
                {
                    runs++;
                    hasPrevious = true;
                    previousLayer = layer;
                    previousMaterial = material;
                    previousTextureKey = textureKey;
                }
            }
        }

        return runs;
    }

    private static bool Matches(
        in EntityVisualSubmissionBucket bucket,
        EntityVisualSubmissionLayer layer,
        EntityVisualSubmissionMaterial material,
        string textureKey)
    {
        return bucket.Layer == layer &&
               bucket.Material == material &&
               string.Equals(bucket.TextureKey, textureKey, StringComparison.OrdinalIgnoreCase);
    }

    private static EntityVisualSubmissionLayer ResolveLayer(EntityVisualDrawCommandKind kind)
    {
        return kind == EntityVisualDrawCommandKind.ShadowEllipse
            ? EntityVisualSubmissionLayer.Shadows
            : EntityVisualSubmissionLayer.Actors;
    }

    private static EntityVisualSubmissionMaterial ResolveMaterial(EntityVisualSubmissionLayer layer)
    {
        return layer == EntityVisualSubmissionLayer.Shadows
            ? EntityVisualSubmissionMaterial.ShadowMask
            : EntityVisualSubmissionMaterial.SpriteTexture;
    }

    private static string ResolveTextureKey(
        in EntityVisualDrawCommand command,
        EntityVisualSubmissionLayer layer)
    {
        return layer == EntityVisualSubmissionLayer.Shadows ? string.Empty : command.SpriteId;
    }
}
