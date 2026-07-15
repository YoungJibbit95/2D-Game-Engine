using System.Text;

namespace Game.Core.Randomness;

public sealed class SessionRandomRegistry
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;
    private readonly Dictionary<string, DeterministicRandomStream> _streams = new(StringComparer.Ordinal);

    public SessionRandomRegistry(ulong sessionSeed)
    {
        SessionSeed = sessionSeed;
    }

    public ulong SessionSeed { get; }

    public int StreamCount => _streams.Count;

    public DeterministicRandomStream GetStream(string name)
    {
        var canonicalName = CanonicalizeName(name);
        if (_streams.TryGetValue(canonicalName, out var stream))
        {
            return stream;
        }

        stream = new DeterministicRandomStream(CreateInitialSnapshot(SessionSeed, canonicalName));
        _streams.Add(canonicalName, stream);
        return stream;
    }

    public bool TryGetStream(string name, out DeterministicRandomStream? stream)
    {
        var canonicalName = CanonicalizeName(name);
        return _streams.TryGetValue(canonicalName, out stream);
    }

    public DeterministicRandomAdapter CreateSystemRandomAdapter(string name)
    {
        return new DeterministicRandomAdapter(GetStream(name));
    }

    public RandomRegistrySnapshot ExportSnapshot()
    {
        var streamSnapshots = new List<RandomStreamSnapshot>(_streams.Count);
        foreach (var streamName in _streams.Keys.Order(StringComparer.Ordinal))
        {
            streamSnapshots.Add(_streams[streamName].ExportSnapshot());
        }

        return new RandomRegistrySnapshot
        {
            SessionSeed = SessionSeed,
            Streams = streamSnapshots
        };
    }

    public void ImportSnapshot(RandomRegistrySnapshot snapshot)
    {
        ValidateSnapshot(snapshot, SessionSeed);

        var importedSnapshots = new Dictionary<string, RandomStreamSnapshot>(StringComparer.Ordinal);
        foreach (var streamSnapshot in snapshot.Streams)
        {
            var canonicalName = CanonicalizeName(streamSnapshot.Name);
            if (!string.Equals(canonicalName, streamSnapshot.Name, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Random stream name '{streamSnapshot.Name}' is not in canonical form.");
            }

            DeterministicRandomStream.ValidateSnapshot(streamSnapshot);
            if (!importedSnapshots.TryAdd(canonicalName, streamSnapshot))
            {
                throw new InvalidDataException($"Random registry snapshot contains duplicate stream '{canonicalName}'.");
            }
        }

        var importedStreams = new Dictionary<string, DeterministicRandomStream>(StringComparer.Ordinal);
        foreach (var pair in importedSnapshots)
        {
            if (_streams.TryGetValue(pair.Key, out var existingStream))
            {
                existingStream.Restore(pair.Value);
                importedStreams.Add(pair.Key, existingStream);
            }
            else
            {
                importedStreams.Add(pair.Key, new DeterministicRandomStream(pair.Value));
            }
        }

        _streams.Clear();
        foreach (var pair in importedStreams)
        {
            _streams.Add(pair.Key, pair.Value);
        }
    }

    public static SessionRandomRegistry FromSnapshot(RandomRegistrySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var registry = new SessionRandomRegistry(snapshot.SessionSeed);
        registry.ImportSnapshot(snapshot);
        return registry;
    }

    private static RandomStreamSnapshot CreateInitialSnapshot(ulong sessionSeed, string streamName)
    {
        unchecked
        {
            var hash = FnvOffsetBasis;
            for (var byteIndex = 0; byteIndex < sizeof(ulong); byteIndex++)
            {
                hash ^= (byte)(sessionSeed >> (byteIndex * 8));
                hash *= FnvPrime;
            }

            hash ^= byte.MaxValue;
            hash *= FnvPrime;
            foreach (var value in Encoding.UTF8.GetBytes(streamName))
            {
                hash ^= value;
                hash *= FnvPrime;
            }

            var splitMixState = hash;
            return new RandomStreamSnapshot
            {
                Name = streamName,
                State0 = NextSplitMix64(ref splitMixState),
                State1 = NextSplitMix64(ref splitMixState),
                State2 = NextSplitMix64(ref splitMixState),
                State3 = NextSplitMix64(ref splitMixState),
                DrawCount = 0
            };
        }
    }

    private static ulong NextSplitMix64(ref ulong state)
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            var value = state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }

    private static string CanonicalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!string.Equals(name, name.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Random stream names cannot have leading or trailing whitespace.", nameof(name));
        }

        if (name.Length > 128)
        {
            throw new ArgumentException("Random stream names cannot exceed 128 characters.", nameof(name));
        }

        if (name.Any(char.IsControl))
        {
            throw new ArgumentException("Random stream names cannot contain control characters.", nameof(name));
        }

        return name.Normalize(NormalizationForm.FormC);
    }

    private static void ValidateSnapshot(RandomRegistrySnapshot snapshot, ulong expectedSessionSeed)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.FormatVersion != RandomRegistrySnapshot.CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"Unsupported random registry snapshot format version {snapshot.FormatVersion}.");
        }

        if (snapshot.SessionSeed != expectedSessionSeed)
        {
            throw new InvalidDataException(
                $"Random registry snapshot seed {snapshot.SessionSeed} does not match session seed {expectedSessionSeed}.");
        }

        if (snapshot.Streams is null)
        {
            throw new InvalidDataException("Random registry snapshot does not contain a stream collection.");
        }
    }
}
