namespace Game.Core.UI.Animation;

public sealed class UiAnimationPlayer
{
    private readonly Dictionary<UiAnimationProperty, float> _values = new();

    public UiAnimationClip? Clip { get; private set; }

    public float TimeSeconds { get; private set; }

    public bool IsPlaying { get; private set; }

    public IReadOnlyDictionary<UiAnimationProperty, float> Values => _values;

    public void Play(UiAnimationClip clip, bool restart = true)
    {
        ArgumentNullException.ThrowIfNull(clip);

        Clip = clip;
        IsPlaying = true;
        if (restart)
        {
            TimeSeconds = 0f;
        }

        Evaluate();
    }

    public void Stop(bool snapToEnd = false)
    {
        if (snapToEnd && Clip is not null)
        {
            TimeSeconds = Clip.DurationSeconds;
            Evaluate();
        }

        IsPlaying = false;
    }

    public void Update(float deltaSeconds, float speed = 1f)
    {
        if (!IsPlaying || Clip is null || deltaSeconds <= 0f || speed <= 0f)
        {
            return;
        }

        TimeSeconds += deltaSeconds * speed;
        if (TimeSeconds >= Clip.DurationSeconds)
        {
            if (Clip.Loop && Clip.DurationSeconds > 0f)
            {
                TimeSeconds %= Clip.DurationSeconds;
            }
            else
            {
                TimeSeconds = Clip.DurationSeconds;
                IsPlaying = false;
            }
        }

        Evaluate();
    }

    public float GetValue(UiAnimationProperty property, float fallback = 0f)
    {
        return _values.TryGetValue(property, out var value)
            ? value
            : fallback;
    }

    private void Evaluate()
    {
        if (Clip is null)
        {
            return;
        }

        foreach (var track in Clip.Tracks)
        {
            _values[track.Property] = track.Evaluate(TimeSeconds);
        }
    }
}
