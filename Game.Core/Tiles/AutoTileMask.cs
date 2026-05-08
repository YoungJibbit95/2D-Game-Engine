namespace Game.Core.Tiles;

[Flags]
public enum AutoTileMask
{
    None = 0,
    Top = 1,
    Right = 2,
    Bottom = 4,
    Left = 8
}
