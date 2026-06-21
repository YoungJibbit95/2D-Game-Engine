namespace Game.Core.Characters;

public sealed class CharacterEditorSession
{
    public CharacterEditorSession(CharacterAppearance appearance, CharacterEditorOptionSet? options = null)
    {
        Appearance = appearance;
        Options = options ?? CharacterEditorOptionSet.CreateDefault();
    }

    public CharacterAppearance Appearance { get; private set; }

    public CharacterEditorOptionSet Options { get; }

    public bool SetSkinTone(string value) => SetIfAllowed(Options.SkinTones, value, appearance => Appearance = appearance with { SkinTone = value });

    public bool SetHairStyle(string value) => SetIfAllowed(Options.HairStyles, value, appearance => Appearance = appearance with { HairStyleId = value });

    public bool SetHairColor(string value) => SetIfAllowed(Options.HairColors, value, appearance => Appearance = appearance with { HairColor = value });

    public bool SetShirtColor(string value) => SetIfAllowed(Options.ShirtColors, value, appearance => Appearance = appearance with { ShirtColor = value });

    public bool SetPantsColor(string value) => SetIfAllowed(Options.PantsColors, value, appearance => Appearance = appearance with { PantsColor = value });

    private bool SetIfAllowed(IReadOnlyList<string> allowedValues, string value, Action<CharacterAppearance> apply)
    {
        if (!allowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        apply(Appearance);
        return true;
    }
}
