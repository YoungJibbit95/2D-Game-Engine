
# YjsE Asset Wave Ledger

Stand: 2026-07-22 nach Adventure Style V5, V7-Runtime-, Mobility- und Living-World-Validierung

Branch-Snapshot: `codex/runtime-resilience-engine-update` bei `9ce29ff`, gemischter Working Tree

Aktiver Asset-Status: `partial`

## Adventure Style V5 - Worldscape And Interface Direction

Wave-ID: `adventure_style_v5`

Assetstatus: `partial`: Main-Menu-Panorama und globale Code-UI-Materialsprache sind runtime-aktiv und lokal verifiziert; der spielbare Vordergrund benoetigt weiterhin authored Ruinen-, Wasserfall-, Bruecken-, Vegetations- und grosse Baum-Overlay-Assets, bevor die gesamte Welt die Referenzdichte erreicht.

Verbindliche Referenzen und Quellen:

- akzeptierte Worldscape-Referenz/Generated Source: `Game.Data/art_direction/generated_sources/parallax_worldscape_v7_concept_source.png`;
- akzeptierte UI-Referenz/Generated Source: `Game.Data/art_direction/generated_sources/ui_adventure_shell_v2_concept_source.png`;
- runtime-aktives Main-Menu-Asset: `Game.Data/assets/MainMenuAdventureV1/main_menu_adventure_v1.png`;
- Manifest, Brief, Provenienz und Preview: `main_menu_adventure_v1_asset_manifest.json`, `main_menu_adventure_v1_brief.json`, `main_menu_adventure_v1_provenance.json` und `main_menu_adventure_v1_preview.png`.

Integration:

- `MainMenuState` laedt das Panorama nur in `LoadContent`, nutzt point-sampled Aspect-Fill und behaelt einen prozeduralen Fallback.
- `UiTheme` und `PixelUiPrimitives` definieren ein gemeinsames dunkles Holz-/Messing-/Schiefer-/Pergament-Material fuer HUD, Settings/Pause, Inventory, Crafting, Character Editor und Developer UI.
- Der 1080p/1440p-HUD besitzt groessere Vitals-/Kompassrahmen und 52-Pixel-Hotbar-Slots; kleine Viewports behalten eigene begrenzte Layouts.
- Die Terrain- und Baumverbesserungen sind code-native und erzeugen in dieser Welle keine neuen Rasterquellen: freiliegende Tile-Kanten erhalten materialabhaengige Details, regionale Baeume verwenden je Baum genau eine kohärente Kronenpalette.

Validierung:

- Main-Menu-Asset: exakte `1672x941`, bytegepruefte Provenienz/Preview und korrigierter allgemeiner Brief-Loader-Vertrag;
- vier aktuelle 1920x1080-Release-Smokes fuer Main Menu, Forest Gameplay, Pause/Settings und Crafting: jeweils bestanden, 263 Ressourcen, 1.518 Frames, 0 ungueltige Ressourcen;
- 47/47 fokussierte Adventure-Layout-/Terrain-/Tree-Tests sowie 31/31 Menu-/Asset-/Living-World-Tests bestanden;
- keine neue Imagegen-Ausfuehrung in diesem Integrationsschritt: die bereits akzeptierten, eingecheckten Quellen und ihre vorhandene Provenienz wurden wiederverwendet.

## Background Features V7 - Biome Worldscape Planes

Wave-ID: `background_features_v7`

Assetstatus: `verified` fuer acht Runtime-Ebenen; Manifest, Brief, Generator, Provenienz, Kontaktbogen, strikter Audit, Content-/Planner-Integration sowie aktuelle 1080p/1440p-Release-Smokes sind lokal verifiziert.

Jedes der vier Biome Forest, Amber Grove, Twilight Marsh und Crystal Depths erhaelt eine `1024x256`-Bergsilhouette und eine transparente `1024x256`-Ebene mit schwebenden Inseln. Sie werden vor den drei bestehenden V6 Far/Mid/Near-Ebenen gerendert. Alle fuenf Ebenen behalten ihr natives `4:1`-Format mit genau einem einheitlichen X/Y-Faktor; der Faktor folgt nur Viewport-Tier und authored depth, nicht Kamera-Zoom, Sprunghoehe oder Terrain. Die Feature-Ebenen erhalten weltseed-stabile Phase, kleine Y-Variation und seam-tolerante Spiegelvariation; alle Ebenen sind point-sampled. Die neue Fullscreen-Projektion haelt jede Ebene vertikal geschlossen und benoetigt weder untere Pixelzeilen-Replikation noch Terrain-Farbband.

Reproduzierbare Artefakte:

- Generator: `Game.Data/art_direction/tools/generate_background_features_v7.py`;
- Manifest: `Game.Data/assets/background_features_v7.sprites.json`;
- Brief: `Game.Data/asset_briefs/background_features_v7_briefs.json`;
- Provenienz: `Game.Data/art_direction/background_features_v7_provenance.json`;
- Kontaktbogen: `Game.Data/art_direction/background_features_v7_contact_sheet.png`;
- strikter Audit: `Game.Data/art_direction/background_features_v7_asset_audit.json`.

Validierung:

- strikter Supplemental-Audit 8/8 Quality Tier `pass`, 0 harte Befunde, 0 fehlende Dateien und 0 Dimensionsfehler;
- Provenienz-Hashes 8/8 sowie Generator-/Preview-Hash stimmen nach deterministischer Regeneration;
- Runtime-/Content-/Parallax-Fokus 98/98 vor der finalen kombinierten Regression; der komplette Parallax-Scope besteht 89/89;
- alle Content-Pfade verwenden das Linux-sichere `assets/BackgroundFeaturesV7/**`-Casing.
- aktuelle Forest-Release-Smokes bestehen bei `1920x1080` und `2560x1440` mit 259 Ressourcen, 1.514 Frames und 0 ungueltigen Ressourcen; Background Draw liegt bei 0,097/0,073 ms im Mittel und 0 B.

## Legacy Boar Polish V1 - Forest Enemy Family

Wave-ID: `legacy_boar_polish_v1`

Assetstatus: `verified` fuer Normal- und Elite-Sheet, Manifest, aktualisierte Briefs, exakte Masse/Frames/Origins, Runtime-Referenzen, binaere Alpha, Stilpalette, Preview, fokussierten Familienaudit und strikten Gesamtaudit.

Der vorherige Eber bestand aus vier nahezu gleichen blockigen Posen. Die Runtime-IDs bleiben unveraendert, aber beide `128x32`-Sheets besitzen jetzt vier klar getrennte native `32x32`-Silhouetten in der bestehenden Reihenfolge `idle`, `windup`, `charge`, `attack`. Die Elite-Variante verwendet dieselbe Anatomie und Registrierung mit posegebundenen Blattplatten statt einer lose aufgesetzten Farbmarkierung. Pivot und Bodenanker bleiben bei `(16,31)`; es entstehen weder weitere Runtime-Texturen noch neue Draw-Pfade.

Reproduzierbare Artefakte:

- Generator: `Game.Data/art_direction/tools/generate_legacy_boar_polish_v1.py`;
- Briefs: `Game.Data/asset_briefs/base_sprite_generation_briefs.json` und `production_wave_03_briefs.json`;
- Provenienz: `Game.Data/art_direction/legacy_boar_polish_v1_provenance.json`;
- Preview und Summary: `legacy_boar_polish_v1_contact_sheet.png` und `legacy_boar_polish_v1_preview_summary.json`;
- fokussierter Audit: `Game.Data/art_direction/legacy_boar_polish_v1_asset_audit.json`.

Runtime-Vertrag:

- `entities/forest_boar.json` referenziert weiterhin `entities/enemies/forest_boar`;
- `forest.json` referenziert weiterhin `entities/enemies/forest_boar_elite` als Elite-Presentation;
- die Runtime-Animationsprofile `forest_boar` und `forest_boar_elite` konsumieren weiterhin die vier Manifest-Source-Rectangles aus je einer PNG-Ressource.

Validierung:

- Familienaudit 2/2 bestanden: `128x32`, vier `32x32`-Frames, Pivot `(16,31)`, transparente Ecken, binaere Alpha-Werte, 100 Prozent `yjse-pixel-v1`-Palette und vier unterschiedliche Frame-Hashes;
- strikter Gesamtaudit: beide IDs Quality Tier `pass`, 0 Befunde, Dark-Boundary-Ratios `0.9601`/`0.9145`, insgesamt 0 harte Befunde;
- Generator-Replay: 6/6 PNG-/Preview-/JSON-Ausgaben hashstabil;
- fokussierter Runtime-/Asset-Audit-Test: Debug 1/1 und Release 1/1 bestanden;
- finale SHA-256: Normal `4A5305EE2A4143E95CF6899F58BC0525097A2061F4042DEA243F88854DB67F05`, Elite `DF3ABEFE10386F1762CB843C1F7C114CC036C6C5848DC7A2161F753F34160A05`, Preview `D7038566053ACDD1606F1483033813730AB234916C22C880539C334F33AC765D`.

## Wave 04 - Character, Atmosphere, Combat And UI Production

Wave-ID: `wave_04_character_atmosphere_ui_production`

Assetstatus: `verified` fuer 31/31 PNG-Quellen, Manifest, Brief, exakte Masse/Frames/Origins, Promptset, Generated-Source-Provenienz, Binaer-Alpha, Stilpalette, Preview und strikten Audit. Runtimeaktivierung bleibt `partial`, weil C#, Character-/Biome-/Entity-/Item-JSONs und Website ausdruecklich ausserhalb des Asset-Scopes lagen.

Produktionsumfang:

- fuenf deckungsgleiche Character-v1-Wave-04-Layer mit je 16 `16x32`-Frames: Body, Hair, Clothes, Armor und Equipment;
- fuenf additive `512x128`-Parallax-Layer: Meadow, Forest, Cave, Mushroom Cave und Crystal Depths;
- 13 sechsframe Effect-Sheets: fuenf Ambient-, vier Weather- und vier Combat-Familien;
- zwei echte 3x3-Nine-Slice-Sheets mit je neun `16x16`-Frames;
- sechs `32x32`-Action-Icons.

Kompatibilitaet: Alle Wave-03-IDs bleiben unveraendert vorhanden. Aktuelle Biome-Presentation-JSONs koennen ihre Wave-03-Background-, Particle-, Critter-, UI- und Elite-IDs weiterhin referenzieren; Wave 04 verlangt keinen Alias und keine Content-Migration.

Reproduzierbare Artefakte:

- Generated Source: `Game.Data/art_direction/generated_sources/wave_04_character_source.png`;
- Promptset: `Game.Data/art_direction/wave_04_promptset.json`;
- Generator: `Game.Data/art_direction/tools/generate_wave_04_assets.py`;
- Manifest-Referenzfragment: `Game.Data/art_direction/wave_04_manifest_entries.json`;
- Brief: `Game.Data/asset_briefs/production_wave_04_briefs.json`;
- Provenienz: `Game.Data/art_direction/wave_04_provenance.json`;
- Preview-Builder: `Game.Data/art_direction/tools/build_wave_04_preview.py`;
- Preview und QA-Summary: `wave_04_production_preview.png` und `wave_04_preview_summary.json`;
- Gesamtaudit: `Game.Data/art_direction/sprite_quality_audit.json`.

Validierung:

- 180 Manifest-IDs, 174 eindeutige Pfade und 174 PNGs;
- 180 eindeutige Brief-IDs, 0 fehlende oder dimensionsabweichende Briefs;
- 31/31 Wave-04-Assets Quality Tier `pass`, 100 Prozent Stilpalettenkonformitaet, 0 Einzelbefunde und 0 harte Befunde;
- alle fuenf Parallax-Quellen mit bytegleichen Seam-Spalten;
- Preview `2400x4380`, SHA-256 `F66A67A9B9FADFCAE29943B2E6A76436798235FE1B8E676242AAFD8FBA5B5815`;
- 31 eindeutige PNG-Quellen, 187 neue Frame-Descriptors und 19.197 Byte PNG-Dateigroesse;
- kompletter statischer Manifestpfad: 928 Frame-Descriptors inklusive System-Fallback und 4.647.940 Byte decodierte RGBA-Nutzdaten bei einem 1-Pixel-Fallback;
- strikter Audit: 0 Missing, 0 Unmanifested, 0 Dimensionsfehler, 0 echte Duplikatgruppen und 0 harte Befunde.
- kompletter zweiter Generator-/Preview-Lauf: 36/36 gepruefte Outputs hashstabil;
- fokussierte `SpriteAssetTests` plus `GameContentLoaderTests`: Debug 20/20 und Release 20/20 in isolierten Repository-Artifacts-Pfaden bestanden.

Vollstaendiger Gate-Bericht: `Docs/Art/WAVE_04_PRODUCTION_REPORT.md`.

## Wave 03 - Player And Biome Production

Wave-ID: `wave_03_player_biome_production`

Assetstatus: `verified` fuer PNG, Manifest, Brief, Masse, Frames/Origins, Provenienz, Preview und strikten Audit. Die Szenenauswahl der neuen Ambient-, Particle-, Elite- und Biome-Icon-IDs bleibt `partial`, weil Gameplay-JSON und C# ausserhalb des Asset-Scopes liegen.

Die begrenzte Welle erzeugt 24 eindeutige PNG-Quellen:

- vier Priority-Regenerationen: `items/copper_hoe`, `items/iron_hoe`, `projectiles/wooden_arrow`, `projectiles/magic_spark_particles`;
- fuenf Player-Quellen: `base_actions`, `body_variants`, `hair_variants_v2`, `clothes_variants_v2`, `accessories_hats`;
- drei tile-abgestimmte, horizontal nahtlose Backgrounds: Meadow, Forest und Cave;
- je Biome ein vierframe Ambient-Mob, ein vierframe Particle-Sheet, ein vierframe Elite-Mob und ein 32x32 UI-Icon.

Reproduzierbare Artefakte:

- Generator: `Game.Data/art_direction/tools/generate_wave_03_assets.py`;
- Provenienz: `Game.Data/art_direction/wave_03_provenance.json`;
- Preview-Builder: `Game.Data/art_direction/tools/build_wave_03_preview.py`;
- Preview: `Game.Data/art_direction/wave_03_production_preview.png`;
- Brief fuer die zwoelf neuen IDs: `Game.Data/asset_briefs/production_wave_03_briefs.json`.

Statischer Runtime-Vertrag: `ClientTextureRegistry.PreloadAll` konsumiert jede manifestierte Wave-03-Quelle und ihre 740 Gesamt-Frame-Descriptors. Player Base/Body/Hair/Clothes/Accessories sowie Forest/Cave Backgrounds und die Priority-Assets behalten vorhandene konkrete Nutzer. Neue Biome-IDs tragen ehrlich `runtime-preloaded`, nicht `runtime-used`.

Validierung:

- 149 Manifest-IDs, 143 eindeutige Pfade und 143 PNGs;
- 149 eindeutige Brief-IDs, keine fehlenden oder dimensionsabweichenden Briefs;
- 24 generierte Quellen beziehungsweise 26 Manifest-IDs inklusive der bestehenden Forest-/Cave-Aliase: alle Quality Tier `pass`, 100 Prozent Stilpalettenkonformitaet, keine Einzelbefunde;
- 0 fehlende Dateien, 0 unmanifestierte PNGs, 0 Dimensions-/Frame-/Originfehler, 0 echte Duplikate und 0 harte Auditbefunde;
- Preview SHA-256 `A7FFACCCA9D65BCD379DEAD8C6E93AD495AB6102677D2C1942F3B25BE68457BE`; kompletter zweiter Generator-/Preview-Lauf mit 26/26 stabilen Hashes.
- fokussierte AssetTests: Debug 11/11 und Release 11/11 bestanden;
- isolierter Release Asset-Preload-Smoke: 144 Ressourcen, 143 reale Loads, 741/741 Frames und 0 ungueltige Ressourcen. Der direkte Checkout-Smoke bleibt vor Preload durch eine fremde ungueltige Worldgen-Datei blockiert.

Vollstaendiger Gate-Bericht: `Docs/Art/WAVE_03_PRODUCTION_REPORT.md`.

## Wave 02 - Creatures, Biomes, Character And Props

Wave-ID: `wave_02_creatures_biomes_character_props`

Assetstatus: `verified` fuer PNG/Manifest/Brief/Provenienz/Preview/Audit; `partial` fuer den vollstaendigen Szenen-Smoke.

Neue stabile IDs:

- `entities/critters/squirrel` - 64x16, vier 16x16 Frames;
- `entities/critters/firefly` - 64x16, vier 16x16 Frames;
- `entities/enemies/forest_boar` - 128x32, vier 32x32 Frames;
- `entities/enemies/cave_spider` - 128x32, vier 32x32 Frames;
- `world/backgrounds/meadow_parallax_layer` - 512x128, horizontal nahtlos;
- `world/backgrounds/mushroom_cave_parallax_layer` - 512x128, horizontal nahtlos;
- `world/backgrounds/crystal_depths_parallax_layer` - 512x128, horizontal nahtlos.

Ueberarbeitete Runtime-IDs: `entities/player/hair_variants_v2`, `entities/player/clothes_variants_v2`, `entities/player/accessories_hats`, `items/mana_crystal`, `items/mining_charm`, `world/objects/chair`, `world/objects/table`, `world/objects/chest` und `world/objects/lantern`.

Reproduzierbare Artefakte:

- Generator: `Game.Data/art_direction/tools/generate_wave_02_assets.py`;
- Provenienz und finale SHA-256-Hashes: `Game.Data/art_direction/wave_02_provenance.json`;
- Preview-Builder: `Game.Data/art_direction/tools/build_wave_02_preview.py`;
- Preview: `Game.Data/art_direction/wave_02_asset_preview.png`;
- 1x/2x/3x/4x auf hellen und dunklen Feldern, native Background-Perioden, Wrap-Seam und Detailzooms.

Validierung am 2026-07-12:

- strikter Python-Audit: 137 IDs, 131 Quellpfade, 131 PNGs, 6 Aliasgruppen, 0 echte Duplikate, 0 fehlende Dateien, 0 Dimensionsabweichungen, 0 harte Befunde;
- alle 16 Wave-02-Assets: Quality Tier `pass`, 100 Prozent Stilpalettenkonformitaet und keine harten oder weichen Einzelbefunde;
- drei Backgrounds: linke und rechte Pixelspalte bytegleich;
- Wave-02-Preview nach kompletter Sprite-Neugenerierung byteidentisch, SHA-256 `0F554A71180F97714F7D0AED3D6B1ED733B2343395460488DD6F17BC89EFD2AA`;
- fokussierte Sprite-Asset-Tests: Debug 11/11 und Release 11/11 bestanden;
- der Release-Client-Smoke ist vor dem Texture-Preload durch eine fremde Loot-Definition ohne Pflichtfeld `Chance` blockiert; diese Asset-Wave veraendert gemaess Scope keine Loot-, Entity-, Biome- oder Item-Daten.

Runtime-Vertrag: Das Runtime-Manifest referenziert und preloadet alle Wave-02-Quellen. Character-v2 und die Item-Icons sind aktiv verdrahtet. Creature-, Background- und Furniture-Szenenauswahl bleibt `partial`, bis die zustaendigen Content-Owner die vorhandenen IDs in Entity-/Biome-/Map-Daten aktivieren und ein Screenshot-Smoke moeglich ist.

## Verbindlicher Status

Die sieben Art-Contracts unter `Docs/Art` sind vorhanden. Der Renderer-/Asset-Pfad ist jetzt korrekt genug fÃ¼r eine kleine reprÃ¤sentative UI-Wave, aber noch nicht fÃ¼r eine groÃŸe Character-, Biome-, Background- oder Furniture-Produktion. Ein vorhandenes PNG allein ist keine verifizierte Capability.

## Aktuelle gemessene Baseline

| Messpunkt | Ergebnis | Status |
| --- | ---: | --- |
| Manifest-IDs | 180 | verified |
| Eindeutige Quellpfade | 174 | verified |
| PNG-Dateien unter `Game.Data/sprites` | 174 | verified |
| Valide `sourceAliasOf`-Gruppen | 6 | verified |
| Echte Duplicate-Content-Gruppen | 0 | verified |
| Missing/unmanifested/dimension mismatch | 0/0/0 | verified |
| Harte Audit-Befunde | 0 | verified |
| Produktions-UI-PalettenkonformitÃ¤t | 100 % | verified |
| Produktions-UI-Farbanzahl | 3 / 12 / 9 bei Maximum 16 | verified |
| Erwartete Frame Descriptors | 928 inklusive System-Fallback | verified statisch; Wave-04-Preload-Smoke ausstehend |
| Texture Resources | 175, davon 174 Dateien + 1 System-Fallback | verified statisch; Wave-04-Preload-Smoke ausstehend |
| Texture-Ladezeit | 182.2208 ms | verified im isolierten Release-Smoke |
| Texture-Ladeallokationen | 3,838,128 B | verified im isolierten Release-Smoke |
| Dekodierte RGBA-Nutzdaten | 4.647.940 B bei 1-Pixel-Fallback | statisch berechnet; kein GPU-RAM-Wert |
| Texture Switches / reale Batches | unknown | Counter fehlen |

## Source Consolidation

Die frÃ¼her sechs byteidentischen Dateipaare verwenden jetzt je eine kanonische Quelle:

- `tiles/dirt` -> `tiles/dirt_autotile`
- `tiles/grass` -> `tiles/grass_autotile`
- `tiles/stone` -> `tiles/stone_autotile`
- `projectiles/poison_arrow` -> `items/poison_arrow`
- `world/backgrounds/forest_parallax_layer_v2` -> `world/backgrounds/forest_parallax_layer`
- `world/backgrounds/cave_parallax_layer_v2` -> `world/backgrounds/cave_parallax_layer`

Manifest und Brief behalten die stabilen Rollen-IDs, verweisen aber auf denselben Pfad und dokumentieren die Aliasbeziehung. Sechs redundante PNGs wurden entfernt. Der Audit unterscheidet valide Aliasgruppen von echten Duplicate-Content-Gruppen.

## Verifizierte Representative Sample Wave

Wave-ID: `wave_ui_resource_navigation_sample`

Gesamtstatus: `verified` auf dem lokalen Windows-Publish-Smoke

| Sprite ID | Dimension | Aktiver Nutzer | Contract | Validierung |
| --- | ---: | --- | --- | --- |
| `ui/mana_star` | 16x16 | HUD Mana Bar | `atlasId=ui`, Origin, Layer, Lizenz, Provenienz, Production Tags | Source-Alpha und Zielpixel sichtbar |
| `ui/inventory_tab` | 32x32 | Inventory Header | derselbe Production-Metadatenvertrag | Source-Alpha und Zielpixel sichtbar |
| `ui/crafting_hammer` | 32x32 | Crafting Header | derselbe Production-Metadatenvertrag | Source-Alpha und Zielpixel sichtbar |

Preview: `Game.Data/art_direction/ui_sample_preview.png`

- reproduzierbar aus Manifest und PNGs;
- 648x992;
- 1x, 2x, 3x und 4x mit Nearest Neighbor;
- helle und dunkle HintergrÃ¼nde;
- byteidentischer CI-Vergleich;
- visuell geprÃ¼ft.

Der verÃ¶ffentlichte Client-Smoke prÃ¼ft nicht nur Draw-Aufrufe: alle drei Source Rectangles enthalten sichtbare Alpha-Pixel und alle drei Zielbereiche unterscheiden sich tatsÃ¤chlich vom Panel-Hintergrund.

## Vorherige 9-Asset-Wave

Wave-ID: `wave_01_style_sample`

Preview: `Game.Data/art_direction/wave_01_contact_sheet.png`

Gesamtstatus: `partial`

Die neun vorhandenen Item-/Tool-/Weapon-/Workbench-/UI-Samples besitzen korrekte 32x32- beziehungsweise 64x32-Dimensionen, binÃ¤re Alpha-Kanten und eine kohÃ¤rente Richtung. Die beiden UI-Assets sind jetzt Teil der verifizierten UI-Wave. Item/Tool/Weapon und World Workbench bleiben `partial`, solange Icon-, World-Drop-, Held- und Multi-Tile-Footprint-VertrÃ¤ge nicht sauber getrennt und aktiv validiert sind.

## Audit-QualitÃ¤t

Der Python-Audit v2 prÃ¼ft:

- Datei- und ManifestvollstÃ¤ndigkeit;
- Linux-relevante Pfade Ã¼ber die bestehenden C#-Repositorytests;
- deklarierte und tatsÃ¤chliche Dimensionen;
- Alpha, Partial Alpha und transparente Ecken;
- Style-Palette und empfohlene Farblimits;
- dunkle Silhouetten;
- Production-Metadaten fÃ¼r Atlas, Origin, Render Layer, Lizenz und Provenienz;
- echte Duplicate Content Hashes;
- kanonische Source Aliases;
- harte Scope-Befunde ohne DoppelzÃ¤hlung.

Aktuelle weiche Legacy-Schuld auÃŸerhalb der Production-Sample-Tags:

- 119 Style-Palette-Abweichungen;
- 38 schwache dunkle Silhouetten;
- 6 niedrige Canvas-Belegungen.

Diese Befunde werden nicht als grÃ¼n versteckt, blockieren aber die kleine Production-Wave nicht rÃ¼ckwirkend. Jede zukÃ¼nftige Production-Wave muss die harten Regeln erfÃ¼llen.

## Production-Wave-Status

| Wave | Umfang | Status | NÃ¤chstes Gate |
| --- | --- | --- | --- |
| A | Detailed Player Character V2 | `planned` | versionierter Visual-/Physics-Bounds-, Pivot-, Layer- und Compatibility-Contract |
| B | Biome World Tiles | `partial` | vollstÃ¤ndiges Representative Biome Set mit Seam-/Negative-X-/Atlas-Smoke |
| C | World Objects And Furniture | `partial` | Footprint, Object States, Anchors, Persistence und Multi-Tile-Renderer |
| D | Backgrounds And Atmosphere | `partial` | datengetriebene Layer, Seam Tests, Texture Group und Speicherbudget |
| E | Items And Equipment | `partial` | Icon-, Drop-, Held- und Projectile-Rollen trennen |
| F | Entities And Creatures | `partial` | Animation/Bounds/Spawn/AI/Loot Runtime-Smoke |
| G | Effects And Feedback | `partial` | Wave-04-Sheets verifiziert; spritefaehiger Effect Renderer mit aktivem Nutzer fehlt |
| H | UI Art | `partial` | Nine-Slice/Icon-Quellen verifiziert; Theme-/Widget-Aktivierung und Atlas-Gruppe fehlen |

## Bewusst zurÃ¼ckgestellte Arbeit

- keine weitere groÃŸe Item-Wave vor getrennten Icon/Drop/Held-Contracts;
- keine Furniture-Wave vor Footprint/Multi-Tile/Save-Smoke;
- kein Character v2 vor versioniertem Bounds/Pivot/Layer-Contract;
- keine Biome-/Background-Massenproduktion vor gruppiertem Preload, Atlas und Texture-Budget;
- keine Effect-Sprite-Wave, solange der aktive Particle Renderer nur primitive Shapes verwendet.

## Offene Asset-/Renderer-Risiken

- `PreloadAll` ist correctness-first und hÃ¤lt aktuell jede Quelle resident; groÃŸe Waves wÃ¼rden den Start und Speicher linear erhÃ¶hen.
- `atlasId` ist Metadatenvertrag, noch keine Atlas Runtime.
- reale Batches und Texture Switches sind nicht instrumentiert.
- `pixelsPerUnit`, Origins und Visual Bounds werden nicht in allen Renderpfaden vollstÃ¤ndig ausgewertet.
- Tile Rendering besitzt noch keinen allgemeinen Multi-Tile-Visual-Footprint.
- Legacy-Assets erfÃ¼llen den neuen Production-Style nicht automatisch.

## Asset Resume Point

Wave 04 ist assetseitig abgeschlossen. Als naechstes entscheiden Character-/Biome-/Effect-/UI-Owner gezielt ueber die Aktivierung einzelner Wave-04-IDs und fuehren Playing-Screenshot- sowie Preload-Smokes aus. Bestehende Biome-Presentation darf bis dahin unveraendert Wave-03-IDs verwenden; keine Wave-04-Alias- oder Contentmigration ist erforderlich.
