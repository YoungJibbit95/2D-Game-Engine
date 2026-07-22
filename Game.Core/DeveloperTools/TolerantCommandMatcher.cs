namespace Game.Core.DeveloperTools;

public static class TolerantCommandMatcher
{
    private const int MaximumEditLength = 48;

    public static int Score(ReadOnlySpan<char> query, ReadOnlySpan<char> candidate)
    {
        query = query.Trim();
        candidate = candidate.Trim();
        if (query.IsEmpty)
        {
            return 1;
        }

        if (candidate.IsEmpty)
        {
            return -1;
        }

        if (candidate.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1_000;
        }

        if (candidate.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 900;
        }

        var wordPrefix = FindWordPrefix(candidate, query);
        if (wordPrefix >= 0)
        {
            return 820 - Math.Min(100, wordPrefix);
        }

        var substring = candidate.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (substring >= 0)
        {
            return 720 - Math.Min(120, substring * 2);
        }

        var subsequencePenalty = GetSubsequencePenalty(query, candidate);
        if (subsequencePenalty >= 0)
        {
            return 560 - Math.Min(180, subsequencePenalty);
        }

        if (query.Length <= MaximumEditLength && candidate.Length <= MaximumEditLength)
        {
            var maximumDistance = query.Length <= 4 ? 1 : 2;
            var distance = BoundedLevenshteinDistance(query, candidate, maximumDistance);
            if (distance <= maximumDistance)
            {
                return 420 - distance * 60 - Math.Abs(candidate.Length - query.Length) * 4;
            }
        }

        return -1;
    }

    private static int FindWordPrefix(ReadOnlySpan<char> candidate, ReadOnlySpan<char> query)
    {
        for (var index = 1; index < candidate.Length; index++)
        {
            if (!IsWordBoundary(candidate[index - 1], candidate[index]))
            {
                continue;
            }

            if (candidate[index..].StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsWordBoundary(char previous, char current)
    {
        return previous is ' ' or '-' or '_' or '/' or '.' ||
               char.IsLower(previous) && char.IsUpper(current);
    }

    private static int GetSubsequencePenalty(ReadOnlySpan<char> query, ReadOnlySpan<char> candidate)
    {
        var queryIndex = 0;
        var firstMatch = -1;
        var previousMatch = -1;
        var gapPenalty = 0;
        for (var candidateIndex = 0; candidateIndex < candidate.Length && queryIndex < query.Length; candidateIndex++)
        {
            if (!CharactersEqual(query[queryIndex], candidate[candidateIndex]))
            {
                continue;
            }

            firstMatch = firstMatch < 0 ? candidateIndex : firstMatch;
            if (previousMatch >= 0)
            {
                gapPenalty += candidateIndex - previousMatch - 1;
            }

            previousMatch = candidateIndex;
            queryIndex++;
        }

        return queryIndex == query.Length
            ? Math.Max(0, firstMatch) * 3 + gapPenalty * 2 + candidate.Length - query.Length
            : -1;
    }

    private static int BoundedLevenshteinDistance(
        ReadOnlySpan<char> left,
        ReadOnlySpan<char> right,
        int maximumDistance)
    {
        if (Math.Abs(left.Length - right.Length) > maximumDistance)
        {
            return maximumDistance + 1;
        }

        Span<int> previous = stackalloc int[MaximumEditLength + 1];
        Span<int> current = stackalloc int[MaximumEditLength + 1];
        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;
            var rowMinimum = current[0];
            for (var column = 1; column <= right.Length; column++)
            {
                var substitutionCost = CharactersEqual(left[row - 1], right[column - 1]) ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + substitutionCost);
                rowMinimum = Math.Min(rowMinimum, current[column]);
            }

            if (rowMinimum > maximumDistance)
            {
                return maximumDistance + 1;
            }

            var swap = previous;
            previous = current;
            current = swap;
        }

        return previous[right.Length];
    }

    private static bool CharactersEqual(char left, char right)
    {
        return char.ToUpperInvariant(left) == char.ToUpperInvariant(right);
    }
}
