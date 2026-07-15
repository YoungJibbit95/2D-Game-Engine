using Game.Core.Animation;
using Game.Core.Runtime;

namespace Game.Client.Rendering.Entities;

internal sealed class RuntimeEntityVisualProfileCatalog
{
    private readonly EntityVisualProfileCatalog _profiles;

    public RuntimeEntityVisualProfileCatalog(AnimationContentRegistry runtimeAnimations)
    {
        ArgumentNullException.ThrowIfNull(runtimeAnimations);
        var builder = new EntityVisualProfileCatalogBuilder();
        var converted = new Dictionary<string, EntityVisualProfile>(
            runtimeAnimations.Entities.Count,
            StringComparer.OrdinalIgnoreCase);

        for (var profileIndex = 0; profileIndex < runtimeAnimations.Entities.Count; profileIndex++)
        {
            var source = runtimeAnimations.Entities[profileIndex];
            converted.Add(source.Id, Convert(source));
        }

        for (var profileIndex = 0; profileIndex < runtimeAnimations.Entities.Count; profileIndex++)
        {
            var source = runtimeAnimations.Entities[profileIndex];
            for (var bindingIndex = 0; bindingIndex < source.Bindings.Count; bindingIndex++)
            {
                var binding = source.Bindings[bindingIndex];
                var resolution = runtimeAnimations.ResolveEntity(binding, source.Kind);
                builder.Add(binding, converted[resolution.Profile.Id]);
            }
        }

        for (var kindIndex = 0; kindIndex <= (int)AnimationEntityKind.DroppedItem; kindIndex++)
        {
            var runtimeKind = (AnimationEntityKind)kindIndex;
            var source = runtimeAnimations.GetFallback(runtimeKind);
            if (!converted.TryGetValue(source.Id, out var fallback))
            {
                fallback = Convert(source);
                converted.Add(source.Id, fallback);
            }

            builder.AddFallback(Convert(runtimeKind), fallback);
        }

        _profiles = builder.Build();
    }

    public EntityVisualProfile Resolve(string binding, EntityFrameKind kind, out bool usedFallback)
    {
        return _profiles.Resolve(binding, kind, out usedFallback);
    }

    private static EntityVisualProfile Convert(EntityAnimationProfile source)
    {
        return new EntityVisualProfile
        {
            Id = source.Id,
            SpriteId = source.SpriteId,
            EliteSpriteId = source.EliteSpriteId,
            Kind = Convert(source.Kind),
            MotionStyle = Convert(source.MotionStyle),
            IsFlying = source.IsFlying,
            IsElite = source.IsElite,
            CastsShadow = source.CastsShadow,
            DisplayScale = source.DisplayScale,
            WalkSpeedThreshold = source.WalkSpeedThreshold,
            RunSpeedThreshold = source.RunSpeedThreshold,
            AirborneSpeedThreshold = source.AirborneSpeedThreshold,
            BobAmplitude = source.BobAmplitude,
            ShadowWidthFactor = source.ShadowWidthFactor,
            ShadowOpacity = source.ShadowOpacity,
            HurtLockTicks = source.HurtLockTicks,
            AttackLockTicks = source.AttackLockTicks,
            Idle = Convert(source.GetAnimation(AnimationEntityVisualState.Idle)),
            Walk = Convert(source.GetAnimation(AnimationEntityVisualState.Walk)),
            Run = Convert(source.GetAnimation(AnimationEntityVisualState.Run)),
            Jump = Convert(source.GetAnimation(AnimationEntityVisualState.Jump)),
            Fly = Convert(source.GetAnimation(AnimationEntityVisualState.Fly)),
            Attack = Convert(source.GetAnimation(AnimationEntityVisualState.Attack)),
            Hurt = Convert(source.GetAnimation(AnimationEntityVisualState.Hurt)),
            Death = Convert(source.GetAnimation(AnimationEntityVisualState.Death))
        };
    }

    private static EntityVisualAnimationRange Convert(AnimationEntityFrameRange source)
    {
        return new EntityVisualAnimationRange(
            source.StartFrame,
            source.FrameCount,
            source.TicksPerFrame,
            source.Playback switch
            {
                AnimationEntityPlayback.PingPong => EntityVisualPlayback.PingPong,
                AnimationEntityPlayback.Clamp => EntityVisualPlayback.Clamp,
                _ => EntityVisualPlayback.Loop
            });
    }

    private static EntityFrameKind Convert(AnimationEntityKind kind)
    {
        return kind switch
        {
            AnimationEntityKind.Enemy => EntityFrameKind.Enemy,
            AnimationEntityKind.Projectile => EntityFrameKind.Projectile,
            AnimationEntityKind.DroppedItem => EntityFrameKind.DroppedItem,
            _ => EntityFrameKind.Entity
        };
    }

    private static EntityVisualMotionStyle Convert(AnimationEntityMotionStyle style)
    {
        var converted = EntityVisualMotionStyle.None;
        if ((style & AnimationEntityMotionStyle.Bob) != 0)
        {
            converted |= EntityVisualMotionStyle.Bob;
        }

        if ((style & AnimationEntityMotionStyle.SquashStretch) != 0)
        {
            converted |= EntityVisualMotionStyle.SquashStretch;
        }

        if ((style & AnimationEntityMotionStyle.WingFlap) != 0)
        {
            converted |= EntityVisualMotionStyle.WingFlap;
        }

        if ((style & AnimationEntityMotionStyle.RotateToVelocity) != 0)
        {
            converted |= EntityVisualMotionStyle.RotateToVelocity;
        }

        if ((style & AnimationEntityMotionStyle.Spin) != 0)
        {
            converted |= EntityVisualMotionStyle.Spin;
        }

        return converted;
    }
}
