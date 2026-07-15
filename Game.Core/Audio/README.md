# YjsE Audio Core

`Game.Core.Audio` is renderer and device neutral. It owns the deterministic mix and presentation decisions; `Game.Client.Audio` owns MonoGame playback.

## Runtime flow

1. Register `SoundscapeDefinition` records in a `SoundscapeCatalog`.
   `SoundscapeDefinitionJsonLoader` can build the catalog from a game-owned directory.
2. Pass the fixed-tick `LivingWorldFrameSnapshot` and `WorldTimeFrameSnapshot` to `SoundscapeCommandPlanner.Plan` during client `Update`.
3. Route the returned commands to the client audio manager. The planner writes into a caller-owned span and emits at most eight commands.
4. Apply category settings and temporary ducking through validated `AudioMixerCommand` values.
5. Read mixer, planner and client telemetry from debug UI outside `Draw`.

Biome, cave, weather and world-event selection stays in Core. Asset loading, voice limits, panning and actual playback stay in the client adapter. Missing soundscape definitions produce stop commands and telemetry; missing audio assets remain silent and are counted by the client.
