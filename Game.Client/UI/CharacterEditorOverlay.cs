using Game.Client.Input;
using Game.Client.Rendering;
using Game.Core.Animations;
using Game.Core.Characters;
using Game.Core.Data;
using Game.Core.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.UI;

public sealed class CharacterEditorOverlay
{
    private static readonly string[] Tabs = ["BODY", "OUTFIT", "COLORS", "MOTION"];

    private readonly SpriteAnimator _previewAnimator = new();
    private readonly List<EditorRow> _rows = new();
    private readonly List<HitZone> _hitZones = new();
    private readonly List<HitZone> _tabZones = new();
    private readonly List<ActionHitZone> _actionZones = new();
    private CharacterEditorSession? _session;
    private CharacterAppearance _defaultAppearance = new();
    private int _rowIndex;
    private int _tabIndex;
    private CharacterAnimationState _previewState = CharacterAnimationState.Idle;
    private string _status = "CHARACTER EDITOR";

    public bool IsOpen { get; private set; }

    public CharacterAppearance Appearance => _session?.Appearance ?? new CharacterAppearance();

    public void SetAppearance(GameContentDatabase content, CharacterAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(appearance);

        _defaultAppearance = content.Characters.TryGetById("player", out var character)
            ? character.DefaultAppearance
            : new CharacterAppearance();
        _session = new CharacterEditorSession(appearance, CharacterEditorOptionSet.CreateDefault());
        _previewAnimator.Stop();
        _status = "APPEARANCE LOADED";
    }

    public void Open(GameContentDatabase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureSession(content);
        IsOpen = true;
        _status = "CHARACTER EDITOR";
    }

    public void Close()
    {
        IsOpen = false;
    }

    public bool Update(InputManager input, GameContentDatabase content, GameSettings settings, double deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(settings);

        if (input.IsBindingPressed(settings.Input.KeyBindings.OpenCharacterEditor) || input.IsKeyPressed(Keys.F2))
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open(content);
            }

            return true;
        }

        if (!IsOpen)
        {
            return false;
        }

        EnsureSession(content);
        RebuildRows(content);
        if (input.IsKeyPressed(Keys.Escape))
        {
            Close();
            return true;
        }

        var mouseConsumed = UpdateMouse(input);
        if (input.IsKeyPressed(Keys.Down) || input.IsKeyPressed(Keys.S))
        {
            _rowIndex = (_rowIndex + 1) % _rows.Count;
        }
        else if (input.IsKeyPressed(Keys.Up) || input.IsKeyPressed(Keys.W))
        {
            _rowIndex = ((_rowIndex - 1) % _rows.Count + _rows.Count) % _rows.Count;
        }

        if (!mouseConsumed && (input.IsKeyPressed(Keys.Left) || input.IsKeyPressed(Keys.A) || input.IsRightMousePressed))
        {
            _rows[_rowIndex].Change(-1);
        }
        else if (!mouseConsumed && (input.IsKeyPressed(Keys.Right) || input.IsKeyPressed(Keys.D) || input.IsLeftMousePressed))
        {
            _rows[_rowIndex].Change(1);
        }

        UpdatePreviewAnimation(content, (float)deltaSeconds);
        return true;
    }

    public void Draw(RenderContext context, GameContentDatabase content, ClientTextureRegistry? textures, GameSettings settings)
    {
        if (!IsOpen)
        {
            return;
        }

        EnsureSession(content);
        RebuildRows(content);

        var palette = UiTheme.Resolve(settings);
        UiTheme.DrawBackdrop(context, palette, settings.Ui.MenuBackdropOpacity * 0.86f, settings);

        var panelWidth = Math.Min(840, context.ViewportBounds.Width - 56);
        var panelHeight = Math.Min(500, context.ViewportBounds.Height - 56);
        var panel = new Rectangle(
            context.ViewportBounds.Width / 2 - panelWidth / 2,
            context.ViewportBounds.Height / 2 - panelHeight / 2,
            panelWidth,
            panelHeight);

        UiTheme.DrawPanel(context, panel, palette, settings.Ui.PanelOpacity);
        UiTheme.DrawHeader(context, new Rectangle(panel.X + 1, panel.Y + 1, panel.Width - 2, 66), palette, settings: settings);
        context.DebugText.Draw(new Vector2(panel.X + 22, panel.Y + 18), "CHARACTER", palette.Accent, 3);
        context.DebugText.Draw(new Vector2(panel.X + 24, panel.Y + 50), "F2 ESC CLOSE   TAB/CLICK SECTION   UP/DOWN SELECT   LEFT/RIGHT CHANGE", palette.TextMuted, 1);

        var tabBounds = new Rectangle(panel.X + 24, panel.Y + 76, panel.Width - 48, 30);
        DrawTabs(context, palette, tabBounds);

        var previewBounds = new Rectangle(panel.X + 26, panel.Y + 122, 286, panel.Height - 168);
        var editorBounds = new Rectangle(previewBounds.Right + 22, previewBounds.Y, panel.Right - previewBounds.Right - 48, previewBounds.Height);
        DrawPreview(context, content, textures, palette, previewBounds);
        DrawRows(context, palette, editorBounds);
        DrawActions(context, palette, new Rectangle(editorBounds.X, editorBounds.Bottom - 44, editorBounds.Width, 32));

        context.DebugText.Draw(new Vector2(panel.X + 24, panel.Bottom - 28), _status, palette.Warning, 1);
    }

    private void EnsureSession(GameContentDatabase content)
    {
        if (_session is not null)
        {
            return;
        }

        var appearance = content.Characters.TryGetById("player", out var character)
            ? character.DefaultAppearance
            : new CharacterAppearance();
        _defaultAppearance = appearance;
        _session = new CharacterEditorSession(appearance, CharacterEditorOptionSet.CreateDefault());
    }

    private void RebuildRows(GameContentDatabase content)
    {
        if (_session is null)
        {
            return;
        }

        _rows.Clear();
        switch (Tabs[Math.Clamp(_tabIndex, 0, Tabs.Length - 1)])
        {
            case "BODY":
                _rows.Add(new EditorRow("SKIN", _session.Appearance.SkinTone, direction => Cycle(_session.Options.SkinTones, _session.Appearance.SkinTone, direction, _session.SetSkinTone)));
                _rows.Add(new EditorRow("HAIR STYLE", _session.Appearance.HairStyleId, direction => Cycle(_session.Options.HairStyles, _session.Appearance.HairStyleId, direction, _session.SetHairStyle)));
                _rows.Add(new EditorRow("ACCESSORY", _session.Appearance.AccessoryId, direction => Cycle(_session.Options.Accessories, _session.Appearance.AccessoryId, direction, _session.SetAccessory)));
                break;
            case "OUTFIT":
                _rows.Add(new EditorRow("CLOTHES", _session.Appearance.ClothesStyleId, direction => Cycle(_session.Options.ClothesStyles, _session.Appearance.ClothesStyleId, direction, _session.SetClothesStyle)));
                _rows.Add(new EditorRow("ACCESSORY", _session.Appearance.AccessoryId, direction => Cycle(_session.Options.Accessories, _session.Appearance.AccessoryId, direction, _session.SetAccessory)));
                _rows.Add(new EditorRow("ANIMATION", _previewState.ToString().ToUpperInvariant(), CyclePreviewState));
                break;
            case "COLORS":
                _rows.Add(new EditorRow("HAIR COLOR", _session.Appearance.HairColor, direction => Cycle(_session.Options.HairColors, _session.Appearance.HairColor, direction, _session.SetHairColor)));
                _rows.Add(new EditorRow("SHIRT", _session.Appearance.ShirtColor, direction => Cycle(_session.Options.ShirtColors, _session.Appearance.ShirtColor, direction, _session.SetShirtColor)));
                _rows.Add(new EditorRow("PANTS", _session.Appearance.PantsColor, direction => Cycle(_session.Options.PantsColors, _session.Appearance.PantsColor, direction, _session.SetPantsColor)));
                _rows.Add(new EditorRow("SKIN", _session.Appearance.SkinTone, direction => Cycle(_session.Options.SkinTones, _session.Appearance.SkinTone, direction, _session.SetSkinTone)));
                break;
            default:
                _rows.Add(new EditorRow("ANIMATION", _previewState.ToString().ToUpperInvariant(), CyclePreviewState));
                _rows.Add(new EditorRow("HAIR STYLE", _session.Appearance.HairStyleId, direction => Cycle(_session.Options.HairStyles, _session.Appearance.HairStyleId, direction, _session.SetHairStyle)));
                _rows.Add(new EditorRow("CLOTHES", _session.Appearance.ClothesStyleId, direction => Cycle(_session.Options.ClothesStyles, _session.Appearance.ClothesStyleId, direction, _session.SetClothesStyle)));
                break;
        }
        _rowIndex = Math.Clamp(_rowIndex, 0, _rows.Count - 1);
    }

    private void DrawTabs(RenderContext context, UiPalette palette, Rectangle bounds)
    {
        _tabZones.Clear();
        var gap = 6;
        var tabWidth = Math.Max(86, (bounds.Width - gap * (Tabs.Length - 1)) / Tabs.Length);
        for (var index = 0; index < Tabs.Length; index++)
        {
            var tab = new Rectangle(bounds.X + index * (tabWidth + gap), bounds.Y, tabWidth, bounds.Height);
            var selected = index == _tabIndex;
            UiTheme.DrawButton(context, tab, palette, selected, tab.Contains(Mouse.GetState().Position));
            context.DebugText.Draw(new Vector2(tab.X + 12, tab.Y + 10), Tabs[index], selected ? palette.Text : palette.TextMuted, 1);
            _tabZones.Add(new HitZone(tab, index));
        }
    }

    private void DrawPreview(RenderContext context, GameContentDatabase content, ClientTextureRegistry? textures, UiPalette palette, Rectangle bounds)
    {
        UiTheme.DrawPanel(context, bounds, palette, 0.68f, raised: false);
        context.DebugText.Draw(new Vector2(bounds.X + 14, bounds.Y + 12), "PREVIEW", palette.Text, 2);

        if (_previewAnimator.CurrentFrame is not { } frame)
        {
            UpdatePreviewAnimation(content, 0.001f);
        }

        var previewFrame = _previewAnimator.CurrentFrame;
        if (previewFrame is null || textures is null || _session is null)
        {
            return;
        }

        var sprite = textures.Get(previewFrame.SpriteId, previewFrame.FrameIndex);
        var scale = 4;
        var destination = new Rectangle(
            bounds.X + bounds.Width / 2 - sprite.SourceRectangle.Width * scale / 2,
            bounds.Y + 108,
            sprite.SourceRectangle.Width * scale,
            sprite.SourceRectangle.Height * scale);

        context.SpriteBatch.Draw(context.Pixel, new Rectangle(destination.X - 18, destination.Bottom - 10, destination.Width + 36, 8), UiTheme.WithAlpha(Color.Black, 0.28f));
        context.SpriteBatch.Draw(sprite.Texture, destination, sprite.SourceRectangle, Color.White);
        DrawPart(context, textures, "entities/player/body_variants", destination, ResolveFrameIndex(_session.Options.SkinTones, _session.Appearance.SkinTone), ParseHex(_session.Appearance.SkinTone));
        DrawPart(context, textures, "entities/player/clothes_variants_v2", destination, ResolveFrameIndex(_session.Options.ClothesStyles, _session.Appearance.ClothesStyleId), ParseHex(_session.Appearance.ShirtColor));
        DrawPart(context, textures, "entities/player/hair_variants_v2", destination, ResolveFrameIndex(_session.Options.HairStyles, _session.Appearance.HairStyleId), ParseHex(_session.Appearance.HairColor));
        if (!string.Equals(_session.Appearance.AccessoryId, "none", StringComparison.OrdinalIgnoreCase))
        {
            DrawPart(context, textures, "entities/player/accessories_hats", destination, Math.Max(0, ResolveFrameIndex(_session.Options.Accessories, _session.Appearance.AccessoryId) - 1), Color.White);
        }

        var swatchY = bounds.Bottom - 92;
        DrawSwatch(context, new Rectangle(bounds.X + 20, swatchY, 28, 28), ParseHex(_session.Appearance.SkinTone), palette);
        DrawSwatch(context, new Rectangle(bounds.X + 58, swatchY, 28, 28), ParseHex(_session.Appearance.HairColor), palette);
        DrawSwatch(context, new Rectangle(bounds.X + 96, swatchY, 28, 28), ParseHex(_session.Appearance.ShirtColor), palette);
        DrawSwatch(context, new Rectangle(bounds.X + 134, swatchY, 28, 28), ParseHex(_session.Appearance.PantsColor), palette);
        context.DebugText.Draw(new Vector2(bounds.X + 20, swatchY + 38), _session.Appearance.HairStyleId.ToUpperInvariant(), palette.TextMuted, 1);
        context.DebugText.Draw(new Vector2(bounds.X + 20, bounds.Bottom - 30), $"{_previewState.ToString().ToUpperInvariant()}  {_session.Appearance.ClothesStyleId.ToUpperInvariant()}", palette.Warning, 1);
    }

    private static void DrawPart(RenderContext context, ClientTextureRegistry textures, string spriteId, Rectangle destination, int frameIndex, Color color)
    {
        var part = textures.Get(spriteId, Math.Max(0, frameIndex));
        if (part.IsPlaceholder)
        {
            return;
        }

        context.SpriteBatch.Draw(part.Texture, destination, part.SourceRectangle, color);
    }

    private void DrawRows(RenderContext context, UiPalette palette, Rectangle bounds)
    {
        UiTheme.DrawPanel(context, bounds, palette, 0.68f, raised: false);
        _hitZones.Clear();
        for (var index = 0; index < _rows.Count; index++)
        {
            var row = _rows[index];
            var rowBounds = new Rectangle(bounds.X + 12, bounds.Y + 14 + index * 42, bounds.Width - 24, 34);
            var selected = index == _rowIndex;
            UiTheme.DrawButton(context, rowBounds, palette, selected, false);
            context.DebugText.Draw(new Vector2(rowBounds.X + 12, rowBounds.Y + 10), row.Label, selected ? palette.Text : palette.TextMuted, 1);
            context.DebugText.Draw(new Vector2(rowBounds.Right - 180, rowBounds.Y + 10), row.Value, selected ? palette.Warning : palette.TextMuted, 1);
            _hitZones.Add(new HitZone(rowBounds, index));
        }
    }

    private void DrawActions(RenderContext context, UiPalette palette, Rectangle bounds)
    {
        _actionZones.Clear();
        var random = new Rectangle(bounds.X, bounds.Y, 112, bounds.Height);
        var reset = new Rectangle(random.Right + 10, bounds.Y, 112, bounds.Height);
        UiTheme.DrawButton(context, random, palette, selected: false, hovered: random.Contains(Mouse.GetState().Position));
        UiTheme.DrawButton(context, reset, palette, selected: false, hovered: reset.Contains(Mouse.GetState().Position));
        context.DebugText.Draw(new Vector2(random.X + 18, random.Y + 10), "RANDOM", palette.Text, 1);
        context.DebugText.Draw(new Vector2(reset.X + 25, reset.Y + 10), "RESET", palette.Text, 1);
        _actionZones.Add(new ActionHitZone(random, "random"));
        _actionZones.Add(new ActionHitZone(reset, "reset"));
    }

    private void UpdatePreviewAnimation(GameContentDatabase content, float deltaSeconds)
    {
        var clipId = content.Characters.TryGetById("player", out var character)
            ? character.AnimationSet.ResolveClipId(_previewState)
            : "player.idle";
        if (!string.IsNullOrWhiteSpace(clipId) &&
            content.Animations.TryGetById(clipId, out var clip) &&
            !string.Equals(_previewAnimator.Clip?.Id, clip.Id, StringComparison.OrdinalIgnoreCase))
        {
            _previewAnimator.Play(clip, restartIfSame: true);
        }

        _previewAnimator.Update(Math.Max(0.001f, deltaSeconds));
    }

    private bool UpdateMouse(InputManager input)
    {
        for (var index = 0; index < _tabZones.Count; index++)
        {
            if (_tabZones[index].Bounds.Contains(input.MousePosition) && input.IsLeftMousePressed)
            {
                _tabIndex = _tabZones[index].Index;
                _rowIndex = 0;
                _status = Tabs[_tabIndex];
                return true;
            }
        }

        for (var index = 0; index < _actionZones.Count; index++)
        {
            if (!_actionZones[index].Bounds.Contains(input.MousePosition) || !input.IsLeftMousePressed)
            {
                continue;
            }

            if (_actionZones[index].ActionId == "random")
            {
                RandomizeAppearance();
            }
            else
            {
                ResetAppearance();
            }

            return true;
        }

        for (var index = 0; index < _hitZones.Count; index++)
        {
            if (_hitZones[index].Bounds.Contains(input.MousePosition))
            {
                _rowIndex = _hitZones[index].Index;
                return false;
            }
        }

        return false;
    }

    private void RandomizeAppearance()
    {
        if (_session is null)
        {
            return;
        }

        Pick(_session.Options.SkinTones, _session.SetSkinTone);
        Pick(_session.Options.HairStyles, _session.SetHairStyle);
        Pick(_session.Options.ClothesStyles, _session.SetClothesStyle);
        Pick(_session.Options.Accessories, _session.SetAccessory);
        Pick(_session.Options.HairColors, _session.SetHairColor);
        Pick(_session.Options.ShirtColors, _session.SetShirtColor);
        Pick(_session.Options.PantsColors, _session.SetPantsColor);
        _status = "RANDOMIZED";
    }

    private void ResetAppearance()
    {
        if (_session is null)
        {
            return;
        }

        _session = new CharacterEditorSession(_defaultAppearance, _session.Options);
        _previewState = CharacterAnimationState.Idle;
        _previewAnimator.Stop();
        _status = "RESET";
    }

    private static void Pick(IReadOnlyList<string> options, Func<string, bool> set)
    {
        if (options.Count == 0)
        {
            return;
        }

        set(options[Random.Shared.Next(options.Count)]);
    }

    private void CyclePreviewState(int direction)
    {
        var states = new[]
        {
            CharacterAnimationState.Idle,
            CharacterAnimationState.Walk,
            CharacterAnimationState.Jump,
            CharacterAnimationState.Fall,
            CharacterAnimationState.Mine,
            CharacterAnimationState.Attack
        };
        var current = Array.IndexOf(states, _previewState);
        if (current < 0)
        {
            current = 0;
        }

        var next = ((current + Math.Sign(direction == 0 ? 1 : direction)) % states.Length + states.Length) % states.Length;
        _previewState = states[next];
        _previewAnimator.Stop();
        _status = $"ANIMATION {_previewState}";
    }

    private void Cycle(IReadOnlyList<string> options, string current, int direction, Func<string, bool> set)
    {
        if (options.Count == 0)
        {
            return;
        }

        var index = IndexOf(options, current);
        if (index < 0)
        {
            index = 0;
        }

        var next = ((index + Math.Sign(direction == 0 ? 1 : direction)) % options.Count + options.Count) % options.Count;
        if (set(options[next]))
        {
            _status = $"{_rows[_rowIndex].Label} {options[next]}";
        }
    }

    private static int ResolveFrameIndex(IReadOnlyList<string> options, string value)
    {
        var index = IndexOf(options, value);
        return index < 0 ? 0 : index;
    }

    private static int IndexOf(IReadOnlyList<string> values, string value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static void DrawSwatch(RenderContext context, Rectangle bounds, Color color, UiPalette palette)
    {
        context.SpriteBatch.Draw(context.Pixel, bounds, color);
        UiTheme.DrawBorder(context, bounds, UiTheme.WithAlpha(palette.Text, 0.82f), 1);
    }

    private static Color ParseHex(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#')
        {
            return Color.White;
        }

        return byte.TryParse(value.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var r) &&
               byte.TryParse(value.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) &&
               byte.TryParse(value.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var b)
            ? new Color(r, g, b)
            : Color.White;
    }

    private sealed record EditorRow(string Label, string Value, Action<int> Change);

    private readonly record struct HitZone(Rectangle Bounds, int Index);

    private readonly record struct ActionHitZone(Rectangle Bounds, string ActionId);
}
