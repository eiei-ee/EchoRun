# ECHO//RUN — Current requirements

This document describes the current public-alpha scope. It supersedes the
original prototype notes and avoids presenting ECHO//RUN as an imitation of
another game.

## Product loop

The player calibrates the local shadow model in an initial run, then races an
evolving non-colliding AI shadow in later runs. A contextual-bandit director
adapts pacing while deterministic validators retain final authority over route
safety.

## Required player experience

- Three-lane movement, jumping, sliding, obstacle avoidance, collectibles,
  scoring, death, restart, pause, and persistent local progress.
- Keyboard, mouse-drag, and touch input, with responsive WebGL and mobile
  layouts.
- A locally executed behavior-cloning shadow that trains from player actions
  and is frozen for the next run.
- An adaptive track director whose choices cannot produce an impossible route.
- Clear calibration, progress, win/loss, and error feedback.

## Engineering requirements

- Route generation must preserve at least one valid route; learned AI can
  influence pacing but cannot bypass deterministic safety checks.
- Recurring runtime track content must use pooling where practical.
- EditMode and PlayMode regression suites must cover gameplay and restart
  behavior.
- WebGL, Windows x64, and Android are build targets. A target may be described
  as an official download only after its release artifact has been tested and
  attached to GitHub Releases.
- Source, dependencies, and bundled assets must have documented redistribution
  rights. Signing keys, credentials, editor licenses, generated builds, and
  local training exports must not enter the repository.

## Out of scope for the public alpha

- Online leaderboards, rewards, or any client-trusted competitive scoring.
- Cloud inference or hosted player telemetry.
- Claims of platform availability without a tested release artifact.

## Source of truth

- [README.md](README.md) for the public project overview and supported
  downloads.
- [PRODUCT.md](PRODUCT.md) for the product intent.
- [docs/ROADMAP.md](docs/ROADMAP.md) for planned work.
- [docs/BUILDING.md](docs/BUILDING.md) for reproducible setup, tests, and
  builds.
