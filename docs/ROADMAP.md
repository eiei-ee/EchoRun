# Roadmap

This roadmap distinguishes verified current behavior from planned work. It is
not a promise of delivery dates.

## Current public alpha

- Playable WebGL build and Windows/Android build pipelines
- Keyboard, mouse-drag, and touch input
- Pooled procedural tracks with deterministic safe-route validation
- Local behavior-cloning shadow with cross-run generations
- Contextual-bandit track direction with delayed feedback
- Persistent progression, four consumable power-ups, audio, and settings
- EditMode and PlayMode regression coverage

## Near term

- Improve onboarding and make the calibration requirements visible during play
- Add deterministic run seeds and shareable diagnostic reports
- Expand accessibility options and mobile device coverage
- Publish performance budgets for WebGL, Windows, and representative Android
  hardware
- Add focused contribution issues with acceptance criteria

## Before 1.0

- Stabilize save and balance configuration formats
- Document the AI training data schema and evaluation methodology
- Add reproducible release packaging and checksums for all supported targets
- Complete an asset-provenance audit for every distributable file
- Establish compatibility and deprecation policies

## Possible future work

- Reusable sample packages for the shadow runner and safe track generator
- Seeded replay tooling for bug reports and AI comparisons
- Opt-in, privacy-reviewed aggregate telemetry
- Server-authoritative leaderboards only if a secure backend is introduced

Community proposals are welcome, but new scope must preserve route safety,
offline playability, platform performance, and license clarity.
