using System.Text.Json;

namespace Game.Core.Diagnostics.Replay;

public static class ReplayJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static byte[] Serialize(ReplayRecordingSnapshot snapshot)
    {
        ReplayRecordingSnapshot.Validate(snapshot);
        return JsonSerializer.SerializeToUtf8Bytes(snapshot, Options);
    }

    public static ReplayRecordingSnapshot Deserialize(ReadOnlySpan<byte> payload)
    {
        if (payload.Length > ReplayLimits.MaximumSerializedBytes)
        {
            throw new InvalidDataException(
                $"Replay JSON exceeds the {ReplayLimits.MaximumSerializedBytes}-byte safety limit.");
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<ReplayRecordingSnapshot>(payload, Options)
                ?? throw new InvalidDataException("Replay JSON is empty.");
            ReplayRecordingSnapshot.Validate(snapshot);
            return snapshot;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Replay JSON is malformed.", exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException("Replay JSON contains unsupported values.", exception);
        }
    }
}
