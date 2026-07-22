using Game.Client.Rendering.Effects;
using Game.Core.Feedback;
using Game.Core.Particles;
using Game.Core.Runtime;
using Game.Core.Settings;
using Game.Core.Weather;
using Game.Core.World;
using Game.Core.World.Vegetation;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering;

public readonly record struct AmbientParticleFrame(
    LivingWorldFrameSnapshot LivingWorld,
    Rectangle VisibleWorld,
    long TickNumber,
    int Quality,
    World? World = null);

public sealed class GameplayParticleSystem
{
    private const int AbsoluteCapacity = ParticleQualityBudget.AbsoluteMaximumParticles;
    private const int PhysicalCapacity = 384;
    private const float PhysicalGravity = 100f;
    private readonly ParticleState[] _particles = new ParticleState[AbsoluteCapacity];
    private readonly ParticlePhysicsWorld _physicalParticles = new(PhysicalCapacity);
    private readonly ParticleState[] _physicalVisuals = new ParticleState[PhysicalCapacity];
    private readonly ParticleSnapshot[] _physicalSnapshots = new ParticleSnapshot[PhysicalCapacity];
    private readonly ParticlePhysicsEvent[] _physicalEvents = new ParticlePhysicsEvent[64];
    private readonly WorldParticleCollisionAdapter _particleCollision = new();
    private int _count;
    private int _replacementCursor;
    private int _physicalSnapshotCount;
    private uint _randomState = 0x59E3A1C7u;
    private long _lastAmbientTick = -1;

    public int ActiveCount => _count + _physicalParticles.ActiveCount;

    public int Capacity => AbsoluteCapacity;

    public int PhysicalParticleCount => _physicalParticles.ActiveCount;

    public ParticleStepResult LastPhysicsStep { get; private set; }

    public int LastDrawPrimitiveCount { get; private set; }

    public int LastVisibleParticleCount { get; private set; }

    public void Emit(GameplayFeedbackCue cue, int quality)
    {
        var budget = ParticleQualityBudget.ForQuality(quality);
        var maximum = budget.MaximumParticles;
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
        var count = Math.Clamp(
            baseCount * Math.Clamp(quality, 1, 3) / 2,
            2,
            budget.MaximumEmissionsPerCall);
        var color = ResolveColor(cue.Kind);
        var buoyant = cue.Kind is GameplayFeedbackCueKind.ItemPickup or
            GameplayFeedbackCueKind.RareItemPickup or
            GameplayFeedbackCueKind.LootDropped or
            GameplayFeedbackCueKind.RareLootDropped or
            GameplayFeedbackCueKind.CraftCompleted or
            GameplayFeedbackCueKind.ResourceRestored;
        var lifetime = buoyant ? 0.7f : 0.45f;
        var cueIntensity = float.IsFinite(cue.Intensity)
            ? Math.Clamp(cue.Intensity, 0.35f, 4f)
            : 1f;
        var shape = ResolveShape(cue.Kind);

        for (var index = 0; index < count; index++)
        {
            var angle = NextUnit() * MathF.Tau;
            var speed = 20f + NextUnit() * 55f * cueIntensity;
            var velocity = new System.Numerics.Vector2(
                MathF.Cos(angle) * speed,
                MathF.Sin(angle) * speed - 15f);
            AddFeedbackParticle(
                new ParticleState(
                    cue.WorldPosition,
                    velocity,
                    color,
                    lifetime + NextUnit() * 0.25f,
                    1.5f + NextUnit() * 2.5f,
                    buoyant ? -12f : 90f,
                    shape,
                    buoyant ? 0.18f : 0.75f,
                    shape is ParticleShape.Spark or ParticleShape.Ring ? 0.28f : 0.12f,
                    NextUnit() * MathF.Tau,
                    buoyant ? 3.5f : 0.8f,
                    4f + NextUnit() * 5f),
                maximum,
                cue.Kind);
        }
    }

    public void EmitAmbient(in AmbientParticleFrame frame)
    {
        var budget = ParticleQualityBudget.ForQuality(frame.Quality);
        var maximum = budget.MaximumParticles;
        if (maximum == 0 ||
            frame.TickNumber < 0 ||
            frame.TickNumber == _lastAmbientTick ||
            frame.VisibleWorld.IsEmpty)
        {
            return;
        }

        _lastAmbientTick = frame.TickNumber;
        TrimToBudget(maximum);
        if (frame.TickNumber % budget.AmbientTickStride != 0)
        {
            return;
        }

        var living = frame.LivingWorld;
        var quality = Math.Clamp(frame.Quality, 1, 3);
        var weatherKind = WeatherParticlePresentation.Resolve(living);
        var weatherBaseCount = weatherKind switch
        {
            WeatherParticlePresentationKind.Blizzard => quality * 5,
            WeatherParticlePresentationKind.Storm => quality * 4,
            WeatherParticlePresentationKind.Rain or WeatherParticlePresentationKind.Snow => quality * 3,
            WeatherParticlePresentationKind.Fog => quality,
            _ => 0
        };
        var weatherIntensity = float.IsFinite(living.WeatherIntensity)
            ? Math.Clamp(living.WeatherIntensity, 0f, 1f)
            : 0f;
        var weatherCount = Math.Min(
            budget.MaximumAmbientEmissionsPerTick,
            (int)MathF.Ceiling(weatherBaseCount * weatherIntensity));
        var emitted = 0;
        for (var index = 0; index < weatherCount; index++)
        {
            EmitWeatherParticle(frame, index, maximum);
            emitted++;
        }

        if (frame.TickNumber % 3 != 0 || emitted >= budget.MaximumAmbientEmissionsPerTick)
        {
            return;
        }

        var ambientKind = ResolveAmbientKind(living.BiomeId, living.SubBiomeId);
        var density = float.IsFinite(living.Presentation.AmbientParticleDensity)
            ? Math.Clamp(living.Presentation.AmbientParticleDensity, 0f, 3f)
            : 0f;
        var ambientCount = ambientKind == AmbientParticleKind.None
            ? 0
            : Math.Min(
                budget.MaximumAmbientEmissionsPerTick - emitted,
                quality + (int)MathF.Ceiling(quality * density));
        for (var index = 0; index < ambientCount; index++)
        {
            EmitBiomeParticle(frame, ambientKind, index, maximum);
        }

        emitted += ambientCount;
        EmitFallingLeaves(frame, budget.MaximumAmbientEmissionsPerTick - emitted, maximum);
    }

    public void Update(float deltaSeconds, World? world = null)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0f || ActiveCount == 0)
        {
            return;
        }

        deltaSeconds = Math.Min(deltaSeconds, 0.25f);
        if (_physicalParticles.ActiveCount > 0)
        {
            _particleCollision.World = world;
            LastPhysicsStep = _physicalParticles.Step(
                deltaSeconds,
                new ParticleForces(
                    new System.Numerics.Vector2(0f, PhysicalGravity),
                    System.Numerics.Vector2.Zero),
                new ParticleStepBudget(
                    PhysicalCapacity,
                    PhysicalCapacity * 48,
                    MaximumCollisionsPerParticle: 3),
                world is null ? null : _particleCollision,
                _physicalEvents);
            _particleCollision.World = null;
            _physicalSnapshotCount = _physicalParticles.CopyActiveParticles(_physicalSnapshots);
            for (var index = 0; index < _physicalSnapshotCount; index++)
            {
                ref readonly var snapshot = ref _physicalSnapshots[index];
                if ((uint)snapshot.Handle.Slot >= (uint)_physicalVisuals.Length)
                {
                    continue;
                }

                ref var visual = ref _physicalVisuals[snapshot.Handle.Slot];
                visual.Position = snapshot.Position;
                visual.Velocity = snapshot.Velocity;
                visual.Age = snapshot.AgeSeconds;
                visual.Lifetime = snapshot.LifetimeSeconds;
                visual.Phase += visual.AnimationSpeed * deltaSeconds;
            }
        }
        else
        {
            _physicalSnapshotCount = 0;
            LastPhysicsStep = default;
        }

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
            var damping = 1f / (1f + particle.Drag * deltaSeconds);
            particle.Velocity *= damping;
            particle.Position += particle.Velocity * deltaSeconds;
            particle.Phase += particle.AnimationSpeed * deltaSeconds;
        }
    }
    public void Draw(RenderContext context, Camera2D camera, GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(settings);
        var budget = ParticleQualityBudget.ForQuality(settings.Rendering.ParticleQuality);
        var maximum = budget.MaximumParticles;
        LastDrawPrimitiveCount = 0;
        LastVisibleParticleCount = 0;
        if (maximum == 0)
        {
            return;
        }

        TrimToBudget(maximum);
        for (var index = 0; index < _count; index++)
        {
            if (LastDrawPrimitiveCount >= budget.MaximumDrawPrimitives)
            {
                break;
            }

            DrawParticleState(context, camera, in _particles[index], budget.MaximumDrawPrimitives);
        }

        for (var index = 0; index < _physicalSnapshotCount; index++)
        {
            if (LastDrawPrimitiveCount >= budget.MaximumDrawPrimitives)
            {
                break;
            }

            var slot = _physicalSnapshots[index].Handle.Slot;
            if ((uint)slot >= (uint)_physicalVisuals.Length)
            {
                continue;
            }

            DrawParticleState(context, camera, in _physicalVisuals[slot], budget.MaximumDrawPrimitives);
        }
    }

    private void DrawParticleState(
        RenderContext context,
        Camera2D camera,
        in ParticleState particle,
        int maximumDrawPrimitives)
    {
        var screen = camera.WorldToScreen(
            new Vector2(particle.Position.X, particle.Position.Y),
            context.ViewportBounds);
        if (screen.X < context.ViewportBounds.Left - 32f ||
            screen.X > context.ViewportBounds.Right + 32f ||
            screen.Y < context.ViewportBounds.Top - 32f ||
            screen.Y > context.ViewportBounds.Bottom + 32f)
        {
            return;
        }

        var animation = ParticleAnimationPlanner.Sample(
            particle.Age,
            particle.Lifetime,
            particle.Phase,
            particle.Pulse,
            particle.SwayAmplitude);
        if (animation.Opacity <= 0.001f)
        {
            return;
        }

        LastVisibleParticleCount++;
        var width = Math.Max(1, (int)MathF.Round(particle.Size * animation.Scale * camera.Zoom));
        var height = particle.Shape == ParticleShape.Streak
            ? Math.Max(width + 2, (int)MathF.Round(particle.Size * 3.4f * camera.Zoom))
            : width;
        var bounds = new Rectangle(
            SaturatingRound(screen.X + animation.Sway),
            SaturatingRound(screen.Y),
            width,
            height);
        if (particle.Shape is ParticleShape.Dot or ParticleShape.Streak &&
            !WeatherParticlePresentation.TryClipToViewport(
                bounds,
                context.ViewportBounds,
                out bounds))
        {
            return;
        }

        var remaining = maximumDrawPrimitives - LastDrawPrimitiveCount;
        if (particle.Shape == ParticleShape.Leaf &&
            DrawProceduralLeaf(context, screen, camera.Zoom, particle, animation, remaining) is var leafPrimitives &&
            leafPrimitives > 0)
        {
            LastDrawPrimitiveCount += leafPrimitives;
            return;
        }

        LastDrawPrimitiveCount += DrawParticle(
            context,
            bounds,
            particle.Color * animation.Opacity,
            particle.Shape,
            remaining);
    }
    public void Clear()
    {
        Array.Clear(_particles, 0, _count);
        Array.Clear(_physicalVisuals);
        Array.Clear(_physicalSnapshots);
        _physicalParticles.Clear();
        _count = 0;
        _replacementCursor = 0;
        _physicalSnapshotCount = 0;
        _lastAmbientTick = -1;
        LastPhysicsStep = default;
        LastDrawPrimitiveCount = 0;
        LastVisibleParticleCount = 0;
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
            Mix(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(particle.Phase)));
        }

        var physicalCount = _physicalParticles.CopyActiveParticles(_physicalSnapshots);
        Mix(ref hash, (ulong)physicalCount);
        for (var index = 0; index < physicalCount; index++)
        {
            ref readonly var snapshot = ref _physicalSnapshots[index];
            Mix(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(snapshot.Position.X)));
            Mix(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(snapshot.Position.Y)));
            Mix(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(snapshot.Velocity.X)));
            Mix(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(snapshot.Velocity.Y)));
            Mix(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(snapshot.AgeSeconds)));
        }

        return hash;
    }

    private void EmitWeatherParticle(in AmbientParticleFrame frame, int index, int maximum)
    {
        var living = frame.LivingWorld;
        var kind = WeatherParticlePresentation.Resolve(living);
        if (kind == WeatherParticlePresentationKind.None)
        {
            return;
        }

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
        if (kind == WeatherParticlePresentationKind.Fog)
        {
            Add(
                new ParticleState(
                    new System.Numerics.Vector2(x, y),
                    new System.Numerics.Vector2(8f + wind * 16f, -2f),
                    WeatherParticlePresentation.PremultiplyAlpha(new Color(170, 190, 196), 74f / 255f),
                    1.8f,
                    2.2f,
                    0f,
                    ParticleShape.Dot,
                    0.08f,
                    0.18f,
                    SampleUnit(frame.TickNumber, index, 0xD4E12C77u) * MathF.Tau,
                    7f,
                    5f),
                maximum);
            return;
        }

        if (kind is WeatherParticlePresentationKind.Snow or WeatherParticlePresentationKind.Blizzard)
        {
            var variation = SampleUnit(frame.TickNumber, index, 0x8CB92BA7u);
            var blizzard = kind == WeatherParticlePresentationKind.Blizzard;
            Add(
                new ParticleState(
                    new System.Numerics.Vector2(x, y),
                    new System.Numerics.Vector2(
                        wind * (blizzard ? 88f : 24f),
                        (blizzard ? 96f : 34f) + variation * (blizzard ? 48f : 24f)),
                    WeatherParticlePresentation.PremultiplyAlpha(
                        new Color(232, 241, 248),
                        (blizzard ? 0.56f : 0.48f) + variation * 0.16f),
                    blizzard ? 1.35f : 2.8f + variation,
                    blizzard ? 0.75f + variation * 0.45f : 1.1f + variation * 1.2f,
                    blizzard ? 0f : 5f,
                    blizzard ? ParticleShape.Streak : ParticleShape.Dot,
                    0.2f,
                    0.08f,
                    SampleUnit(frame.TickNumber, index, 0xD4E12C77u) * MathF.Tau,
                    4f + variation * 3f,
                    1.2f + variation),
                maximum);
            return;
        }

        var storm = kind == WeatherParticlePresentationKind.Storm;
        Add(
            new ParticleState(
                new System.Numerics.Vector2(x, y),
                new System.Numerics.Vector2(wind * (storm ? 90f : 55f), storm ? 330f : 245f),
                WeatherParticlePresentation.PremultiplyAlpha(
                    new Color(145, 190, 218),
                    (storm ? 190f : 150f) / 255f),
                storm ? 0.72f : 0.9f,
                storm ? 1.4f : 1f,
                0f,
                ParticleShape.Streak,
                0.02f,
                0f,
                0f,
                0f,
                0f),
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
                kind == AmbientParticleKind.CrystalMote ? ParticleShape.Spark : ParticleShape.Dot,
                0.12f,
                kind == AmbientParticleKind.Firefly ? 0.32f : 0.18f,
                SampleUnit(frame.TickNumber, index, salt ^ 0x94D049BBu) * MathF.Tau,
                kind == AmbientParticleKind.Firefly ? 5f : 2.5f,
                3f + SampleUnit(frame.TickNumber, index, salt ^ 0x369DEA0Fu) * 5f),
            maximum);
    }

    private void EmitFallingLeaves(in AmbientParticleFrame frame, int emissionBudget, int maximum)
    {
        if (frame.World is null ||
            frame.LivingWorld.IsUnderground ||
            emissionBudget <= 0)
        {
            return;
        }

        var quality = Math.Clamp(frame.Quality, 1, 3);
        var tickStride = quality switch
        {
            1 => 12,
            2 => 8,
            _ => 6
        };
        if (frame.TickNumber % tickStride != 0)
        {
            return;
        }

        Span<FallingLeafSpawn> planned = stackalloc FallingLeafSpawn[6];
        var requested = Math.Min(planned.Length, Math.Min(emissionBudget, quality * 2));
        if (requested <= 0)
        {
            return;
        }

        var visible = new RectI(
            frame.VisibleWorld.X,
            frame.VisibleWorld.Y,
            frame.VisibleWorld.Width,
            frame.VisibleWorld.Height);
        var count = FallingLeafPlanner.Plan(
            frame.World,
            visible,
            frame.LivingWorld.SurfaceTileY,
            frame.TickNumber,
            frame.LivingWorld.Wind,
            frame.LivingWorld.VegetationDensityMultiplier,
            planned[..requested]);
        for (var index = 0; index < count; index++)
        {
            ref readonly var leaf = ref planned[index];
            Add(
                new ParticleState(
                    leaf.WorldPosition,
                    leaf.InitialVelocity,
                    ResolveLeafColor(frame.LivingWorld.BiomeId, leaf.ColorVariation),
                    leaf.Lifetime,
                    1.7f * leaf.Scale,
                    gravity: 13f,
                    ParticleShape.Leaf,
                    drag: 0.16f,
                    pulse: 0.08f,
                    leaf.Phase,
                    leaf.SwayAmplitude,
                    leaf.AnimationSpeed),
                maximum);
        }
    }

    private void AddFeedbackParticle(
        in ParticleState particle,
        int maximum,
        GameplayFeedbackCueKind cueKind)
    {
        if (cueKind is GameplayFeedbackCueKind.TileBroken or
            GameplayFeedbackCueKind.MeleeHit or
            GameplayFeedbackCueKind.ProjectileHit &&
            TryAddPhysicalParticle(particle, maximum))
        {
            return;
        }

        Add(particle, maximum);
    }

    private bool TryAddPhysicalParticle(in ParticleState particle, int maximum)
    {
        if (ActiveCount >= maximum || _physicalParticles.AvailableCapacity <= 0)
        {
            return false;
        }

        var command = new ParticleSpawnCommand
        {
            Position = particle.Position,
            Velocity = particle.Velocity,
            LifetimeSeconds = particle.Lifetime,
            Radius = Math.Max(0.75f, particle.Size * 0.35f),
            GravityScale = particle.Gravity / PhysicalGravity,
            LinearDrag = particle.Drag,
            Restitution = 0.28f,
            Friction = 0.58f,
            SleepSpeed = 5f,
            SleepDelaySeconds = 0.08f,
            Seed = _randomState,
            Sequence = unchecked((ulong)(uint)ActiveCount << 32) | _randomState,
            UserData = (int)particle.Shape,
            Flags = ParticleSimulationFlags.CollideWithTiles |
                ParticleSimulationFlags.AllowSleep
        };
        if (!_physicalParticles.TrySpawn(command, out var handle))
        {
            return false;
        }

        _physicalVisuals[handle.Slot] = particle;
        return true;
    }

    private void Add(in ParticleState particle, int maximum)
    {
        var visualMaximum = Math.Max(0, maximum - _physicalParticles.ActiveCount);
        if (visualMaximum == 0)
        {
            return;
        }

        if (_count < visualMaximum)
        {
            _particles[_count++] = particle;
            return;
        }

        _particles[_replacementCursor] = particle;
        _replacementCursor++;
        if (_replacementCursor >= visualMaximum)
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
        maximum = Math.Clamp(maximum, 0, AbsoluteCapacity);
        if (ActiveCount <= maximum)
        {
            return;
        }

        if (_physicalParticles.ActiveCount > 0)
        {
            _physicalParticles.Clear();
            Array.Clear(_physicalVisuals);
            Array.Clear(_physicalSnapshots);
            _physicalSnapshotCount = 0;
            LastPhysicsStep = default;
        }

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

    private static int DrawProceduralLeaf(
        RenderContext context,
        Vector2 screen,
        float zoom,
        in ParticleState particle,
        in ParticleAnimationSample animation,
        int remainingPrimitives)
    {
        if (remainingPrimitives <= 0)
        {
            return 0;
        }

        var size = Math.Max(1, (int)MathF.Round(particle.Size * animation.Scale * zoom));
        var horizontal = MathF.Sin(particle.Phase) >= 0f;
        var width = horizontal ? Math.Max(2, size + 1) : Math.Max(1, size);
        var height = horizontal ? Math.Max(1, size) : Math.Max(2, size + 1);
        var left = SaturatingRound(screen.X + animation.Sway - width * 0.5f);
        var top = SaturatingRound(screen.Y - height * 0.5f);
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(left, top, width, height),
            particle.Color * animation.Opacity);
        if (remainingPrimitives < 2 || width <= 1 || height <= 1)
        {
            return 1;
        }

        var highlight = Color.Lerp(particle.Color, Color.White, 0.28f) * (animation.Opacity * 0.7f);
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(
                horizontal ? left + width - 1 : left,
                horizontal ? top : top + height - 1,
                1,
                1),
            highlight);
        return 2;
    }

    private static int DrawParticle(
        RenderContext context,
        in Rectangle bounds,
        Color color,
        ParticleShape shape,
        int remainingPrimitives)
    {
        if (remainingPrimitives <= 0 || bounds.IsEmpty || color.A == 0)
        {
            return 0;
        }

        if (shape == ParticleShape.Spark && remainingPrimitives >= 2)
        {
            var centerX = bounds.X + bounds.Width / 2;
            var centerY = bounds.Y + bounds.Height / 2;
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(bounds.X - bounds.Width, centerY, bounds.Width * 3, Math.Max(1, bounds.Height / 3)),
                color);
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(centerX, bounds.Y - bounds.Height, Math.Max(1, bounds.Width / 3), bounds.Height * 3),
                color);
            return 2;
        }

        if (shape == ParticleShape.Ring && remainingPrimitives >= 4)
        {
            var thickness = Math.Max(1, Math.Min(bounds.Width, bounds.Height) / 4);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness),
                color);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height),
                color);
            return 4;
        }

        context.SpriteBatch.Draw(context.Pixel, bounds, color);
        return 1;
    }

    private static ParticleShape ResolveShape(GameplayFeedbackCueKind kind)
    {
        return kind switch
        {
            GameplayFeedbackCueKind.WorldEventActivated or
                GameplayFeedbackCueKind.RareItemPickup or
                GameplayFeedbackCueKind.RareLootDropped => ParticleShape.Ring,
            GameplayFeedbackCueKind.MeleeHit or
                GameplayFeedbackCueKind.ProjectileHit or
                GameplayFeedbackCueKind.EntityDeath => ParticleShape.Spark,
            _ => ParticleShape.Dot
        };
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

    private static Color ResolveLeafColor(string? biomeId, float variation)
    {
        variation = float.IsFinite(variation) ? Math.Clamp(variation, 0f, 1f) : 0.5f;
        if (biomeId?.Contains("amber", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Color.Lerp(new Color(196, 101, 42), new Color(242, 178, 67), variation);
        }

        if (biomeId?.Contains("marsh", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Color.Lerp(new Color(47, 108, 83), new Color(115, 158, 92), variation);
        }

        return Color.Lerp(new Color(55, 125, 54), new Color(151, 184, 75), variation);
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

    private sealed class WorldParticleCollisionAdapter : IParticleTileCollisionAdapter
    {
        public World? World { get; set; }

        public float TileSize => Game.Core.GameConstants.TileSize;

        public bool TryGetCollider(int tileX, int tileY, out ParticleTileCollider collider)
        {
            if (World is not null &&
                World.TryGetTile(tileX, tileY, out var tile) &&
                tile.CollisionShape == TileCollisionShape.FullBlock)
            {
                collider = new ParticleTileCollider(
                    IsSolid: true,
                    Restitution: 0.12f,
                    Friction: 0.68f);
                return true;
            }

            collider = default;
            return false;
        }
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
        Streak,
        Spark,
        Ring,
        Leaf
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
            ParticleShape shape,
            float drag,
            float pulse,
            float phase,
            float swayAmplitude,
            float animationSpeed)
        {
            Position = position;
            Velocity = velocity;
            Color = color;
            Lifetime = lifetime;
            Size = size;
            Gravity = gravity;
            Shape = shape;
            Drag = Math.Max(0f, drag);
            Pulse = Math.Max(0f, pulse);
            Phase = phase;
            SwayAmplitude = Math.Max(0f, swayAmplitude);
            AnimationSpeed = Math.Max(0f, animationSpeed);
            Age = 0f;
        }

        public System.Numerics.Vector2 Position;
        public System.Numerics.Vector2 Velocity;
        public Color Color;
        public float Lifetime;
        public float Size;
        public float Gravity;
        public float Age;
        public float Drag;
        public float Pulse;
        public float Phase;
        public float SwayAmplitude;
        public float AnimationSpeed;
        public ParticleShape Shape;
    }
}
