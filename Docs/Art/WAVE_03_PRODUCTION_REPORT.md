# Wave 03 Production Report

Stand: 2026-07-12
Wave-ID: `wave_03_player_biome_production`
Asset-Scope: `verified`
Aktive Szenenauswahl neuer Biome-IDs: `partial`

## Produktionsumfang

Die Welle erzeugt 24 eindeutige PNG-Quellen. Forest und Cave besitzen zusaetzlich ihre bereits vorhandenen Manifest-Aliase, daher deckt die Provenienz 26 Manifest-IDs ab.

| Rolle | Quellen | Vertrag |
| --- | ---: | --- |
| Priority-Regeneration | 4 | Copper Hoe, Iron Hoe, Wooden Arrow Projectile, Magic Spark |
| Player | 5 | Base Actions, Body, Hair, Clothes, Accessories |
| Parallax | 3 | Meadow, Forest, Cave; jeweils 512x128 und horizontal nahtlos |
| Ambient-Mobs | 3 | je vier 16x16 Frames |
| Particles | 3 | je vier 16x16 Frames |
| Elite-Mobs | 3 | je vier 32x32 Frames |
| Biome-UI-Icons | 3 | je 32x32 |

Die Player-Quellen behalten alle vorhandenen IDs, 16x32-Zellen, Frame-Reihenfolgen und Origins. Die Figur zeigt bei 1x klar Kopf, Gesicht, Hals, Torso, Arme, getrennte Beine und Bodenanker. Ein Preview-Komposit prueft Base, Body, Clothes, Hair und Accessory gemeinsam.

Forest, Cave und Meadow verwenden ausschliesslich die Stilpalette und werden in der Preview direkt neben Grass/Dirt-, Leaves/Trunk- beziehungsweise Stone/Granite-Tile-Referenzen gezeigt. Linke und rechte Pixelspalte sind identisch.

## Produktionsartefakte

- Generator: `Game.Data/art_direction/tools/generate_wave_03_assets.py`
- Brief: `Game.Data/asset_briefs/production_wave_03_briefs.json`
- Provenienz: `Game.Data/art_direction/wave_03_provenance.json`
- Preview-Builder: `Game.Data/art_direction/tools/build_wave_03_preview.py`
- Preview: `Game.Data/art_direction/wave_03_production_preview.png`
- Audit: `Game.Data/art_direction/sprite_quality_audit.json`

Preview SHA-256: `A7FFACCCA9D65BCD379DEAD8C6E93AD495AB6102677D2C1942F3B25BE68457BE`
Provenienz SHA-256: `9C1C0BE5BF62ADDF078A3E9FA2C3A074DF4B80B7E0A4550DAC9526B1A933B4E7`

Ein kompletter zweiter Generator-/Preview-Lauf pruefte 26 Dateien. Ergebnis: 0 Hash-Aenderungen.

## Manifest Und Brief

- 149 Manifest-IDs und 149 eindeutige Brief-IDs;
- 143 eindeutige Manifestpfade und 143 PNGs auf Disk;
- 0 fehlende oder doppelte Brief-IDs;
- 0 Brief-/Manifest-Dimensionsabweichungen;
- 0 unmanifestierte PNGs;
- 0 fehlende Manifestpfade;
- 0 Dimensions-, Frame-Bounds- oder Originfehler;
- 6 gueltige Aliasgruppen und 0 echte Content-Duplikate.

Der Python-Audit validiert jetzt auch `Particle` und `Effect` als Standalone-Kategorien sowie Briefabdeckung, Briefmasse, Frame-Bounds, Frame-IDs und Frame-Origins.

## Qualitaetsaudit

Gesamtaudit:

- Quality Tier `pass`: 115;
- Quality Tier `review`: 34 Legacy-Assets;
- Priority-Regeneration: 0, vorher 4;
- harte Befunde: 0;
- Wave-03-Quellen: 24/24 `pass`, 100 Prozent Stilpalettenkonformitaet, 0 Einzelbefunde.

Die verbleibenden 100 Palette-, 32 Silhouetten- und 2 Occupancy-Hinweise gehoeren ausschliesslich zu Legacy-Assets ausserhalb dieser Produktionswelle und sind nicht hart markiert.

## Runtime-Nutzer

`ClientTextureRegistry.PreloadAll` ist der konkrete Runtime-Consumer aller manifestierten Quellen. Player-Layer, Priority-Assets sowie Forest/Cave behalten ihre bestehenden aktiven Nutzer. Die zwoelf neuen Biome-IDs und Meadow sind korrekt als `runtime-preloaded` markiert; aktive Spawn-, Particle-, Elite- und UI-Auswahl benoetigt den zustaendigen Gameplay-/Client-Owner und wurde in diesem Asset-Scope nicht vorgetaeuscht.

Der direkte Checkout-Smoke ist vor dem Preload durch die fremde Datei `Game.Data/worldgen/living-world.region.json` blockiert, deren World-Generation-Masse ungueltig sind. In einem isolierten Release-Publish wurde nur diese blockierende fremde Datei entfernt. Der danach unveraenderte Asset-Preload-Smoke bestand:

- 144 Texture Resources, davon 143 PNG-Dateien plus System-Fallback;
- 143 reale Texture Loads;
- 741/741 Frame Descriptors inklusive Fallback;
- 0 ungueltige Ressourcen;
- 2,977,792 B decodierte RGBA-Nutzdaten inklusive Fallback;
- 182.2208 ms gemessene Texture-Ladezeit;
- 3,838,128 B gemessene Ladeallokationen;
- 217,228 nichtschwarze Pixel im 640x360 Smoke-Frame.

## Ausgefuehrte Gates

| Gate | Ergebnis |
| --- | --- |
| Python Syntax/JSON Parse | bestanden |
| Strikter Sprite-Audit | bestanden, 0 harte Befunde |
| Deterministische Regeneration | bestanden, 26/26 Hashes stabil |
| Preview-Sichtpruefung | bestanden, 1x bis 4x, hell/dunkel, Komposit und Tile-Vergleich |
| AssetTests Debug | 11/11 bestanden |
| AssetTests Release | 11/11 bestanden |
| Direkter Checkout Client-Smoke | blockiert durch fremde ungueltige Worldgen-Datei vor Preload |
| Isolierter Release Asset-Preload-Smoke | bestanden, 144 Ressourcen und 741 Frames |

## Naechster Verantwortungsuebergang

Content-/Runtime-Owner koennen die vorhandenen `runtime-preloaded` IDs in Biome-Ambientprofilen, Particle-Auswahl, Elite-Spawnregeln und Biome-UI verdrahten. Danach sind Playing-Screenshot-Smokes fuer Meadow, Forest und Cave erforderlich. Bis dahin bleibt nur die Szenenaktivierung `partial`; die Assetproduktion dieser Welle ist abgeschlossen.
