# Background Asset Contract

Stand: 2026-07-11
Status: `partial`

## Aktueller Bestand

Das Manifest enthaelt zehn Background-Assets mit `512x128` Source-Groesse. Wave 02 ergaenzt `meadow_parallax_layer`, `mushroom_cave_parallax_layer` und `crystal_depths_parallax_layer`; alle drei besitzen bytegleiche linke/rechte Kanten, Brief, Produktionsmetadaten, Provenienz und Mehrskalenvorschau.

Wave 03 regeneriert `forest_parallax_layer`, `cave_parallax_layer` und `meadow_parallax_layer` aus der gemeinsamen Stilpalette und stellt in der Preview jeweils die passenden Grass/Dirt-, Leaves/Trunk- beziehungsweise Stone/Granite-Tile-Referenzen daneben. Linke und rechte Pixelspalte sind bytegleich. Forest und Cave behalten ihre aktiven `ParallaxBackgroundRenderer`-Nutzer; Meadow bleibt bis zur Biome-Auswahl `runtime-preloaded`.

Fuenf IDs besitzen einen direkten Nutzer im aktiven Parallax Renderer:

- `world/backgrounds/forest_parallax_layer`
- `world/backgrounds/night_forest_parallax_layer`
- `world/backgrounds/magical_grove_parallax_layer`
- `world/backgrounds/cave_parallax_layer`
- `world/backgrounds/deep_cave_parallax_layer`

Die IDs `forest_parallax_layer_v2` und `cave_parallax_layer_v2` besitzen keinen aktiven Nutzer und sind byteidentisch zu ihren v1-Gegenstuecken. Sie bleiben `partial` und duerfen nicht als eigenstaendige Production-Varianten gezaehlt werden.

Die drei Wave-02-IDs werden vom Runtime-Manifest preloaded, sind aber ohne Aenderung der zustaendigen Biome-/Parallax-Auswahl noch nicht als aktive Szene verdrahtet. Ihr Szenenstatus bleibt deshalb `partial`.

## Layer Contract pro Szene

Ein vollstaendiges Background Set kann folgende Rollen enthalten:

1. Sky Gradient oder Sky Layer
2. Far Silhouette
3. Mid-Distance Landscape
4. Near Landscape
5. Foreground Atmosphere
6. Cloud/Fog Layer
7. optionaler Weather Layer

Jeder Layer benoetigt:

- stabile Sprite-ID;
- Source-Dimension;
- horizontales und vertikales Tile-Verhalten;
- Parallax Factor;
- World-/Camera-Anchor;
- vertikalen Offset;
- Tint- und Opacity-Regeln;
- Render-Reihenfolge;
- Texture-Gruppe und Speicherbudget;
- Provenienz.

Der aktive Renderer haelt mehrere dieser Werte noch hartcodiert. Der Zielvertrag ist datengetrieben, aber nicht als implementiert zu markieren.

## Perspektive und Lesbarkeit

- Alle Layer derselben Szene verwenden dieselbe Horizont- und Perspektivannahme.
- Keine gemalten Chests, Tore, NPCs, Ores oder anderen Formen, die wie echte Interactables wirken.
- Far Layers sind kontrastarm und detailreduziert.
- Near Layers duerfen mehr Formkontrast besitzen, bleiben aber unter Gameplay-Tiles und Entities.
- Cave Layers vermeiden helle Kanten, die mit Ores oder Interaktionsfeedback konkurrieren.

## Tiling und Seams

- Horizontal wiederholbare Layer muessen pixelgenau nahtlos sein.
- Alpha und Farbe der linken und rechten Kante werden automatisiert verglichen.
- Die Wiederholung wird bei mindestens zwei Camera-Zooms und ueber mehrere Perioden geprueft.
- Ein Layer darf nicht nur durch Cropping scheinbar nahtlos werden.
- Padding beziehungsweise Atlas Edge Extrusion darf keine sichtbare Naht erzeugen.

## Tageszeit, Wetter und Tint

- Farbvarianten werden bevorzugt ueber validierte Tint-/Shader-Regeln erzeugt, wenn Silhouette und Material nicht wechseln.
- Separate Day/Night-PNGs sind erlaubt, wenn die Form oder Lichtkomposition wesentlich abweicht.
- Tint darf keine Banding-, Alpha- oder Kontrastfehler bei Pixel Art erzeugen.
- Regen, Schnee und Fog liegen als getrennte Atmosphere-/Effect-Rollen vor und werden nicht fest in den Gameplay-Hintergrund gemalt.

## Textur- und Speicherbudget

Es existiert noch kein gemessener Background-Speicherbaseline und keine funktionierende Background-Atlasgruppe. Deshalb gelten konkrete Bytebudgets derzeit als `unknown`.

Bis zur Messung:

- keine unkontrollierten 4K-Einzeltexturen;
- bevorzugt wiederholbare Segmente statt riesiger Panoramen;
- jede neue Szene dokumentiert unkomprimierte RGBA-Groesse, Dateigroesse und Ladezeit;
- Backgrounds werden ausserhalb des Draw-Hot-Paths geladen;
- Atlas oder kontrollierte eigenstaendige Texture-Gruppe wird vor einer grossen Wave festgelegt.

## Geplante Sets

- Forest Day
- Forest Sunset
- Forest Night
- Rain Forest
- Surface Snow
- Desert Day
- Desert Night
- Cave
- Deep Cave
- Crystal Cave
- Magical Grove

Die vorhandenen Dateien begruenden nur einen `partial` Forest-/Cave-Samplepfad, nicht die Fertigstellung dieser Liste.

## Acceptance Criteria pro Background Set

- alle benoetigten Layer-Rollen und Parallax Factors sind dokumentiert;
- keine sichtbare horizontale Naht;
- Runtime Preview bei Zoom 0.5, 1, 2, 4 und 6;
- Day/Night- und Cave-Tints behalten Gameplay-Kontrast;
- keine gemalten Interactables;
- kein synchrones Laden in `Draw`;
- Texture-Wechsel, Draw Calls, Speicher und Ladezeit sind gemessen;
- aktive Runtime-Szene und Screenshot-Smoke vorhanden;
- Atlas-/Texture-Gruppenstatus und Provenienz sind ehrlich dokumentiert.

## Production Wave 04 Additive Parallax Set

Wave 04 ergaenzt fuenf neue `512x128`-IDs unter `world/backgrounds/wave04/*`: Meadow, Forest, Cave, Mushroom Cave und Crystal Depths. Die bestehenden Wave-02-/Wave-03-IDs bleiben unveraendert gueltig, damit aktuelle Biome-Presentation-JSONs und der `ContentReferenceValidator` keinen Aliaswechsel benoetigen.

- alle fuenf Quellen verwenden ausschliesslich `yjse-pixel-v1`-Farben;
- Tile-Palettenreferenzen sind pro Asset in `wave_04_provenance.json` festgehalten;
- linke und rechte Pixelspalte sind fuer alle fuenf Quellen bytegleich;
- die Preview zeigt Native Period, 64-Pixel-Wrap-Vorlauf, Seam-Markierung, Tile-Referenzen und Integer-Zooms;
- Wetter bleibt in getrennten Effect-Sheets und ist nicht in die Backgrounds eingebacken;
- Assetstatus `verified`, aktive Szenenauswahl `partial` und `runtime-preloaded`.
