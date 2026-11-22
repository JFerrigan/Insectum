# 02 — Hex Terrain, Destruction & Procedural Generation

## Non-negotiable terrain decisions

- Each solid or empty terrain location belongs to a **hexagonal tile grid**; macro level chunks are also **hexagonal**.
- Characters move in a **continuous side-view physics world**. Hex tiles are the map representation; collision is **smoothed** so uneven tile corners do not cause irritating platforming.
- Destruction is **instant** per tile; remaining tiles can hover and **never collapse because of lost support**.
- Worm excavates at an energy cost based on material hardness; other characters need tools/items; some enemies and explosions can destroy tile types.
- Some material is **indestructible**.
- The level is primarily top-to-bottom, with a guaranteed completable path and optional regions.
- Hand-authored chunk templates are assembled into generated level maps, inspired by Spelunky's path-first room approach but adapted to six connections rather than four.
- Game is **platforming first**; no Noita-style fluid/powder simulation, electrical conductor simulation, dynamic falling debris, or generalized cellular-automata chemistry is necessary in Revision 1.

## Candidate material table (PROPOSED numerical costs)

| Material | Energy per worm tile | Destructible | Notes |
|---|---:|---|---|
| Sand | 3 | yes | Desert; cheap shortcuts |
| Soft dirt | 4 | yes | Forest; common |
| Mud | 5 | yes | Swamp |
| Packed dirt | 7 | yes | Denser earth |
| Clay | 9 | yes | Less common |
| Alien organic matter | 10 | yes | Alien world |
| Soft stone | 12 | yes | Cave |
| Hard stone | 18 | yes | Deep cave |
| Metal | 25 | yes | Machinery/alien structures |
| Reinforced | N/A | **no** | Immovable structural/hazard borders |

These numbers assume a **proposed** 100-point energy maximum, *not* settled balancing. Keep material hardness, explosion resistance, Nektar reward chance, visuals and collision settings separate.

### Material representation (suggested)

```text
MaterialDefinition:
  id: string
  display_name: string
  solidity: bool
  destructible: bool
  excavation_energy_cost: number|null
  explosion_resistance: number
  visual_variant_ids: string[]
  biome_tags: string[]
  collision_profile: enum
  nektar_drop_chance: float
  destruction_vfx_id: string
```

A `TileState` references the material ID and can separately contain metadata such as hidden loot. Tile art may have overlays (roots, grass, crystal growth) that do not change gameplay solidity.

## Axial hex model

Use **axial coordinates** `(q, r)` for fine tile and macro chunk grids, optionally a cube coordinate `s = -q-r` for distance. Neighbor offsets: `(1,0),(1,-1),(0,-1),(-1,0),(-1,1),(0,1)`; render and collision conversions are deterministic.

A regular **flat-topped** tile orientation is a reasonable *proposal* for flat-looking ledges, but the visible tile artwork need not be an exact collision polygon. Confirm world-axis orientation after implementing a playable test room.

The art and physics should use one shared grid-to-world mapping. Never infer mechanical topology from rendered texture edges.

## Smoothed collision system (important)

1. Maintain a logical occupancy set and world-space mesh/segments for nearby solid cells.
2. Derive local top/side surfaces from the hex union rather than attaching a single collider per visible pixel.
3. Smooth small stair-step artifacts at exposed outer edges with conservative corner/ledge rules. Avoid smoothing a wall so much that intended climb blockers disappear.
4. Preserve deliberate platform gaps and slopes; avoid incidental one-pixel ledges that trap characters.
5. On excavation, mark the changed cell and neighborhood as dirty, rebuild nearby collision only, then swap in the new geometry in a safe physics step.
6. Movement validation must use this **actual smoothed collision**, not only axial adjacency.

**Critical check:** A worm should not create a hole it is too big to pass through. Choose appropriate collider dimensions or a deterministic clearing shape rather than relying on visual intuition.

## Macro layout — first-pass proposal

The user described a **4x4-square-grid-equivalent** size but wants **hexagonal chunks**. A natural near equivalent is **19 macro chunks in 3–4–5–4–3 rows**, subject to playtesting for length and difficulty. This is **a proposal, not a locked 19-room requirement**.

A hexagonal chunk containing a center cell and five concentric rings would have `1 + 3 * 5 * 6 = 91` small hex tiles, compared to a classic Spelunky room's 10×8=80 square tiles. This is a **candidate chunk radius**, not final sizing.

- Start in an upper-edge chunk, exit in a lower-edge chunk.
- Lay out a connected route through the hex chunk graph that trends downward, possibly weaving laterally.
- Assign **connection ports on six chunk edges**; pick compatible authored chunk templates respecting those edges.
- Infill each template with hex tiles; apply weighted decorations/enemies/traps/chests and optional areas.
- Add shops in eligible off-main-route or roadside chunks, preserving fair access.
- Validate path physically with the **least mobile baseline capability**, not just topology. Optional ledges may favor Spider, Dragonfly, or Worm.

### Pseudocode

```text
GenerateLevel(run_seed, world_id, level_index):
  rng = SeededRandom(run_seed, world_id, level_index)
  layout = BuildHexMacroLayout(target=~19_chunks)
  start = ChooseUpperEntrance(layout, rng)
  exit = ChooseLowerExit(layout, rng)
  route = FindDownwardBiasedConnectedRoute(layout, start, exit, rng)
  required_ports = ComputeRequiredPorts(route)
  graph = AddOptionalBranches(layout, route, rng)
  for chunk in graph:
      template = SelectTemplate(world_id, required_ports[chunk], rng)
      StampHexTiles(chunk, template, rng)
  ApplyMaterialBiomeWeights(world_id, rng)
  PlaceShopChestEnemiesTrapsAndNektar(world_id, graph, rng)
  ApplyLevelVariantIfAny(rng)
  BuildSmoothedCollision()
  if not ReachableWithBaselineMovement(start, exit):
      RepairOrRegenerateLevel()
  return Level
```

The *actual* geometry must honor player jump height and width, clearance, safe landing platforms, damage hazards, one-way/drop-through possibilities, and enemy occupancy rules. The protected solvable route should not require the Worm to dig or Dragonfly to fly.

## Template authoring model (PROPOSED)

Each authored macro template declares:

```text
ChunkTemplate:
  id
  biome_tags
  connection_mask[6]  // entrance/exit openings by hex edge
  base_tile_map
  interior_spawn_markers
  optional_treasure_markers
  optional_enemy_markers
  shop_eligible
  hazard_budget
  minimum_clearance
  difficulty_rating
  weight
```

A connection mask is a **geometrical opening**, not a promise of physical traversability. Validate both.

## Destruction service (suggested)

```text
CanDestroy(cell, cause) -> bool
GetExcavationCost(cell, abilityModifiers) -> number|null
TryDig(character, targetCell) -> success
ApplyExplosion(origin, radius, intensity) -> destroyedCells
RemoveTile(cell, cause) // emit TileDestroyed; spawn hidden Nektar if present
RebuildCollisionInDirtyRegion(changedCells)
```

- Digging a null/air or reinforced cell costs **zero**.
- A failed attempt due to insufficient energy leaves map and energy unchanged.
- Explosions use their own material resistance rules; do **not** automatically interpret Worm digging costs as explosion damage.
- Do not propagate gravity to neighboring tiles after deletion.

## Hazards and biome-specific geometry

Forest = soil and root-supported platforms; Swamp = mud, water regions, reeds, wet hazards; Caves = stone, chasms, optional darkness; Desert = sand, sandstone, buried pickups; Alien = original brightly colored organic/technological materials. Weighted spawning should be configurable. This is **not** a mandate for dynamic liquid, granular sand or lava physics.

## Tests needed before expanding the generator

- All six edge connection directions can make traversable openings.
- Deleting each cell updates only affected surfaces and never leaves ghost collisions.
- The grounded state and jump feel work at hex joins and smoothed steps.
- Every generated seed's required route is physically playable by all starting characters without abilities or inventory.
- There is no accidental soft lock when a flight/dig bar reaches zero.
- Shop and chest markers do not overlap hard obstacles or hazards.
- Deterministic seeds produce identical route, loot and terrain outputs, independent of framerate.
