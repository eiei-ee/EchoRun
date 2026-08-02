# Architecture

ECHO//RUN is an offline, client-side Tuanjie/Unity game. Runtime systems are
implemented in C# and the playable WebGL build does not call a hosted AI API.

## Runtime flow

1. `GameManager` owns menu, play, pause, result, and restart state.
2. `TrackManager` maintains pooled track segments and plans safe lanes before
   placing obstacles and rewards.
3. `PlayerController` normalizes keyboard, pointer-drag, and touch gestures into
   lane, jump, and slide actions.
4. `AIShadowRunner` learns a local behavior-cloning model and freezes the prior
   generation as the next run's opponent.
5. `AITrackDirector` uses contextual-bandit feedback to adjust later track
   pacing and composition.
6. Deterministic spawn rules retain final authority over reachability even when
   learned systems request a harder layout.
7. Save, progression, power-up, audio, UI, and telemetry systems remain local.

## Safety boundary

The learned systems may select among bounded plans, but they cannot bypass
lane-count limits, route continuity, obstacle compatibility, or lifecycle
reset rules. Tests cover the deterministic rules independently of model state.

## Persistence

Settings, progression, training state, and telemetry are stored through local
player storage. WebGL synchronizes persistent data to IndexedDB. Local data is
not authoritative for shared scores or competitive rewards.

## Build boundary

`BuildConfig` applies platform settings and generates WebGL, Windows, and
Android outputs. GitHub Actions uses a self-hosted Tuanjie runner, runs both
test modes, then builds all three targets sequentially.

## Extension points

- Add bounded track plans through the balance/configuration layer.
- Add power-ups through `PowerUpId`, balance definitions, inventory, runtime
  activation, and UI as one reviewed unit.
- Add new media only after documenting provenance and redistribution terms.
- Treat server-backed leaderboards or shared rewards as a separate trust
  domain with server-side validation.
