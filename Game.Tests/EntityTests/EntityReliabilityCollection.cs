using Xunit;

namespace Game.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EntityReliabilityCollection
{
    public const string Name = "Entity reliability";
}
