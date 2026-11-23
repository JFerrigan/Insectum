# Insectum production implementation

## Goal

Build the real game in a separate Unity project at `game/`. Use the movement lab as a reference for hex geometry, movement tuning, and Worm clearance. The production game will have its own scenes, code, assets, and tests.

Target macOS first with Unity 6000.3.11f1 and C#. Complete a playable Forest run with all five insects, then give Forest and the player roster an original pixel-art pass before expanding to Swamp, Caves, Desert, and Alien.

## Production architecture

- **Game and run state.** A boot scene owns character selection, the current run, and the results screen. A run holds the seed, character, health, inventory, Nektar, and level progression. Ship four levels per world, with a boss and item reward on level four. Level entry replenishes ability energy. Health, items, and Nektar carry through the run. Death ends the run unless the player has an unused revive.
- **Movement and combat.** Use a shared 60 Hz motor with buffered, variable-height jumps, air control, and fall damage. Sample keyboard input separately so gameplay and tests can supply the same commands. Start with A/D to move, W/S to aim, Space to jump, J to attack, K for class ability, E to interact, and L for the active item. Resolve melee, projectiles, traps, and falls through a shared damage pipeline with knockback and invulnerability frames.
- **Character abilities.** Start from the proposed balance tables as editable tuning data. Spider gets a directional leap and energy-consuming wall climb. Beetle preserves momentum while shelled, loses steering and jumping, spends energy only on damage it would otherwise take, and can crush enemies from above. If it cannot pay for a hit, the shell breaks and the hit resolves normally. Dragonfly toggles directional flight and disengages at zero energy. Fire Ant gets a wider, stronger melee attack. Worm excavates one adjacent cell ahead or below, at the material’s cost.
- **Hex terrain.** Use pointy-top axial cells for terrain and radius-five clusters for macro chunks. Build collision from exposed tile edges, flatten shallow valleys between suitable shelf tops, and join edge chains across region boundaries. Process excavation and explosions through one terrain interface. Successful removal updates occupancy and rebuilds affected collision before the next physics step. Reinforced cells stay intact, and isolated cells stay in place.
- **Generation.** Start with 19 macro chunks and six port directions. Choose a downward main route first, match authored templates to its ports, then place optional branches, shops, traps, and encounters. Author safe landing checkpoints at main-route ports. Verify port crossings with the actual motor and collision in a separate Unity physics scene, with abilities and items disabled. Select certified templates at runtime and use a known-good layout if bounded generation attempts fail. Give layout, encounters, and loot separate seeded random streams.
- **Economy and presentation.** Keep one active slot and unlimited passive stacking. Start with Excavator Claw, Seed Bomb, Moth Eyes, Burrow Sense, Heartwood, Flame Glands, Volatile Blood, and Emergency Reboot. Use typed item events with stable ordering and a per-step work limit so chain reactions remain predictable. Start presentation at 320×180 with point filtering and integer scaling. Author production sprites, tiles, and animation at native resolution using the supplied art as reference.

## Sequential implementation

The first execution step is to write this plan and its milestone checklist to `docs/07_PRODUCTION_IMPLEMENTATION_PLAN.md`. The coordinator then assigns **one subagent at a time**. Each assignment names the playable outcome, relevant interfaces, and acceptance checks. The coordinator reviews the diff, runs available checks, and sends corrections back to the same agent before assigning the next piece.

| Order | Assignment | Acceptance gate |
| --- | --- | --- |
| 1 | Bootstrap `game/`, add character selection, input, the shared motor, and an authored Forest route | From a clean checkout, the boot scene runs and the player can move, jump, take fall damage, reach the exit, and restart. |
| 2 | Implement terrain collision, destruction, and Worm | The player crosses hex seams smoothly. Worm spends the correct energy and passes through a one-cell opening. Reinforced and floating terrain behave correctly. |
| 3 | Implement shared melee, health, energy, and the other four class abilities | Every insect can complete the test route using shared movement and demonstrate its own ability. Beetle shell passes impact and energy edge-case tests. |
| 4 | Implement macro generation and Forest templates | An authored two-chunk crossing works first. Then at least 100 Forest seeds produce a verified, damage-free traversal route with abilities and items disabled. |
| 5 | Complete the Forest run | Four levels include avoidable encounters, a readable trap, Nektar, a shop, a chest, restoration pickups, a boss, and an item reward. A player can finish or die and restart. |
| 6 | Give Forest and the five insects their original pixel-art pass | Verify a macOS player build and play the full Forest slice. Check readability, camera framing, animation, and terrain destruction at game scale. |
| 7 | Expand one world per assignment in the order Swamp, Caves, Desert, Alien | Each world adds its own templates, material pool, enemies, hazard, boss, and items. Verify that ordinary routes and Worm shortcuts remain viable. |
| 8 | Integrate and tune the full run | Play from Forest to Alien, verify night, heavy-spawn, and double-boss variants, and produce the macOS desktop build. |

## Verification and defaults

- Test run transitions, repeatable seeds, energy accounting, purchase atomicity, active-item replacement, passive stacking, and one revive per run. Test frame-rate independence, region-boundary digs, tunnel clearance, and boss reward and exit behavior in Unity.
- At each world gate, replay at least 100 seeds and report the seed, template, and failed crossing for any regression. At the final gate, play the complete run and profile terrain edits on the target Mac against a 60 FPS target.
- Use the signed-in Unity Hub for local editor sessions. If sandbox licensing blocks an editor check, record which check is pending and keep it open until it runs in Unity.
- Treat the proposed balance, controls, chunk count, and render resolution as starting values. The launch scope is single-player desktop. Permanent unlocks, secret exits, multiplayer, and material simulation remain deferred as described in the design package.

## Milestone checklist

- [x] 1. Production project, boot scene, shared motor, and authored Forest route
- [x] 2. Hex collision, destruction, and Worm clearance
- [x] 3. Shared combat, energy, and the Spider, Beetle, Dragonfly, and Fire Ant abilities
- [x] 4. Forest templates, two-chunk crossing, and 100 verified seeds
- [x] 5. Four-level Forest run, economy, and boss reward
- [x] 6. Forest and roster pixel art, full Forest playthrough, and macOS build
- [x] 7a. Swamp world and 100 verified seeds
- [x] 7b. Caves world and 100 verified seeds
- [x] 7c. Desert world and 100 verified seeds
- [x] 7d. Alien world and 100 verified seeds
- [x] 8. Forest-to-Alien playthrough, run variants, terrain profiling, and macOS build
