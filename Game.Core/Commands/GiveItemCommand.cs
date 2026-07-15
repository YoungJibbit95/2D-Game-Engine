using Game.Core.Inventory;

namespace Game.Core.Commands;

public sealed class GiveItemCommand : TypedConsoleCommand
{
    public GiveItemCommand()
        : base(new CommandSpecification(
            "give",
            "Adds an item stack to the player inventory.",
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
                    isRequired: false,
                    description: "Number of items to add.",
                    minimum: 1)
            },
            aliases: new[] { "item" },
            examples: new[] { "/give gel", "/give gel 25" }))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        if (context.Content is null)
        {
            return CommandResult.Failure("Content database is required for /give.");
        }

        if (context.PlayerInventory is null && context.PlayerLoadoutInventory is null)
        {
            return CommandResult.Failure("Player inventory is required for /give.");
        }

        var itemId = arguments.GetString("itemId");
        if (!context.Content.Items.TryGetById(itemId, out _))
        {
            return CommandResult.Failure($"Unknown item '{itemId}'.");
        }

        var count = arguments.Has("count") ? arguments.GetInt32("count") : 1;

        var stack = new ItemStack(itemId, count);
        if (context.PlayerLoadoutInventory is not null)
        {
            if (!context.PlayerLoadoutInventory.CanAddItem(stack))
            {
                return CommandResult.Failure($"Inventory cannot fit {count}x {itemId}.");
            }

            context.PlayerLoadoutInventory.AddItem(stack);
            return CommandResult.Success($"Gave {count}x {itemId}.");
        }

        if (!context.PlayerInventory!.CanAddItem(stack))
        {
            return CommandResult.Failure($"Inventory cannot fit {count}x {itemId}.");
        }

        context.PlayerInventory.AddItem(stack);
        return CommandResult.Success($"Gave {count}x {itemId}.");
    }
}
