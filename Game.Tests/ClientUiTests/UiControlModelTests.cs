using Game.Core.UI;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class UiControlModelTests
{
    [Fact]
    public void NumericRange_SnapsPointerValuesAndClampsEndpoints()
    {
        var range = new UiNumericRange(0.5f, 4f, 0.25f);

        Assert.Equal(0.5f, range.ValueAt(-1f));
        Assert.Equal(2.25f, range.ValueAt(0.5f));
        Assert.Equal(4f, range.ValueAt(2f));
        Assert.Equal(1f, range.Normalize(float.PositiveInfinity));
    }

    [Fact]
    public void NumericRange_StepByUsesDiscreteStableValues()
    {
        var range = new UiNumericRange(0f, 1f, 0.05f);

        var value = 0f;
        for (var index = 0; index < 7; index++)
        {
            value = range.StepBy(value, 1);
        }

        Assert.Equal(0.35f, value, 3);
        Assert.Equal(0f, range.StepBy(0f, -1));
        Assert.Equal(1f, range.StepBy(1f, 1));
    }

    [Theory]
    [InlineData(0, UiGridDirection.Left, 2)]
    [InlineData(2, UiGridDirection.Right, 0)]
    [InlineData(1, UiGridDirection.Down, 4)]
    [InlineData(4, UiGridDirection.Down, 1)]
    [InlineData(4, UiGridDirection.Right, 3)]
    public void GridNavigator_WrapsWithinRaggedGrid(int current, UiGridDirection direction, int expected)
    {
        Assert.Equal(expected, UiGridNavigator.Move(current, itemCount: 5, columns: 3, direction: direction, wrap: true));
    }

    [Fact]
    public void GridNavigator_ClampsWithoutWrap()
    {
        Assert.Equal(4, UiGridNavigator.Move(4, itemCount: 5, columns: 3, direction: UiGridDirection.Down));
        Assert.Equal(0, UiGridNavigator.Move(0, itemCount: 5, columns: 3, direction: UiGridDirection.Left));
    }
}
