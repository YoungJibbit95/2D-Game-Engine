using Game.Core.Animation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering.Character;

public readonly record struct CharacterDrawCommand(
    string RigLayerId,
    string SpriteId,
    int SpriteFrameIndex,
    Vector2 Position,
    Vector2 Origin,
    float RotationRadians,
    Vector2 Scale,
    Color Color,
    SpriteEffects Effects,
    float LayerDepth,
    int DrawOrder,
    SpriteLayerBlendRole BlendRole);

public sealed class CharacterDrawCommandBuilder
{
    public CharacterDrawCommand[] Build(
        CharacterSpriteLayerPose pose,
        float baseLayerDepth = 0.5f,
        float layerDepthStep = 0.0001f)
    {
        ArgumentNullException.ThrowIfNull(pose);
        var commands = new List<CharacterDrawCommand>(pose.Layers.Count);
        BuildInto(pose, commands, baseLayerDepth, layerDepthStep);
        return commands.ToArray();
    }

    public void BuildInto(
        CharacterSpriteLayerPose pose,
        List<CharacterDrawCommand> destination,
        float baseLayerDepth = 0.5f,
        float layerDepthStep = 0.0001f)
    {
        ArgumentNullException.ThrowIfNull(pose);
        ArgumentNullException.ThrowIfNull(destination);
        if (!float.IsFinite(baseLayerDepth) || baseLayerDepth is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(baseLayerDepth));
        }

        if (!float.IsFinite(layerDepthStep) || layerDepthStep < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(layerDepthStep));
        }

        destination.Clear();
        for (var index = 0; index < pose.Layers.Count; index++)
        {
            var layer = pose.Layers[index];
            if (!layer.Visible || layer.Opacity <= 0f || string.IsNullOrWhiteSpace(layer.SpriteId))
            {
                continue;
            }

            var layerDepth = baseLayerDepth + (layer.DrawOrder * layerDepthStep);
            if (layerDepth is < 0f or > 1f)
            {
                throw new InvalidOperationException(
                    $"Layer depth for rig layer '{layer.RigLayerId}' is outside SpriteBatch's [0, 1] range.");
            }

            destination.Add(new CharacterDrawCommand(
                layer.RigLayerId,
                layer.SpriteId,
                layer.SpriteFrameIndex,
                layer.Position,
                layer.Origin,
                layer.RotationRadians,
                layer.Scale,
                ToXnaColor(layer.Tint.WithOpacity(layer.Opacity)),
                layer.FlipHorizontally ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                layerDepth,
                layer.DrawOrder,
                layer.BlendRole));
        }
    }

    private static Color ToXnaColor(AnimationColor color)
    {
        return new Color(color.R, color.G, color.B, color.A);
    }
}
