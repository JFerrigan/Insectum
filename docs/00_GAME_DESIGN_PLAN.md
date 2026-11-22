# 00 — Core Game Design Plan

## Concept and player fantasy

A side-view, single-player action-platforming roguelike in which five playable insect archetypes navigate **descending procedurally generated levels**, fight or avoid enemies, discover equipment, and manipulate **hexagonal terrain**. The run structure and movement philosophy draw from *Spelunky*; item acquisition, unlimited passive stacking, and occasionally outrageous combinations draw from *The Binding of Isaac*. The setting progresses through natural biomes into a colorful original alien world. The insects' distinct movement and ability-energy economy create alternative ways through the *same* level.

### Design pillars

1. **Platforming first** — responsive acceleration/deceleration, air control, jumping, fall damage, and knockback. Combat and terrain interaction support traversal; this is not primarily a material-physics sandbox.
2. **Five differentiated insect classes** — same basic move/attack grammar, but distinct active abilities, health and traversal advantages.
3. **Hex-world exploration** — both tiles and macro chunks are hexagonal; structural tiles can be erased instantly when destroyed. Real collision is smoothed for fluid movement.
4. **Items can transform the run** — one active item, unlimited passives, strong synergies, mixed global/world/enemy-specific loot pools. Broken-looking builds are intentionally welcome.
5. **Replayable downward adventure** — five themed worlds, nominally four levels each, a boss at the end of each world, hand-authored room/chunk templates assembled by a guaranteed route, plus optional treasure, shops and level modifiers.

## Core run flow (decided except where labeled)

1. Choose Jumping Spider, Beetle, Dragonfly, Fire Ant, or Worm; each has innate ability, differing heart count, and a full ability-energy bar.
2. Enter World 1, Level 1. Movement is continuous, not hex-to-hex, with a simple front/up melee attack available to all classes.
3. Navigate toward the **bottom** exit using an ordinary traversal route; fighting every enemy is optional. Explore for **Nektar**, chests, food/energy restoration, and optional loot. Some chunks contain traps; most chunks have enemies.
4. Spend Nektar in shops; acquire items through chests, boss rewards, shops, and rare enemy drops. One active item can be held; passive items stack without a cap.
5. Reaching the exit advances the level and **fully replenishes ability energy** (health is not assumed to refill; see open questions). Energy may be restored mid-level by food and by items.
6. The fourth level of each world is provisionally its boss encounter. Beat the boss to obtain an item reward and enter the next world.
7. Repeat through five worlds in a fixed order initially. **Death ends the run** unless a revive item intervenes. Current prototype: all characters and items accessible from the start; later permanent progression unlocks content.

**Scope/time:** Approximately 30 minutes is a loose run-duration ambition, **not** a hard limit or a reason to force sizes. There is no level countdown or ghost/timer mechanic in Revision 1.

## Five worlds (decided themes; exact order can be revisited)

| World | Theme | Primary terrain | Mechanical atmosphere | Potential fauna (examples, not a finalized roster) |
|---|---|---|---|---|
| 1 | Forest | Dirt, packed dirt, roots | Introductory traversal, natural ledges, ground insects, foliage | Ants, beetles, caterpillars, spiders |
| 2 | Swamp | Mud, dirt, water | Wet patches, denser vegetation, flying nuisances, pools and low overhangs | Mosquitoes, dragonflies, water beetles, wetland arthropods |
| 3 | Caves | Soft/hard stone | Narrower channels, steep shafts, dark levels, crystal pockets | Cave crickets, spiders, cave beetles |
| 4 | Desert | Sand, packed dirt, sandstone | Loose sand shapes, exposed ledges, buried treasure, dry hazards | Antlions, desert beetles, scorpions |
| 5 | Alien | Strange organic and mineral materials, metal | Luminous geometry, original flora/fauna, exotic hazards and devices | Original alien organisms (not Earth-accurate) |

The **biome controls weighted pools** for tile types, decorations, traps, enemies, templates, items, and optional modifiers. The game can grow increasingly futuristic, but primitive items can remain useful everywhere; "technology tier" is a presentation and pool concept, not a forced numerical power ladder.

## Lives, health and death (decided)

- Heart-based health, inspired by *Isaac*; starting hearts differ by class.
- Beetle has **most** starting hearts, Dragonfly **fewest**.
- Extra maximum health/hearts may be gained through items.
- Normal death is a complete run reset; a special revive item may save the run once.
- Long falls can damage characters; class abilities may avoid or modify this.
- Exact heart counts, damage thresholds, i-frames, health pickups, and revive semantics are open or proposed, never locked solely by concept art.

## Controls and moment-to-moment play

- Shared actions: walk/run (how the speed modifier is input remains open), jump, attack forward or upward, interact, use active item, use class ability.
- **Movement feel:** brief acceleration, quick braking, useful air steering, variable-height jump suggested, platforming precision inspired by Spelunky. Numerical values must be tuned by playtesting rather than asserted as authentic Spelunky code.
- Only some insects can wall-climb; **ledge grabbing itself remains open**.
- Attacks play an animation, work while moving, and cause knockback. Enemies similarly use attack animations/hitboxes and vary in behavior. Basic attacks are common, while Fire Ant has enhanced offense.
- Ranged options come primarily from acquired items, not mandatory default weapons.

## Map & hazards (decided)

- The world is made of hexagonal tiles, packed into **hexagonal macro chunks**.
- Primarily descending routes, with a guaranteed entrance-to-exit solution path; optional branches, hidden loot, and shop templates around the main route.
- Some terrain is **indestructible**. Destruction of an eligible hex is instantaneous: remove tile, update collision, show effect. Remaining floating tiles do **not** fall.
- The Worm can excavate innately; other characters need an item or tool to destroy terrain. Some enemies also destroy tiles. Explosions can remove eligible tiles.
- Varying material hardness determines **Worm energy cost**; no multi-hit excavation animation required.
- Deadly Spelunky-like traps are in scope; detailed trap roster to be designed later.
- No dynamic collapsing debris, electric conduction simulation, or general material-fluid simulation required for Revision 1.
- Shops are included in Revision 1. Secret rooms and alternative exits may be added later.
- Optional level variants include **night/darkness** with small visible radius, heavy enemy spawning, and double-boss encounters. Their frequencies and combinations are open.

## Economy, items and replayability (decided)

- Currency is **Nektar** (exact spelling).
- Find it visibly in the map, from excavation, and from certain enemy drops.
- Acquire items through treasure chests, boss rewards, shops and rare enemy-specific drops.
- Item pools are general/universal plus world-specific plus enemy-specific.
- Example confirmed item: **Moth Eyes** — night vision in dark levels, potentially dropped by a moth-like enemy.
- Items are fixed rather than individually leveled up. Synergies should sometimes make the player **ridiculously powerful**.
- Average acquisition goal is **a few items per world, roughly three** (~15 across five worlds as a *soft tuning target*, not a quota).
- Persistent progression eventually unlocks items and characters rather than being required for a successful initial prototype. Initially everything is available.

## Milestone target

Build a vertical slice with: smooth movement, baseline combat, five selectable classes, ability energy, hearts and fall damage, a hex terrain map with local destruction, the first biome's basic generation, an enemy and trap, one shop, Nektar, a chest and sample items, a simple boss, and restart-on-death. Keep first-pass graphics placeholder or low-resolution native pixel art.
