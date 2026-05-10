namespace Game.Core.UI.Animation;

public sealed class UiAnimationClip
{
    public UiAnimationClip(string id, IEnumerable<UiAnimationTrack> tracks, bool loop = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(tracks);

        Id = id;
        Tracks = tracks.ToArray();
        if (Tracks.Count == 0)
        {
            throw new ArgumentException("Animation clips require at least one track.", nameof(tracks));
        }

        Loop = loop;
        DurationSeconds = Tracks.Max(track => track.DurationSeconds);
    }

    public string Id { get; }

    public IReadOnlyList<UiAnimationTrack> Tracks { get; }

    public bool Loop { get; }

    public float DurationSeconds { get; }

    public static UiAnimationClip FadeIn(float durationSeconds)
    {
        return new UiAnimationClip(
            "ui.fade_in",
            new[]
            {
                new UiAnimationTrack(
                    UiAnimationProperty.Opacity,
                    new[]
                    {
                        new UiAnimationKeyframe(0f, 0f),
                        new UiAnimationKeyframe(Math.Max(0.001f, durationSeconds), 1f, UiAnimationCurve.EaseOut)
                    })
            });
    }

    public static UiAnimationClip SlideFadeIn(float durationSeconds, float startOffsetY)
    {
        durationSeconds = Math.Max(0.001f, durationSeconds);
        return new UiAnimationClip(
            "ui.slide_fade_in",
            new[]
            {
                new UiAnimationTrack(
                    UiAnimationProperty.Opacity,
                    new[]
                    {
                        new UiAnimationKeyframe(0f, 0f),
                        new UiAnimationKeyframe(durationSeconds, 1f, UiAnimationCurve.EaseOut)
                    }),
                new UiAnimationTrack(
                    UiAnimationProperty.OffsetY,
                    new[]
                    {
                        new UiAnimationKeyframe(0f, startOffsetY),
                        new UiAnimationKeyframe(durationSeconds, 0f, UiAnimationCurve.SmoothStep)
                    })
            });
    }
}
