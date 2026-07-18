# Product

## Register

product

## Users

Tencent Game Creation Competition open AI-track judges and players evaluating both game quality and whether AI is indispensable to the playable experience. They need to understand the controls quickly, train a personal shadow, challenge it in the next run, and observe both the opponent and road plan evolve from their behavior.

## Product Purpose

Deliver an AI-native shadow-racing runner built with Tuanjie Engine for the Tencent Game Creation Competition open AI track. The first run trains an online behavior-cloning model; later runs ask the player to beat a visible, non-colliding opponent modeled on their own action style. A second online model directs road rhythm from the live duel gap, making personal AI competition rather than score accumulation the core loop.

## Competition Goals

- Demonstrate a real runtime behavior model that learns and persists player actions.
- Make the AI shadow a visible opponent with a formal win/loss result and generations.
- Show that different player behavior produces measurably different opponents, road plans, and difficulty curves.
- Preserve responsive controls, reachable routes, and reliable WebGL delivery around the AI system.
- Clearly separate learned opponent/director decisions from deterministic safety validation.

## Brand Personality

Direct, energetic, readable. The game should feel arcade-like and responsive without becoming noisy or theatrical.

## Anti-references

Feature-heavy menus, decorative UI that hides the game, unfinished systems presented as complete, and interactions that require instructions before the first run.

## Design Principles

- Keep the calibrate-train-challenge-evolve loop obvious and fast.
- Prefer reliable feedback over additional features.
- Make every claim in the submission match the playable build.
- Separate demonstrated AI capabilities from planned work and deterministic fallbacks.
- Preserve readable UI across WebGL, Android, desktop, and mobile input.
- Spend visual emphasis on game state and player actions.

## Accessibility & Inclusion

Bundle fonts required by localized text, support keyboard and touch input, keep text contrast high, and avoid using color as the only indicator of state.
