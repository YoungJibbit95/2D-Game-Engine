using System.Text.Json;
using Game.Core.WorldEvents;

namespace Game.Core.Saving;

public sealed class WorldEventStateSaveService
{
    public const string DefaultFileName = "world-events.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public void Save(WorldEventRuntimeStateSnapshot state, string filePath)
    {
        WorldEventRuntimeStateSnapshot.Validate(state);
        var payload = JsonSerializer.SerializeToUtf8Bytes(state, Options);
        WriteAtomically(payload, GetFullFilePath(filePath));
    }

    public WorldEventStateLoadResult LoadOrDefault(string filePath)
    {
        var fullPath = GetFullFilePath(filePath);
        var backupPath = GetBackupPath(fullPath);
        if (!File.Exists(fullPath))
        {
            if (File.Exists(backupPath))
            {
                return new WorldEventStateLoadResult
                {
                    State = ReadState(backupPath),
                    Source = WorldEventStateLoadSource.BackupRecovery,
                    Warning = $"World-event primary file '{fullPath}' was missing; loaded its backup."
                };
            }

            return new WorldEventStateLoadResult
            {
                Source = WorldEventStateLoadSource.LegacyFallback
            };
        }

        try
        {
            return new WorldEventStateLoadResult
            {
                State = ReadState(fullPath),
                Source = WorldEventStateLoadSource.Primary
            };
        }
        catch (Exception primaryException) when (IsRecoverableReadFailure(primaryException))
        {
            if (!File.Exists(backupPath))
            {
                throw new InvalidDataException(
                    $"World-event state file '{fullPath}' is invalid and no recovery backup is available.",
                    primaryException);
            }

            try
            {
                return new WorldEventStateLoadResult
                {
                    State = ReadState(backupPath),
                    Source = WorldEventStateLoadSource.BackupRecovery,
                    Warning = $"World-event primary file '{fullPath}' was invalid; loaded its backup."
                };
            }
            catch (Exception backupException) when (IsRecoverableReadFailure(backupException))
            {
                throw new InvalidDataException(
                    $"World-event state file '{fullPath}' and its recovery backup are invalid.",
                    new AggregateException(primaryException, backupException));
            }
        }
    }

    public static string GetBackupPath(string filePath)
    {
        return GetFullFilePath(filePath) + ".bak";
    }

    private static WorldEventRuntimeStateSnapshot ReadState(string filePath)
    {
        WorldEventRuntimeStateSnapshot state;
        try
        {
            state = JsonSerializer.Deserialize<WorldEventRuntimeStateSnapshot>(File.ReadAllBytes(filePath), Options)
                ?? throw new InvalidDataException("World-event state data is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("World-event state JSON is malformed.", exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException("World-event state JSON uses unsupported values.", exception);
        }

        WorldEventRuntimeStateSnapshot.Validate(state);
        return state;
    }

    private static void WriteAtomically(ReadOnlySpan<byte> payload, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new ArgumentException("World-event state file path must include a valid directory.", nameof(filePath));
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
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
                File.Replace(temporaryPath, filePath, GetBackupPath(filePath), ignoreMetadataErrors: true);
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
            throw new ArgumentException("World-event state file path must identify a file.", nameof(filePath));
        }

        return fullPath;
    }

    private static bool IsRecoverableReadFailure(Exception exception)
    {
        return exception is IOException or InvalidDataException;
    }
}
