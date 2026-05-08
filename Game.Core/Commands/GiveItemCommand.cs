using Game.Core.Inventory;

namespace Game.Core.Commands;

public sealed class GiveItemCommand : IConsoleCommand
{
    public string Name => "give";

    public string Description => "Adds an item stack to the player inventory.";

    public IReadOnlyList<string> Aliases { get; } = new[] { "item" };

    public CommandResult Execute(CommandContext context, IReadOnlyList<string> arguments)
    {
        if (context.Content is null)
        {
            return CommandResult.Failure("Content database is required for /give.");
        }

        if (context.PlayerInventory is null)
        {
            return CommandResult.Failure("Player inventory is required for /give.");
        }

        if (arguments.Count == 0)
        {
            return CommandResult.Failure("Usage: /give <itemId> [count]");
        }

        var itemId = arguments[0];
        if (!context.Content.Items.TryGetById(itemId, out _))
        {
            return CommandResult.Failure($"Unknown item '{itemId}'.");
        }

        var count = 1;
        if (arguments.Count >= 2 && (!int.TryParse(arguments[1], out count) || count <= 0))
        {
            return CommandResult.Failure("Item count must be a positive integer.");
        }

        var stack = new ItemStack(itemId, count);
        if (!context.PlayerInventory.CanAddItem(stack))
        {
            return CommandResult.Failure($"Inventory cannot fit {count}x {itemId}.");
        }

        context.PlayerInventory.AddItem(stack);
        return CommandResult.Success($"Gave {count}x {itemId}.");
    }
}
