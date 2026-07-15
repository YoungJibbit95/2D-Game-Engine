# Wave 04 Production Report

Stand: 2026-07-14
Wave-ID: `wave_04_character_atmosphere_ui_production`
Asset-Scope: `verified`
Aktive Runtime-/Szenenauswahl: `partial`

## Produktionsumfang

Production Wave 04 liefert 31 additive, eindeutige PNG-Quellen. Sie ersetzt keine Wave-03-ID und veraendert keine C#-, Biome-, Entity-, Item- oder Website-Datei.

| Familie | Quellen | Source Contract | Frames |
| --- | ---: | --- | ---: |
| Character Layer | 5 | je `256x32`, Zelle `16x32` | 80 |
| Parallax | 5 | je `512x128`, horizontal nahtlos | 5 |
| Ambient Effects | 5 | je `96x16`, Zelle `16x16` | 30 |
| Weather Effects | 4 | je `96x16`, Zelle `16x16` | 24 |
| Combat Effects | 4 | je `192x32`, Zelle `32x32` | 24 |
| UI Nine-Slice | 2 | je `48x48`, 3x3-Zellen `16x16` | 18 |
| UI Action Icons | 6 | je `32x32` | 6 |
| **Gesamt** | **31** | | **187** |

## Character Contract

Die neue Familie `entities/player/character_v1_wave04/*` enthaelt Body, Hair, Clothes, Armor und Equipment. Alle Layer teilen dieselben 16 Frames, Source Rectangles und Origins:

1. `idle_0`, `idle_1`
2. `run_0` bis `run_5`
3. `jump`, `fall`
4. `tool_0` bis `tool_2`
5. `block`
6. `hurt_0`, `hurt_1`

Der Manifest-Origin bleibt kompatibel `(8,16)`, der dokumentierte visuelle Bodenanker liegt bei `(8,31)`. Die IDs sind versioniert und ersetzen den aktiven 12-Frame-Character-v1-Pfad nicht. Character v2 bleibt unberuehrt `planned`.

## Generated Source Und Aufbereitung

Der Generated Source wurde mit dem `imagegen`-Skill ueber den eingebauten `image_gen`-Pfad erstellt. Als Referenzen dienten die Wave-03-Preview, `player_base_actions.png` und `player_clothes_variants_v2.png`.

- Source: `Game.Data/art_direction/generated_sources/wave_04_character_source.png`
- Source-Dimension: `1983x793`
- Source-SHA-256: `DA0534FD0E76F9172BB57D880080FD4B7095D3D3DD8D93F5CD870B7FAFA2E661`
- Prompt: `wave04_character_source_v1` in `wave_04_promptset.json`
- Toolpfad: Built-in `image_gen`, kein CLI-/API-Fallback
- Chroma: `#FF00FF`, nur als Analysehintergrund; kein Chroma-Pixel gelangt in Runtime-Sprites

Deterministische Aufbereitung:

1. nearest-neighbor Analyse-Resize;
2. Chroma-Ausschluss;
3. Quantisierung auf `yjse-pixel-v1`;
4. deterministische Auswahl der Hair-/Cloth-/Armor-Familienfarben;
5. pixel-native Rekonstruktion direkt in den finalen Zielrastern;
6. Erzwingung von Alpha `0/255` und stabiler PNG-Kompression.

Die Runtime-Sprites sind keine verkleinerten, unscharfen Concept-Bilder. Der Generated Source liefert Formensprache und Materialgewicht; finale Silhouetten, Frames und Pixelcluster werden reproduzierbar im eingecheckten Generator aufgebaut.

## Parallax Und Integration

Meadow, Forest, Cave, Mushroom Cave und Crystal Depths besitzen jeweils eigene Wave-04-IDs unter `world/backgrounds/wave04/*`. Jede Provenienzzeile nennt die verwendeten Tile-Palettenreferenzen. Linke und rechte Pixelspalte sind in allen fuenf Dateien bytegleich.

Die aktuelle Biome-Presentation referenziert weiterhin Wave-03-IDs fuer Backgrounds, Particles, Critter, UI und Elite. Diese IDs und Pfade bleiben unveraendert vorhanden; Wave 04 verlangt weder Alias noch JSON-Migration. Damit bleibt der bestehende `ContentReferenceValidator`-Vertrag erhalten.

## Manifest, Brief Und Provenienz

- 180 Manifest-IDs und 174 eindeutige Quellen im Gesamtbestand;
- 31 neue Wave-04-IDs und 31 eindeutige Wave-04-Pfade;
- 180 eindeutige Brief-IDs, davon 31 im Wave-04-Brief;
- jede neue ID besitzt Dimension, Kategorie, Atlas-Metadatum, Origin, Render-Layer, Lizenz, Provenienz, Runtime-ID-Ziel und ehrliches `runtime-preloaded`-Tag;
- animierte Quellen besitzen vollstaendige Frame-IDs, Rectangles und Origins;
- alle 31 Provenienzdatensaetze enthalten Output-Hash, Source-Hash, Generator, Methode, Prompt-ID, Lizenz und Runtime-Ziel.

## Preview

- Builder: `Game.Data/art_direction/tools/build_wave_04_preview.py`
- Output: `Game.Data/art_direction/wave_04_production_preview.png`
- Dimension: `2400x4380`
- SHA-256: `F66A67A9B9FADFCAE29943B2E6A76436798235FE1B8E676242AAFD8FBA5B5815`

Die Preview zeigt Generated Source und finales Character-Komposit, alle fuenf Layer auf hellen/dunklen Feldern, die komplette 16-Frame-Reihenfolge, alle fuenf Native-Period-/Wrap-Seams mit Tile-Referenzen sowie alle Effect- und UI-Quellen bei integer nearest-neighbor scales.

## Audit

Der strikte Python-Audit ueber das aktuelle Gesamtmanifest bestand:

- 180 Manifest-IDs;
- 174 PNG-Dateien;
- 0 fehlende und 0 unmanifestierte Dateien;
- 0 Dimensions-, Frame-, Origin-, Brief- oder Production-Metadatenfehler;
- 0 echte Duplicate-Content-Gruppen und 6 valide Aliasgruppen;
- 0 harte Befunde im Gesamtbestand;
- 31/31 Wave-04-Assets `pass`, 100 Prozent Stilpalettenkonformitaet und 0 Wave-04-Einzelbefunde.

Die verbleibenden weichen Hinweise gehoeren ausschliesslich zu aelteren Legacy-Assets und wurden durch Wave 04 weder erweitert noch versteckt.

## Ausgefuehrte Gates

| Gate | Ergebnis |
| --- | --- |
| Python Syntax und JSON Parse | bestanden |
| Strikter Sprite-Audit | bestanden, 0 harte Befunde |
| Wave-04-Qualitaet | 31/31 `pass`, 0 Einzelbefunde |
| Manifestfragment gegen Gesamtmanifest | 31/31 kanonisch identisch |
| Wave-03-Kompatibilitaetscheck | 17/17 gepruefte IDs weiterhin vorhanden |
| Deterministische Regeneration | 36/36 PNG-/Brief-/Manifest-/Provenienz-/Preview-Outputs hashstabil |
| Parallax Seam | 5/5 linke/rechte Spalten bytegleich |
| Asset/Content Tests Debug | 20/20 bestanden |
| Asset/Content Tests Release | 20/20 bestanden |

Der erste Testversuch im gemeinsam genutzten `Game.Tests/bin` traf auf eine von einem parallelen Prozess gesperrte `Game.Tests.deps.json`. Die finalen Debug-/Release-Laeufe verwendeten deshalb isolierte Artifacts-Pfade innerhalb des Repositories; dadurch blieb die Repository-Suche der Tests gueltig, ohne fremde Build-Ausgaben anzufassen.

## Runtime-Grenze

Assetseitig ist Wave 04 `verified`. Sichtbare Runtimeaktivierung, Scene Selection, Character Clips, spritebasierte Effect-Ausgabe, Nine-Slice-Widget-Nutzung, Texture-Preload-Messung und Playing-Screenshot-Smokes bleiben `partial`, bis die jeweiligen Runtime-/Content-Owner die neuen IDs bewusst uebernehmen. Dieser Bericht behauptet keinen Live-Nutzer ausser `ClientTextureRegistry.PreloadAll`.
