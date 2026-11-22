# 05 — Technical Plan & Playable Milestones

This section suggests an **engine-neutral** architecture and test sequence. It is **not** a claim that a specific programming language or game engine has been chosen.

## Suggested modules

```text
GameLoop / FixedStepClock
RunManager
  - WorldSequence / LevelProgression / DeathAndRevive
LevelGenerator
  - MacroHexGraph / RoutePlanner / TemplateSelector / BiomeSpawner / Validator
TerrainService
  - AxialHexGrid / MaterialRegistry / TileStore / DestructionService
  - CollisionSurfaceBuilder / DirtyRegionUpdater
Character
  - SharedMovementController / Jump / FallDamage / LedgeInteractions
  - MeleeCombat / HealthAndIFrames / Knockback / AbilityEnergy
  - AbilityInterface [SpiderLeap, BeetleShell, DragonflyFlight, FireAntStrike, WormDig]
Inventory
  - ActiveItemSlot / PassiveItemList / ItemEventBus / ModifierResolver
AI
  - Patrol / FlyAndAggro / Ambush / Digger / BossStates
Economy
  - NektarWallet / ShopInventory / LootPools / ChestRolls
Presentation
  - PixelRenderer / Camera / HUD / Audio / VFX / SaveData
Debug
  - SeedInspector / PathOverlay / CollisionOverlay / EventAndEnergyLog
```

## Core function contracts (illustrative)

```text
RunManager.StartRun(characterId, seed)
RunManager.EndLevel()
RunManager.KillOrRevivePlayer(cause)
LevelGenerator.Generate(worldId, levelNo, seed) -> Level
LevelGenerator.ValidateBaselinePath(level, defaultMover) -> ValidationResult
HexGrid.Neighbors(q,r) -> six coordinates
HexGrid.CellToWorld(q,r) -> position
Terrain.TryDestroy(cell, source, actor) -> Result
Terrain.TryDig(actor, cell) -> Result
Terrain.OnTileRemoved(cell) -> update material state and dirty collision region
Collision.BuildLocalSurface(region) -> mesh/segments
Energy.TrySpend(amount, cause) -> bool
CharacterAbility.TryActivate(context) / TickFixed(dt) / Deactivate(reason)
Combat.ExecuteAttack(actor, forwardOrUp) -> hits
Health.TryApplyHit(hit) -> Blocked | Damaged | Dead | Revived
Items.DispatchEvent(event) -> effects with deterministic bounded chain depth
Shop.Purchase(itemOffer, wallet) -> Result
```

## Proposed milestone order

**M0 — Pure gameplay lab**
- Debug flat platforms; five insect silhouettes as colored placeholders; frame-perfect logging / speed and jump tuners.
- Walking, horizontal acceleration, jump apex and height control, collision, air steering, fall damage, knockback; moving attack in two directions.
- Acceptance: movement remains predictable across 60/120 FPS rendering.

**M1 — Hex collision and destruction lab**
- Flat-topped (or empirically chosen) logical hex grid; terrain stamp/unstamp; locally rebuilt smooth collision boundary.
- Worm dig in front or below with cost by material; metal and reinforced test; bomb destruction independently.
- Acceptance: no stuck characters or stale colliders; floating terrain remains in place; entering/leaving tunnels works.

**M2 — Character ability showcase**
- Spider big leap and wall climb; Beetle toggle shell, no steering, absorb energy instead of heart hits and crush enemies; Dragonfly toggle/drain flight; Fire Ant enlarged strong attack; Worm excavates.
- Energy refill on level transition and in-level pickups; start with different heart totals.
- Acceptance: player can complete a small test arena with every class; each class has at least one demonstrable advantage.

**M3 — Macro generator / reusable chunks**
- Hex chunk graph and path-first 19-chunk proposal. Template connectors across 6 sides; first biome 10–20 authored templates (count **proposed**); deterministic seeds and physical path validator.
- Acceptance: 100+ generated seeds solvable by default movement, no special ability or item required, correct macro paths.

**M4 — Combat, AI, hazards**
- Patrol, flier, ambush baseline enemies; projectile/item support; invulnerability frames, basic deadly trap; a very simple boss.
- Acceptance: combat does not lock movement; attacks and knockback have visible responses; fight or bypass works where intended.

**M5 — Loot/shop/run progression**
- Hearts, Nektar, shops, chests, 1 active item, passives/unlimited stacking, world/item pools, a small synergy demo, reset-on-death, restart button.
- Acceptance: a complete miniature run can be played; seeded loot is repeatable and purchases spend exactly the intended amount.

**M6 — 5-world vertical expansion**
- Implement forest, swamp, cave, desert, alien material palettes and template pools. Distinct enemy pools and one boss each. Level modifiers: night, high-density, double boss after stability.
- Acceptance: each world looks and plays recognizably different; no biome eliminates an ordinary route or makes Worm useless.

**M7 — Art and polish**
- Lock native pixel resolution and size-relative-to-hex; replace placeholders using original sprite sheets and tile atlases; refine camera, HUD and VFX; tune item synergies and resource economics.
- Acceptance: clean readability at screen size, consistent pixel scale and stable performance after local destruction.

## High-risk mechanics to prototype first

1. **Hex tiles vs platforming feel** — smooth collision without making geometry look disconnected.
2. **Worm tunnel clearance** — a single tile removal must behave intuitively with character hitbox size.
3. **Beetle shell** — control lock, momentum, zero-energy handling, fall damage and enemy-crush collision order.
4. **Generator solvability** — graph connectivity is not enough; validate actual jump clearance and landing safety.
5. **Items with enormous synergy** — use event ordering, typed effect hooks and loop safeguards to prevent performance collapse.

## Testing and debug tools (suggested)

- Seed replay and per-chunk path overlay; regenerate any failed seed.
- Render exact hex cells, smoothed collision, character hitbox, jump arc and dig target selection as toggles.
- Track energy per action and hits blocked; manually edit materials and Nektar drops.
- Generate 100+ test maps per biome overnight in a build/dev harness (no runtime background job assumed).
- Simulate standard character jumps between neighboring templates without extra abilities.
- Track run item count by world; show shop affordability and pickup location in debug UI.

## Explicitly deferred

- Secret rooms and alternative endings/exits, dynamic terrain falling/collapse, general fluid/sand cellular physics, electric-conductor simulation, complex boss phase graphs, multiplayer, permanent upgrade economy, final save scheme and full animation suite.
