using Game.Core.Animation;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Character;

public enum SpriteLayerBlendRole
{
    Outgoing,
    Current
}

public readonly record struct CharacterSocketWorldPose(
    string Id,
    Vector2 Position,
    float RotationRadians);

public sealed record SpriteLayerPose
{
    public required string RigLayerId { get; init; }

    public required CharacterAppearanceSlot AppearanceSlot { get; init; }

    public required string SpriteId { get; init; }

    public required int SpriteFrameIndex { get; init; }

    public required Vector2 Position { get; init; }

    public required Vector2 Origin { get; init; }

    public required float RotationRadians { get; init; }

    public required Vector2 Scale { get; init; }

    public required AnimationColor Tint { get; init; }

    public required float Opacity { get; init; }

    public required bool Visible { get; init; }

    public required bool FlipHorizontally { get; init; }

    public required int DrawOrder { get; init; }

    public required SpriteLayerBlendRole BlendRole { get; init; }

    public required IReadOnlyList<CharacterSocketWorldPose> Sockets { get; init; }

    public bool TryGetSocket(string socketId, out CharacterSocketWorldPose socket)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socketId);
        for (var index = 0; index < Sockets.Count; index++)
        {
            if (string.Equals(Sockets[index].Id, socketId, StringComparison.OrdinalIgnoreCase))
            {
                socket = Sockets[index];
                return true;
            }
        }

        socket = default;
        return false;
    }
}

public sealed record CharacterSpriteLayerPose
{
    public required string RigId { get; init; }

    public required Vector2 WorldPosition { get; init; }

    public required CharacterFacingDirection FacingDirection { get; init; }

    public required long FixedTick { get; init; }

    public required IReadOnlyList<SpriteLayerPose> Layers { get; init; }
}
