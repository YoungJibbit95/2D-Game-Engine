# Sprite Scale And Grid Contract

Stand: 2026-07-11
Status: `partial`

## Verbindliche Runtime-Basis

- Ein World Tile entspricht `16x16` World-Pixeln.
- Ein Chunk umfasst `32x32` Tiles beziehungsweise `512x512` World-Pixel.
- Der aktuelle Standard-Camera-Zoom ist `2.0`.
- Die aktuelle Settings-Range erlaubt Zoom `0.5` bis `6.0`.
- Visuelle QA muss mindestens bei 1x, 2x, 3x und 4x erfolgen; zusaetzlich werden die erlaubten Extremwerte 0.5x und 6x auf Clipping und Lesbarkeit geprueft.

Source-Pixel, World-Pixel und UI-Pixel sind getrennte Begriffe. Eine groessere Quelldatei darf ein Objekt nicht automatisch in der Spielwelt vergroessern.

## Aktuelle und geplante Groessen

| Asset-Rolle | Source Contract | Runtime-Status | Regel |
| --- | --- | --- | --- |
| Terrain Tile | 16x16 pro Frame | aktiv | fuellt genau ein Tile |
| 4-Bit Autotile | 16 Frames horizontal, derzeit meist 256x16 | aktiv/partial | Masken `0..15` in aufsteigender Reihenfolge |
| Character v1 | 16x32 pro Frame | aktiv | kompatibel halten |
| Character v1 Physics Body | 12x28 | aktiv | nicht aus Sprite-Groesse ableiten |
| Item/Tool/Weapon Icon v1 | ueberwiegend 16x16 | aktiv | Legacy-Vertrag |
| Item/Tool/Weapon Icon der aktuellen Sample-Wave | 32x32 | partial | UI lesbar; World-Drop-Skalierung noch nicht getrennt |
| UI Icon v1 | ueberwiegend 16x16 | aktiv/partial | Widget bestimmt Zielgroesse |
| UI Icon Sample | 32x32 | partial | `FitInside`-artige Skalierung, keine World Units |
| Workbench World Sample | 64x32 | partial | benoetigt 4x2-Source-Pixel-Dichte oder dokumentierten 2-Tile-Footprint; aktuell nicht geklaert |
| Aktuelle Parallax-Layer | 512x128 | aktiv/partial | horizontale Wiederholung und Budget pruefen |

32x32 Items und die 64x32 Workbench sind kein global vollzogener Scale-Wechsel. Sie bleiben `partial`, bis der Renderer `PixelsPerUnit`, Visual Bounds und Footprints beachtet.

## Pixels Per Unit

`pixelsPerUnit` beschreibt, wie viele Source-Pixel einer definierten World-Einheit entsprechen. Der Manifesttyp besitzt das Feld bereits, der aktive Renderer wertet es jedoch noch nicht aus.

Vertragsregeln:

- UI-Sprites verwenden keine World-Unit-Ableitung; das Widget legt die Zielgroesse fest.
- Tiles verwenden 16 Source-Pixel pro Tile-Kante, sofern der versionierte Tile-Vertrag nichts anderes festlegt.
- World Entities und Drops erhalten explizite Visual Bounds. Ihre Bildschirmgroesse wird nicht direkt aus der Source-Rectangle-Groesse abgeleitet.
- Eine 32x32-Item-Quelle darf als World Drop weiterhin beispielsweise in einem 16x16-Visual-Bound erscheinen.
- Ein Multi-Tile-Objekt benoetigt einen Footprint und eine davon getrennte Source-Aufloesung.

Bis diese Regeln im Renderer implementiert und getestet sind, darf eine Dimensionsaenderung nicht als reine Asset-Ersetzung behandelt werden.

## Pivot- und Origin-Regeln

Pivots sind ganzzahlige Source-Pixel-Koordinaten relativ zum Frame.

| Rolle | Standard-Pivot |
| --- | --- |
| UI Icon | Frame-Mitte |
| Inventory Icon | Frame-Mitte |
| Terrain Tile | links oben fuer Grid-Platzierung |
| Grounded Entity | unten mittig zwischen den Standpixeln |
| Fliegende Entity | visuelles Zentrum, mit separat dokumentiertem Body Offset |
| World Object | unten mittig oder expliziter Placement Anchor |
| Held Item | Hand-Pivot plus optionaler Effekt-/Muzzle-Pivot |
| Projectile | Bewegungsursprung beziehungsweise Schwerpunkt |
| Background | links oben der wiederholbaren Source-Region |

Der aktuelle Player-v1-Manifest besitzt `originX=8`, `originY=16` fuer seine Frames. Diese Werte werden vom aktiven Renderer nicht verwendet und gelten deshalb als vorhandene, aber unverified Metadaten.

## Frame Grid

- Alle Frames eines Sheets haben dieselbe Zellgroesse, sofern ein expliziter variabler Frame Contract eingefuehrt wird.
- Frame-Reihenfolge ist dokumentiert und stabil.
- Animationen referenzieren Frame-Indizes; eine Reorder-Aenderung ist breaking und benoetigt eine neue Version oder Migration.
- Autotiles verwenden Masken `0..15`; Masken duerfen nicht implizit aus der Dateireihenfolge geraten werden.
- Transparente Randpixel gehoeren zur Zelle und duerfen beim Atlas-Pack nicht abgeschnitten werden.
- Pivots aller Layer einer Character-Animation muessen identisch sein.

## Atlas Padding

Es existiert noch keine funktionierende Atlas-Pipeline. Folgende Werte sind der Zielvertrag fuer den kuenftigen Packer, keine Behauptung ueber aktuelle Runtime-Unterstuetzung:

- mindestens 2 transparente Padding-Pixel um jedes Sprite;
- zusaetzlich 1 bis 2 Pixel Edge Extrusion fuer filter- und skalierungsbedingtes Bleeding;
- keine Rotation von Pixel-Art-Sprites im Atlas;
- identische Frames duerfen nicht automatisch zusammengelegt werden, wenn Pivot, Rolle oder Provenienz abweichen;
- Source Rectangles werden als Packer-Output validiert;
- Atlas-Seiten verwenden stabile Gruppen: `world`, `items`, `entities`, `effects`, `ui`, `backgrounds`.

## Kamera- und Preview-Vertrag

Jede Sample-Wave wird dargestellt bei:

- nativer Pixelgroesse;
- 2x, 3x und 4x Nearest-Neighbor;
- Camera Zoom 0.5, 1, 2, 4 und 6 fuer World Assets;
- hellem Surface-Hintergrund;
- dunkler Cave-Szene;
- Flip Horizontal, falls die Rolle gespiegelt werden kann;
- dem realen UI-Slot, falls es sich um ein Icon handelt.

Keine Vorschau darf die Source hochskalieren und dadurch Clipping, falsche PPU-Interpretation oder Footprint-Probleme verdecken.

## Versions- und Migrationsregel

- Bestehende 16x16-Tiles und Character-v1-Frames bleiben gueltig.
- Ein Character-v2-Scale wird erst nach Vermessung von Physics Body, Visual Bounds, Kamera und Layer-Pivots festgelegt.
- Neue inkompatible Dimensionen erhalten neue IDs beziehungsweise Contracts; alte Inhalte bleiben ueber Compatibility Mapping ladbar.
- Save-relevante Tile- oder Entity-IDs werden nicht wegen einer Grafikmigration geaendert.
- Ein Scale Contract gilt erst nach Runtime- und Screenshot-Smoke als `verified`.
