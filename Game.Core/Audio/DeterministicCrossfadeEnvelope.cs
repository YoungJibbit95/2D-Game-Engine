namespace Game.Core.Audio;

public readonly record struct CrossfadeEnvelopeSnapshot(
    float Progress,
    float OutgoingGain,
    float IncomingGain,
    bool IsComplete);

public sealed class DeterministicCrossfadeEnvelope
{
    private float _durationSeconds;
    private float _elapsedSeconds;

    public CrossfadeEnvelopeSnapshot Snapshot => CreateSnapshot();

    public void Begin(float durationSeconds)
    {
        if (!float.IsFinite(durationSeconds) || durationSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        }

        _durationSeconds = durationSeconds;
        _elapsedSeconds = durationSeconds == 0f ? durationSeconds : 0f;
    }

    public CrossfadeEnvelopeSnapshot Advance(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        _elapsedSeconds = MathF.Min(_durationSeconds, _elapsedSeconds + elapsedSeconds);
        return CreateSnapshot();
    }

    private CrossfadeEnvelopeSnapshot CreateSnapshot()
    {
        var progress = _durationSeconds <= 0f ? 1f : _elapsedSeconds / _durationSeconds;
        return new CrossfadeEnvelopeSnapshot(
            progress,
            MathF.Sqrt(MathF.Max(0f, 1f - progress)),
            MathF.Sqrt(progress),
            progress >= 1f);
    }
}
