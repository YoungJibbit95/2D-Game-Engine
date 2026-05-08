using Game.Core.World;

namespace Game.Core.Lighting;

public readonly record struct LightSource(TilePos Position, byte Intensity, int Radius);
