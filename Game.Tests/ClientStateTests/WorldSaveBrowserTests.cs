using Game.Client.GameStates;
using Xunit;

namespace Game.Tests.ClientStateTests;

public sealed class WorldSaveBrowserTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "yjse-world-browser-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ListWorlds_SkipsCorruptEntriesAndKeepsValidWorlds()
    {
        var valid = Path.Combine(_root, "valid");
        Directory.CreateDirectory(valid);
        File.WriteAllText(Path.Combine(valid, "metadata.json"), """
        {
          "Name": "Playable",
          "Seed": 42,
          "CreatedAtUtc": "2026-07-12T00:00:00+00:00",
          "WidthTiles": 128,
          "HeightTiles": 96,
          "IsHorizontallyInfinite": true
        }
        """);
        var corrupt = Path.Combine(_root, "corrupt");
        Directory.CreateDirectory(corrupt);
        File.WriteAllText(Path.Combine(corrupt, "metadata.json"), "{ definitely not json }");

        var entry = Assert.Single(new WorldSaveBrowser(() => _root).ListWorlds());

        Assert.Equal("Playable", entry.Name);
        Assert.Equal(42, entry.Seed);
        Assert.True(entry.IsHorizontallyInfinite);
    }

    [Fact]
    public void ListWorlds_RecoversWhenSaveRootCannotBeResolved()
    {
        var browser = new WorldSaveBrowser(() => throw new UnauthorizedAccessException("blocked"));

        Assert.Empty(browser.ListWorlds());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
