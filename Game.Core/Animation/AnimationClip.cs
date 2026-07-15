namespace Game.Core.Animation;

public sealed record AnimationEventMarker
{
    public AnimationEventMarker(string id, int tick, string? payload = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick));
        }

        Id = id;
        Tick = tick;
        Payload = payload;
    }

    public string Id { get; }

    public int Tick { get; }

    public string? Payload { get; }
}

public sealed record AnimationFrame
{
    private readonly AttachmentSocketPose[] _sockets;

    public AnimationFrame(
        int spriteFrameIndex,
        int durationTicks,
        AnimationTransform2D? transform = null,
        AnimationColor? tint = null,
        bool visible = true,
        IEnumerable<AttachmentSocketPose>? sockets = null,
        string? spriteIdOverride = null)
    {
        if (spriteFrameIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(spriteFrameIndex));
        }

        if (durationTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationTicks), "Frame duration must be at least one tick.");
        }

        SpriteFrameIndex = spriteFrameIndex;
        DurationTicks = durationTicks;
        Transform = transform ?? AnimationTransform2D.Identity;
        Tint = tint ?? AnimationColor.White;
        Visible = visible;
        if (spriteIdOverride is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(spriteIdOverride);
        }

        SpriteIdOverride = spriteIdOverride;
        _sockets = sockets?.ToArray() ?? Array.Empty<AttachmentSocketPose>();
        ValidateSockets(_sockets);
    }

    public int SpriteFrameIndex { get; }

    public int DurationTicks { get; }

    public AnimationTransform2D Transform { get; }

    public AnimationColor Tint { get; }

    public bool Visible { get; }

    public string? SpriteIdOverride { get; }

    public IReadOnlyList<AttachmentSocketPose> Sockets => _sockets;

    public bool TryGetSocket(string socketId, out AttachmentSocketPose socket)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socketId);
        for (var index = 0; index < _sockets.Length; index++)
        {
            if (string.Equals(_sockets[index].Id, socketId, StringComparison.OrdinalIgnoreCase))
            {
                socket = _sockets[index];
                return true;
            }
        }

        socket = default;
        return false;
    }

    private static void ValidateSockets(IReadOnlyList<AttachmentSocketPose> sockets)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < sockets.Count; index++)
        {
            var socket = sockets[index];
            ArgumentException.ThrowIfNullOrWhiteSpace(socket.Id);
            if (!ids.Add(socket.Id))
            {
                throw new ArgumentException($"Duplicate socket id '{socket.Id}'.", nameof(sockets));
            }
        }
    }
}

public sealed class AnimationTrack
{
    private readonly AnimationFrame[] _frames;
    private readonly int[] _frameStartTicks;

    public AnimationTrack(
        string id,
        string targetLayerId,
        string spriteId,
        IEnumerable<AnimationFrame> frames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLayerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        ArgumentNullException.ThrowIfNull(frames);

        _frames = frames.ToArray();
        if (_frames.Length == 0)
        {
            throw new ArgumentException("Animation track must contain at least one frame.", nameof(frames));
        }

        Id = id;
        TargetLayerId = targetLayerId;
        SpriteId = spriteId;
        _frameStartTicks = new int[_frames.Length];

        var duration = 0;
        for (var index = 0; index < _frames.Length; index++)
        {
            _frameStartTicks[index] = duration;
            duration = checked(duration + _frames[index].DurationTicks);
        }

        DurationTicks = duration;
    }

    public string Id { get; }

    public string TargetLayerId { get; }

    public string SpriteId { get; }

    public IReadOnlyList<AnimationFrame> Frames => _frames;

    public int DurationTicks { get; }

    public AnimationFrame GetFrameAtTick(int tick)
    {
        if ((uint)tick >= (uint)DurationTicks)
        {
            throw new ArgumentOutOfRangeException(nameof(tick));
        }

        var index = Array.BinarySearch(_frameStartTicks, tick);
        if (index < 0)
        {
            index = ~index - 1;
        }

        return _frames[index];
    }
}

public sealed class AnimationClip
{
    private readonly AnimationTrack[] _tracks;
    private readonly AnimationEventMarker[] _events;

    public AnimationClip(
        string id,
        AnimationLoopMode loopMode,
        IEnumerable<AnimationTrack> tracks,
        IEnumerable<AnimationEventMarker>? events = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(tracks);

        _tracks = tracks.ToArray();
        if (_tracks.Length == 0)
        {
            throw new ArgumentException("Animation clip must contain at least one track.", nameof(tracks));
        }

        Id = id;
        LoopMode = loopMode;
        DurationTicks = _tracks[0].DurationTicks;

        var trackIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < _tracks.Length; index++)
        {
            var track = _tracks[index];
            if (track.DurationTicks != DurationTicks)
            {
                throw new ArgumentException(
                    $"Track '{track.Id}' has {track.DurationTicks} ticks but clip '{id}' requires {DurationTicks}.",
                    nameof(tracks));
            }

            if (!trackIds.Add(track.Id))
            {
                throw new ArgumentException($"Duplicate track id '{track.Id}' in clip '{id}'.", nameof(tracks));
            }
        }

        _events = events?
            .OrderBy(marker => marker.Tick)
            .ThenBy(marker => marker.Id, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<AnimationEventMarker>();

        for (var index = 0; index < _events.Length; index++)
        {
            if (_events[index].Tick >= DurationTicks)
            {
                throw new ArgumentException(
                    $"Event '{_events[index].Id}' lies outside clip '{id}'.",
                    nameof(events));
            }
        }
    }

    public string Id { get; }

    public AnimationLoopMode LoopMode { get; }

    public int DurationTicks { get; }

    public IReadOnlyList<AnimationTrack> Tracks => _tracks;

    public IReadOnlyList<AnimationEventMarker> Events => _events;

    internal void AppendEventsAtTick(int tick, AnimationEventCursor cursor)
    {
        for (var index = 0; index < _events.Length; index++)
        {
            var marker = _events[index];
            if (marker.Tick > tick)
            {
                break;
            }

            if (marker.Tick == tick)
            {
                cursor.Append(Id, marker);
            }
        }
    }
}

public readonly record struct AnimationTrackSample(
    string TrackId,
    string TargetLayerId,
    string SpriteId,
    int SpriteFrameIndex,
    AnimationTransform2D Transform,
    AnimationColor Tint,
    bool Visible,
    IReadOnlyList<AttachmentSocketPose> Sockets);

public sealed record AnimationClipSample
{
    public required string ClipId { get; init; }

    public required int TimelineTick { get; init; }

    public required bool IsComplete { get; init; }

    public required IReadOnlyList<AnimationTrackSample> Tracks { get; init; }

    public bool TryGetTrack(string trackId, out AnimationTrackSample sample)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        for (var index = 0; index < Tracks.Count; index++)
        {
            if (string.Equals(Tracks[index].TrackId, trackId, StringComparison.OrdinalIgnoreCase))
            {
                sample = Tracks[index];
                return true;
            }
        }

        sample = default;
        return false;
    }
}
