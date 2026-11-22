# 04 — Visual Direction & Reference Art Index

## Selected style

**Use native pixel art, with more visible, lower-resolution pixels than the initial concept.** The specific preferred reference is the **top-left** panel of the user's four-style comparison: crisp handcrafted pixel art, earthy-violet hex cave terrain, a cute but readable orange jumping spider, strong blue and magenta accent lights, a chest, and visible dangerous insects. The user later requested an even lower internal-resolution version; both the preferred reference and the chunky-pixel test are included.

### Art identity

- Side-view 2D insect-scale adventure, dark underground environments, futuristic biopunk details (pipes, glowing devices, honeycomb motifs, ruins) evolving into alien abstractions.
- Intentional **pixel clusters**, not fake painterly pixels or blurry downscaling. Visible large nearest-neighbor pixels are a positive trait.
- Clear, compact insect silhouettes with distinctive anatomy: spider (eight legs, big leap pose), beetle (heavy shell), dragonfly (transparent broad wings), fire ant (strong mandibles), worm (segmented excavator).
- Hex tile materials read instantly, even in motion; top faces and ledge boundaries take precedence over background texture.
- Strong separation: solid foreground > moving hazards/enemies > decorative midground > low-contrast parallax background.
- Vibrant but selective cyan/violet/magenta glow against dark violet, warm orange stone, dirt, moss and fungal green.
- Future worlds shift palette and materials without changing the overall sprite density or readability grammar.

## Production-side art targets (PROPOSED, not fixed)

- Experiment with a **256×192 base canvas** nearest-neighbor-upscaled to game resolution as the most pixelated prototype, or a somewhat higher logical base resolution if the moving insect is too small to animate clearly. The prior test used 256×192 as a visual demonstration, not a mandatory project setting.
- Design animations at native resolution; snap environment tiles to the logical grid, with subpixel physics independent of rendering.
- Keep HUD hearts, energy and Nektar icons crisp and away from busy scenery. The screenshots' exact heart counts, currency and item icons are **mockups**.
- Use palettes tied to materials and light level; avoid over-detailing each destructible cell, because a typical screen may contain hundreds of them.
- Build separate sprite sheets / atlases for characters, terrain surfaces, decorative overlays, pickups/items, environmental machines, enemies, bosses, VFX and HUD.

## Biome palette direction (inspiration, not locked palettes)

| World | Dark base | Primary light/accent | Terrain read | Motifs |
|---|---|---|---|---|
| Forest | Violet/shadow navy | Amber and fresh green | Soil brown/orange | Roots, leaf litter, fungi |
| Swamp | Deep teal and purple | Toxic lime + cyan | Dark green/brown mud | Reed silhouettes, pools, pipe remnants |
| Caves | Blue-black | Sapphire / violet crystals | Slate and dark stone | Spires, stalactites, ore/ruins |
| Desert | Warm umber shadows | Gold/coral/bright cyan tech | Sandstone/golden sand | Burrows, broken machines |
| Alien | Magenta/navy | Electric cyan, pink, lime | Unfamiliar biomatter | Luminous plants, geometry, impossible organisms |

## Image files

### Core references

- `art/references/01_preferred_native_pixel_top_left.png` — **the selected style** extracted from the previously generated four-style comparison.
- `art/references/02_chunky_low_resolution.png` — earlier low-resolution version in same broad art family.
- `art/references/03_pixel_source_256x192.png` — actual 256×192 pixel input/reference; useful for examining pixel density.
- `art/references/04_vertical_shop_level.png` — downward-oriented level, a shop, enemies, food, chest, terrain and exit.
- `art/references/05_boss_arena.png` — combat, projectiles, boss UI and destructible/solid arena motifs.
- `art/references/06_swamp_level.png` — swamp color/material/vegetation concept; pool and ruined tech motifs.
- `art/references/07_forest_cavern_biopunk.png` — earlier full-scene natural/tech cave, useful for layering and terrain textures.
- `art/references/08_four_style_comparison.png` — historical comparisons: native pixel, painted-to-pixel, painterly non-pixel, cartoon non-pixel.

### Concept/reference boards (NOT production assets)

- `art/concepts/biome_color_guide.png` — color and shape direction for all five worlds; drawn as a guide, not sourced biome screenshots.
- `art/concepts/terrain_hex_guide.png` — proposed material colors and approximate dig-cost key; test readability before committing.
- `art/concepts/art_contact_sheet.png` — overview of selected images with labels to navigate the suite quickly.
- `art/concepts/character_lineup_concept.png` — five proposed insect silhouettes cropped from an exploratory concept board; **not game-ready sprites or actual animation frames**.
- `art/concepts/five_biomes_thumbnail_concepts.png` — small conceptual color/layout thumbnails of all five worlds cropped from the same exploratory board, not full gameplay screenshots.

**Note:** Only certain biomes have full-size scene artwork in this package. The Desert and Alien boards are palette directions, *not* individually generated finished gameplay screens. Do not treat the rendered tech, monsters, items, or HUD as final designs.

## Suggested prompts for later scene illustration

All prompts should **retain the selected crisp native pixel-art style** and keep a visible hex-terrain grid and clear platforming silhouettes.

- **Forest:** 'Side-view native low-resolution pixel art screenshot for an insect roguelike: damp leaf litter, warm orange-brown hexagonal dirt terrain, tree roots, fungus clusters, a small orange jumping spider on an elevated ledge, enemies on lower platforms, subtle cyan industrial ruin in distance, readable gaps and descent, high-contrast silhouettes, no photorealism.'
- **Swamp:** 'Same pixel grid/style and spider scale, bright lime-turquoise wetland pools within hex mud tiles, dense hanging vines, reeds and reed-insects, flooded machine wreck, chest and dangerous flying insects, clear side-view descent, dark backgrounds.'
- **Caves:** 'Same native pixel style, deep slate stone hex tiles, stacked caves with glowing blue/purple crystal pockets, narrow shafts and tunnels, cave crickets and beetles, ominous abandoned machinery, lower-light platforming.'
- **Desert:** 'Same native pixel style, sand and sandstone hex platforms, burrow tubes, orange sunlight shafts, ruined mechanical pump, antlion and desert arthropod silhouettes, descending route with ledges, chest, bright cyan tech highlights.'
- **Alien:** 'Same native pixel style and readable hex tiles, truly original magenta/cyan/acid-green biomatter, impossible luminous plants, iridescent insectoid alien threats, strange geometrical ruins, descending platform route, unsettling futuristic atmosphere.'

## Quality gates

- At game scale, player silhouette is unmistakable within 200 ms; enemies distinguishable from decoration.
- Hex walls and floors appear material-specific and show collision boundaries.
- The darkest night variant still communicates reachable surfaces in the small lit area.
- Pixel look remains coherent across characters, HUD, VFX and background, not a mix of radically different resolutions.
- No generated art is used directly as a final tiling asset without cleanup, modularization and animation/collision validation.
