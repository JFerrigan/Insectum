# Hex Insect Roguelike — Game Design & Reference Art Suite

**Working title:** Hex Insect Roguelike (not the final game name)  
**Revision:** Concept / prototype design, November 2026  
**Design influences:** *Spelunky* (platforming, descending chunk-built levels), *The Binding of Isaac* (passive item collection and potent synergies)  
**Visual direction:** **Crisp, intentionally low-resolution native pixel art**, specifically the **top-left native-pixel example** in the prior four-style comparison. Distinct identity: bugs, hexagonal destructible terrain, biopunk and increasingly futuristic world materials.

## Start here

1. `docs/00_GAME_DESIGN_PLAN.md` — overall gameplay contract, loop, five worlds, progression, and core decisions.
2. `docs/01_CHARACTERS_AND_ABILITIES.md` — all five insects, the shared movement/combat systems, energy costs and edge cases.
3. `docs/02_TERRAIN_AND_GENERATION.md` — hex tiles, smoothed collision, material excavation costs, 19-chunk proposal, template-based procedural level assembly, solvability.
4. `docs/03_ITEMS_ECONOMY_AND_ENEMIES.md` — Isaac-like item rules, Nektar, shops, examples of synergies, enemies, bosses, and modifiers.
5. `docs/04_ART_DIRECTION_AND_REFERENCES.md` — actionable art bible and image index.
6. `docs/05_IMPLEMENTATION_ROADMAP.md` — suggested engine-neutral code architecture, milestones, and tests.
7. `docs/06_OPEN_QUESTIONS_AND_DECISION_LOG.md` — precise separation of decided features, suggested defaults, and unresolved details.
8. `data/` — illustrative, editable CSV/JSON tables. The numeric balance data is **not finalized**.
9. `art/references/` — selected prior concept art, including a direct crop of the top-left preferred style, the chunky low-resolution test, shop screenshot, boss arena, and a swamp scene.
10. `art/concepts/` — simple palette and material-reference boards made to organize the visuals; they are **not production tiles or sprites**.

Tooling: `tools/map.sh <seed> [level|all]` renders generated levels to PNG and a text listing without Unity. See `game/README.md` → *Map generator*.

## Important qualifications

- **Decided** = something specified by the game designer in the conversation.
- **Proposed** = a reasonable but unapproved placeholder, primarily numeric tuning or implementation choice.
- **Open** = needs a later design decision.
- The supplied illustrations are **concept references only**, not animation sheets, tile atlases, final UI, exact-to-scale maps, or collision meshes. Details shown in imagery (e.g., UI prices, exact enemy roster, gear) do not override written design decisions.
- Art shown here is produced as concept references for this project. No external game art is copied as an asset.

## Build philosophy

Start by making the common platformer fun with placeholder art. Then prove hex terrain, local destruction/collision updates, procedural chunk solvability, and the beetle shell mechanic before scaling content production.
