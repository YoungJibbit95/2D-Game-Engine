using Game.Core.Inventory;
using System.Text.Json;

namespace Game.Core.Crafting;

public sealed class RecipeDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public RecipeRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return RecipeRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<RecipeDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<RecipeDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public RecipeDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var stream = File.OpenRead(filePath);
        return LoadDefinition(stream, filePath);
    }

    public RecipeDefinition LoadDefinitionFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return LoadDefinition(stream, "inline json");
    }

    private static RecipeDefinition LoadDefinition(Stream stream, string source)
    {
        var dto = JsonSerializer.Deserialize<RecipeDefinitionDto>(stream, Options);
        if (dto is null)
        {
            throw new JsonException($"Recipe definition was empty: {source}");
        }

        return dto.ToDefinition();
    }

    private sealed record RecipeDefinitionDto
    {
        public string? Id { get; init; }

        public ItemStackDto? Result { get; init; }

        public List<ItemStackDto> Ingredients { get; init; } = new();

        public string? Station { get; init; }

        public RecipeDefinition ToDefinition()
        {
            return new RecipeDefinition
            {
                Id = Id ?? string.Empty,
                Result = Result?.ToItemStack() ?? ItemStack.Empty,
                Ingredients = Ingredients
                    .Select(ingredient => new RecipeIngredient
                    {
                        ItemId = ingredient.ItemId ?? string.Empty,
                        Count = ingredient.Count
                    })
                    .ToArray(),
                Station = Station
            };
        }
    }

    private sealed record ItemStackDto
    {
        public string? ItemId { get; init; }

        public int Count { get; init; }

        public ItemStack ToItemStack()
        {
            return string.IsNullOrWhiteSpace(ItemId) || Count <= 0
                ? ItemStack.Empty
                : new ItemStack(ItemId, Count);
        }
    }
}
