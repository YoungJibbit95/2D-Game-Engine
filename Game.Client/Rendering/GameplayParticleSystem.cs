using Game.Client.Rendering.Effects;
using Game.Core.Feedback;
using Game.Core.Runtime;
using Game.Core.Settings;
using Game.Core.Weather;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering;

public readonly record struct AmbientParticleFrame(
    LivingWorldFrameSnapshot LivingWorld,
    Rectangle VisibleWorld,
    long TickNumber,
    int Quality);

public sealed class GameplayParticleSystem
{
    private const int AbsoluteCapacity = 640;
    private readonly ParticleState[] _particles = new ParticleState[AbsoluteCapacity];
    private int _count;
    private int _replacementCursor;
    private uint _randomState = 0x59E3A1C7u;
    private long _lastAmbientTick = -1;

    public int ActiveCount => _count;

    public int Capacity => AbsoluteCapacity;

    public void Emit(GameplayFeedbackCue cue, int quality)
    {
        var maximum = ResolveMaximum(quality);
        if (maximum == 0)
        {
            return;
        }

        TrimToBudget(maximum);
        var baseCount = cue.Kind switch
        {
            GameplayFeedbackCueKind.WorldEventActivated => 18,
            GameplayFeedbackCueKind.RareLootDropped => 16,
            GameplayFeedbackCueKind.EntityDeath or GameplayFeedbackCueKind.RareItemPickup => 12,
            GameplayFeedbackCueKind.TileBroken => 10,
            GameplayFeedbackCueKind.MeleeHit or GameplayFeedbackCueKind.ProjectileHit => 7,
            GameplayFeedbackCueKind.ItemPickup or
                GameplayFeedbackCueKind.RareItemPickup or
                GameplayFeedbackCueKind.LootDropped or
                GameplayFeedbackCueKind.CraftCompleted or
                GameplayFeedbackCueKind.ResourceRestored => 6,
            _ => 4
        };
        var count = Math.Clamp(baseCount * Math.Clamp(quality, 1, 3) / 2, 2, 24);
        var color = ResolveColor(cue.Kind);
        var buoyant = cue.Kind is GameplayFeedbackCueKind.ItemPickup or
            GameplayFeedbackCueKind.RareItemPickup or
            GameplayFeedbackCueKind.LootDropped or
            GameplayFeedbackCueKind.RareLootDropped or
            GameplayFeedbackCueKind.CraftCompleted or
            GameplayFeedbackCueKind.ResourceRestored;
        var lifetime = buoyant ? 0.7f : 0.45f;

        for (var index = 0; index < count; index++)
        {
            var angle = NextUnit() * MathF.Tau;
            var cueIntensity = float.IsFinite(cue.Intensity)
                ? Math.Clamp(cue.Intensity, 0.35f, 4f)
                : 1f;
            var speed = 20f + NextUnit() * 55f * cueIntensity;
            var velocity = new System.Numerics.Vector2(
                MathF.Cos(angle) * speed,
                MathF.Sin(angle) * speed - 15f);
            Add(
                new ParticleState(
                    cue.WorldPosition,
                    velocity,
                    color,
                    lifetime + NextUnit() * 0.25f,
                    1.5f + NextUnit() * 2.5f,
                    buoyant ? -12f : 90f,
                    ParticleShape.Dot),
                maximum);
        }
    }

    public void EmitAmbient(in AmbientParticleFrame frame)
    {
        var maximum = ResolveMaximum(frame.Quality);
        if (maximum == 0 ||
            frame.TickNumber < 0 ||
            frame.TickNumber == _lastAmbientTick ||
            frame.VisibleWorld.IsEmpty)
        {
            return;
        }

        _lastAmbientTick = frame.TickNumber;
        TrimToBudget(maximum);
        var living = frame.LivingWorld;
        var quality = Math.Clamp(frame.Quality, 1, 3);
        var weatherCount = living.Weather switch
        {
            WeatherKind.Storm => quality * 3,
            WeatherKind.Rain => quality * 2,
            WeatherKind.Fog => quality,
            _ => 0
        };
        for (var index = 0; index < weatherCount; index++)
        {
            EmitWeatherParticle(frame, index, maximum);
        }

        if (frame.TickNumber % 4 != 0)
        {
            return;
        }

        var ambientKind = ResolveAmbientKind(living.BiomeId, living.SubBiomeId);
        var ambientCount = ambientKind == AmbientParticleKind.None ? 0 : quality;
        for (var index = 0; index < ambientCount; index++)
        {
            EmitBiomeParticle(frame, ambientKind, index, maximum);
        }
    }

    public void Update(float deltaSeconds)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0f || _count == 0)
        {
            return;
        }

        deltaSeconds = Math.Min(deltaSeconds, 0.25f);
        for (var index = _count - 1; index >= 0; index--)
        {
            ref var particle = ref _particles[index];
            particle.Age += deltaSeconds;
            if (particle.Age >= particle.Lifetime)
            {
                RemoveAtSwapBack(index);
                continue;
            }

            particle.Velocity.Y += particle.Gravity * deltaSeconds;
            particle.Position += particle.Velocity * deltaSeconds;
        }
    }

    public void Draw(RenderContext context, Camera2D camera, GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(settings);
        var maximum = ResolveMaximum(settings.Rendering.ParticleQuality);
        if (maximum == 0)
        {
            return;
        }

        TrimToBudget(maximum);
        for (var index = 0; index < _count; index++)
        {
            ref readonly var particle = ref _particles[index];
            var screen = camera.WorldToScreen(
                new Vector2(particle.Position.X, particle.Position.Y),
                context.ViewportBounds);
            var normalizedAge = particle.Age / particle.Lifetime;
            var alpha = 1f - Math.Clamp(normalizedAge, 0f, 1f);
            var width = Math.Max(1, (int)MathF.Round(particle.Size * camera.Zoom));
            var height = particle.Shape == ParticleShape.Streak
                ? Math.Max(width + 2, (int)MathF.Round(particle.Size * 3.4f * camera.Zoom))
                : width;
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(
                    SaturatingRound(screen.X),
                    SaturatingRound(screen.Y),
                    width,
                    height),
                particle.Color * alpha);
        }
    }

    public void Clear()
    {
        Array.Clear(_particles, 0, _count);
        _count = 0;
        _replacementCursor = 0;
        _lastAmbientTick = -1;
    }

    internal ulong ComputeStateHash()
    {
        var hash = 1469598103934665603UL;
        Mix(ref hash, (ulong)_count);
        for (var index = 0; index < _count; index++)
        {
            ref readonly var particle = ref _particles[index];
            Mix(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(particle.Position.X)));
            Mix(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(particle.Position.Y)));
            Mix(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(particle.Velocity.X)));
            Mix(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(particle.Velocity.Y)));
            Mix(ref hash, particle.Color.PackedValue);
            Mix(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(particle.Age)));
        }

        return hash;
    }

    private void EmitWeatherParticle(in AmbientParticleFrame frame, int index, int maximum)
    {
        var living = frame.LivingWorld;
        var x = SampleRange(
            frame.TickNumber,
            index,
            0xA511E9B3u,
            frame.VisibleWorld.X,
            (long)frame.VisibleWorld.X + frame.VisibleWorld.Width);
        var y = SampleRange(
            frame.TickNumber,
            index,
            0x63D83595u,
            (long)frame.VisibleWorld.Y - 24L,
            (long)frame.VisibleWorld.Y + Math.Max(1, frame.VisibleWorld.Height / 3));
        var wind = float.IsFinite(living.Wind) ? Math.Clamp(living.Wind, -1f, 1f) : 0f;
        if (living.Weather == WeatherKind.Fog)
        {
            Add(
                new ParticleState(
                    new System.Numerics.Vector2(x, y),
                    new System.Numerics.Vector2(8f + wind * 16f, -2f),
                    new Color(170, 190, 196, 74),
                    1.8f,
                    2.2f,
                    0f,
                    ParticleShape.Dot),
                maximum);
            return;
        }

        var storm = living.Weather == WeatherKind.Storm;
        Add(
            new ParticleState(
                new System.Numerics.Vector2(x, y),
                new System.Numerics.Vector2(wind * (storm ? 90f : 55f), storm ? 330f : 245f),
                new Color(145, 190, 218, storm ? 190 : 150),
                storm ? 0.72f : 0.9f,
                storm ? 1.4f : 1f,
                0f,
                ParticleShape.Streak),
            maximum);
    }

    private void EmitBiomeParticle(
        in AmbientParticleFrame frame,
        AmbientParticleKind kind,
        int index,
        int maximum)
    {
        var salt = kind switch
        {
            AmbientParticleKind.MushroomSpore => 0x4CF5AD43u,
            AmbientParticleKind.CrystalMote => 0xB5297A4Du,
            _ => 0x68E31DA4u
        };
        var x = SampleRange(
            frame.TickNumber,
            index,
            salt,
            frame.VisibleWorld.X,
            (long)frame.VisibleWorld.X + frame.VisibleWorld.Width);
        var y = SampleRange(
            frame.TickNumber,
            index,
            salt ^ 0x9E3779B9u,
            frame.VisibleWorld.Top,
            (long)frame.VisibleWorld.Y + frame.VisibleWorld.Height);
        var color = kind switch
        {
            AmbientParticleKind.MushroomSpore => new Color(208, 133, 221),
            AmbientParticleKind.CrystalMote => new Color(113, 213, 241),
            _ => new Color(219, 230, 117)
        };
        var velocity = kind switch
        {
            AmbientParticleKind.MushroomSpore => new System.Numerics.Vector2(-3f, -12f),
            AmbientParticleKind.CrystalMote => new System.Numerics.Vector2(2f, -7f),
            _ => new System.Numerics.Vector2(5f, -3f)
        };
        Add(
            new ParticleState(
                new System.Numerics.Vector2(x, y),
                velocity,
                color,
                1.2f + SampleUnit(frame.TickNumber, index, salt ^ 0xD1B54A35u),
                kind == AmbientParticleKind.Firefly ? 2f : 1.4f,
                -1.5f,
                ParticleShape.Dot),
            maximum);
    }

    private void Add(in ParticleState particle, int maximum)
    {
        if (_count < maximum)
        {
            _particles[_count++] = particle;
            return;
        }

        _particles[_replacementCursor] = particle;
        _replacementCursor++;
        if (_replacementCursor >= maximum)
        {
            _replacementCursor = 0;
        }
    }

    private void RemoveAtSwapBack(int index)
    {
        _count--;
        if (index != _count)
        {
            _particles[index] = _particles[_count];
        }

        _particles[_count] = default;
        if (_replacementCursor >= _count)
        {
            _replacementCursor = 0;
        }
    }

    private void TrimToBudget(int maximum)
    {
        if (_count <= maximum)
        {
            return;
        }

        Array.Clear(_particles, maximum, _count - maximum);
        _count = maximum;
        _replacementCursor = 0;
    }

    private float NextUnit()
    {
        var value = _randomState;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        _randomState = value == 0 ? 0xA341316Cu : value;
        return (_randomState & 0x00FFFFFFu) / 16777215f;
    }

    private static int ResolveMaximum(int quality)
    {
        var tier = quality switch
        {
            <= 0 => PresentationQualityTier.Disabled,
            1 => PresentationQualityTier.Low,
            2 => PresentationQualityTier.Medium,
            _ => PresentationQualityTier.High
        };
        return PresentationBudget.ForTier(tier).MaxParticles;
    }

    private static AmbientParticleKind ResolveAmbientKind(string? biomeId, string? subBiomeId)
    {
        var identity = subBiomeId ?? biomeId;
        if (identity?.Contains("mushroom", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AmbientParticleKind.MushroomSpore;
        }

        if (identity?.Contains("crystal", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AmbientParticleKind.CrystalMote;
        }

        return string.Equals(biomeId, "forest", StringComparison.OrdinalIgnoreCase)
            ? AmbientParticleKind.Firefly
            : AmbientParticleKind.None;
    }

    private static float SampleRange(long tick, int index, uint salt, long minimum, long maximum)
    {
        if (maximum <= minimum)
        {
            return minimum;
        }

        return (float)(minimum + SampleUnit(tick, index, salt) * (maximum - (double)minimum));
    }

    private static float SampleUnit(long tick, int index, uint salt)
    {
        var value = unchecked((uint)tick) ^ unchecked((uint)(tick >> 32)) ^ (uint)index * 0x9E3779B9u ^ salt;
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return (value & 0x00FFFFFFu) / 16777215f;
    }

    private static Color ResolveColor(GameplayFeedbackCueKind kind)
    {
        return kind switch
        {
            GameplayFeedbackCueKind.MiningStarted or GameplayFeedbackCueKind.MiningImpact => new Color(196, 142, 82),
            GameplayFeedbackCueKind.TileBroken => new Color(145, 102, 63),
            GameplayFeedbackCueKind.TilePlaced => new Color(105, 169, 104),
            GameplayFeedbackCueKind.MeleeHit => new Color(232, 92, 85),
            GameplayFeedbackCueKind.ProjectileHit => new Color(243, 178, 78),
            GameplayFeedbackCueKind.EntityDeath => new Color(151, 90, 158),
            GameplayFeedbackCueKind.ItemPickup => new Color(242, 205, 91),
            GameplayFeedbackCueKind.RareItemPickup => new Color(105, 214, 255),
            GameplayFeedbackCueKind.LootDropped => new Color(214, 198, 142),
            GameplayFeedbackCueKind.RareLootDropped => new Color(118, 244, 255),
            GameplayFeedbackCueKind.CraftCompleted => new Color(111, 205, 151),
            GameplayFeedbackCueKind.ResourceRestored => new Color(87, 190, 211),
            GameplayFeedbackCueKind.StatusEffectApplied => new Color(129, 113, 210),
            GameplayFeedbackCueKind.WorldEventActivated => new Color(255, 170, 72),
            _ => new Color(164, 172, 181)
        };
    }

    private static int SaturatingRound(float value)
    {
        if (!float.IsFinite(value))
        {
            return 0;
        }

        return value <= int.MinValue
            ? int.MinValue
            : value >= int.MaxValue
                ? int.MaxValue
                : (int)MathF.Round(value);
    }

    private static void Mix(ref ulong hash, ulong value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
    }

    private enum AmbientParticleKind
    {
        None,
        MushroomSpore,
        CrystalMote,
        Firefly
    }

    private enum ParticleShape
    {
        Dot,
        Streak
    }

    private struct ParticleState
    {
        public ParticleState(
            System.Numerics.Vector2 position,
            System.Numerics.Vector2 velocity,
            Color color,
            float lifetime,
            float size,
            float gravity,
            ParticleShape shape)
        {
            Position = position;
            Velocity = velocity;
            Color = color;
            Lifetime = lifetime;
            Size = size;
            Gravity = gravity;
            Shape = shape;
            Age = 0f;
        }

        public System.Numerics.Vector2 Position;
        public System.Numerics.Vector2 Velocity;
        public Color Color;
        public float Lifetime;
        public float Size;
        public float Gravity;
        public float Age;
        public ParticleShape Shape;
    }
}
