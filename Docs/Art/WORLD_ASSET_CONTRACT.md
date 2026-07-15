# World Asset Contract

Stand: 2026-07-11
Status: `partial`

## World Grid

- Ein Terrain Tile belegt `16x16` World-Pixel.
- Tile-Koordinaten duerfen negativ in X sein; Asset-Auswahl und Seam-Tests muessen auch an negativen Chunk-Grenzen funktionieren.
- Terrain, Background Walls, Decor und World Objects besitzen getrennte Render-Rollen.
- Source-Aufloesung, Visual Bounds, Collision und Platzierungs-Footprint sind voneinander getrennte Metadaten.

## Terrain und Autotiles

Der aktuelle Bestand besitzt 33 vollstaendige 4-Bit-Autotile-Sheets. Die vorhandene Maskenreihenfolge ist verbindlich:

- 16 horizontale Frames;
- Maske `0` bis `15` in aufsteigender Reihenfolge;
- jede Zelle `16x16`;
- Maskenmetadaten werden explizit im Manifest gefuehrt.

Ein vollstaendiges Sheet ist noch kein vollstaendiges Biome. Vor Production-Status benoetigt jedes Terrain-Set:

- Ground und Grass Edge;
- Subsoil und Rock;
- Background Wall;
- Ore Variants;
- Platforms, Slopes und Half Blocks;
- Natural Clutter;
- Vegetation und Cave Details;
- Water Edge beziehungsweise Drip/Waterfall, sobald der Renderer dies unterstuetzt;
- Mining-, Placement- und Drop-Nutzer;
- Seam- und Negative-X-Tests;
- Atlas-Gruppe und gemessenes Texture-Budget.

Die vielen derzeit nicht referenzierten Autotiles bleiben Content-Vorrat mit Status `partial`; sie begruenden keine fertigen Biome-Waves.

## Biome-Wave-Reihenfolge

Die geplante Reihenfolge ist:

1. Temperate Forest
2. Stone Caverns
3. Deep Crystal Caverns
4. Desert
5. Snow

Eine Wave beginnt mit einem Representative Sample aus Ground, Edge, Wall, Ore, einem Vegetationsobjekt und einem Parallax-Layer. Erst nach Seam-, Runtime- und Budget-Validierung wird das volle Set produziert.

## World Objects und Furniture

Jedes platzierbare Objekt benoetigt:

- stabile World-Sprite-ID;
- Inventory-/Drop-Sprite-ID als getrennte Rolle;
- Footprint in Tiles;
- Visual Bounds in World Units;
- Placement Anchor und Ground Pivot;
- Collision beziehungsweise Passability;
- Mining-/Removal-Regel;
- Open/Closed, Active/Inactive oder andere Zustandsframes;
- Light Source Metadata, falls relevant;
- Tile Entity ID fuer persistenten Zustand, falls erforderlich;
- Interaction Prompt Icon;
- Save-/Load-Smoke.

## Aktueller Workbench-Vertrag

Wave 02 ersetzt die bestehenden Quellen fuer `world/objects/chair`, `world/objects/table`, `world/objects/chest` und `world/objects/lantern` dimensionsgleich durch klarere Produktionssprites und dokumentiert Origin, Layer, Lizenz und Provenienz. Die Dateien liegen im Runtime-Preload-Scope; Platzierungs-Footprint, Interaktion und Persistenz bleiben unveraendert `partial`, da diese Wave keine Tile-/Map-/Item-Daten aendert.

Die aktuelle Sample-Wave enthaelt:

- `items/workbench` als 32x32 Inventory Icon;
- `tiles/workbench` mit einer 64x32 World Source.

Beide Assets sind vorhanden und content-referenziert. Der Status bleibt `partial`, weil:

- der Tile Content nur ein einzelnes Tile repraesentiert;
- kein Footprint-Metadatum existiert;
- der Tile-Renderer jedes Tile in ein quadratisches 16x16-Ziel zeichnet;
- Pivot, Visual Bounds und Placement Anchors fehlen;
- kein atlasbasierter World-Object-Pfad existiert.

Die 64x32-Datei darf nicht als Beweis fuer ein funktionierendes 2-Tile-Objekt gewertet werden.

## World Layer Order

Zielreihenfolge:

1. Background Sky/Far/Mid/Near
2. Background Walls
3. Terrain Backfaces
4. Terrain Tiles
5. Back Decor
6. World Objects hinter Entities
7. Entities und Drops
8. World Objects vor Entities
9. Liquids und Surface Overlays
10. Particles und Effects
11. Foreground Atmosphere

Der aktive Renderer implementiert nicht alle diese Rollen. Jede neue Rolle bleibt `planned`, bis ein Runtime-Nutzer und ein Render-Smoke existieren.

## Naming und Modding

- Tile IDs: `tiles/<material>_autotile` fuer maskierte Terrain-Sheets.
- World Objects: `world/objects/<name>`.
- Decor: `world/decor/<name>`.
- Walls: `world/walls/<biome>_<material>`.
- Inventory Icons bleiben unter `items/...` und werden nicht als World Source wiederverwendet, wenn Massstab oder Pivot abweichen.
- Mod-Overrides halten Numeric Tile IDs, Footprints und Save-Semantik stabil oder verwenden neue IDs plus Migration.

## Acceptance Criteria fuer ein World Set

- Alle Dateien, Briefs, Dimensionen, Pivots und Footprints stimmen ueberein.
- Autotile-Coverage und Maskenreihenfolge sind automatisch validiert.
- Seams bestehen bei positiver und negativer X-Koordinate sowie an Chunk-Grenzen.
- Mining, Placement, Drop und Save/Load nutzen die finalen Assets.
- Multi-Tile-Objekte rendern ohne Squash, Clipping oder falsche Collision.
- Helle, dunkle, nasse und beleuchtete Szenen wurden geprueft.
- Texture-Wechsel, Ladezeit, Atlas-Seiten und Speicher sind gemessen.
- Kein synchrones Asset-I/O findet in `Draw` statt.
