using Game.Client.GameStates;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientStateTests;

public sealed class WorldMenuPresentationTests
{
    [Theory]
    [InlineData(320, 240)]
    [InlineData(640, 360)]
    [InlineData(1920, 1080)]
    public void WorldSelectLayout_KeepsContentAndHitTargetsInsideViewport(int width, int height)
    {
        var viewport = new Rectangle(0, 0, width, height);
        var layout = WorldMenuLayoutPlanner.PlanWorldSelect(viewport, entryCount: 12);

        AssertContains(viewport, layout.Panel);
        AssertContains(layout.Panel, layout.List);
        AssertContains(layout.Panel, layout.CreateButton);
        AssertContains(layout.Panel, layout.RefreshButton);
        AssertContains(layout.Panel, layout.BackButton);
        AssertContains(layout.List, layout.GetRowBounds(0, reserveScrollRail: true));
        Assert.False(layout.CreateButton.Intersects(layout.RefreshButton));
        Assert.False(layout.RefreshButton.Intersects(layout.BackButton));
        Assert.True(layout.VisibleRowCount >= 1);
    }

    [Fact]
    public void WorldSelectLayout_UsesStackedMetadataOnlyWhenHorizontalSpaceIsConstrained()
    {
        var compact = WorldMenuLayoutPlanner.PlanWorldSelect(new Rectangle(0, 0, 640, 360), entryCount: 8);
        var wide = WorldMenuLayoutPlanner.PlanWorldSelect(new Rectangle(0, 0, 1280, 720), entryCount: 8);

        Assert.True(compact.StackedMetadata);
        Assert.False(wide.StackedMetadata);
        Assert.True(wide.VisibleRowCount > compact.VisibleRowCount);
    }

    [Theory]
    [InlineData(320, 240)]
    [InlineData(640, 360)]
    [InlineData(1920, 1080)]
    public void CreateWorldLayout_KeepsFieldsButtonsAndStatusCardInsidePanel(int width, int height)
    {
        var viewport = new Rectangle(0, 0, width, height);
        var layout = WorldMenuLayoutPlanner.PlanCreateWorld(viewport);

        AssertContains(viewport, layout.Panel);
        AssertContains(layout.Panel, layout.NameField);
        AssertContains(layout.Panel, layout.SeedField);
        AssertContains(layout.Panel, layout.CreateButton);
        AssertContains(layout.Panel, layout.RandomButton);
        AssertContains(layout.Panel, layout.BackButton);
        AssertContains(layout.Panel, layout.ProfileCard);
        Assert.False(layout.CreateButton.Intersects(layout.RandomButton));
        Assert.False(layout.RandomButton.Intersects(layout.BackButton));
    }

    [Fact]
    public void LayoutPlanning_HasZeroSteadyStateAllocation()
    {
        _ = WorldMenuLayoutPlanner.PlanWorldSelect(new Rectangle(0, 0, 1920, 1080), entryCount: 24);
        _ = WorldMenuLayoutPlanner.PlanCreateWorld(new Rectangle(0, 0, 1920, 1080));

        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;
        for (var index = 0; index < 10_000; index++)
        {
            var select = WorldMenuLayoutPlanner.PlanWorldSelect(new Rectangle(0, 0, 1920, 1080), entryCount: 24);
            var create = WorldMenuLayoutPlanner.PlanCreateWorld(new Rectangle(0, 0, 1920, 1080));
            checksum += select.List.Height + create.NameField.Width;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(checksum > 0);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void CreationForm_ReplacesDefaultsAndKeepsLettersForTextEditing()
    {
        var form = new WorldCreationForm(initialSeed: 12345);

        Assert.False(form.AcceptsLetterNavigation);
        Assert.True(form.Append('W'));
        Assert.True(form.Append('o'));
        Assert.True(form.Append('r'));
        Assert.Equal("Wor", form.WorldName);
        Assert.Equal("WOR", form.WorldNameDisplay);

        form.Focus(WorldCreationForm.SeedIndex, selectContents: true);
        Assert.False(form.AcceptsLetterNavigation);
        Assert.True(form.Append('4'));
        Assert.True(form.Append('2'));
        Assert.Equal("42", form.SeedText);
        Assert.True(form.IsSeedValid);
    }

    [Fact]
    public void CreationForm_SkipsDisabledCreateUntilSeedIsValid()
    {
        var form = new WorldCreationForm(initialSeed: 12345);
        form.Focus(WorldCreationForm.SeedIndex, selectContents: true);

        Assert.True(form.Backspace());
        Assert.False(form.IsSeedValid);
        form.MoveSelection(1);
        Assert.Equal(WorldCreationForm.RandomSeedIndex, form.SelectedIndex);

        form.Focus(WorldCreationForm.SeedIndex, selectContents: true);
        Assert.True(form.Append('7'));
        form.MoveSelection(1);
        Assert.Equal(WorldCreationForm.CreateIndex, form.SelectedIndex);
        Assert.True(form.TryGetWorld(out var name, out var seed));
        Assert.Equal("New World", name);
        Assert.Equal(7, seed);
    }

    [Fact]
    public void WorldEntry_PrecomputesReadableDisplayLabels()
    {
        var entry = new WorldSaveEntry(
            "Long_world-name-that-needs-a-readable-label",
            42,
            new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero),
            128,
            96,
            IsHorizontallyInfinite: true,
            "world");

        Assert.Equal("LONG WORLD NAME THA.", entry.CompactDisplayName);
        Assert.Contains("INF X 96", entry.MetadataLabel, StringComparison.Ordinal);
        Assert.Contains("SEED 42", entry.MetadataLabel, StringComparison.Ordinal);
        Assert.Same(entry.MetadataLabel, entry.MetadataLabel);
    }

    private static void AssertContains(Rectangle outer, Rectangle inner)
    {
        Assert.True(
            inner.Left >= outer.Left &&
            inner.Top >= outer.Top &&
            inner.Right <= outer.Right &&
            inner.Bottom <= outer.Bottom,
            $"Expected {outer} to contain {inner}.");
    }
}
