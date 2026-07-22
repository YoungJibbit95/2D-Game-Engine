using Game.Core.Inventory;
using InventoryModel = Game.Core.Inventory.Inventory;

namespace Game.Core.Commands;

public sealed class RemoveItemCommand : TypedConsoleCommand
{
    public RemoveItemCommand()
        : base(new CommandSpecification(
            "remove",
            "Removes an item stack from the player inventory.",
            new[]
            {
                new CommandArgumentSpecification(
                    "itemId",
                    CommandArgumentType.Identifier,
                    description: "Registered item id.",
                    suggestionSource: CommandSuggestionSource.Items),
                new CommandArgumentSpecification(
                    "count",
                    CommandArgumentType.Integer,
                    false,
                    "Number of items to remove; defaults to one.",
                    minimum: 1)
            },
            aliases: new[] { "take" },
            examples: new[] { "/remove gel", "/remove gel 10" },
            category: CommandCategory.Inventory,
            searchTerms: new[] { "item", "inventory", "take" }))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        if (context.Content is null)
        {
            return CommandResult.Failure("missing_content", "Content database is required for /remove.");
        }

        if (context.PlayerInventory is null && context.PlayerLoadoutInventory is null)
        {
            return CommandResult.Failure("missing_inventory", "Player inventory is required for /remove.");
        }

        var itemId = arguments.GetString("itemId");
        if (!context.Content.Items.TryGetById(itemId, out _))
        {
            return CommandResult.Failure("unknown_item", $"Unknown item '{itemId}'.");
        }

        var count = arguments.Has("count") ? arguments.GetInt32("count") : 1;
        var available = context.PlayerLoadoutInventory?.CountItem(itemId) ?? context.PlayerInventory!.CountItem(itemId);
        if (available < count)
        {
            return CommandResult.Failure("insufficient_items", $"Inventory only contains {available}x {itemId}.");
        }

        var removed = context.PlayerLoadoutInventory is not null
            ? context.PlayerLoadoutInventory.RemoveTransaction(itemId, count, new InventoryTransactionOptions(IncludeFavorites: true)).Completed
            : context.PlayerInventory!.RemoveTransaction(itemId, count, new InventoryTransactionOptions(IncludeFavorites: true)).Completed;
        return removed
            ? CommandResult.Success("inventory_item_removed", $"Removed {count}x {itemId}.")
            : CommandResult.Failure("remove_failed", $"Could not remove {count}x {itemId}.");
    }
}

public sealed class ClearInventoryCommand : TypedConsoleCommand
{
    public ClearInventoryCommand()
        : base(new CommandSpecification(
            "clear",
            "Clears the player inventory.",
            new[]
            {
                new CommandArgumentSpecification(
                    "target",
                    CommandArgumentType.Choice,
                    choices: new[] { "inventory" },
                    description: "Inventory target to clear.")
            },
            aliases: new[] { "clearinventory" },
            examples: new[] { "/clear inventory" },
            category: CommandCategory.Inventory,
            searchTerms: new[] { "item", "inventory", "empty" }))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        if (context.PlayerInventory is null && context.PlayerLoadoutInventory is null)
        {
            return CommandResult.Failure("missing_inventory", "Player inventory is required for /clear inventory.");
        }

        var removed = context.PlayerLoadoutInventory is not null
            ? Clear(context.PlayerLoadoutInventory.Hotbar) + Clear(context.PlayerLoadoutInventory.Main)
            : Clear(context.PlayerInventory!);
        return CommandResult.Success("inventory_cleared", $"Cleared {removed} item(s) from the player inventory.");
    }

    private static int Clear(InventoryModel inventory)
    {
        var removed = 0;
        foreach (var slot in inventory.Slots)
        {
            removed += slot.Clear().Count;
        }

        return removed;
    }
}
