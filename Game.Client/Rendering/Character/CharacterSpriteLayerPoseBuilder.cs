using Game.Core.Animation;
using Microsoft.Xna.Framework;
using CoreVector2 = System.Numerics.Vector2;

namespace Game.Client.Rendering.Character;

public sealed class CharacterSpriteLayerPoseBuilder
{
    private readonly record struct ResolvedAnimationTrack(
        AnimationStateLayerPose StateLayer,
        AnimationTrackSample Current,
        AnimationTrackSample? Outgoing);

    public CharacterSpriteLayerPose Build(
        CharacterRigProfile rig,
        CharacterAppearanceProfile appearance,
        LayeredAnimationPose animationPose,
        Vector2 worldPosition,
        CharacterRenderOverrides? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(rig);
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(animationPose);
        overrides ??= CharacterRenderOverrides.Empty;

        var layers = new List<SpriteLayerPose>(rig.Layers.Count * 2);
        for (var index = 0; index < rig.Layers.Count; index++)
        {
            var rigLayer = rig.Layers[index];
            var part = appearance.GetPart(rigLayer.AppearanceSlot);
            var layerOverride = overrides.Resolve(rigLayer.Id);

            if (TryResolveAnimationTrack(animationPose, rigLayer.AnimationTrackId, out var resolved))
            {
                if (resolved.Outgoing is { } outgoing && resolved.StateLayer.BlendWeight < 1f)
                {
                    AddLayerPose(
                        layers,
                        rigLayer,
                        part,
                        layerOverride,
                        resolved.StateLayer,
                        outgoing,
                        SpriteLayerBlendRole.Outgoing,
                        1f - resolved.StateLayer.BlendWeight,
                        animationPose.FacingDirection,
                        worldPosition);
                }

                AddLayerPose(
                    layers,
                    rigLayer,
                    part,
                    layerOverride,
                    resolved.StateLayer,
                    resolved.Current,
                    SpriteLayerBlendRole.Current,
                    resolved.Outgoing is null ? 1f : resolved.StateLayer.BlendWeight,
                    animationPose.FacingDirection,
                    worldPosition);
            }
            else
            {
                AddStaticLayerPose(
                    layers,
                    rigLayer,
                    part,
                    layerOverride,
                    animationPose.FacingDirection,
                    worldPosition);
            }
        }

        return new CharacterSpriteLayerPose
        {
            RigId = rig.Id,
            WorldPosition = worldPosition,
            FacingDirection = animationPose.FacingDirection,
            FixedTick = animationPose.FixedTick,
            Layers = layers.ToArray()
        };
    }

    private static bool TryResolveAnimationTrack(
        LayeredAnimationPose animationPose,
        string trackId,
        out ResolvedAnimationTrack resolved)
    {
        AnimationStateLayerPose? selectedLayer = null;
        AnimationTrackSample selectedCurrent = default;
        AnimationTrackSample? selectedOutgoing = null;

        for (var index = 0; index < animationPose.Layers.Count; index++)
        {
            var stateLayer = animationPose.Layers[index];
            if (!stateLayer.Visible || stateLayer.Opacity <= 0f ||
                !stateLayer.Current.TryGetTrack(trackId, out var current))
            {
                continue;
            }

            if (selectedLayer is not null && stateLayer.Priority < selectedLayer.Priority)
            {
                continue;
            }

            selectedLayer = stateLayer;
            selectedCurrent = current;
            selectedOutgoing = stateLayer.Outgoing is not null &&
                stateLayer.Outgoing.TryGetTrack(trackId, out var outgoing)
                ? outgoing
                : null;
        }

        if (selectedLayer is null)
        {
            resolved = default;
            return false;
        }

        resolved = new ResolvedAnimationTrack(selectedLayer, selectedCurrent, selectedOutgoing);
        return true;
    }

    private static void AddLayerPose(
        List<SpriteLayerPose> destination,
        CharacterRigLayerDefinition rigLayer,
        CharacterPartAppearance part,
        ResolvedCharacterLayerOverride layerOverride,
        AnimationStateLayerPose stateLayer,
        AnimationTrackSample track,
        SpriteLayerBlendRole blendRole,
        float blendOpacity,
        CharacterFacingDirection direction,
        Vector2 worldPosition)
    {
        var spriteId = ResolveSpriteId(rigLayer, part, layerOverride, track.SpriteId);
        var frameIndex = ResolveFrameIndex(layerOverride, track.SpriteFrameIndex);
        var partVisible = layerOverride.Visible ?? (part.Visible || layerOverride.SpriteId is not null);
        var visible = rigLayer.Visible &&
            partVisible &&
            stateLayer.Visible &&
            track.Visible &&
            !string.IsNullOrWhiteSpace(spriteId);
        var tint = part.Tint
            .Multiply(rigLayer.Tint)
            .Multiply(track.Tint)
            .Multiply(stateLayer.Tint)
            .Multiply(layerOverride.Tint ?? AnimationColor.White);
        var transform = CombineTransforms(rigLayer.LocalTransform, track.Transform);

        destination.Add(CreatePose(
            destination,
            rigLayer,
            spriteId,
            frameIndex,
            transform,
            tint,
            stateLayer.Opacity * blendOpacity,
            visible,
            blendRole,
            direction,
            worldPosition,
            track.Sockets));
    }

    private static void AddStaticLayerPose(
        List<SpriteLayerPose> destination,
        CharacterRigLayerDefinition rigLayer,
        CharacterPartAppearance part,
        ResolvedCharacterLayerOverride layerOverride,
        CharacterFacingDirection direction,
        Vector2 worldPosition)
    {
        var spriteId = layerOverride.SpriteId ?? part.SpriteId;
        var frameIndex = ResolveFrameIndex(layerOverride, 0);
        var partVisible = layerOverride.Visible ?? (part.Visible || layerOverride.SpriteId is not null);
        var visible = rigLayer.Visible &&
            partVisible &&
            !string.IsNullOrWhiteSpace(spriteId);
        var tint = part.Tint
            .Multiply(rigLayer.Tint)
            .Multiply(layerOverride.Tint ?? AnimationColor.White);

        destination.Add(CreatePose(
            destination,
            rigLayer,
            spriteId,
            frameIndex,
            rigLayer.LocalTransform,
            tint,
            1f,
            visible,
            SpriteLayerBlendRole.Current,
            direction,
            worldPosition,
            Array.Empty<AttachmentSocketPose>()));
    }

    private static SpriteLayerPose CreatePose(
        IReadOnlyList<SpriteLayerPose> existingLayers,
        CharacterRigLayerDefinition rigLayer,
        string spriteId,
        int frameIndex,
        AnimationTransform2D transform,
        AnimationColor tint,
        float opacity,
        bool visible,
        SpriteLayerBlendRole blendRole,
        CharacterFacingDirection direction,
        Vector2 worldPosition,
        IReadOnlyList<AttachmentSocketPose> sourceSockets)
    {
        var anchor = ResolveAnchor(existingLayers, rigLayer, blendRole, worldPosition);
        var flip = direction == CharacterFacingDirection.Left;
        var translation = transform.Translation;
        if (flip)
        {
            translation.X = -translation.X;
        }

        var rotation = transform.RotationRadians;
        if (flip)
        {
            rotation = -rotation;
        }

        var scale = transform.Scale;
        var position = anchor + ToXna(translation);
        var sockets = ResolveSockets(sourceSockets, position, rotation, scale, flip);

        return new SpriteLayerPose
        {
            RigLayerId = rigLayer.Id,
            AppearanceSlot = rigLayer.AppearanceSlot,
            SpriteId = spriteId,
            SpriteFrameIndex = frameIndex,
            Position = position,
            Origin = ToXna(rigLayer.Pivot),
            RotationRadians = rotation,
            Scale = ToXna(scale),
            Tint = tint,
            Opacity = Math.Clamp(opacity, 0f, 1f),
            Visible = visible,
            FlipHorizontally = flip,
            DrawOrder = rigLayer.DrawOrder,
            BlendRole = blendRole,
            Sockets = sockets
        };
    }

    private static Vector2 ResolveAnchor(
        IReadOnlyList<SpriteLayerPose> existingLayers,
        CharacterRigLayerDefinition rigLayer,
        SpriteLayerBlendRole blendRole,
        Vector2 fallback)
    {
        if (rigLayer.ParentLayerId is null || rigLayer.ParentSocketId is null)
        {
            return fallback;
        }

        if (TryFindSocket(existingLayers, rigLayer.ParentLayerId, rigLayer.ParentSocketId, blendRole, out var socket) ||
            TryFindSocket(existingLayers, rigLayer.ParentLayerId, rigLayer.ParentSocketId, SpriteLayerBlendRole.Current, out socket) ||
            TryFindSocket(existingLayers, rigLayer.ParentLayerId, rigLayer.ParentSocketId, SpriteLayerBlendRole.Outgoing, out socket))
        {
            return socket.Position;
        }

        return fallback;
    }

    private static bool TryFindSocket(
        IReadOnlyList<SpriteLayerPose> existingLayers,
        string parentLayerId,
        string socketId,
        SpriteLayerBlendRole blendRole,
        out CharacterSocketWorldPose socket)
    {
        for (var index = existingLayers.Count - 1; index >= 0; index--)
        {
            var layer = existingLayers[index];
            if (layer.BlendRole == blendRole &&
                string.Equals(layer.RigLayerId, parentLayerId, StringComparison.OrdinalIgnoreCase) &&
                layer.TryGetSocket(socketId, out socket))
            {
                return true;
            }
        }

        socket = default;
        return false;
    }

    private static CharacterSocketWorldPose[] ResolveSockets(
        IReadOnlyList<AttachmentSocketPose> sourceSockets,
        Vector2 position,
        float layerRotation,
        CoreVector2 scale,
        bool flip)
    {
        if (sourceSockets.Count == 0)
        {
            return Array.Empty<CharacterSocketWorldPose>();
        }

        var result = new CharacterSocketWorldPose[sourceSockets.Count];
        var cosine = MathF.Cos(layerRotation);
        var sine = MathF.Sin(layerRotation);
        for (var index = 0; index < sourceSockets.Count; index++)
        {
            var source = sourceSockets[index];
            var x = source.Position.X * scale.X;
            var y = source.Position.Y * scale.Y;
            if (flip)
            {
                x = -x;
            }

            var rotated = new Vector2(
                (x * cosine) - (y * sine),
                (x * sine) + (y * cosine));
            var socketRotation = layerRotation + (flip ? -source.RotationRadians : source.RotationRadians);
            result[index] = new CharacterSocketWorldPose(source.Id, position + rotated, socketRotation);
        }

        return result;
    }

    private static string ResolveSpriteId(
        CharacterRigLayerDefinition rigLayer,
        CharacterPartAppearance part,
        ResolvedCharacterLayerOverride layerOverride,
        string trackSpriteId)
    {
        if (layerOverride.SpriteId is not null)
        {
            return layerOverride.SpriteId;
        }

        return rigLayer.UseTrackSprite || string.IsNullOrWhiteSpace(part.SpriteId)
            ? trackSpriteId
            : part.SpriteId;
    }

    private static int ResolveFrameIndex(ResolvedCharacterLayerOverride layerOverride, int animationFrameIndex)
    {
        var frameIndex = checked((layerOverride.FrameIndex ?? animationFrameIndex) + layerOverride.FrameIndexOffset);
        if (frameIndex < 0)
        {
            throw new InvalidOperationException("A character layer override resolved to a negative frame index.");
        }

        return frameIndex;
    }

    private static AnimationTransform2D CombineTransforms(
        AnimationTransform2D left,
        AnimationTransform2D right)
    {
        return new AnimationTransform2D(
            Add(left.Translation, right.Translation),
            left.RotationRadians + right.RotationRadians,
            Multiply(left.Scale, right.Scale));
    }

    private static CoreVector2 Add(CoreVector2 left, CoreVector2 right)
    {
        return new CoreVector2(left.X + right.X, left.Y + right.Y);
    }

    private static CoreVector2 Multiply(CoreVector2 left, CoreVector2 right)
    {
        return new CoreVector2(left.X * right.X, left.Y * right.Y);
    }

    private static Vector2 ToXna(CoreVector2 value)
    {
        return new Vector2(value.X, value.Y);
    }
}
