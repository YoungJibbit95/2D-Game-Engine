namespace Game.Core.Physics;

public sealed class PhysicsBroadphase
{
    private readonly PhysicsBroadphaseSettings _settings;

    public PhysicsBroadphase(PhysicsBroadphaseSettings? settings = null)
    {
        _settings = (settings ?? PhysicsBroadphaseSettings.Default).Validate();
    }

    public PhysicsBroadphaseSettings Settings => _settings;

    public PhysicsBroadphaseTelemetry QueryOverlaps(
        ReadOnlySpan<PhysicsBody> bodies,
        Span<int> sortedBodyIndices,
        Span<PhysicsBodyPair> pairs)
    {
        var bodyCount = Math.Min(
            Math.Min(bodies.Length, sortedBodyIndices.Length),
            _settings.MaximumBodies);

        for (var index = 0; index < bodyCount; index++)
        {
            ValidateBody(bodies[index], index);
            sortedBodyIndices[index] = index;
        }

        SortByMinimumX(bodies, sortedBodyIndices[..bodyCount]);

        var pairTests = 0;
        var pairsFound = 0;
        var pairsWritten = 0;
        var budgetExhausted = false;

        for (var sortedIndex = 0; sortedIndex < bodyCount; sortedIndex++)
        {
            var bodyAIndex = sortedBodyIndices[sortedIndex];
            var bodyA = bodies[bodyAIndex];
            var maximumX = bodyA.Position.X + bodyA.Size.X;

            for (var candidateIndex = sortedIndex + 1; candidateIndex < bodyCount; candidateIndex++)
            {
                var bodyBIndex = sortedBodyIndices[candidateIndex];
                var bodyB = bodies[bodyBIndex];
                if (bodyB.Position.X >= maximumX)
                {
                    break;
                }

                if (pairTests >= _settings.MaximumPairTests)
                {
                    budgetExhausted = true;
                    break;
                }

                pairTests++;
                if (!ShouldCollide(bodyA, bodyB) || !Overlaps(bodyA, bodyB))
                {
                    continue;
                }

                pairsFound++;
                if (pairsWritten < pairs.Length)
                {
                    pairs[pairsWritten++] = bodyAIndex < bodyBIndex
                        ? new PhysicsBodyPair(bodyAIndex, bodyBIndex)
                        : new PhysicsBodyPair(bodyBIndex, bodyAIndex);
                }
            }

            if (budgetExhausted)
            {
                break;
            }
        }

        return new PhysicsBroadphaseTelemetry(
            bodies.Length,
            bodyCount,
            bodies.Length - bodyCount,
            pairTests,
            pairsFound,
            pairsWritten,
            budgetExhausted);
    }

    public PhysicsBroadphaseTelemetry QuerySweptOverlaps(
        ReadOnlySpan<PhysicsBody> bodies,
        ReadOnlySpan<PhysicsBodySweepState> sweepStates,
        float deltaSeconds,
        Span<int> sortedBodyIndices,
        Span<PhysicsBodyPair> pairs)
    {
        if (sweepStates.Length < bodies.Length)
        {
            throw new ArgumentException(
                "Sweep-state storage must contain one slot for every submitted body.",
                nameof(sweepStates));
        }
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        var bodyCount = Math.Min(
            Math.Min(bodies.Length, sortedBodyIndices.Length),
            _settings.MaximumBodies);
        for (var index = 0; index < bodyCount; index++)
        {
            ValidateBody(bodies[index], index);
            if (!float.IsFinite(sweepStates[index].PredictedVelocity.X) ||
                !float.IsFinite(sweepStates[index].PredictedVelocity.Y))
            {
                throw new ArgumentException($"Sweep state at index {index} has invalid velocity.", nameof(sweepStates));
            }

            sortedBodyIndices[index] = index;
        }

        SortBySweptMinimumX(bodies, sweepStates, deltaSeconds, sortedBodyIndices[..bodyCount]);
        var pairTests = 0;
        var pairsFound = 0;
        var pairsWritten = 0;
        var budgetExhausted = false;
        for (var sortedIndex = 0; sortedIndex < bodyCount; sortedIndex++)
        {
            var bodyAIndex = sortedBodyIndices[sortedIndex];
            var bodyA = bodies[bodyAIndex];
            GetSweptBounds(
                bodyA,
                sweepStates[bodyAIndex].PredictedVelocity,
                deltaSeconds,
                out _,
                out var minimumY,
                out var maximumX,
                out var maximumY);
            for (var candidateIndex = sortedIndex + 1; candidateIndex < bodyCount; candidateIndex++)
            {
                var bodyBIndex = sortedBodyIndices[candidateIndex];
                var bodyB = bodies[bodyBIndex];
                GetSweptBounds(
                    bodyB,
                    sweepStates[bodyBIndex].PredictedVelocity,
                    deltaSeconds,
                    out var candidateMinimumX,
                    out var candidateMinimumY,
                    out _,
                    out var candidateMaximumY);
                if (candidateMinimumX > maximumX)
                {
                    break;
                }
                if (pairTests >= _settings.MaximumPairTests)
                {
                    budgetExhausted = true;
                    break;
                }

                pairTests++;
                if (!ShouldCollide(bodyA, bodyB) ||
                    candidateMinimumY > maximumY ||
                    candidateMaximumY < minimumY)
                {
                    continue;
                }

                pairsFound++;
                if (pairsWritten < pairs.Length)
                {
                    pairs[pairsWritten++] = bodyAIndex < bodyBIndex
                        ? new PhysicsBodyPair(bodyAIndex, bodyBIndex)
                        : new PhysicsBodyPair(bodyBIndex, bodyAIndex);
                }
            }

            if (budgetExhausted)
            {
                break;
            }
        }

        return new PhysicsBroadphaseTelemetry(
            bodies.Length,
            bodyCount,
            bodies.Length - bodyCount,
            pairTests,
            pairsFound,
            pairsWritten,
            budgetExhausted);
    }

    private static void SortByMinimumX(
        ReadOnlySpan<PhysicsBody> bodies,
        Span<int> indices)
    {
        // Stateless heapsort keeps the worst case bounded at O(N log N) without
        // hidden comparer/delegate allocations or persistent broadphase state.
        for (var parent = indices.Length / 2 - 1; parent >= 0; parent--)
        {
            SiftDown(bodies, indices, parent, indices.Length);
        }

        for (var end = indices.Length - 1; end > 0; end--)
        {
            (indices[0], indices[end]) = (indices[end], indices[0]);
            SiftDown(bodies, indices, 0, end);
        }
    }

    private static void SiftDown(
        ReadOnlySpan<PhysicsBody> bodies,
        Span<int> indices,
        int root,
        int count)
    {
        while (true)
        {
            var leftChild = root * 2 + 1;
            if (leftChild >= count)
            {
                return;
            }

            var greaterChild = leftChild;
            var rightChild = leftChild + 1;
            if (rightChild < count && ComesAfter(bodies, indices[rightChild], indices[leftChild]))
            {
                greaterChild = rightChild;
            }

            if (!ComesAfter(bodies, indices[greaterChild], indices[root]))
            {
                return;
            }

            (indices[root], indices[greaterChild]) = (indices[greaterChild], indices[root]);
            root = greaterChild;
        }
    }

    private static void SortBySweptMinimumX(
        ReadOnlySpan<PhysicsBody> bodies,
        ReadOnlySpan<PhysicsBodySweepState> sweepStates,
        float deltaSeconds,
        Span<int> indices)
    {
        for (var parent = indices.Length / 2 - 1; parent >= 0; parent--)
        {
            SiftDownSwept(bodies, sweepStates, deltaSeconds, indices, parent, indices.Length);
        }
        for (var end = indices.Length - 1; end > 0; end--)
        {
            (indices[0], indices[end]) = (indices[end], indices[0]);
            SiftDownSwept(bodies, sweepStates, deltaSeconds, indices, 0, end);
        }
    }

    private static void SiftDownSwept(
        ReadOnlySpan<PhysicsBody> bodies,
        ReadOnlySpan<PhysicsBodySweepState> sweepStates,
        float deltaSeconds,
        Span<int> indices,
        int root,
        int count)
    {
        while (true)
        {
            var leftChild = root * 2 + 1;
            if (leftChild >= count)
            {
                return;
            }
            var greaterChild = leftChild;
            var rightChild = leftChild + 1;
            if (rightChild < count && ComesAfterSwept(
                    bodies,
                    sweepStates,
                    deltaSeconds,
                    indices[rightChild],
                    indices[leftChild]))
            {
                greaterChild = rightChild;
            }
            if (!ComesAfterSwept(
                    bodies,
                    sweepStates,
                    deltaSeconds,
                    indices[greaterChild],
                    indices[root]))
            {
                return;
            }

            (indices[root], indices[greaterChild]) = (indices[greaterChild], indices[root]);
            root = greaterChild;
        }
    }

    private static bool ComesAfterSwept(
        ReadOnlySpan<PhysicsBody> bodies,
        ReadOnlySpan<PhysicsBodySweepState> sweepStates,
        float deltaSeconds,
        int leftIndex,
        int rightIndex)
    {
        var left = bodies[leftIndex];
        var right = bodies[rightIndex];
        var leftMinimumX = Math.Min(
            left.Position.X,
            left.Position.X + sweepStates[leftIndex].PredictedVelocity.X * deltaSeconds);
        var rightMinimumX = Math.Min(
            right.Position.X,
            right.Position.X + sweepStates[rightIndex].PredictedVelocity.X * deltaSeconds);
        var comparison = leftMinimumX.CompareTo(rightMinimumX);
        if (comparison != 0)
        {
            return comparison > 0;
        }

        comparison = left.DeterministicOrder.CompareTo(right.DeterministicOrder);
        if (comparison != 0)
        {
            return comparison > 0;
        }
        return leftIndex > rightIndex;
    }

    private static void GetSweptBounds(
        PhysicsBody body,
        System.Numerics.Vector2 velocity,
        float deltaSeconds,
        out float minimumX,
        out float minimumY,
        out float maximumX,
        out float maximumY)
    {
        var endX = body.Position.X + velocity.X * deltaSeconds;
        var endY = body.Position.Y + velocity.Y * deltaSeconds;
        minimumX = Math.Min(body.Position.X, endX);
        minimumY = Math.Min(body.Position.Y, endY);
        maximumX = Math.Max(body.Position.X + body.Size.X, endX + body.Size.X);
        maximumY = Math.Max(body.Position.Y + body.Size.Y, endY + body.Size.Y);
    }

    private static bool ComesAfter(
        ReadOnlySpan<PhysicsBody> bodies,
        int leftIndex,
        int rightIndex)
    {
        var left = bodies[leftIndex];
        var right = bodies[rightIndex];
        var comparison = left.Position.X.CompareTo(right.Position.X);
        if (comparison != 0)
        {
            return comparison > 0;
        }

        comparison = left.Position.Y.CompareTo(right.Position.Y);
        if (comparison != 0)
        {
            return comparison > 0;
        }

        comparison = left.DeterministicOrder.CompareTo(right.DeterministicOrder);
        if (comparison != 0)
        {
            return comparison > 0;
        }

        comparison = left.Size.X.CompareTo(right.Size.X);
        if (comparison != 0)
        {
            return comparison > 0;
        }

        comparison = left.Size.Y.CompareTo(right.Size.Y);
        return comparison != 0
            ? comparison > 0
            : leftIndex > rightIndex;
    }

    private bool ShouldCollide(PhysicsBody bodyA, PhysicsBody bodyB)
    {
        if (!_settings.IncludeStaticStaticPairs &&
            bodyA.BodyType == PhysicsBodyType.Static &&
            bodyB.BodyType == PhysicsBodyType.Static)
        {
            return false;
        }

        return bodyA.CollisionLayer != PhysicsCollisionLayer.None &&
               bodyB.CollisionLayer != PhysicsCollisionLayer.None &&
               (bodyA.CollisionMask & bodyB.CollisionLayer) != PhysicsCollisionLayer.None &&
               (bodyB.CollisionMask & bodyA.CollisionLayer) != PhysicsCollisionLayer.None;
    }

    private static bool Overlaps(PhysicsBody bodyA, PhysicsBody bodyB)
    {
        return bodyA.Position.X < bodyB.Position.X + bodyB.Size.X &&
               bodyA.Position.X + bodyA.Size.X > bodyB.Position.X &&
               bodyA.Position.Y < bodyB.Position.Y + bodyB.Size.Y &&
               bodyA.Position.Y + bodyA.Size.Y > bodyB.Position.Y;
    }

    private static void ValidateBody(PhysicsBody? body, int index)
    {
        if (body is null)
        {
            throw new ArgumentException($"Physics body at index {index} is null.", nameof(body));
        }

        if (!float.IsFinite(body.Position.X) ||
            !float.IsFinite(body.Position.Y) ||
            !float.IsFinite(body.Size.X) ||
            !float.IsFinite(body.Size.Y) ||
            body.Size.X <= 0f ||
            body.Size.Y <= 0f)
        {
            throw new ArgumentException($"Physics body at index {index} has invalid bounds.", nameof(body));
        }
    }
}
