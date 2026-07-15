namespace Game.Core.Entities;

public enum EntityFaction
{
    Neutral,
    Friendly,
    Hostile
}

public enum EntityDisposition
{
    Friendly,
    Neutral,
    Hostile
}

public static class EntityFactionResolver
{
    public static EntityFaction GetFaction(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity is EnemyEntity actor ? actor.Faction : EntityFaction.Friendly;
    }

    public static EntityDisposition GetDisposition(EntityFaction observer, EntityFaction subject)
    {
        if (observer == EntityFaction.Neutral || subject == EntityFaction.Neutral)
        {
            return EntityDisposition.Neutral;
        }

        return observer == subject ? EntityDisposition.Friendly : EntityDisposition.Hostile;
    }
}
