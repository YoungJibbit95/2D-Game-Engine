using System.Numerics;
using Game.Core.World;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Physics;

public sealed class TileCollisionResolver
{
    // Must remain larger than one float ULP at the engine's validated far-travel range.
    private const float ContactEpsilon = 0.01f;
    private readonly TileCollisionSettings _settings;

    public TileCollisionResolver(TileCollisionSettings? settings = null)
    {
        _settings = (settings ?? TileCollisionSettings.Default).Validate();
    }

    public TileCollisionSettings Settings => _settings;

    internal bool CanOccupy(GameWorld world, PhysicsBody body, Vector2 position)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(body);

        if (!IsFinite(position))
        {
            return false;
        }

        if (!body.CollidesWithTiles ||
            (body.CollisionMask & PhysicsCollisionLayer.World) == PhysicsCollisionLayer.None)
        {
            return true;
        }

        var bounds = GetBodyBounds(body, position);
        var minTileX = PixelToTile(bounds.Left);
        var maxTileX = PixelToTile(InsideEdge(bounds.Right));
        var minTileY = PixelToTile(bounds.Top);
        var maxTileY = PixelToTile(InsideEdge(bounds.Bottom));
        var tilesTested = 0;
        for (var tileY = (long)minTileY; tileY <= maxTileY; tileY++)
        {
            for (var tileX = (long)minTileX; tileX <= maxTileX; tileX++)
            {
                if (tilesTested >= _settings.MaxTileTests ||
                    tileX is < int.MinValue or > int.MaxValue ||
                    tileY is < int.MinValue or > int.MaxValue ||
                    IsBlocking(world, (int)tileX, (int)tileY))
                {
                    return false;
                }

                tilesTested++;
            }
        }

        return true;
    }

    public void Move(GameWorld world, PhysicsBody body, float deltaSeconds)
    {
        MoveDetailed(world, body, deltaSeconds, Span<PhysicsContact>.Empty);
    }

    public PhysicsMoveResult MoveDetailed(
        GameWorld world,
        PhysicsBody body,
        float deltaSeconds,
        Span<PhysicsContact> contacts)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(body);
        ValidateInput(body, deltaSeconds);

        var startPosition = body.Position;
        var requestedDisplacement = body.Velocity * deltaSeconds;
        if (!IsFinite(requestedDisplacement))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "The requested physics displacement must be finite.");
        }

        if (!body.CollidesWithTiles ||
            (body.CollisionMask & PhysicsCollisionLayer.World) == PhysicsCollisionLayer.None)
        {
            body.Position += requestedDisplacement;
            return CreateResult(
                body,
                startPosition,
                requestedDisplacement,
                PhysicsContactFlags.None,
                0,
                0,
                0,
                requestedDisplacement == Vector2.Zero ? 0 : 1);
        }

        body.OnGround = false;
        var counters = new MoveCounters();
        var writer = new ContactWriter(contacts);
        var material = body.Material.Normalized();
        if (!ResolveInitialOverlap(world, body, material, ref counters, ref writer))
        {
            return CreateResult(
                body,
                startPosition,
                requestedDisplacement,
                counters.Flags,
                writer.Found,
                writer.Written,
                counters.TilesTested,
                0);
        }

        var maximumAxisTravel = Math.Max(
            MathF.Abs(requestedDisplacement.X),
            MathF.Abs(requestedDisplacement.Y));
        var requiredSubsteps = maximumAxisTravel <= 0f
            ? 0
            : (int)MathF.Ceiling(maximumAxisTravel / _settings.MaxSubstepDistance);
        var substeps = Math.Min(requiredSubsteps, _settings.MaxSubsteps);
        if (substeps == 0)
        {
            return CreateResult(
                body,
                startPosition,
                requestedDisplacement,
                counters.Flags,
                writer.Found,
                writer.Written,
                counters.TilesTested,
                0);
        }

        var substepSeconds = deltaSeconds / substeps;

        for (var substep = 0; substep < substeps; substep++)
        {
            var displacement = body.Velocity * substepSeconds;
            MoveX(world, body, displacement.X, material, ref counters, ref writer);
            MoveY(world, body, displacement.Y, material, ref counters, ref writer);
            counters.Substeps++;

            if (counters.BudgetExhausted)
            {
                break;
            }
        }

        return CreateResult(
            body,
            startPosition,
            requestedDisplacement,
            counters.Flags,
            writer.Found,
            writer.Written,
            counters.TilesTested,
            counters.Substeps);
    }

    private bool ResolveInitialOverlap(
        GameWorld world,
        PhysicsBody body,
        PhysicsMaterial material,
        ref MoveCounters counters,
        ref ContactWriter writer)
    {
        var position = body.Position;
        var bounds = GetBodyBounds(body, position);
        var minTileX = PixelToTile(bounds.Left);
        var maxTileX = PixelToTile(InsideEdge(bounds.Right));
        var minTileY = PixelToTile(bounds.Top);
        var maxTileY = PixelToTile(InsideEdge(bounds.Bottom));
        var overlapFound = false;
        var minimumBlockingTileX = int.MaxValue;
        var maximumBlockingTileX = int.MinValue;
        var minimumBlockingTileY = int.MaxValue;
        var maximumBlockingTileY = int.MinValue;
        var minimumXContactTileY = 0;
        var maximumXContactTileY = 0;
        var minimumYContactTileX = 0;
        var maximumYContactTileX = 0;
        var best = default(DepenetrationCandidate);

        for (var tileY = (long)minTileY; tileY <= maxTileY; tileY++)
        {
            for (var tileX = (long)minTileX; tileX <= maxTileX; tileX++)
            {
                if (!TryTestTile(world, tileX, tileY, ref counters, out var blocking))
                {
                    StopAtInitialOverlapBudget(body, overlapFound, ref counters);
                    return false;
                }

                if (!blocking)
                {
                    continue;
                }

                overlapFound = true;
                if (tileX < minimumBlockingTileX)
                {
                    minimumBlockingTileX = (int)tileX;
                    minimumXContactTileY = (int)tileY;
                }
                if (tileX > maximumBlockingTileX)
                {
                    maximumBlockingTileX = (int)tileX;
                    maximumXContactTileY = (int)tileY;
                }
                if (tileY < minimumBlockingTileY)
                {
                    minimumBlockingTileY = (int)tileY;
                    minimumYContactTileX = (int)tileX;
                }
                if (tileY > maximumBlockingTileY)
                {
                    maximumBlockingTileY = (int)tileY;
                    maximumYContactTileX = (int)tileX;
                }
            }
        }

        if (!overlapFound)
        {
            return true;
        }

        var blockingLeft = (float)((long)minimumBlockingTileX * GameConstants.TileSize);
        var blockingTop = (float)((long)minimumBlockingTileY * GameConstants.TileSize);
        var blockingRight = (float)(((long)maximumBlockingTileX + 1L) * GameConstants.TileSize);
        var blockingBottom = (float)(((long)maximumBlockingTileY + 1L) * GameConstants.TileSize);
        Span<Vector2> candidatePositions = stackalloc Vector2[4]
        {
            new(position.X, blockingTop - body.Size.Y),
            new(blockingLeft - body.Size.X, position.Y),
            new(blockingRight, position.Y),
            new(position.X, blockingBottom)
        };
        Span<PhysicsContactFlags> candidateFlags = stackalloc PhysicsContactFlags[4]
        {
            PhysicsContactFlags.Ground,
            PhysicsContactFlags.RightWall,
            PhysicsContactFlags.LeftWall,
            PhysicsContactFlags.Ceiling
        };
        Span<Vector2> candidateNormals = stackalloc Vector2[4]
        {
            -Vector2.UnitY,
            -Vector2.UnitX,
            Vector2.UnitX,
            Vector2.UnitY
        };
        Span<int> candidateTileXs = stackalloc int[4]
        {
            minimumYContactTileX,
            minimumBlockingTileX,
            maximumBlockingTileX,
            maximumYContactTileX
        };
        Span<int> candidateTileYs = stackalloc int[4]
        {
            minimumBlockingTileY,
            minimumXContactTileY,
            maximumXContactTileY,
            maximumBlockingTileY
        };
        for (var rank = 0; rank < candidatePositions.Length && !counters.BudgetExhausted; rank++)
        {
            var candidatePosition = candidatePositions[rank];
            if (!TryCanOccupy(world, body, candidatePosition, ref counters))
            {
                continue;
            }

            var distanceSquared = Vector2.DistanceSquared(position, candidatePosition);
            if (!best.IsValid || distanceSquared < best.DistanceSquared)
            {
                best = new DepenetrationCandidate(
                    candidatePosition,
                    candidateTileXs[rank],
                    candidateTileYs[rank],
                    candidateFlags[rank],
                    candidateNormals[rank],
                    distanceSquared,
                    true);
            }
        }

        if (!best.IsValid)
        {
            body.Velocity = Vector2.Zero;
            counters.Flags |= PhysicsContactFlags.InitialOverlapUnresolved;
            return false;
        }

        body.Position = best.Position;
        counters.Flags |= best.Flags | PhysicsContactFlags.InitialOverlapRecovered;
        ResolveDepenetrationVelocity(body, material, best.Flags);
        writer.Add(new PhysicsContact(
            best.TileX,
            best.TileY,
            ResolveDepenetrationContactPoint(body, best),
            best.Normal,
            0f,
            best.Flags | PhysicsContactFlags.InitialOverlapRecovered));
        return true;
    }

    private bool TryCanOccupy(
        GameWorld world,
        PhysicsBody body,
        Vector2 position,
        ref MoveCounters counters)
    {
        var bounds = GetBodyBounds(body, position);
        var minTileX = PixelToTile(bounds.Left);
        var maxTileX = PixelToTile(InsideEdge(bounds.Right));
        var minTileY = PixelToTile(bounds.Top);
        var maxTileY = PixelToTile(InsideEdge(bounds.Bottom));
        for (var tileY = (long)minTileY; tileY <= maxTileY; tileY++)
        {
            for (var tileX = (long)minTileX; tileX <= maxTileX; tileX++)
            {
                if (!TryTestTile(world, tileX, tileY, ref counters, out var blocking) || blocking)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void ResolveDepenetrationVelocity(
        PhysicsBody body,
        PhysicsMaterial material,
        PhysicsContactFlags flags)
    {
        if ((flags & PhysicsContactFlags.Ground) != 0)
        {
            body.Velocity = new Vector2(
                body.Velocity.X * (1f - material.Friction),
                ResolveBounce(body.Velocity.Y, material.Restitution));
            body.OnGround = true;
            return;
        }

        if ((flags & PhysicsContactFlags.Ceiling) != 0)
        {
            body.Velocity = new Vector2(
                body.Velocity.X,
                ResolveBounce(body.Velocity.Y, material.Restitution));
            return;
        }

        body.Velocity = new Vector2(
            ResolveBounce(body.Velocity.X, material.Restitution),
            body.Velocity.Y);
    }

    private static Vector2 ResolveDepenetrationContactPoint(
        PhysicsBody body,
        DepenetrationCandidate candidate)
    {
        return new Vector2(
            body.Center.X - candidate.Normal.X * body.Size.X * 0.5f,
            body.Center.Y - candidate.Normal.Y * body.Size.Y * 0.5f);
    }

    private static void StopAtInitialOverlapBudget(
        PhysicsBody body,
        bool overlapFound,
        ref MoveCounters counters)
    {
        body.Velocity = Vector2.Zero;
        counters.BudgetExhausted = true;
        counters.Flags |= PhysicsContactFlags.WorkBudgetExhausted;
        if (overlapFound)
        {
            counters.Flags |= PhysicsContactFlags.InitialOverlapUnresolved;
        }
    }

    private void MoveX(
        GameWorld world,
        PhysicsBody body,
        float amount,
        PhysicsMaterial material,
        ref MoveCounters counters,
        ref ContactWriter writer)
    {
        if (amount == 0f || counters.BudgetExhausted)
        {
            return;
        }

        var startPosition = body.Position;
        var previousBounds = GetBodyBounds(body, startPosition);
        var candidatePosition = startPosition + new Vector2(amount, 0f);
        var candidateBounds = GetBodyBounds(body, candidatePosition);
        var minTileY = PixelToTile(candidateBounds.Top);
        var maxTileY = PixelToTile(InsideEdge(candidateBounds.Bottom));

        if (amount > 0f)
        {
            var startTileX = (long)PixelToTile(InsideEdge(previousBounds.Right)) + 1L;
            var endTileX = PixelToTile(InsideEdge(candidateBounds.Right));
            for (var tileX = startTileX; tileX <= endTileX; tileX++)
            {
                for (var tileY = (long)minTileY; tileY <= maxTileY; tileY++)
                {
                    if (!TryTestTile(world, tileX, tileY, ref counters, out var blocking))
                    {
                        StopAtBudget(body, startPosition, horizontal: true, ref counters);
                        return;
                    }

                    if (!blocking)
                    {
                        continue;
                    }

                    var resolvedX = (float)(tileX * GameConstants.TileSize) - body.Size.X;
                    body.Position = new Vector2(resolvedX, startPosition.Y);
                    body.Velocity = new Vector2(ResolveBounce(body.Velocity.X, material.Restitution), body.Velocity.Y);
                    var flags = PhysicsContactFlags.RightWall;
                    counters.Flags |= flags;
                    writer.Add(new PhysicsContact(
                        (int)tileX,
                        (int)tileY,
                        new Vector2(
                            (float)(tileX * GameConstants.TileSize),
                            ClampToTile(body.Center.Y, (int)tileY)),
                        -Vector2.UnitX,
                        ResolveTravelFraction(startPosition.X, body.Position.X, amount),
                        flags));
                    return;
                }
            }
        }
        else
        {
            var startTileX = PixelToTile(previousBounds.Left) - 1L;
            var endTileX = PixelToTile(candidateBounds.Left);
            for (var tileX = startTileX; tileX >= endTileX; tileX--)
            {
                for (var tileY = (long)minTileY; tileY <= maxTileY; tileY++)
                {
                    if (!TryTestTile(world, tileX, tileY, ref counters, out var blocking))
                    {
                        StopAtBudget(body, startPosition, horizontal: true, ref counters);
                        return;
                    }

                    if (!blocking)
                    {
                        continue;
                    }

                    var resolvedX = (float)((tileX + 1L) * GameConstants.TileSize);
                    body.Position = new Vector2(resolvedX, startPosition.Y);
                    body.Velocity = new Vector2(ResolveBounce(body.Velocity.X, material.Restitution), body.Velocity.Y);
                    var flags = PhysicsContactFlags.LeftWall;
                    counters.Flags |= flags;
                    writer.Add(new PhysicsContact(
                        (int)tileX,
                        (int)tileY,
                        new Vector2(
                            resolvedX,
                            ClampToTile(body.Center.Y, (int)tileY)),
                        Vector2.UnitX,
                        ResolveTravelFraction(startPosition.X, body.Position.X, amount),
                        flags));
                    return;
                }
            }
        }

        body.Position = candidatePosition;
    }

    private void MoveY(
        GameWorld world,
        PhysicsBody body,
        float amount,
        PhysicsMaterial material,
        ref MoveCounters counters,
        ref ContactWriter writer)
    {
        if (amount == 0f || counters.BudgetExhausted)
        {
            return;
        }

        var startPosition = body.Position;
        var previousBounds = GetBodyBounds(body, startPosition);
        var candidatePosition = startPosition + new Vector2(0f, amount);
        var candidateBounds = GetBodyBounds(body, candidatePosition);
        var minTileX = PixelToTile(candidateBounds.Left);
        var maxTileX = PixelToTile(InsideEdge(candidateBounds.Right));

        if (amount > 0f)
        {
            var startTileY = (long)PixelToTile(InsideEdge(previousBounds.Bottom)) + 1L;
            var endTileY = PixelToTile(InsideEdge(candidateBounds.Bottom));
            for (var tileY = startTileY; tileY <= endTileY; tileY++)
            {
                for (var tileX = (long)minTileX; tileX <= maxTileX; tileX++)
                {
                    if (!TryTestTile(world, tileX, tileY, ref counters, out var blocking))
                    {
                        StopAtBudget(body, startPosition, horizontal: false, ref counters);
                        return;
                    }

                    if (!blocking)
                    {
                        continue;
                    }

                    var resolvedY = (float)(tileY * GameConstants.TileSize) - body.Size.Y;
                    body.Position = new Vector2(startPosition.X, resolvedY);
                    body.Velocity = new Vector2(
                        body.Velocity.X * (1f - material.Friction),
                        ResolveBounce(body.Velocity.Y, material.Restitution));
                    body.OnGround = true;
                    var flags = PhysicsContactFlags.Ground;
                    counters.Flags |= flags;
                    writer.Add(new PhysicsContact(
                        (int)tileX,
                        (int)tileY,
                        new Vector2(
                            ClampToTile(body.Center.X, (int)tileX),
                            (float)(tileY * GameConstants.TileSize)),
                        -Vector2.UnitY,
                        ResolveTravelFraction(startPosition.Y, body.Position.Y, amount),
                        flags));
                    return;
                }
            }
        }
        else
        {
            var startTileY = PixelToTile(previousBounds.Top) - 1L;
            var endTileY = PixelToTile(candidateBounds.Top);
            for (var tileY = startTileY; tileY >= endTileY; tileY--)
            {
                for (var tileX = (long)minTileX; tileX <= maxTileX; tileX++)
                {
                    if (!TryTestTile(world, tileX, tileY, ref counters, out var blocking))
                    {
                        StopAtBudget(body, startPosition, horizontal: false, ref counters);
                        return;
                    }

                    if (!blocking)
                    {
                        continue;
                    }

                    var resolvedY = (float)((tileY + 1L) * GameConstants.TileSize);
                    body.Position = new Vector2(startPosition.X, resolvedY);
                    body.Velocity = new Vector2(body.Velocity.X, ResolveBounce(body.Velocity.Y, material.Restitution));
                    var flags = PhysicsContactFlags.Ceiling;
                    counters.Flags |= flags;
                    writer.Add(new PhysicsContact(
                        (int)tileX,
                        (int)tileY,
                        new Vector2(
                            ClampToTile(body.Center.X, (int)tileX),
                            resolvedY),
                        Vector2.UnitY,
                        ResolveTravelFraction(startPosition.Y, body.Position.Y, amount),
                        flags));
                    return;
                }
            }
        }

        body.Position = candidatePosition;
    }

    private bool TryTestTile(
        GameWorld world,
        long tileX,
        long tileY,
        ref MoveCounters counters,
        out bool blocking)
    {
        if (counters.TilesTested >= _settings.MaxTileTests ||
            tileX is < int.MinValue or > int.MaxValue ||
            tileY is < int.MinValue or > int.MaxValue)
        {
            counters.BudgetExhausted = true;
            counters.Flags |= PhysicsContactFlags.WorkBudgetExhausted;
            blocking = false;
            return false;
        }

        counters.TilesTested++;
        blocking = IsBlocking(world, (int)tileX, (int)tileY);
        return true;
    }

    private static void StopAtBudget(
        PhysicsBody body,
        Vector2 safePosition,
        bool horizontal,
        ref MoveCounters counters)
    {
        body.Position = safePosition;
        body.Velocity = horizontal
            ? new Vector2(0f, body.Velocity.Y)
            : new Vector2(body.Velocity.X, 0f);
        counters.BudgetExhausted = true;
        counters.Flags |= PhysicsContactFlags.WorkBudgetExhausted;
    }

    private static PhysicsMoveResult CreateResult(
        PhysicsBody body,
        Vector2 startPosition,
        Vector2 requestedDisplacement,
        PhysicsContactFlags flags,
        int contactsFound,
        int contactsWritten,
        int tilesTested,
        int substeps)
    {
        return new PhysicsMoveResult(
            startPosition,
            requestedDisplacement,
            body.Position - startPosition,
            body.Velocity,
            flags,
            contactsFound,
            contactsWritten,
            tilesTested,
            substeps);
    }

    private static BodyBounds GetBodyBounds(PhysicsBody body, Vector2 position)
    {
        return new BodyBounds(
            position.X,
            position.Y,
            position.X + body.Size.X,
            position.Y + body.Size.Y);
    }

    private static bool IsBlocking(GameWorld world, int tileX, int tileY)
    {
        return !world.TryGetTile(tileX, tileY, out var tile) || tile.IsSolid;
    }

    private static int PixelToTile(float pixel)
    {
        var tile = Math.Floor((double)pixel / GameConstants.TileSize);
        return tile <= int.MinValue
            ? int.MinValue
            : tile >= int.MaxValue
                ? int.MaxValue
                : (int)tile;
    }

    private static float ClampToTile(float coordinate, int tileCoordinate)
    {
        var minimum = (float)((long)tileCoordinate * GameConstants.TileSize);
        return Math.Clamp(coordinate, minimum, minimum + GameConstants.TileSize);
    }

    private static float ResolveBounce(float velocity, float restitution)
    {
        var bounced = -velocity * restitution;
        return MathF.Abs(bounced) <= ContactEpsilon ? 0f : bounced;
    }

    private static float InsideEdge(float coordinate)
    {
        return MathF.BitDecrement(coordinate);
    }

    private static float ResolveTravelFraction(float start, float end, float requested)
    {
        return requested == 0f
            ? 0f
            : Math.Clamp(MathF.Abs((end - start) / requested), 0f, 1f);
    }

    private static void ValidateInput(PhysicsBody body, float deltaSeconds)
    {
        if (!float.IsFinite(deltaSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        if (!IsFinite(body.Position) || !IsFinite(body.Velocity))
        {
            throw new InvalidOperationException("Physics body position and velocity must be finite.");
        }

        if (!IsFinite(body.Size) || body.Size.X <= 0f || body.Size.Y <= 0f)
        {
            throw new InvalidOperationException("Physics body size must be positive and finite.");
        }
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    private struct MoveCounters
    {
        public PhysicsContactFlags Flags;
        public int TilesTested;
        public int Substeps;
        public bool BudgetExhausted;
    }

    private ref struct ContactWriter
    {
        private readonly Span<PhysicsContact> _contacts;

        public ContactWriter(Span<PhysicsContact> contacts)
        {
            _contacts = contacts;
        }

        public int Found { get; private set; }

        public int Written { get; private set; }

        public void Add(PhysicsContact contact)
        {
            Found++;
            if (Written >= _contacts.Length)
            {
                return;
            }

            _contacts[Written++] = contact;
        }
    }

    private readonly record struct DepenetrationCandidate(
        Vector2 Position,
        int TileX,
        int TileY,
        PhysicsContactFlags Flags,
        Vector2 Normal,
        float DistanceSquared,
        bool IsValid);

    private readonly record struct BodyBounds(float Left, float Top, float Right, float Bottom);
}
