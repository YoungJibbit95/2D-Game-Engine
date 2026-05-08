using Game.Client.Rendering;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public readonly record struct UIContext(RenderContext RenderContext, Point MousePosition);
