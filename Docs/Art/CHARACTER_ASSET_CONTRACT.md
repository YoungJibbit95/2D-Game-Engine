# Character Asset Contract

Stand: 2026-07-11
Status Character v1: `partial`
Status Character v2: `planned`

## Character v1 Compatibility Contract

Der aktive Character-v1-Pfad bleibt kompatibel:

- Hauptsheet: `entities/player/base_actions`.
- Source: `192x32`.
- Zellgroesse: `16x32`.
- Physics Body laut aktuellem Character Content: `12x28`.
- 12 Frames: zwei Idle-, vier Walk-, ein Jump-, ein Fall-, zwei Mine- und zwei Attack-Frames.
- Aktuelle Appearance-Layer: Body, Clothes, Hair und Accessory/Hat.
- Aktuelle Frame-Origin-Metadaten: `(8,16)`; der Renderer wertet sie noch nicht aus.

Dieser Vertrag wird nicht still auf 32x48, 32x56 oder 32x64 migriert. Bestehende Animation IDs, Save-Inhalte und Mods muessen weiterhin laden koennen.

## Visual Bounds und Physics Bounds

- Physics Bounds sind autoritativ fuer Kollision, Reichweite und Positionierung.
- Visual Bounds beschreiben nur die Darstellung und duerfen groesser als der Body sein.
- Der Ground Anchor verbindet beide Vertraege, ohne Sprite-Abmessungen als Body-Abmessungen zu verwenden.
- Haare, Accessoires, Armor und Held Items duerfen die Visual Bounds erweitern, nicht den Physics Body.
- Crouch, Swim oder Climb duerfen eine bewusst definierte Physics-Pose verwenden; sie werden nicht aus transparenten Sprite-Pixeln abgeleitet.

Der aktive Renderer leitet die Player-Darstellung noch direkt aus Source Width/Height ab. Die Trennung ist daher Zielvertrag und Acceptance Gate, nicht bereits verifizierte Capability.

## Character v2 Versionierung

`character_v2` wird als neuer Contract eingefuehrt. Vor der ersten Production-Wave werden festgelegt:

- Frame-Groesse;
- Pixels-per-Unit;
- Visual Bounds;
- Ground Anchor;
- Hand-, Muzzle-, Tool-Impact- und Effekt-Pivots;
- Layer-Zellstruktur;
- Camera-Zoom- und Clipping-Auswirkung;
- Compatibility Mapping von Character v1.

Empfohlene logische IDs verwenden ein ausdrueckliches Versionssegment, zum Beispiel `entities/player/character_v2/base_body`. Die konkrete Frame-Groesse bleibt bis zur Messung `planned`; dieses Dokument waehlt nicht vorzeitig zwischen 32x48, 32x56 und 32x64.

## Layer-Reihenfolge

Der Character-v2-Zielvertrag verwendet folgende geordnete Rollen:

1. Held Item Back
2. Hair Back
3. Back Accessory
4. Base Body
5. Eyes/Face
6. Undershirt
7. Leg Clothing
8. Shoes
9. Torso Clothing
10. Gloves
11. Leg Armor
12. Torso Armor
13. Head Armor
14. Hair Front
15. Front Accessory
16. Held Item Front
17. Effects

Jede Layer-Datei verwendet dieselbe Frame-Zellgroesse, Frame-Anzahl, Pivot-Struktur und Animation-Reihenfolge. Nicht belegte Frames bleiben transparent, statt die Reihenfolge zu verkuerzen.

## Animation Contract

Minimum Production Set fuer Character v2:

- `idle`
- `walk`
- `jump`
- `fall`
- `mine`
- `melee`
- `shoot`
- `cast`
- `hurt`
- `death`

Spaetere Erweiterungen:

- `run`, `jump_start`, `rise`, `land`, `crouch`
- `chop`, `hammer`, `dig`
- `water`, `plant`, `harvest`
- `melee_light`, `melee_heavy`
- `bow_draw`, `bow_release`
- `cast_wand`, `cast_book`
- `consume`, `interact`, `climb`, `swim`

Frame-Dauer und Events liegen in einem expliziten Animation Contract. Der Client darf keine neuen Character-Animationen nur ueber hartcodierte FPS ableiten.

## Pivot Contract

Jeder Frame benoetigt:

- Ground Pivot;
- Physics-to-Visual Offset;
- linke und rechte Handposition oder einen symmetrisch spiegelbaren Hand-Pivot;
- Held-Item-Pivot;
- optionalen Muzzle-/Cast-Origin;
- optionalen Tool-Impact-Pivot;
- optionalen Footstep-Kontaktpunkt.

Flip Horizontal spiegelt alle Pivots um dieselbe Zellachse. Links-/Rechtsvarianten werden nur separat gezeichnet, wenn ein asymmetrisches Design technisch und visuell begruendet ist.

## Character Editor

Wave 02 ueberarbeitet die bereits von `CharacterEditorOverlay` und `PlayingState` genutzten IDs `entities/player/hair_variants_v2`, `entities/player/clothes_variants_v2` und `entities/player/accessories_hats`. Die 8/8/6 Varianten behalten ihre 16x32-Zellen, Frame-Reihenfolge und gemeinsamen Origins; ihre Silhouetten, Materialcluster und Hell-/Dunkel-Lesbarkeit sind in `wave_02_asset_preview.png` bei 1x bis 4x geprueft.

Wave 03 regeneriert zusaetzlich `base_actions` und `body_variants` und fuehrt alle fuenf sichtbaren Layer auf dieselbe klar menschliche 16x32-Figur im Mob-Artstyle zurueck. Kopf, Gesicht, Hals, Torso, Arme, getrennte Beine und Bodenanker bleiben bei nativer Aufloesung lesbar. Die Preview prueft die Einzel-Layer und einen realen Base/Body/Clothes/Hair/Accessory-Komposit bei 1x bis 4x auf hellen und dunklen Feldern. Die vorhandene 12-Frame-Reihenfolge und alle 16x32 Source Rectangles bleiben kompatibel.

- Editor und Runtime verwenden dieselben finalen Layer und Animation Clips.
- Preview zeigt jede Layer-Kombination auf hellem und dunklem Hintergrund.
- Preview prueft mindestens Idle, Walk, Jump, Fall, Mine, Melee und Cast.
- Hair, Clothes und Armor duerfen zwischen Frames nicht springen.
- Randomize oder Palette Tint darf keine nicht erlaubten Farben beziehungsweise Alpha-Werte erzeugen.
- Character-v1-Auswahl bleibt verfuegbar, solange v2 nicht vollstaendig migriert ist.

## Production Wave 04 Character-v1 Extension

Wave 04 fuehrt eine additive, versionierte Character-v1-Familie unter `entities/player/character_v1_wave04/*` ein. Sie ersetzt weder `entities/player/base_actions` noch Character v2 und veraendert keine Save-, Entity- oder Animation-JSONs.

- Fuenf deckungsgleiche Layer: Body, Hair, Clothes, Armor und Equipment.
- Je Layer `256x32`, 16 horizontale `16x32`-Frames und Manifest-Origin `(8,16)`.
- Stabile Reihenfolge: zwei Idle-, sechs Run-, Jump-, Fall-, drei Tool-, Block- und zwei Hurt-Frames.
- Gemeinsamer visueller Bodenanker bei Source-Pixel `(8,31)`; der kompatible Manifest-Origin bleibt separat `(8,16)`.
- Equipment enthaelt nur zellgebundene Tool-, Shield- und Hurt-Feedback-Pixel; kein Frame schreibt in eine Nachbarzelle.
- Assetstatus `verified` fuer PNG, Binaer-Alpha, Manifest, Brief, Promptset, Provenienz, Preview und strikten Audit.
- Runtimeaktivierung `partial`: `ClientTextureRegistry.PreloadAll` kann die IDs laden, aber Clip-/Layer-Auswahl bleibt Aufgabe des Character-/Runtime-Owners.

Der Generated Source ist eine Designreferenz, kein Runtime-Sprite. Der Generator schliesst das Chroma-Feld aus, quantisiert die sichtbaren Farben auf `yjse-pixel-v1` und rekonstruiert die finalen Sheets deterministisch direkt im Zielraster.

## Mod-Kompatibilitaet

- Ein Mod, der Character v1 ersetzt, muss 16x32-Zellen und die stabile v1-Frame-Reihenfolge einhalten.
- Character-v2-Mods deklarieren die exakte Contract-Version.
- Layer eines anderen Contracts duerfen nicht still gemischt werden.
- Fehlende optionale Layer werden transparent uebersprungen; fehlende Base-Layer sind Content-Fehler.
- Runtime und Editor melden Contract-Mismatches sichtbar.

## Acceptance Criteria fuer Character v2 Minimum Set

- Physics und Visual Bounds sind getrennt und getestet.
- Alle Layer besitzen identische Pivots und Frame-Struktur.
- V1-Compatibility Mapping und Save-Load-Smoke bestehen.
- Links/Rechts, mehrere Haare, Kleidung, Armor und mindestens zwei Held Items sind visuell geprueft.
- Editor und Runtime verwenden dieselben Ressourcen.
- Eine Texture Resource wird unabhaengig von der Frame-Anzahl nur einmal geladen.
- Preview Sheet und Runtime Scene decken helle/dunkle Hintergruende sowie 1x bis 4x ab.
- Atlas- und Speicherwirkung sind gemessen; bis dahin bleibt der Status `partial` oder `planned`.
