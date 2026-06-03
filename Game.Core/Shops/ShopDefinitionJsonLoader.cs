using Game.Core.Data;
using System.Text.Json;

namespace Game.Core.Shops;

public sealed class ShopDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public ShopRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return ShopRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<ShopDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<ShopDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public ShopDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return LoadDefinitionFromJson(File.ReadAllText(filePath), filePath);
    }

    public ShopDefinition LoadDefinitionFromJson(string json)
    {
        return LoadDefinitionFromJson(json, "inline json");
    }

    private static ShopDefinition LoadDefinitionFromJson(string json, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var dto = JsonSerializer.Deserialize<ShopDefinitionDto>(json, Options);
        if (dto is null)
        {
            throw new JsonException($"Shop definition was empty: {source}");
        }

        return dto.ToDefinition();
    }

    private sealed record ShopDefinitionDto
    {
        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public string CurrencyItemId { get; init; } = "copper_coin";

        public ShopStockEntryDto[] Stock { get; init; } = Array.Empty<ShopStockEntryDto>();

        public ShopSellEntryDto[] SellPrices { get; init; } = Array.Empty<ShopSellEntryDto>();

        public string[] Tags { get; init; } = Array.Empty<string>();

        public ShopDefinition ToDefinition()
        {
            return new ShopDefinition
            {
                Id = Id ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                CurrencyItemId = CurrencyItemId,
                Stock = Stock.Select(stock => stock.ToDefinition()).ToArray(),
                SellPrices = SellPrices.Select(sell => sell.ToDefinition()).ToArray(),
                Tags = DefinitionTags.Normalize(Tags)
            };
        }
    }

    private sealed record ShopStockEntryDto
    {
        public string? ItemId { get; init; }

        public int Count { get; init; } = 1;

        public int Price { get; init; }

        public string? CurrencyItemId { get; init; }

        public int SortOrder { get; init; }

        public Dictionary<string, string> Conditions { get; init; } =
            new(StringComparer.OrdinalIgnoreCase);

        public ShopStockEntry ToDefinition()
        {
            return new ShopStockEntry
            {
                ItemId = ItemId ?? string.Empty,
                Count = Count,
                Price = Price,
                CurrencyItemId = CurrencyItemId,
                SortOrder = SortOrder,
                Conditions = new Dictionary<string, string>(Conditions, StringComparer.OrdinalIgnoreCase)
            };
        }
    }

    private sealed record ShopSellEntryDto
    {
        public string? ItemId { get; init; }

        public int Price { get; init; }

        public string? CurrencyItemId { get; init; }

        public ShopSellEntry ToDefinition()
        {
            return new ShopSellEntry
            {
                ItemId = ItemId ?? string.Empty,
                Price = Price,
                CurrencyItemId = CurrencyItemId
            };
        }
    }
}
