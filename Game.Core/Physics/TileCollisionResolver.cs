using Game.Core.World;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Physics;

public sealed class TileCollisionResolver
{
    public void Move(GameWorld world, PhysicsBody body, float deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(body);

        if (!body.CollidesWithTiles)
        {
            body.Position += body.Velocity * deltaSeconds;
            return;
        }

        body.OnGround = false;
        MoveX(world, body, body.Velocity.X * deltaSeconds);
        MoveY(world, body, body.Velocity.Y * deltaSeconds);
    }

    private static void MoveX(GameWorld world, PhysicsBody body, float amount)
    {
        if (amount == 0)
        {
            return;
        }

        var previousBounds = GetBodyBounds(body);
        body.Position += new Vector2(amount, 0);
        var bounds = GetBodyBounds(body);
        var minTileY = PixelToTile(bounds.Top);
        var maxTileY = PixelToTile(bounds.Bottom - 0.01f);

        if (amount > 0)
        {
            var startTileX = PixelToTile(previousBounds.Right - 0.01f) + 1;
            var endTileX = PixelToTile(bounds.Right - 0.01f);
            for (var tileX = startTileX; tileX <= endTileX; tileX++)
            {
                for (var tileY = minTileY; tileY <= maxTileY; tileY++)
                {
                    if (!world.IsSolid(tileX, tileY))
                    {
                        continue;
                    }

                    body.Position = new Vector2(tileX * GameConstants.TileSize - body.Size.X, body.Position.Y);
                    body.Velocity = new Vector2(0, body.Velocity.Y);
                    return;
                }
            }
        }
        else
        {
            var startTileX = PixelToTile(previousBounds.Left);
            var endTileX = PixelToTile(bounds.Left);
            for (var tileX = startTileX; tileX >= endTileX; tileX--)
            {
                for (var tileY = minTileY; tileY <= maxTileY; tileY++)
                {
                    if (!world.IsSolid(tileX, tileY))
                    {
                        continue;
                    }

                    body.Position = new Vector2((tileX + 1) * GameConstants.TileSize, body.Position.Y);
                    body.Velocity = new Vector2(0, body.Velocity.Y);
                    return;
                }
            }
        }
    }

    private static void MoveY(GameWorld world, PhysicsBody body, float amount)
    {
        if (amount == 0)
        {
            return;
        }

        var previousBounds = GetBodyBounds(body);
        body.Position += new Vector2(0, amount);
        var bounds = GetBodyBounds(body);
        var minTileX = PixelToTile(bounds.Left);
        var maxTileX = PixelToTile(bounds.Right - 0.01f);

        if (amount > 0)
        {
            var startTileY = PixelToTile(previousBounds.Bottom - 0.01f) + 1;
            var endTileY = PixelToTile(bounds.Bottom - 0.01f);
            for (var tileY = startTileY; tileY <= endTileY; tileY++)
            {
                for (var tileX = minTileX; tileX <= maxTileX; tileX++)
                {
                    if (!world.IsSolid(tileX, tileY))
                    {
                        continue;
                    }

                    body.Position = new Vector2(body.Position.X, tileY * GameConstants.TileSize - body.Size.Y);
                    body.Velocity = new Vector2(body.Velocity.X, 0);
                    body.OnGround = true;
                    return;
                }
            }
        }
        else
        {
            var startTileY = PixelToTile(previousBounds.Top);
            var endTileY = PixelToTile(bounds.Top);
            for (var tileY = startTileY; tileY >= endTileY; tileY--)
            {
                for (var tileX = minTileX; tileX <= maxTileX; tileX++)
                {
                    if (!world.IsSolid(tileX, tileY))
                    {
                        continue;
                    }

                    body.Position = new Vector2(body.Position.X, (tileY + 1) * GameConstants.TileSize);
                    body.Velocity = new Vector2(body.Velocity.X, 0);
                    return;
                }
            }
        }
    }

    private static BodyBounds GetBodyBounds(PhysicsBody body)
    {
        return new BodyBounds(
            body.Position.X,
            body.Position.Y,
            body.Position.X + body.Size.X,
            body.Position.Y + body.Size.Y);
    }

    private static int PixelToTile(float pixel)
    {
        return (int)MathF.Floor(pixel / GameConstants.TileSize);
    }

    private readonly record struct BodyBounds(float Left, float Top, float Right, float Bottom);
}
