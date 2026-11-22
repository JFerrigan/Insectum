# 06 — Decision Log & Open Questions

## Confirmed by designer

- Action-platforming-first roguelike; hex tiles AND hex chunks; primarily downward level routes, Spelunky-style hand-built templates + procedural path layout.
- Five named characters and their innate powers: Spider big leap/wall climb; Beetle toggle invincible/no movement control/energy on would-be damage/fall-crush attacks; Dragonfly toggle flight & continuous drain; Fire Ant strong wider special at fixed energy; Worm instant adjacent front/down excavation at material-dependent energy.
- Basic attack shared (front/up), attack while moving, knockback; range mainly through items; combat can be avoided.
- Heart-based lives and fall damage; permadeath except possible revive item; differing starting hearts (Beetle most, Dragonfly fewest).
- Energy replenishes between levels, plus selected food and item effects during a level.
- Some indestructible terrain; no falling tiles; no default excavation for non-Worm; some enemy- and item-based terrain destruction.
- No time limit; shops from Revision 1; secrets/alternate exits later.
- Five worlds: Forest / Swamp / Caves / Desert / Alien. Mixed enemy/material/item pools varying by biome; original colorful Alien flora/fauna.
- Currency name: **Nektar**. Visible pickups, excavation finds, some enemy drops.
- One active item, unlimited passives, fixed item effects, some global / some world-specific / some enemy-specific drops. Moth Eyes gives night vision; very strong synergies desirable. ~3 acquired items per world on average as a soft objective.
- Five-world fixed sequence initially; boss at end of each world, boss drops an item; different enemy archetypes; a 30-minute run is an aspiration not a hard rule.
- Permanent progression to unlock content later, but initially everything available.
- Level modifiers may include night/dark, heavy enemy spawning, double boss.
- Preferred visual: **top-left crisp native pixel artwork**, then **chunkier/lower-resolution** variation, not painterly realistic/cartoon direction.

## Proposed placeholder values (not designer-approved)

- Universal initial energy 100 points; starting hearts: Spider 3, Beetle 6, Dragonfly 2, Fire Ant 4, Worm 4.
- Spider leap 16 energy/action, climbing 3 energy/sec.
- Dragonfly flight 14 energy/sec.
- Beetle 25 energy per 1 heart prevented.
- Fire Ant special 15 energy/action, 2 damage vs normal 1.
- Worm materials: Sand 3, Soft Dirt 4, Mud 5, Packed Dirt 7, Clay 9, Alien Organic 10, Soft Stone 12, Hard Stone 18, Metal 25, Reinforced indestructible.
- Worm hold-to-dig repeat interval ~0.15 sec.
- 19 hexagonal macro chunks in 3–4–5–4–3 rows, each possibly 91 hex tiles (5 rings); use a physical movement validator.
- Smoothed collision via local hex-union geometry, potentially flat-topped hex orientation.
- Flight steering, special leap charge behavior, impact-crush minimum speed, Beetle zero-energy shield break behavior are *suggestions*, not confirmed rules.
- Rendering internal 256×192 resolution for an intentionally chunky prototype.

## Prioritized open questions

1. **Hex/character scale:** size of a cell, character collider width/height, and whether single-cell tunnels always fit Worm.
2. **Movement numbers:** jump apex/time, walk/run toggle, grounded/air acceleration, fall-damage threshold, i-frames, how ledge grabs work.
3. **Spider:** free vs energy-cost wall cling, exact leap direction/charge, whether ceiling clinging/webs/venom are innate.
4. **Beetle:** exact momentum cancellation/preservation, bounce behavior, zero-energy shield break, crushing attack edge cases and cost for would-be fall damage.
5. **Dragonfly:** steering speed and whether toggling in midair applies existing gravity/momentum; auto cutoff at zero.
6. **Fire Ant:** attack recovery, special hitbox directions and damage vs normal, cooldown and stun.
7. **Worm:** target selection on six hex edges, explicit ban/permission for digging upward, dig repetition, material energy budget, collider clearance.
8. **Health:** exact starting hearts, pickups, whether health refills between worlds or levels (only energy refill is decided).
9. **Shops:** how frequent, what prices, how much Nektar spawns, how many simultaneous offers and whether shop is always on the main path.
10. **Terrain:** exact explosion resistance and behavior for water and sand; precise reinforced-material distribution.
11. **Level templates:** exact macro chunk dimensions/shape, default path turn frequencies, minimum template library, hazard budgets, shop placement.
12. **Item catalogs and pools:** first 20–30 production items, rarity weights, duplication rules, synergies and event recursion caps.
13. **Bosses and enemies:** actual species/abilities, world-specific enemy schedules, trap roster and boss patterns.
14. **Level variants:** night brightness/lighting rules, modifiers frequency, if heavy enemies or double boss can occur early, and combinations.
15. **Permanent progression:** unlock criteria and scope; later phase, not needed for initial prototype.
16. **Tech/production:** actual engine, target platform, control mapping, save/resume and co-op support (none assumed).

## Suggested near-term design order

A. Lock a playable **collision/scale test** with a hex tile and a movement character.  
B. Prove **Worm** digging and **Beetle** shell behavior, then tune Spider/Dragonfly/Ant.  
C. Select **one world**, a small macro layout and a minimal set of actual chunk templates; stress-test solvability.  
D. Implement one boss, one shop and 8–12 illustrative items to test strong synergy combinations.  
E. Only then commit to final pixel resolution and a content-production art pipeline.
