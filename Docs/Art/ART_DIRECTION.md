# YjsE Art Direction

Stand: 2026-07-14
Status: `partial`
Geltungsbereich: `Game.Data`-Sprites und alle Runtime-Renderer, die diese Sprites darstellen

## Zweck und Verbindlichkeit

Dieses Dokument ist der menschlich lesbare Art Contract fuer YjsE. Die maschinenlesbare Stilquelle ist derzeit `Game.Data/art_direction/yjse_pixel_style.json`. Manifest, Generation Brief, PNG und Runtime-Nutzer muessen denselben Vertrag abbilden. Bei Widerspruechen gilt keine stillschweigende Interpretation: Der Widerspruch muss vor einer groesseren Asset-Wave aufgeloest werden.

Der aktuelle Stil traegt die Kennung `yjse-pixel-v1`. Er ersetzt keine bestehenden Runtime-Vertraege still. Insbesondere bleiben 16x16-Tiles und der Character-v1-Vertrag kompatibel, bis eine versionierte Migration validiert ist.

## Gemessener Ist-Stand

- 180 Sprite-Manifest-Eintraege, 180 eindeutige Generation Briefs und 174 eindeutige PNG-Quellen.
- Aktueller statischer Recheck: keine Pfad-, Brief- oder PNG-Dimensionsabweichung.
- 33 als `autotile` markierte Sheets besitzen vollstaendige Masken `0..15`.
- Production-Samples bis einschliesslich Wave 04 besitzen `atlasId`, Origin, Render-Layer, Lizenz und assetgenaue Provenienz. Ein tatsaechlicher Atlas-Renderer bleibt davon unberuehrt `partial`.
- Alle Manifestquellen liegen im Runtime-Preload-Scope. Character-v2-Layer und die zwei Item-Icons besitzen aktive Nutzer; neue Creatures, Backgrounds und reine Furniture-Quellen sind ohne Aenderung fremder Entity-/Biome-/Item-Daten noch nicht szenenselektiert.
- Die aktuelle neun Assets umfassende Sample-Wave ist formal auf 32x32 fuer Items/UI und 64x32 fuer die Workbench-Weltgrafik abgeglichen. Sie bleibt `partial`, weil World-Skalierung, Footprint, Pivots, Atlas, Provenienz und zwei UI-Nutzer fehlen.
- Die vorhandene Contact Sheet ist eine Arbeitsvorschau. Sie ersetzt keine helle/dunkle, Zoom-, Flip- oder Runtime-Preview-Matrix.

## Visuelle Leitlinien

### Pixel und Kanten

- Harte Pixelkanten; kein Anti-Aliasing, Blur oder Subpixel-Gradient.
- Nearest-Neighbor fuer jede Skalierung und jeden Export.
- Transparente Sprites verwenden binaeres Alpha: nur `0` oder `255`.
- Standalone-Sprites besitzen transparente Eckpixel und einen zusammenhaengenden dunklen 1-Pixel-Aussenumriss.
- Innere Trennlinien verwenden die Outline-Farbe sparsam und duerfen die Silhouette nicht zerlegen.

### Licht und Material

- Gemeinsame Lichtquelle: oben links.
- Materialien verwenden normalerweise drei bis fuenf klar getrennte Tonwerte.
- Metall: dunkler Koerper, eine Mittelflaeche, warme Kantenaufhellung, optional ein einzelner heller Glanzcluster.
- Holz: breite Farbcluster und hoechstens wenige kurze Maserungsmarken.
- Glas: stilisierte deckende Kontur plus klar abgegrenzte Fluessigkeitsmasse; keine weiche Transparenz am kleinen Sprite-Massstab.
- Magie: heller Kern innerhalb einer dunklen Kontur; kein weiches Glow ausserhalb der Alpha-Silhouette.

### Lesbarkeit und Kontrast

- Gameplay-Silhouetten muessen in nativer Pixelgroesse unterscheidbar sein.
- Benachbarte Progressionsstufen unterscheiden sich zuerst ueber Form, danach ueber Farbe.
- Interaktive Objekte duerfen nicht mit Hintergrunddetails verwechselt werden.
- Hintergruende verwenden weniger lokalen Kontrast als Terrain, Entities und Interaktionsobjekte.
- Effekte bleiben kurz, richtungslesbar und verdecken keine wichtigen Treffer- oder Platzierungsinformationen.

## Palette

Die verbindlichen Gruppen stehen in `yjse_pixel_style.json`: Outline, Neutral, Wood, Copper, Health, Mana, Spark, UI Accent sowie die Wave-02-Erweiterungen Foliage, Cave Earth, Mushroom, Crystal Depths und Character Cloth. Bestehende Farben werden nicht rueckwirkend umgedeutet.

Ein Asset darf zusaetzliche Farben erst verwenden, wenn:

1. die Material- oder Biome-Anforderung dokumentiert ist;
2. die neue Palette in der maschinenlesbaren Stilquelle steht;
3. verwandte Assets gemeinsam migriert werden;
4. der Sprite Audit die neue Palette kennt.

## Render-Kategorien und Layer

`SpriteAssetCategory` beschreibt derzeit nur die grobe Asset-Art und ist kein vollstaendiger Layer-Vertrag. Jede Production-Wave muss zusaetzlich eine Render-Kategorie und eine geordnete Layer-Rolle dokumentieren.

Vorgesehene Render-Kategorien:

1. `background.sky`
2. `background.far`
3. `background.mid`
4. `background.near`
5. `world.wall`
6. `world.tile`
7. `world.decor.back`
8. `world.entity`
9. `world.held.back`
10. `world.held.front`
11. `world.decor.front`
12. `world.effect`
13. `ui`

Diese Namen sind Vertragsziele, keine Behauptung ueber bereits implementierte Manifestfelder oder Renderer-Layer.

## Naming Contract

- Logische IDs verwenden kleingeschriebene, durch `/` gegliederte Namen und `snake_case` pro Segment.
- Dateinamen spiegeln die stabile ID-Familie wider und unterscheiden keine Version nur ueber Gross-/Kleinschreibung.
- Versionierte inkompatible Assets erhalten ein ausdrueckliches Segment oder Suffix wie `character_v2`; bestehende IDs werden nicht still ueberschrieben.
- `_autotile` bezeichnet ein Sheet mit dokumentierter Maskenreihenfolge.
- Rollen werden eindeutig benannt, zum Beispiel `items/...`, `world/objects/...`, `projectiles/...` oder `ui/...`. Ein Inventory Icon ist nicht automatisch ein Held-, Drop- oder Projectile-Sprite.
- Linux-Case-Sensitivity ist verbindlich; Manifestpfad und Dateisystempfad muessen bytegenau in der Schreibweise uebereinstimmen.

## Provenienz und Lizenz

Jedes neue oder ersetzte Production Asset benoetigt mindestens:

- Ersteller oder Generator;
- Erstellungsdatum und Wave-ID;
- Quelle beziehungsweise Prompt-/Artist-Spec-ID;
- Lizenz und erlaubte Nutzungsarten;
- Hinweis auf verwendete Referenzen;
- Transformationsschritte;
- Content-Hash des finalen Outputs.

Der aktuelle Manifest- und Brief-Typ besitzt dafuer noch keinen vollstaendigen Vertrag. Bis die Schema-Erweiterung existiert, wird die Provenienz pro Asset im `ASSET_WAVE_LEDGER.md` gefuehrt. `Built-in image_gen` in der globalen Stilbeschreibung ist keine ausreichende assetgenaue Provenienz.

Es sind keine Logos, Watermarks oder kopierten Terraria-, Stardew- oder Zelda-Assets erlaubt.

## Mod-Kompatibilitaet

- Mods duerfen stabile Sprite-IDs ueber den bestehenden Content-Pack-Mergepfad ersetzen.
- Ein Override muss Dimension, Frame-Struktur, Pivot, Pixels-per-Unit und Rolle des ersetzten Vertrags einhalten oder eine neue versionierte ID verwenden.
- Base- und Mod-Assets teilen denselben Audit- und Case-Sensitivity-Vertrag.
- Atlas-Zuordnung darf keine logische ID in eine atlasabhaengige Gameplay-Referenz verwandeln.
- Fehlende Mod-Assets muessen ueber denselben Placeholder-Handle-Vertrag wie Base-Assets laufen; Ownership und Dispose duerfen nicht vom Pack-Ursprung abhaengen.

## Definition of Done fuer ein Production Asset

Ein Asset ist erst `verified`, wenn alle zutreffenden Punkte erfuellt sind:

- stabile ID, Manifest und Brief;
- exakter Pfad und exakte Dimension;
- Pivot/Origin, Pixels-per-Unit, Render-Kategorie und Layer;
- Atlas-Gruppe als Metadatum und ein tatsaechlich validierter Atlas-Pfad, sobald Atlas-Unterstuetzung gefordert wird;
- assetgenaue Provenienz und Lizenz;
- aktiver Runtime-Nutzer;
- Content-Reference- und Reverse-Reference-Validation;
- automatischer Sprite Audit ohne Fehler;
- Preview bei 1x, 2x, 3x und 4x, auf hellem und dunklem Hintergrund;
- Runtime-Smoke im relevanten Renderer;
- gemessene Texture-, Ladezeit- und Batch-Auswirkung;
- keine stillen Dimensions-, Footprint- oder Bounds-Aenderungen.

## Aktuelles Gate

Production Wave 04 ist fuer PNG, Manifest, Brief, Provenienz, Promptset, Preview, Binaer-Alpha und strikten Audit `verified`. Ihre 31 neuen IDs bleiben ehrlich `runtime-preloaded`: bestehende Wave-03-Biome-IDs bleiben gueltig und werden durch die neue Wave weder ersetzt noch umgeleitet. Das naechste Gate ist eine Runtime-Owner-Entscheidung, welche Wave-04-IDs in Character-, Biome-, Effect- und UI-Presentation aktiviert werden; bis dahin bleibt die sichtbare Szenenaktivierung `partial`.
