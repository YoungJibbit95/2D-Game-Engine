using System.Text.Json;
using Game.Core.Randomness;

namespace Game.Core.Saving;

public sealed class RandomStateSaveService
{
    public const string DefaultFileName = "random-state.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public void Save(SessionRandomRegistry registry, string filePath)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Save(registry.ExportSnapshot(), filePath);
    }

    public void Save(RandomRegistrySnapshot snapshot, string filePath)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var validatedRegistry = SessionRandomRegistry.FromSnapshot(snapshot);
        var payload = JsonSerializer.SerializeToUtf8Bytes(validatedRegistry.ExportSnapshot(), Options);
        WriteAtomically(payload, GetFullFilePath(filePath));
    }

    public RandomStateLoadResult LoadOrCreate(string filePath, ulong sessionSeed)
    {
        var fullPath = GetFullFilePath(filePath);
        var backupPath = GetBackupPath(fullPath);
        if (!File.Exists(fullPath))
        {
            if (File.Exists(backupPath))
            {
                return new RandomStateLoadResult
                {
                    Registry = ReadRegistry(backupPath, sessionSeed),
                    Source = RandomStateLoadSource.BackupRecovery,
                    Warning = $"Random state primary file '{fullPath}' was missing; loaded its backup."
                };
            }

            return new RandomStateLoadResult
            {
                Registry = new SessionRandomRegistry(sessionSeed),
                Source = RandomStateLoadSource.LegacyFallback
            };
        }

        try
        {
            return new RandomStateLoadResult
            {
                Registry = ReadRegistry(fullPath, sessionSeed),
                Source = RandomStateLoadSource.Primary
            };
        }
        catch (Exception primaryException) when (IsRecoverableReadFailure(primaryException))
        {
            if (!File.Exists(backupPath))
            {
                throw new InvalidDataException(
                    $"Random state file '{fullPath}' is invalid and no recovery backup is available.",
                    primaryException);
            }

            try
            {
                return new RandomStateLoadResult
                {
                    Registry = ReadRegistry(backupPath, sessionSeed),
                    Source = RandomStateLoadSource.BackupRecovery,
                    Warning = $"Random state primary file '{fullPath}' was invalid; loaded its backup."
                };
            }
            catch (Exception backupException) when (IsRecoverableReadFailure(backupException))
            {
                throw new InvalidDataException(
                    $"Random state file '{fullPath}' and its recovery backup are invalid.",
                    new AggregateException(primaryException, backupException));
            }
        }
    }

    public static string GetBackupPath(string filePath)
    {
        return GetFullFilePath(filePath) + ".bak";
    }

    private static SessionRandomRegistry ReadRegistry(string filePath, ulong expectedSessionSeed)
    {
        RandomRegistrySnapshot snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<RandomRegistrySnapshot>(File.ReadAllBytes(filePath), Options)
                ?? throw new InvalidDataException("Random state data is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Random state JSON is malformed.", exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException("Random state JSON uses unsupported values.", exception);
        }

        if (snapshot.SessionSeed != expectedSessionSeed)
        {
            throw new InvalidDataException(
                $"Random state session seed {snapshot.SessionSeed} does not match expected seed {expectedSessionSeed}.");
        }

        return SessionRandomRegistry.FromSnapshot(snapshot);
    }

    private static void WriteAtomically(ReadOnlySpan<byte> payload, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new ArgumentException("Random state file path must include a valid directory.", nameof(filePath));
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                stream.Write(payload);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(filePath))
            {
                File.Replace(
                    temporaryPath,
                    filePath,
                    GetBackupPath(filePath),
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, filePath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string GetFullFilePath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        if (string.IsNullOrWhiteSpace(Path.GetFileName(fullPath)))
        {
            throw new ArgumentException("Random state file path must identify a file.", nameof(filePath));
        }

        return fullPath;
    }

    private static bool IsRecoverableReadFailure(Exception exception)
    {
        return exception is IOException or InvalidDataException;
    }
}
