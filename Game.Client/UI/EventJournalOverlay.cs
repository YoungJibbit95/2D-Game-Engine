using Game.Client.Rendering;
using Game.Core.Events;
using Game.Core.Settings;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public sealed class EventJournalOverlay
{
    private const int MaximumVisibleEvents = 9;

    public void Draw(RenderContext context, GameEventJournal? journal, GameSettings settings)
    {
        if (!settings.Debug.ShowEventJournal || journal is null)
        {
            return;
        }

        var events = journal.Events;
        var visibleCount = Math.Min(MaximumVisibleEvents, events.Count);
        var palette = UiTheme.Resolve(settings);
        var width = Math.Min(390, context.ViewportBounds.Width - 24);
        var height = 48 + Math.Max(1, visibleCount) * 18;
        var bounds = new Rectangle(
            context.ViewportBounds.Right - width - 12,
            context.ViewportBounds.Bottom - height - 12,
            width,
            height);

        UiTheme.DrawPanel(context, bounds, palette, settings.Ui.PanelOpacity * 0.96f);
        context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 9), "EVENT JOURNAL", palette.Accent, 2);
        context.DebugText.Draw(new Vector2(bounds.Right - 84, bounds.Y + 11), journal.Count.ToString(), palette.TextMuted, 1);

        if (visibleCount == 0)
        {
            context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 35), "NO EVENTS", palette.TextMuted, 1);
            return;
        }

        var first = events.Count - visibleCount;
        for (var index = first; index < events.Count; index++)
        {
            var recorded = events[index];
            var y = bounds.Y + 35 + (index - first) * 18;
            var eventName = Trim(RemoveEventSuffix(recorded.Event.GetType().Name), 27);
            context.DebugText.Draw(new Vector2(bounds.X + 10, y), $"#{recorded.Sequence}", palette.TextMuted, 1);
            context.DebugText.Draw(new Vector2(bounds.X + 78, y), eventName, palette.Text, 1);
        }
    }

    private static string RemoveEventSuffix(string name)
    {
        return name.EndsWith("Event", StringComparison.Ordinal) ? name[..^5] : name;
    }

    private static string Trim(string text, int maximumLength)
    {
        return text.Length <= maximumLength ? text : text[..(maximumLength - 1)] + ">";
    }
}
