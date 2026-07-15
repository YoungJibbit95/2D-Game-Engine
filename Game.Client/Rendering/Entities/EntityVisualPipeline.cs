using System.Numerics;
using Game.Core.Animation;
using Game.Core.Runtime;
using Game.Core.World;

namespace Game.Client.Rendering.Entities;

public delegate bool EntityVisualSpriteFallbackResolver(
    in EntityFrameSnapshot entity,
    out string spriteId);

public sealed class EntityVisualPipeline
{
    private const int DefaultCullPadding = 48;
    private const long TrackRetentionTicks = 1_800;

    private readonly EntityVisualProfileCatalog _profiles;
    private readonly EntityVisualTrack[] _tracks;
    private readonly int _trackMask;
    private readonly EntityVisualCommandBuffer _commands;
    private RuntimeEntityVisualProfileCatalog? _runtimeProfiles;
    private EntityVisualSpriteFallbackResolver? _spriteFallbackResolver;

    public EntityVisualPipeline(
        EntityVisualProfileCatalog? profiles = null,
        int maximumTrackedEntities = 512,
        int maximumDrawCommands = 2_048)
    {
        if (maximumTrackedEntities <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTrackedEntities));
        }

        _profiles = profiles ?? EntityVisualProfileCatalog.CreateDefault();
        var trackCapacity = RoundUpToPowerOfTwo(maximumTrackedEntities);
        _tracks = new EntityVisualTrack[trackCapacity];
        _trackMask = trackCapacity - 1;
        _commands = new EntityVisualCommandBuffer(maximumDrawCommands);
    }

    public EntityVisualCommandBuffer CommandBuffer => _commands;

    public void Configure(
        AnimationContentRegistry? runtimeAnimations,
        EntityVisualSpriteFallbackResolver? spriteFallbackResolver = null)
    {
        _runtimeProfiles = runtimeAnimations is null
            ? null
            : new RuntimeEntityVisualProfileCatalog(runtimeAnimations);
        _spriteFallbackResolver = spriteFallbackResolver;
        Array.Clear(_tracks);
        _commands.Clear();
    }

    public EntityVisualTelemetry Prepare(GameFrameSnapshot snapshot, RectI visibleWorld)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Prepare(snapshot.Entities, visibleWorld, snapshot.TickNumber);
    }

    public EntityVisualTelemetry Prepare(GameFrameSnapshot snapshot, Microsoft.Xna.Framework.Rectangle visibleWorld)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Prepare(
            snapshot.Entities,
            new RectI(visibleWorld.X, visibleWorld.Y, visibleWorld.Width, visibleWorld.Height),
            snapshot.TickNumber);
    }

    public EntityVisualTelemetry Prepare(
        IReadOnlyList<EntityFrameSnapshot> entities,
        RectI visibleWorld,
        long fixedTick)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _commands.Clear();
        var visibleBounds = visibleWorld.Inflate(DefaultCullPadding);
        var visibleCount = 0;
        var culledCount = 0;
        var inactiveCount = 0;
        var commandOverflow = 0;
        var trackOverflow = 0;

        for (var index = 0; index < entities.Count; index++)
        {
            var entity = entities[index];
            if (!entity.IsActive && entity.Health > 0)
            {
                inactiveCount++;
                continue;
            }

            if (!entity.Bounds.Intersects(visibleBounds))
            {
                culledCount++;
                continue;
            }

            visibleCount++;
            var profile = ResolveProfile(entity, out var usedFallback);
            var trackIndex = FindOrCreateTrack(entity.Id, fixedTick);
            if (trackIndex < 0)
            {
                trackOverflow++;
            }

            var pose = ResolvePose(entity, profile, usedFallback, fixedTick, trackIndex);
            var requiredCommands = 1 + (pose.CastsShadow ? 1 : 0) + (pose.DrawOutline ? 1 : 0);
            if (!_commands.HasCapacity(requiredCommands))
            {
                commandOverflow++;
                continue;
            }

            AppendCommands(pose);
        }

        var telemetry = new EntityVisualTelemetry(
            entities.Count,
            visibleCount,
            culledCount,
            inactiveCount,
            _commands.Count,
            commandOverflow,
            trackOverflow);
        _commands.Telemetry = telemetry;
        return telemetry;
    }

    public bool NotifyAttack(int entityId, long fixedTick, int durationTicks = 0)
    {
        return NotifyTransientState(entityId, EntityVisualState.Attack, fixedTick, durationTicks);
    }

    public bool NotifyTransientState(
        int entityId,
        EntityVisualState state,
        long fixedTick,
        int durationTicks)
    {
        if (entityId <= 0)
        {
            return false;
        }

        var trackIndex = FindOrCreateTrack(entityId, fixedTick);
        if (trackIndex < 0)
        {
            return false;
        }

        ref var track = ref _tracks[trackIndex];
        track.ForcedState = state;
        track.ForcedUntilTick = fixedTick + Math.Max(1, durationTicks);
        track.LastSeenTick = fixedTick;
        return true;
    }

    public EntityVisualState GetTrackedState(int entityId)
    {
        var index = FindTrack(entityId);
        return index >= 0 ? _tracks[index].State : EntityVisualState.Idle;
    }

    public bool TryGetLastPose(int entityId, out EntityVisualPose pose)
    {
        var index = FindTrack(entityId);
        if (index >= 0 && _tracks[index].HasPose)
        {
            pose = _tracks[index].LastPose;
            return true;
        }

        pose = default;
        return false;
    }

    private EntityVisualPose ResolvePose(
        EntityFrameSnapshot entity,
        EntityVisualProfile profile,
        bool usedFallback,
        long fixedTick,
        int trackIndex)
    {
        var desiredState = ResolveDesiredState(entity, profile);
        var state = desiredState;
        var stateStartedTick = fixedTick;
        var facingLeft = entity.Velocity.X < -0.01f;
        var previousState = EntityVisualState.Idle;

        if (trackIndex >= 0)
        {
            ref var track = ref _tracks[trackIndex];
            previousState = track.State;
            if (track.ForcedUntilTick > fixedTick &&
                desiredState is not EntityVisualState.Death and not EntityVisualState.Hurt)
            {
                desiredState = track.ForcedState;
            }
            else if (track.State is EntityVisualState.Hurt or EntityVisualState.Attack &&
                     fixedTick < track.StateLockUntilTick &&
                     desiredState is not EntityVisualState.Death and not EntityVisualState.Hurt)
            {
                desiredState = track.State;
            }

            if (desiredState != track.State)
            {
                track.PreviousState = track.State;
                track.State = desiredState;
                track.StateStartedTick = fixedTick;
                track.StateLockUntilTick = desiredState switch
                {
                    EntityVisualState.Hurt => fixedTick + profile.HurtLockTicks,
                    EntityVisualState.Attack => fixedTick + profile.AttackLockTicks,
                    _ => fixedTick
                };
            }

            if (MathF.Abs(entity.Velocity.X) > 0.01f)
            {
                track.FacingLeft = entity.Velocity.X < 0f;
            }

            track.LastSeenTick = fixedTick;
            state = track.State;
            previousState = track.PreviousState;
            stateStartedTick = track.StateStartedTick;
            facingLeft = track.FacingLeft;
        }

        var elapsedTicks = Math.Max(0, fixedTick - stateStartedTick);
        var animation = profile.GetAnimation(state);
        var phaseOffset = animation.FrameCount <= 1
            ? 0
            : PositiveModulo(entity.Id * 17, animation.FrameCount);
        var frameIndex = animation.ResolveFrame(elapsedTicks, phaseOffset);
        var phase = (fixedTick + entity.Id * 17L) * 0.16f;
        var anchor = ResolveAnchor(entity);
        var scale = new Vector2(profile.DisplayScale);
        var rotation = 0f;
        var bob = 0f;

        if ((profile.MotionStyle & EntityVisualMotionStyle.Bob) != 0)
        {
            bob = ResolveBob(state, phase, profile.BobAmplitude);
        }

        if ((profile.MotionStyle & EntityVisualMotionStyle.SquashStretch) != 0)
        {
            ApplySquashStretch(entity, state, previousState, elapsedTicks, ref scale);
        }

        if ((profile.MotionStyle & EntityVisualMotionStyle.WingFlap) != 0)
        {
            var flap = MathF.Sin(phase * 1.85f);
            scale.X *= 1f + flap * 0.035f;
            scale.Y *= 1f - flap * 0.025f;
        }

        if ((profile.MotionStyle & EntityVisualMotionStyle.RotateToVelocity) != 0 &&
            entity.Velocity.LengthSquared() > 0.001f)
        {
            rotation = MathF.Atan2(entity.Velocity.Y, entity.Velocity.X);
            facingLeft = false;
        }
        else if ((profile.MotionStyle & EntityVisualMotionStyle.Spin) != 0)
        {
            rotation = MathF.Sin(phase * 0.35f) * 0.16f;
        }

        if (state == EntityVisualState.Death)
        {
            rotation += facingLeft ? -0.9f : 0.9f;
            scale.Y *= 0.58f;
        }

        var spriteId = profile.IsElite && !string.IsNullOrWhiteSpace(profile.EliteSpriteId)
            ? profile.EliteSpriteId
            : profile.SpriteId;
        if ((usedFallback || string.Equals(spriteId, "missing_sprite", StringComparison.OrdinalIgnoreCase)) &&
            _spriteFallbackResolver is not null &&
            _spriteFallbackResolver(in entity, out var fallbackSpriteId) &&
            !string.IsNullOrWhiteSpace(fallbackSpriteId))
        {
            spriteId = fallbackSpriteId;
        }

        var tint = entity.IsDamageFlashing || state == EntityVisualState.Hurt
            ? EntityVisualColor.HitFlash
            : profile.IsElite
                ? EntityVisualColor.Elite
                : entity.Kind == EntityFrameKind.Projectile &&
                  entity.DamageType == Game.Core.Combat.DamageType.Magic
                    ? EntityVisualColor.MagicProjectile
                    : EntityVisualColor.White;
        var shadowWidth = Math.Max(4f, entity.Bounds.Width * profile.ShadowWidthFactor * profile.DisplayScale);
        var shadowHeight = Math.Max(2f, entity.Bounds.Height * 0.16f * profile.DisplayScale);
        var airborneFade = state is EntityVisualState.Jump or EntityVisualState.Fly
            ? Math.Clamp(1f - MathF.Abs(entity.Velocity.Y) / 700f, 0.3f, 0.82f)
            : 1f;
        var layerDepth = Math.Clamp(0.35f + entity.Bounds.Bottom * 0.000002f, 0.05f, 0.92f);
        var pose = new EntityVisualPose(
            entity.Id,
            state,
            spriteId,
            frameIndex,
            anchor + new Vector2(0f, bob),
            new Vector2(0.5f, 1f),
            scale,
            rotation,
            facingLeft,
            tint,
            profile.IsElite,
            EntityVisualColor.EliteOutline,
            profile.IsElite ? 1.25f : 0f,
            profile.CastsShadow,
            new Vector2(entity.Bounds.X + entity.Bounds.Width * 0.5f, entity.Bounds.Bottom + 1f),
            new Vector2(shadowWidth, shadowHeight),
            EntityVisualColor.Shadow(profile.ShadowOpacity * airborneFade),
            layerDepth);
        if (trackIndex >= 0)
        {
            _tracks[trackIndex].LastPose = pose;
            _tracks[trackIndex].HasPose = true;
        }

        return pose;
    }

    private EntityVisualProfile ResolveProfile(EntityFrameSnapshot entity, out bool usedFallback)
    {
        if (_runtimeProfiles is not null)
        {
            var runtimeProfile = _runtimeProfiles.Resolve(entity.ContentId, entity.Kind, out var runtimeFallback);
            if (!runtimeFallback)
            {
                usedFallback = false;
                return runtimeProfile;
            }

            var legacyProfile = _profiles.Resolve(entity.ContentId, entity.Kind, out var legacyFallback);
            if (!legacyFallback)
            {
                usedFallback = false;
                return legacyProfile;
            }

            usedFallback = true;
            return runtimeProfile;
        }

        return _profiles.Resolve(entity.ContentId, entity.Kind, out usedFallback);
    }

    private void AppendCommands(in EntityVisualPose pose)
    {
        if (pose.CastsShadow)
        {
            _commands.TryAppend(new EntityVisualDrawCommand(
                EntityVisualDrawCommandKind.ShadowEllipse,
                pose.EntityId,
                string.Empty,
                0,
                pose.ShadowPosition,
                new Vector2(0.5f),
                pose.ShadowSize,
                0f,
                false,
                pose.ShadowColor,
                0f,
                Math.Max(0f, pose.LayerDepth - 0.0002f)));
        }

        if (pose.DrawOutline)
        {
            _commands.TryAppend(new EntityVisualDrawCommand(
                EntityVisualDrawCommandKind.Outline,
                pose.EntityId,
                pose.SpriteId,
                pose.FrameIndex,
                pose.Position,
                pose.OriginNormalized,
                pose.Scale,
                pose.RotationRadians,
                pose.FlipHorizontally,
                pose.OutlineColor,
                pose.OutlineThickness,
                Math.Max(0f, pose.LayerDepth - 0.0001f)));
        }

        _commands.TryAppend(new EntityVisualDrawCommand(
            EntityVisualDrawCommandKind.Sprite,
            pose.EntityId,
            pose.SpriteId,
            pose.FrameIndex,
            pose.Position,
            pose.OriginNormalized,
            pose.Scale,
            pose.RotationRadians,
            pose.FlipHorizontally,
            pose.Tint,
            0f,
            pose.LayerDepth));
    }

    private static EntityVisualState ResolveDesiredState(
        EntityFrameSnapshot entity,
        EntityVisualProfile profile)
    {
        if (!entity.IsActive || entity.MaxHealth > 0 && entity.Health <= 0)
        {
            return EntityVisualState.Death;
        }

        if (entity.IsDamageFlashing)
        {
            return EntityVisualState.Hurt;
        }

        if (entity.Kind == EntityFrameKind.Projectile || profile.IsFlying)
        {
            return EntityVisualState.Fly;
        }

        if (entity.Kind != EntityFrameKind.DroppedItem &&
            MathF.Abs(entity.Velocity.Y) >= profile.AirborneSpeedThreshold)
        {
            return EntityVisualState.Jump;
        }

        var horizontalSpeed = MathF.Abs(entity.Velocity.X);
        if (horizontalSpeed >= profile.RunSpeedThreshold)
        {
            return EntityVisualState.Run;
        }

        return horizontalSpeed >= profile.WalkSpeedThreshold
            ? EntityVisualState.Walk
            : EntityVisualState.Idle;
    }

    private static Vector2 ResolveAnchor(EntityFrameSnapshot entity)
    {
        return entity.Kind switch
        {
            EntityFrameKind.Projectile or EntityFrameKind.DroppedItem => new Vector2(
                entity.Bounds.X + entity.Bounds.Width * 0.5f,
                entity.Bounds.Y + entity.Bounds.Height * 0.5f),
            _ => new Vector2(
                entity.Bounds.X + entity.Bounds.Width * 0.5f,
                entity.Bounds.Bottom)
        };
    }

    private static float ResolveBob(EntityVisualState state, float phase, float amplitude)
    {
        return state switch
        {
            EntityVisualState.Fly => MathF.Sin(phase * 0.7f) * amplitude,
            EntityVisualState.Walk => -MathF.Abs(MathF.Sin(phase)) * amplitude * 0.65f,
            EntityVisualState.Run => -MathF.Abs(MathF.Sin(phase * 1.35f)) * amplitude,
            EntityVisualState.Hurt => MathF.Sin(phase * 2.4f) * amplitude * 0.4f,
            EntityVisualState.Death => 0f,
            _ => MathF.Sin(phase * 0.35f) * amplitude * 0.25f
        };
    }

    private static void ApplySquashStretch(
        EntityFrameSnapshot entity,
        EntityVisualState state,
        EntityVisualState previousState,
        long elapsedTicks,
        ref Vector2 scale)
    {
        if (state == EntityVisualState.Jump)
        {
            var stretch = Math.Clamp(MathF.Abs(entity.Velocity.Y) / 600f, 0f, 0.18f);
            scale.X *= 1f - stretch * 0.45f;
            scale.Y *= 1f + stretch;
            return;
        }

        if (state == EntityVisualState.Hurt)
        {
            scale.X *= 1.08f;
            scale.Y *= 0.92f;
            return;
        }

        if (previousState == EntityVisualState.Jump && elapsedTicks < 4)
        {
            var landing = 1f - elapsedTicks / 4f;
            scale.X *= 1f + landing * 0.12f;
            scale.Y *= 1f - landing * 0.16f;
        }
    }

    private int FindOrCreateTrack(int entityId, long fixedTick)
    {
        if (entityId <= 0)
        {
            return -1;
        }

        var start = MixEntityId(entityId) & _trackMask;
        var replaceIndex = -1;
        var oldestTick = long.MaxValue;
        for (var probe = 0; probe < _tracks.Length; probe++)
        {
            var index = (start + probe) & _trackMask;
            ref var track = ref _tracks[index];
            if (track.EntityId == entityId)
            {
                return index;
            }

            if (track.EntityId == 0)
            {
                InitializeTrack(ref track, entityId, fixedTick);
                return index;
            }

            if (track.LastSeenTick < oldestTick)
            {
                oldestTick = track.LastSeenTick;
                replaceIndex = index;
            }
        }

        if (replaceIndex >= 0 && fixedTick - oldestTick > TrackRetentionTicks)
        {
            InitializeTrack(ref _tracks[replaceIndex], entityId, fixedTick);
            return replaceIndex;
        }

        return -1;
    }

    private int FindTrack(int entityId)
    {
        if (entityId <= 0)
        {
            return -1;
        }

        var start = MixEntityId(entityId) & _trackMask;
        for (var probe = 0; probe < _tracks.Length; probe++)
        {
            var index = (start + probe) & _trackMask;
            if (_tracks[index].EntityId == entityId)
            {
                return index;
            }

            if (_tracks[index].EntityId == 0)
            {
                return -1;
            }
        }

        return -1;
    }

    private static void InitializeTrack(ref EntityVisualTrack track, int entityId, long fixedTick)
    {
        track = new EntityVisualTrack
        {
            EntityId = entityId,
            State = EntityVisualState.Idle,
            PreviousState = EntityVisualState.Idle,
            StateStartedTick = fixedTick,
            StateLockUntilTick = fixedTick,
            LastSeenTick = fixedTick,
            FacingLeft = false
        };
    }

    private static int MixEntityId(int entityId)
    {
        var value = unchecked((uint)entityId);
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        return unchecked((int)value);
    }

    private static int PositiveModulo(int value, int divisor)
    {
        var result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static int RoundUpToPowerOfTwo(int value)
    {
        var result = 1;
        while (result < value && result < 1 << 30)
        {
            result <<= 1;
        }

        return result;
    }

    private struct EntityVisualTrack
    {
        public int EntityId;
        public EntityVisualState State;
        public EntityVisualState PreviousState;
        public long StateStartedTick;
        public long StateLockUntilTick;
        public long LastSeenTick;
        public bool FacingLeft;
        public EntityVisualState ForcedState;
        public long ForcedUntilTick;
        public EntityVisualPose LastPose;
        public bool HasPose;
    }
}
