namespace Game.Core.Items;

public interface IItemDefinitionProvider
{
    ItemDefinition GetById(string id);

    bool TryGetById(string id, out ItemDefinition definition);
}
