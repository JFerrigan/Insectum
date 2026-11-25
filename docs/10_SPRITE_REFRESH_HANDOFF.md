# Sprite refresh handoff

This is for a new session that picks up the sprite refresh. Read it all before changing anything. The
original plan is at `~/.claude/plans/replicated-bouncing-brook-of-b6262879-7040-4c52-aeac-2dc7cf7ba177.md`.
This document supersedes that plan where they differ; the differences are listed under "Changes from the plan".

## Goal

New art for the playable insects, the non-boss enemies, every item, the pickups and the chests. The shop
stall is replaced by a pedestal with the item for sale hovering above it. The user picked the final mix
after reviewing versions v1 to v10:

| Group | Decision |
|---|---|
| Jumping Spider, Fire Ant | Current game art, unchanged |
| Beetle | Current game art, plus a visible eye |
| Dragonfly, Worm | New models |
| All 10 non-boss enemies | New models: Crawler, Spore Moth, Reed Runner, Skimmer, Cave Hopper, Glow Fly, Dune Beetle, Sand Wasp, Lattice Tick, Arc Mote |
| Excavator Claw, Moth Eyes | The v3 drawings, in the original Forest style |
| Other 10 items | New models |
| Nektar, Heart Sap, Energy Sap, Chest, Chest (opened), Boss Reward | New models. The Boss Reward becomes a gold chest, replacing the violet gem |
| Shop | Gone. A stone pedestal stands in its place, and the item for sale floats above it, bobbing up and down |
| Bosses (all five) | Untouched. Another session is overhauling them |

### The style the new models use

Everything stays close to the original sprites: the same shapes, palette and dark ink outline. What's new is
one shading rule applied the same way to every body part, in `game/ArtSource/sprite_style.py`:

- **Shade:** the bottom edge (2px on taller parts) and the lower right edge get a darker tone shifted toward purple.
- **Highlight:** the top-left edge gets a lighter tone shifted warmer, and bigger parts get one glint pixel.
- **Details:** seams and stripes use darker or lighter tones of the same part's colour, from `ramp()`.
- **Eyes:** every eye is an ink socket with one glint pixel. Characters get cream glints, enemies their biome's accent.
- **Limbs:** legs, horns, mandibles and similar get an ink outline, with a highlighted knee.
- **Ink:** `Pic(width, height, ink)` takes the outline colour, so each biome keeps its own ink.

## Where things stand (2026-11-24)

**Waiting on the user.** The last step was showing them the full preview sheet. They have not yet approved:

1. **Pedestal look:** a stone plinth with a flat cap, a gold gem inlay on the column and a little moss.
2. **Item size:** items float over the pedestal at full size (1x, crisp). Item plus pedestal is roughly twice
   the insect's height. The alternative is ¾ size like the pickups, which costs some pixel sharpness.

Show the user the preview again (see "Preview sheet") and get an OK on both before Step 2 below.

**Done, uncommitted:**

| File | State |
|---|---|
| `game/ArtSource/sprite_style.py` | New. The shared renderer: `ramp`, `Pic`, `limb` |
| `game/ArtSource/item_pixels.py` | New. Writes `Items.png`. Not run yet |
| `game/ArtSource/sprite_preview.py` | New. The contact sheet below |
| `game/ArtSource/forest_pixels.py` | Edited. New `dragonfly`, `worm`, `crawler`, `spore_moth`; beetle eye; `props()` rewritten for the new pickups, chests and pedestal |
| `game/ArtSource/swamp_pixels.py`, `caves_pixels.py`, `desert_pixels.py`, `alien_pixels.py` | Edited. New `styled_actor(kind, frame)` for kinds 0 and 1. `actor()` sends those kinds there and keeps the boss (kind 2) code as it was |

**Not done:** no PNG in `game/Assets/Resources/PixelArt` has been regenerated. No C# has changed. Nothing is committed.

**Already checked:** rendering the edited scripts in memory, the spider, fire ant and charge boss rows, and the
boss row of every biome sheet, are pixel-identical to the PNGs in the repo.

### Another session is working on bosses in this repo

It has uncommitted changes in `game/ArtSource/boss_forest_pixels.py`, `ForestBoss.png` and
`ForestBossGlow.png`, and commits often. See `docs/09_BOSS_DESIGN_HANDOFF.md`.

- **Never stage those files**, or any boss art.
- **Stage only explicit paths.** The tree also has unrelated untracked files (`game/Insectum/`,
  `game/game1.app/`, assorted `.meta` files), so never use `git add -A` or `git add .`.

## How the game uses this art

- **Scale:** `PixelViewport.PixelsPerUnit = 16`, so a 32px cell is 2 world units. Everything renders at 480×270 with point filtering.
- **Actor sheets:** each creature is one row of 6 frames, 32×24, with the pivot at `(0.5, 0.18)`.
  - **Player frames** (`InsectActorView.Pose` in `ForestArt.cs`): 0 idle, 1–2 walk or fly (the Dragonfly also flaps on 0–1 while idle), 3 airborne, 4 attack, 5 heavy attack, shell or ability cue.
  - **Enemy frames** (`LevelEnemy.UpdateArt`): 1–2 walk, 4 attacking, 5 warning. Frames 0 and 3 are drawn but unused.
  - **Which sheet each creature uses:**
    - `Actors.png`: rows 0–4 are Spider, Beetle, Dragonfly, Fire Ant, Worm (the `Insect` enum order); rows 5–7 are Crawler, Spore Moth, Charge Boss.
    - Each biome sheet: row 0 walker, row 1 flyer, row 2 boss. `LevelEnemy.ActorArt` maps `EnemyStyle` to sheet and row.
- **Props:** `Props.png` is 32×32 cells, 8 per row. `ForestArt.Prop(ForestProp)` uses cell index = enum value.
  - Features use `(ForestProp)LevelFeatureKind`, so cells 0–5 are Nektar, HeartSap, EnergySap, Shop, Chest, BossReward.
  - `LevelContent.AddFeature` places them. Pickups (kinds below `Shop`) are scaled to 0.52 or 0.75; everything else is 1x.
  - **HUD crop:** the HUD Nektar counter (`ForestScreen.cs`) crops Props cell 0 at exactly `x 12, y 9, 9×16` with `ForestArt.TexCoords`. The new gems keep exactly that outline. Don't change it.
- **Shop:** one shop is one `LevelFeature`, selling one item. When bought, the stall art stays and `DrawShopOffer` says "SOLD OUT".
  - Today, `LevelFeature.Consume` hides the art of every feature except the shop, so opened chests vanish.
  - `DrawShopOffer` anchors its label at `shop.Position + up * 1.4`. `DrawWorldPrompt` puts the text's bottom edge about at that point.
- **Import settings:** any PNG under `Assets/Resources/PixelArt/` gets point filtering, no compression and no mipmaps from `Editor/ForestTextureImport.cs`. A new `Items.png` needs no setup.
- **MapGen:** `tools/MapGen` doesn't compile `LevelContent.cs`, `ForestArt.cs` or `ForestScreen.cs`, so no `#if !MAPGEN` fencing is needed.

## Remaining steps

Commit after each step. Stage explicit paths only, and end messages with the attribution line the harness gives.

### Step 1: commit the art scripts (after the user's OK)

1. Commit the source changes, without regenerated PNGs: `sprite_style.py`, `sprite_preview.py`, and the edited `*_pixels.py` scripts.
2. If the user asks for changes, make them first and re-run the preview.

### Step 2: item sheet

From `game/ArtSource`, run `python3 item_pixels.py`. It writes `Items.png`, 192×64: 6 columns × 2 rows of 32×32 cells, with cell = `(int)ItemId - 1`.

Commit `Items.png` with `item_pixels.py`. Unity creates the `.meta` file on its next import; commit it too if it appears, because the other PixelArt metas are tracked.

### Step 3: Forest sheets

Regenerate only the two changed sheets. Don't run the whole script, which also rewrites terrain, cavern, glyphs and hazards:

```sh
cd game/ArtSource && python3 -c "import forest_pixels as f; f.actors(); f.props()"
```

Then check:
- `git status` shows only `Actors.png` and `Props.png` changed under PixelArt.
- In `Actors.png`, rows 0 (Spider), 3 (Fire Ant) and 7 (Charge Boss) are byte-identical to HEAD.
- Row 1 (Beetle) differs only in the eye pixels, in frames 0–4. The old gold dot at `(24, 12)` is gone, and the new socket is at `(25..26, 14..15)`. Frame 5 (shelled) is unchanged.
- `Props.png` is now 256×96. Cells 6–15 are identical to HEAD. Cells 16 and 17 are the opened chests.

### Step 4: biome sheets

Regenerate each biome's actor sheet only:

```sh
python3 -c "import swamp_pixels as s; s.actors()"
```

Then the same for `caves_pixels`, `desert_pixels` and `alien_pixels`. For each sheet, check that row 2 (the boss) is identical to HEAD.

### Step 5: game code

**`game/Assets/Scripts/ForestArt.cs`**
- **Prop enum:** append `ChestOpen` (cell 16) and `BossRewardOpen` (cell 17) to the end of `ForestProp`. Grow `propSprites` from `new Sprite[16]` to fit, sized from the enum's length. `Prop()` already handles a third row.
- **Item sprites:** add `internal static Sprite Item(ItemId id)`.
  - It loads `Items.png` lazily with `Load("Items")`, and caches like `Actor`, in a `Sprite[12]`.
  - It slices with `Frame(atlas, (i % 6), (i / 6), 32, 32, Vector2.one * 0.5f)`, where `i = (int)id - 1`.
- **Hover component:** add `internal sealed class HoverBob : MonoBehaviour` next to `InsectActorView`.
  - In `LateUpdate`, set `localPosition = rest + up * px / PixelViewport.PixelsPerUnit`, where `px = Mathf.RoundToInt((Mathf.Sin(Time.time * 2π / 1.6f + phase) + 1f) * 1.5f)`.
  - That gives 0–3 px, upward only, in whole pixels.
  - Take `phase` from the world x, so neighbouring pedestals don't bob in step.

**`game/Assets/Scripts/LevelContent.cs`**
- **Shop item:** when `kind == Shop`, `AddFeature` keeps placing the pedestal (Props cell 3) as now. It also places a child renderer, named "Offer", on the pedestal object:
  - Sprite `ForestArt.Item(item)`, at 1x.
  - Local position `Vector2.up * (20f / PixelViewport.PixelsPerUnit)`: 1.25 units, so the item's lowest pixel floats about 2px above the cap.
  - Sorting order 4, so it draws over the pedestal's 3.
  - Add `HoverBob` to it, and pass the renderer to `LevelFeature`.
- **`LevelFeature.Consume`:**
  - **Shop:** hide only the Offer renderer; the empty pedestal stays.
  - **Chest:** set `art.sprite = ForestArt.Prop(ForestProp.ChestOpen)`.
  - **BossReward:** set it to `BossRewardOpen`.
  - **Pickups:** hide as now.

**`game/Assets/Scripts/ForestScreen.cs`**
- **Label height:** in `DrawShopOffer`, raise `Vector2.up * 1.4f` to about `2.6f`. The item's top reaches about 2.45 units above the pedestal's centre at the top of its bob. Check by eye.

**Docs**
- **README:** in `game/README.md`'s art paragraph (around the `*_pixels.py` line), mention `item_pixels.py`, `sprite_style.py` and `sprite_preview.py`.

Then run `tools/compilecheck.sh`. It compiles every game and editor script without Unity, and works while the editor is open.

## Changes from the plan

- **Item height:** the item sits 20px above the pedestal's centre, not 18. The tallest icons (Emergency Reboot, Moth Eyes) reach row 30 of their cell, and 18 would let them touch the cap.
- **Bob range:** the bob is 0–3px upward only, not ±1.5px, so the item never dips into the pedestal.
- **Label height:** the shop label anchor goes to about +2.6 units, not +2.5, for the same reason.
- **Opened gold chest:** there is an opened version of the gold chest (`BossRewardOpen`), so a claimed boss reward stays on screen like a looted chest.
- **Preview script:** the sheet is drawn by `sprite_preview.py` in the repo, not a scratchpad script.
- **Beetle eye:** it sits at `(25, 14)` with its socket at `(25..26, 14..15)`, low at the front of the head, because higher up it merged with the horn.

## Preview sheet

```sh
cd game/ArtSource && python3 sprite_preview.py 62af2fc && open ../Builds/SpritePreviews/sprites_preview.png
```

It draws straight from the scripts. The comparison column reads the game PNGs at the given git ref.
`62af2fc` is the last commit before any of this work, so keep using it after the new sheets are committed.

## Verification

1. **Contact sheet:** run `sprite_preview.py 62af2fc` after regenerating. Check that every changed row has 6 frames and that nothing is clipped at a cell edge.
2. **Unchanged rows:** check them pixel-for-pixel against HEAD. These must be identical: Spider, Fire Ant, Charge Boss, every biome row 2, and Props cells 6–15.
3. **Compile:** `tools/compilecheck.sh`.
4. **Unity production verification:** run it from `game/`, using the command in `game/README.md` (`-executeMethod Insectum.Game.Editor.ProductionVerification.Run`). It needs the editor closed. It buys from shops, opens chests and claims boss rewards with real art, in every world.
5. **Play `Boot.unity`:**
   - **Shop:** a Forest level's item bobs in whole pixels with the label above it. Buying it leaves the empty pedestal and "SOLD OUT".
   - **Chests:** a chest stays open after it's looted, and the boss reward is a gold chest that opens.
   - **Dragonfly and Worm:** they animate through idle, move, jump, attack and ability.
   - **Enemies:** every world's walk, flap, attack and warning frames line up.
   - **HUD:** the Nektar icon still crops cleanly.

## History, if needed

The earlier rounds (v1–v9 scripts and preview sheets) are in the old session's scratchpad. It may be cleared:
`/private/tmp/claude-501/-Users-jakeferrigan-Documents-Insectum/b6262879-7040-4c52-aeac-2dc7cf7ba177/scratchpad/`

Everything needed to finish is in the repo; the scratchpad is only background. The user's feedback over those rounds:
- They liked the chunky v6 style.
- The neon v7 was "too far out".
- They then asked to stay close to the originals, with a moderately new style that is consistent across all sprites. That is v9 and this refresh.
