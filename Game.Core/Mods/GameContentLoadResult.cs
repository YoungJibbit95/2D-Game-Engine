using Game.Core.Data;

namespace Game.Core.Mods;

public sealed record GameContentLoadResult(GameContentDatabase Database, ContentLoadReport Report);
