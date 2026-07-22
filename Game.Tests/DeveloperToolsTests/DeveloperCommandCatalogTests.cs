using Game.Core.Commands;
using Game.Core.DeveloperTools;
using Xunit;

namespace Game.Tests.DeveloperToolsTests;

public sealed class DeveloperCommandCatalogTests
{
    [Fact]
    public void Search_RanksTyposAndRespectsCategoryWithoutAllocatingAfterWarmup()
    {
        var catalog = new DeveloperCommandCatalog(CommandRegistry.CreateDefault());
        var matches = new int[catalog.Entries.Count];

        var count = catalog.Search("ligting", CommandCategory.Rendering, matches);

        Assert.True(count > 0);
        Assert.Equal("lighting", catalog.Entries[matches[0]].Name);
        for (var index = 0; index < count; index++)
        {
            Assert.Equal(CommandCategory.Rendering, catalog.Entries[matches[index]].Category);
        }

        catalog.Search("render", CommandCategory.All, matches);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 200; iteration++)
        {
            catalog.Search("render", CommandCategory.All, matches);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Theory]
    [InlineData("/gve", "give")]
    [InlineData("/weather snw", "snow")]
    [InlineData("/rule enemy-spwn", "enemy-spawning")]
    public void Suggestions_TolerateCommandAndVocabularyTypos(string input, string expected)
    {
        var service = new CommandSuggestionService(CommandRegistry.CreateDefault());

        var suggestions = service.GetSuggestions(input, new CommandContext());

        Assert.Contains(suggestions, suggestion => suggestion.ReplacementText == expected);
    }
}
