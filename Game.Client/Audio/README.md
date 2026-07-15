# YjsE MonoGame Audio Adapter

`SoundEffectRegistry` maps stable engine audio IDs to MonoGame `SoundEffect` assets. Load or register assets during `LoadContent`; playback and telemetry never touch disk and nothing is loaded from `Draw`.

`AudioManager` provides:

- Music, Ambient, SFX and UI buses plus master gain and ducking.
- Fixed total and per-bus voice budgets.
- Priority-based voice stealing and per-ID cooldowns.
- Distance attenuation and stereo pan for world sounds.
- Four crossfaded loop channels for music, ambience, weather and events.
- Silent missing-asset handling with counters and the last missing ID.

`SoundscapeController` consumes immutable living-world snapshots in `Update`, executes the Core planner commands and exposes combined planner/mixer/voice telemetry. It is valid to run it with an empty registry while a game has no authored audio yet.
