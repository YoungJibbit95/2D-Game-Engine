using Game.Core.Commands;
using System.Text;

namespace Game.Core.DeveloperTools;

public sealed class DeveloperCommandCatalog
{
    private const int StackScoreCapacity = 128;
    private readonly DeveloperCommandCatalogEntry[] _entries;

    public DeveloperCommandCatalog(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _entries = new DeveloperCommandCatalogEntry[registry.Commands.Count];
        for (var index = 0; index < registry.Commands.Count; index++)
        {
            _entries[index] = new DeveloperCommandCatalogEntry(registry.GetSpecification(registry.Commands[index]));
        }

        Array.Sort(_entries, CompareEntries);
    }

    public IReadOnlyList<DeveloperCommandCatalogEntry> Entries => _entries;

    public int Search(string? query, CommandCategory category, Span<int> destination)
    {
        if (destination.IsEmpty)
        {
            return 0;
        }

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown command category.");
        }

        Span<int> scores = destination.Length <= StackScoreCapacity
            ? stackalloc int[destination.Length]
            : new int[destination.Length];

        var trimmedQuery = (query ?? string.Empty).AsSpan().Trim();
        if (!trimmedQuery.IsEmpty && trimmedQuery[0] == '/')
        {
            trimmedQuery = trimmedQuery[1..].TrimStart();
        }

        var resultCount = 0;
        for (var entryIndex = 0; entryIndex < _entries.Length; entryIndex++)
        {
            var entry = _entries[entryIndex];
            if (category != CommandCategory.All && entry.Category != category)
            {
                continue;
            }

            var score = trimmedQuery.IsEmpty ? 1 : entry.Score(trimmedQuery);
            if (score < 0)
            {
                continue;
            }

            InsertRanked(entryIndex, score, destination, scores, ref resultCount);
        }

        return resultCount;
    }

    public bool TryFind(string? commandOrAlias, out DeveloperCommandCatalogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(commandOrAlias))
        {
            entry = null!;
            return false;
        }

        var normalized = commandOrAlias.Trim();
        if (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = normalized[1..].TrimStart();
        }

        if (normalized.Length == 0)
        {
            entry = null!;
            return false;
        }

        for (var index = 0; index < _entries.Length; index++)
        {
            var candidate = _entries[index];
            if (candidate.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                entry = candidate;
                return true;
            }

            for (var aliasIndex = 0; aliasIndex < candidate.Aliases.Count; aliasIndex++)
            {
                if (candidate.Aliases[aliasIndex].Equals(normalized, StringComparison.OrdinalIgnoreCase))
                {
                    entry = candidate;
                    return true;
                }
            }
        }

        entry = null!;
        return false;
    }

    private void InsertRanked(
        int entryIndex,
        int score,
        Span<int> destination,
        Span<int> scores,
        ref int count)
    {
        var insertion = 0;
        while (insertion < count &&
               !IsBetter(score, entryIndex, scores[insertion], destination[insertion]))
        {
            insertion++;
        }

        if (insertion >= destination.Length)
        {
            return;
        }

        var last = Math.Min(count, destination.Length - 1);
        for (var index = last; index > insertion; index--)
        {
            destination[index] = destination[index - 1];
            scores[index] = scores[index - 1];
        }

        destination[insertion] = entryIndex;
        scores[insertion] = score;
        count = Math.Min(count + 1, destination.Length);
    }

    private bool IsBetter(int score, int entryIndex, int otherScore, int otherEntryIndex)
    {
        if (score != otherScore)
        {
            return score > otherScore;
        }

        return string.Compare(
            _entries[entryIndex].Name,
            _entries[otherEntryIndex].Name,
            StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static int CompareEntries(
        DeveloperCommandCatalogEntry left,
        DeveloperCommandCatalogEntry right)
    {
        var category = left.Category.CompareTo(right.Category);
        return category != 0
            ? category
            : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class DeveloperCommandCatalogEntry
{
    public DeveloperCommandCatalogEntry(CommandSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        Specification = specification;
        Signature = specification.Usage;
        InsertText = specification.Arguments.Count == 0
            ? $"/{specification.Name}"
            : $"/{specification.Name} ";
        SearchText = BuildSearchText(specification);

        var required = 0;
        for (var index = 0; index < specification.Arguments.Count; index++)
        {
            required += specification.Arguments[index].IsRequired ? 1 : 0;
        }

        RequiredArgumentCount = required;
    }

    public CommandSpecification Specification { get; }

    public string Name => Specification.Name;

    public string Description => Specification.Description;

    public string Signature { get; }

    public string InsertText { get; }

    public string SearchText { get; }

    public CommandCategory Category => Specification.Category;

    public IReadOnlyList<string> Aliases => Specification.Aliases;

    public IReadOnlyList<CommandArgumentSpecification> Arguments => Specification.Arguments;

    public IReadOnlyList<string> Examples => Specification.Examples;

    public Type? RequestIntentType => Specification.RequestIntentType;

    public string DispatchLabel => RequestIntentType is null
        ? "IMMEDIATE"
        : $"REQUEST -> {RequestIntentType.Name}";

    public int RequiredArgumentCount { get; }

    public int Score(ReadOnlySpan<char> query)
    {
        var nameScore = TolerantCommandMatcher.Score(query, Name);
        var best = nameScore < 0 ? -1 : nameScore + 120;
        for (var index = 0; index < Aliases.Count; index++)
        {
            var score = TolerantCommandMatcher.Score(query, Aliases[index]);
            best = Math.Max(best, score < 0 ? score : score + 90);
        }

        var searchScore = TolerantCommandMatcher.Score(query, SearchText);
        return Math.Max(best, searchScore);
    }

    private static string BuildSearchText(CommandSpecification specification)
    {
        var builder = new StringBuilder(specification.Name)
            .Append(' ')
            .Append(specification.Description)
            .Append(' ')
            .Append(specification.Category);

        for (var index = 0; index < specification.Aliases.Count; index++)
        {
            builder.Append(' ').Append(specification.Aliases[index]);
        }

        for (var index = 0; index < specification.SearchTerms.Count; index++)
        {
            builder.Append(' ').Append(specification.SearchTerms[index]);
        }

        for (var index = 0; index < specification.Arguments.Count; index++)
        {
            builder.Append(' ')
                .Append(specification.Arguments[index].Name)
                .Append(' ')
                .Append(specification.Arguments[index].Description);
        }

        if (specification.RequestIntentType is { } requestIntentType)
        {
            builder.Append(" request intent handler ").Append(requestIntentType.Name);
        }
        else
        {
            builder.Append(" immediate command");
        }

        return builder.ToString();
    }
}
