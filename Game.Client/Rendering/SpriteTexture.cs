using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering;

public sealed record SpriteTexture(
    string SpriteId,
    Texture2D Texture,
    Rectangle SourceRectangle,
    bool IsPlaceholder);
