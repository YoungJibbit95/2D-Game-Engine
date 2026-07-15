namespace Game.Core.UI;

public enum UiGridDirection
{
    Left,
    Right,
    Up,
    Down
}

public static class UiGridNavigator
{
    public static int Move(int currentIndex, int itemCount, int columns, UiGridDirection direction, bool wrap = false)
    {
        if (itemCount <= 0)
        {
            return -1;
        }

        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        var current = Math.Clamp(currentIndex, 0, itemCount - 1);
        var row = current / columns;
        var column = current % columns;
        var rows = (itemCount + columns - 1) / columns;

        return direction switch
        {
            UiGridDirection.Left => MoveHorizontal(current, row, column, itemCount, columns, -1, wrap),
            UiGridDirection.Right => MoveHorizontal(current, row, column, itemCount, columns, 1, wrap),
            UiGridDirection.Up => MoveVertical(current, row, column, itemCount, columns, rows, -1, wrap),
            UiGridDirection.Down => MoveVertical(current, row, column, itemCount, columns, rows, 1, wrap),
            _ => current
        };
    }

    private static int MoveHorizontal(
        int current,
        int row,
        int column,
        int itemCount,
        int columns,
        int direction,
        bool wrap)
    {
        var nextColumn = column + direction;
        var rowStart = row * columns;
        var rowLength = Math.Min(columns, itemCount - rowStart);
        if (nextColumn >= 0 && nextColumn < rowLength)
        {
            return rowStart + nextColumn;
        }

        if (!wrap)
        {
            return current;
        }

        return rowStart + (direction < 0 ? rowLength - 1 : 0);
    }

    private static int MoveVertical(
        int current,
        int row,
        int column,
        int itemCount,
        int columns,
        int rows,
        int direction,
        bool wrap)
    {
        var nextRow = row + direction;
        if (wrap)
        {
            nextRow = (nextRow % rows + rows) % rows;
        }
        else if (nextRow < 0 || nextRow >= rows)
        {
            return current;
        }

        return Math.Min(nextRow * columns + column, itemCount - 1);
    }
}
