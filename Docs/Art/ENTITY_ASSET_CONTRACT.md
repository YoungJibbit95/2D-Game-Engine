# Entity Asset Contract

Stand: 2026-07-11
Status: `partial`

## Aktueller Bestand

Das Manifest enthaelt derzeit sechzehn Assets der Kategorie `Entity`. Wave 02 ergaenzt `entities/critters/squirrel`, `entities/critters/firefly`, `entities/enemies/forest_boar` und `entities/enemies/cave_spider` mit je vier sauber gerasterten Frames. Die Quellen sind manifestiert, gebrieft, im Runtime-Preload-Scope und statisch auditiert; Spawn-, AI-, Loot- und Szenenaktivierung bleiben ausserhalb des Asset-Scopes `partial`.

Wave 03 ergaenzt drei biome-spezifische Ambient-Mobs (`meadow_butterfly`, `forest_moth`, `cave_glowbug`) und drei Elite-Mobs (`meadow_slime_elite`, `forest_boar_elite`, `cave_spider_elite`). Jede Quelle besitzt vier manifestierte Frames mit stabilen Origins, Brief, Produktionsmetadaten, Preview und Provenienz. Die Silhouetten unterscheiden Biome und Rolle bereits bei 1x. Sie werden vom Runtime-Preload konsumiert; Spawn, AI, Loot und aktive Szenenauswahl bleiben gemaess Asset-Ownership `partial`.

Der aktive Client kann einige mit `animated` markierte Sheets abspielen, verwendet fuer Nicht-Player-Entities jedoch hartcodierte Bildraten von etwa 6 beziehungsweise 10 FPS. Vollstaendige Entity Animation Sets, Pivots und Visual Bounds sind damit nicht verifiziert.

## Bounds Contract

Jede Entity benoetigt getrennte Daten fuer:

- Physics Body Bounds;
- Visual Bounds;
- Ground beziehungsweise Center Pivot;
- Physics-to-Visual Offset;
- Shadow/Grounding Rule;
- Hurt-/Hit-Flash-Kompatibilitaet;
- Facing- und Flip-Regel.

Sprite Width/Height darf nicht direkt die Collision-Groesse bestimmen. Fluegel, Hoerner, Partikel und Death Bursts erweitern nur Visual Bounds.

## Minimum Animation Contract

Jede Production Entity besitzt mindestens:

- `idle`
- `move`
- `attack`
- `hurt`
- `death`

Typabhaengige Erweiterungen:

- `flight`
- `jump`
- `burrow`
- `cast`
- `ranged_attack`
- `spawn`
- `despawn`

Animationen werden ueber datengetriebene Clips mit Frame-Dauer, Loop Mode und Events definiert. Das Tag `animated` plus hartcodierte FPS ist nur eine `partial` Foundation.

## Pivot- und Event-Punkte

Je nach Entity-Typ werden folgende Punkte dokumentiert:

- Ground Contact;
- Body Center;
- Attack Origin;
- Projectile/Muzzle Origin;
- Cast Origin;
- Hit Effect Origin;
- Loot/Drop Origin;
- Shadow Center.

Flip Horizontal spiegelt alle Punkte konsistent. Asymmetrische Angriffe benoetigen entweder separate Frames oder eine dokumentierte Ausnahme.

## Entity Families

Geplante erste Families:

- Passive Critters
- Surface Slimes
- Flying Enemies
- Burrowing Enemies
- Cave Crawlers
- Ranged Enemies
- Magic Enemies
- NPC Base Characters
- Boss Prototype

Vorhandene Einzelassets markieren eine Family nicht als fertig. Jede Family benoetigt mindestens eine aktive Spawn Rule, AI Behavior, Loot Table, Runtime-Szene und Preview Sheet.

## Runtime- und Gameplay-Vertrag

Ein Production Entity Asset ist mit folgenden Core-Daten verbunden:

- stabile Entity Definition;
- Body und Health;
- AI Behavior;
- Spawn Rule;
- Attack-/Projectile-Regel, falls relevant;
- Loot Table;
- Audio-/Feedback-Hooks;
- Save-/Despawn-Verhalten;
- Animation Set.

Der Renderer besitzt keine autoritativen AI-, Combat- oder Spawn-Regeln.

## Naming und Versionierung

- Player: `entities/player/<contract>/<layer>` fuer neue inkompatible Contracts.
- Critters: `entities/critters/<name>`.
- Enemies: `entities/enemies/<name>`.
- NPCs: `entities/npcs/<name>/<layer>`.
- Bosses: `entities/bosses/<name>/<layer>`.
- Inkompatible Frame-Reihenfolgen oder Zellgroessen erhalten eine neue Contract-Version.

Mods muessen den deklarierten Body-/Visual-/Animation Contract einhalten oder eine neue Entity-ID verwenden. Ein Base-Asset-Override darf Spawn-, Loot- oder Save-Semantik nicht unbemerkt veraendern.

## Preview Contract

Jede Entity Preview zeigt:

- alle Minimum-Animationen;
- Facing Left und Right;
- Grounding beziehungsweise Flight;
- Hurt Flash;
- helle Surface- und dunkle Cave-Szene;
- native Pixelgroesse sowie 2x, 3x und 4x;
- Physics Body, Visual Bounds und Pivots als optionalen Debug Overlay.

## Acceptance Criteria

- Body, Visual Bounds und Pivots sind getrennt und validiert.
- Minimum Animation Set ist datengetrieben; keine neue hartcodierte FPS-Sonderregel.
- Spawn, AI, Combat und Loot besitzen aktive Runtime-Nutzer.
- Hurt, Death und Despawn hinterlassen keine falschen Frames oder Ressourcen.
- Flip Horizontal und Pivots funktionieren.
- Eine Source-Textur wird unabhaengig von Frame-Anzahl nur einmal geladen.
- Preview Sheet, Runtime-Smoke und Screenshot-Smoke bestehen.
- Texture-, Batch- und Speicherwirkung sind gemessen.
- Provenienz und Mod-Contract sind dokumentiert.
