# Boss Rework — Implementation Plan

**Status:** Proposed, November 2026. Nothing below is built yet except the sealed arena (commit `a1f6d11`).
**Scope:** Replace all five bosses with new, world-specific fights: a shared boss framework, a full-screen
health bar, part-based art and animation, telegraphed moves, and death animations. Double boss is switched
off until it gets its own design.

Numbers below (sizes, timings, health) are starting values to tune in play, not final balance.

---

## 1. Ground rules for every boss

**Scale.** The player is about 1.5 units (24 px) tall, and a normal jump rises about 2.06 units. The screen
shows 30 × 17 units. Bosses range from 5 to 22 units across.

**Telegraph language.** Every boss uses the same signals, so players learn them once:

| Signal | Meaning |
|---|---|
| Wind-up pose (rear back, coil, inhale) | A move is coming. Lasts 0.4–0.9 s. |
| Amber glow on the attacking body part | Melee, charge or body slam |
| Magenta/red glow | Projectile or beam |
| White flash on the last 3 frames | The hitbox goes live next frame |
| Ground or wall marker (crack, ripple, shadow, dotted line) | Where the hit will land |
| Screen shake | Heavy impact (slam, burst, wall hit). Small and short. |

Rule: **no damaging hitbox is live during a telegraph.** Each move's warning lasts at least 0.4 s (24 steps).
The tests check this.

**Openings.** After every big move the boss has a recovery window. During it, contact with the body does
no damage, so the player can stand next to it and hit it. Openings are marked by a dull, desaturated tint
(not the attack colors).

**Phases.** Each fight has three phases, changing at 66 % and 33 % health. A phase change plays a short roar
or stagger (about 1 s, invulnerable, no damage) and then unlocks moves or speeds them up.

**Intro.** When the level starts the boss plays a short entrance (about 2 s) while its name card slides in.
The boss can't be hurt and doesn't attack until the entrance ends. The player can move freely.

**Determinism.** Each boss picks moves with `SeededRandom` from the layout's encounter seed, on the fixed
60 Hz step, and never uses `Time.time` or `UnityEngine.Random`. The same seed and the same inputs give the
same fight, so replays and tests stay valid.

**Move picking.** Each move has a weight per phase, a cooldown, and a "not twice in a row" flag. Some moves
are reactive: they jump the queue when a condition is true (for example, the player is above the beetle).

---

## 2. Shared framework (Step 0)

Built once and used by all five bosses. Until each new boss replaces an old one, the old bosses keep running
on the old code and also get the new health bar.

### 2.1 Damage: bosses with several hurtboxes
`Combat.cs`

- `DamageReceiver` gets optional **hurtboxes**: a callback that returns the current list of circles, each with
  a center, radius, damage multiplier and `Armored` flag. Without the callback it keeps today's single circle,
  so the player and regular enemies are unchanged.
- `CombatArena.Melee` and `Crush` test every hurtbox and take the best one that overlaps. One swing hits a boss
  at most once, never once per segment.
- A hit on an **armored** hurtbox is absorbed: a "clank" spark, no damage. It still counts as a hit, so a
  downward strike on armor still bounces the player.
- **Weak-point** hurtboxes multiply damage (×2 to start). Whole-hit-point damage stays: 1 × 2 = 2.
- `LevelContent.Blast` (Volatile Blood, Seed Bomb) also tests hurtboxes instead of `enemy.Position`.

### 2.2 Boss runtime
New files in `Assets/Scripts/Bosses/`.

- **`Boss.cs`** — the abstract base class:
  - States: `Intro → Choosing → Move(…) → Recovery → … → PhaseShift → … → Dying → Dead`
  - A `Move` record: name, per-phase weights, cooldown, telegraph steps, active steps, recovery steps.
  - Hurtboxes, hitboxes (circles and boxes that hurt the player, switched on per frame), a contact-damage
    toggle, current phase, and `SeededRandom`.
  - `Step(DamageReceiver player)` runs one fixed step. `Position`, `Defeated` and `DeathFinished` are what the
    level reads.
- **`BossContext`** — what a boss can reach:
  - parent transform, `HexTerrain` (for digging and blasts), arena constants, the player's receiver
  - spawn-projectile, spawn-minion and camera-shake hooks
  - the RNG seed
- **`LevelEnemy`** stays the handle that the level, items and tests already use (`Damage`, `Position`, `Boss`,
  `Name`, `Nektar`, burn). A boss `LevelEnemy` holds a `Boss` and passes `Step`, `Position` and art to it.
  Flame Glands burn, Flux Core and the reward logic keep working unchanged.
- `EnemySpec` gets a `BossKind` (`StagBeetle`, `Mosquito`, `Centipede`, `Sandworm`, `BlindThrone`) that picks
  the `Boss` subclass. The five old boss `EnemyStyle` values are deleted once their worlds are replaced.

### 2.3 Rig and animation
`Bosses/BossRig.cs`

- A boss is a tree of **parts**. Each part is a `SpriteRenderer` with a parent, a local offset, rotation,
  scale, flip, sprite frame and sorting order.
- Animation is code-driven poses: each state sets target poses and parts ease toward them. Sine helpers
  handle breathing, a leg-gait helper gives tripod/wave cycles, and a chain helper does segment-follow for
  the centipede, the worm and tentacles. Frame swaps cover things a pose can't, such as wing blur, opening
  mandibles and eyelids.
- Helpers: whole-rig hit flash (2 frames white), part glow tint (amber/magenta), opening tint, and a
  death-fade palette.
- Sorting: boss parts draw at order 2–8, in front of terrain (meshes at 0). Parts that are "inside rock" for
  the centipede and the worm switch to order −5: behind terrain, in front of the backdrop (−20). The rock
  then hides them without any masking.

### 2.4 Effects
`Bosses/BossFx.cs`

- **Markers:** ground shadow/target (grows and reddens), dotted aim line, wall/floor crack, sand ripple and
  bulge, falling-debris shadow.
- **Particles:** simple pooled sprite particles for dust, debris chips, blood droplets, sand spray, static
  sparks, and the clank spark.
- **Projectiles:** `BossProjectile` with velocity, gravity, radius, sprite, lifetime, terrain collision via
  the existing `PhysicsScene2D` raycast, and an on-impact action (splash, shatter, puddle). The old fixed
  `LevelDart` stays for regular enemies.
- **Camera shake:** `GameSession.Shake(amount, steps)`. `GameBoot.LateUpdate` adds a decaying offset to the
  camera after the arena clamp, snapped to whole pixels.

### 2.5 Full-screen health bar and name card
`ForestScreen.cs`, drawn in the 320 × 180 HUD space

- **Bar:** 288 × 6 px at the bottom (y = 166), with an ink frame and the boss name centered above it in the
  pixel font. Fill color is red; the bar flashes white for 2 frames on each hit.
- **Chip trail:** after a hit the lost section shows pale and drains to the new value over about 0.5 s.
- **Phase ticks:** notches at 66 % and 33 %. The tick dims once that phase begins.
- **Name card:** during the intro, a large name and an epithet line ("OLD OAKHORN — Elder of the Hollow")
  fade in at the center of the screen, hold 1.5 s, then fade out.
- The bar appears when the intro starts and slides away 1 s after death. The pause screen hides it.
- The bar reads `HealthPool`, so it works for the old bosses as soon as Step 0 lands.

### 2.6 Double boss off
`RunWorlds.ChooseVariant` stops picking `DoubleBoss`, so boss levels are always `Standard`. The enum value and
variant banner stay, so it's easy to bring back later. The echo-boss spawn in `ApplyVariant` and the `echo`
handling in `LevelEnemy` are removed with the old bosses. `FullRunVerification` drops its double-boss
requirement and `VerifyOtherDoubleBosses`. The README's double-boss paragraph goes.

### 2.7 Dev tools
`DevMode.cs`

- Dev panel **BOSS** row: cycle and force a specific move, set the phase (jumps health to 100/65/32 %), and
  freeze the AI.
- Overlay toggle that draws hurtboxes (green), armor (grey), weak points (yellow) and live hitboxes (red).

### 2.8 Art pipeline
`ArtSource/`

- One script per boss: `boss_forest_pixels.py`, `boss_swamp_pixels.py`, `boss_caves_pixels.py`,
  `boss_desert_pixels.py`, `boss_alien_pixels.py`. Each uses the existing `Cel` painter and writes a part
  atlas to `Assets/Resources/PixelArt/<World>Boss.png`.
- `boss_fx_pixels.py` writes the shared `BossFx.png`: markers, cracks, particles, clank, splash frames.
- Each script also writes a **preview contact sheet** (`game/Builds/BossPreviews/<world>.png`). It shows the
  rig assembled in key poses at 4× and at 1×, next to the player for scale. These go to you for approval
  before the moves are wired up.
- `ForestTextureImport` already sets point filtering for `PixelArt/`. New PNG `.meta` files appear when Unity
  imports them; they're committed after that.

### 2.9 Tests for Step 0
- Hurtbox unit checks: one swing hits a multi-box boss once; armor absorbs and still allows a pogo bounce; a
  weak point doubles damage.
- Boss-bar math: chip trail and phase ticks.
- `ChooseVariant` never returns `DoubleBoss`.

**Checkpoint 0:** framework, bar and name card in, old bosses still beatable, double boss off. One commit
per piece.

---

## 3. Forest — Stag beetle "Old Oakhorn"

*Name is a placeholder. The old Forest boss was "Barkback".*

**Feel:** the teaching boss. Heavy, readable, mostly on the ground, sometimes airborne. Health 36.

**Size and shape:** long and low.
- Standing pose: about 5.5 units long including the mandibles, 2.5 units tall (88 × 40 px).
- Mandibles: about 1.8 units, separate left and right parts so they open and close.
- Charge crouch: about 1.3 units tall at the rear, low enough to clear with a running jump toward it. Its back
  can also be pogoed.
- Wings: elytra split open to reveal membrane wings spanning about 7 units.

**Look:** bark-brown elytra with moss and lichen highlights, darker head, amber-lit eyes, mandibles with a
worn-wood grain. Six legs with knobby joints.

### Hurtboxes
| Box | Notes |
|---|---|
| Mandibles | Armored: clank. Pogo still bounces. |
| Head | ×1, and ×2 while the horns are stuck in the wall |
| Thorax, abdomen | ×1 (3 overlapping circles along the body) |
| Back | ×1. The pogo target during charges. |

### Moves
1. **Horn Charge** *(signature, all phases)*
   - *Telegraph (0.75 s):* rears back, scrapes a front foot twice (dust puffs), drops into the crouch,
     mandibles glow amber. A dust streak marks the charge line along the floor.
   - *Attack:* runs along the floor toward the side the player was on, at 11 units/s, until it reaches the
     lane end (x = ±16.2, the face of the terraces). The mandibles and front of the head deal damage
     (1 heart, strong knockback); the back and rear don't.
   - *Dodge:* climb a terrace or platform during the telegraph, make a running jump over the crouch, or pogo
     off its back. Dragonfly flies over; Spider's high jump clears it.
   - *End:* the horns ram the terrace face. Shake, a debris burst, and the horns stick for 1.6 s (P1),
     1.3 s (P2) or 1.0 s (P3): legs scrabble, head shakes, contact damage off, head takes ×2. This is the main
     opening. It then wrenches free and turns around.
   - *P2+:* if the player is within 6 units when it wrenches free, it chains a second, shorter charge
     (0.5 s telegraph) back the other way.
2. **Mandible Snap** *(close range, all phases)*
   - *When:* the player is within about 2.5 units in front at roughly its height.
   - *Telegraph (0.45 s):* head pulls back, mandibles open wide, amber glow.
   - *Attack:* mandibles slam shut. A small box in front, 8 steps.
   - *Recovery (0.7 s):* head hangs low. An opening.
3. **Rearing Snap** *(upward attack, all phases)*
   - *When (reactive):* the player is above it (within 3 units horizontally, more than 1.5 units higher), or
     it has been pogoed twice within 1.5 s.
   - *Telegraph (0.5 s; 0.35 s after a double pogo):* head tilts up, front legs brace, mandibles open
     pointing up, amber glow.
   - *Attack:* rears up on its back legs and snaps the mandibles overhead. Hitbox: a column about 2.5 wide
     and 3.5 tall above the head, 10 steps.
   - *Recovery (0.8 s):* drops back onto its front legs with dust. An opening.
   - *Effect:* pogoing works, but chaining it gets punished.
4. **Wing Leap → Dive Slam** *(air, landing attack; weight about 12 % P1, 18 % P2–P3)*
   - *Telegraph (0.6 s):* elytra lift and split, membrane wings unfold (3-frame blur), dust swirls below.
   - *Lift-off:* rises to y ≈ 13 and hovers, tracking the player's x for 0.9 s (0.6 s in P3). A ground shadow
     under it grows and reddens.
   - *Attack:* locks its x, white flash, drops at 22 units/s. Landing hits a box under its body. The impact
     sends a low shockwave (0.6 units tall, jumpable) along the floor both ways for 6 units (P3: 10 units).
     Big shake.
   - *Recovery (1.2 s):* stunned in a squash pose, wings folding. An opening.
5. **Wing Leap → Air Gore** *(air, stays airborne; weight about 8 % P1, 14 % P2–P3)*
   - *Telegraph:* the same wing opening, but it rises only to mid-height and drifts to one side of the
     arena.
   - *Attack (×2, ×3 in P3):* hovers, horns glow, and a faint line shows its flight height (0.6 s). It then
     dashes horizontally across the arena at that height (16 units/s), horns-first. The height is taken from
     where the player was when the telegraph started, so changing height dodges it: drop off a platform,
     jump, or climb.
   - *End:* lands softly near a wall (small dust, no attack) and stays grounded for 1.0 s. An opening.
6. **Walk:** between moves it walks toward the player at 2.5 units/s with a tripod gait. It turns with a short
   body pivot rather than flipping instantly.

Air moves have their own cooldown (at least 10 s apart), so it stays mostly grounded.

### Phases
| Phase | Changes |
|---|---|
| P1 100–66 % | Charge, Snap, Rearing Snap, rare air move |
| P2 66–33 % | Chained second charge; air moves more often; telegraphs 10 % shorter |
| P3 33–0 % | Shortest stuck time; Dive Slam shockwave spans 10 units; Air Gore makes 3 passes |

### Death animation (as you described)
1. Last hit: big flash, the bar empties, the AI stops.
2. **Falls to its knees** (0.9 s): front legs buckle at the joints, the head drops until the mandibles touch
   the floor, the body tilts forward, the abdomen sags. One weak twitch of the elytra.
3. Pause 0.4 s.
4. **Drops onto its body** (0.5 s): the rear legs give out, the whole body falls flat with a dust cloud and a
   shake, and the legs splay outward.
5. The body darkens to a dull palette over 1 s and Nektar bursts out. The corpse stays as scenery. The
   reward appears.

### Animation list
Idle (breathing abdomen, antenna twitch), walk, turn, charge wind-up, charge run (low gallop with a dust
trail), horns stuck (scrabble and shake), wrench free, mandible snap, rear-up snap, wing open, hover (wing
blur), dive, landing squash, air gore dash, soft landing, phase roar, hit flash, death (5 stages above).

### Tests
- Every move's warning is at least 24 steps, with no live hitbox before it ends.
- A charge ends stuck at the lane end. Contact damage is off while stuck, and the head takes ×2.
- Two pogos within 1.5 s trigger the Rearing Snap.
- Air moves are at least 10 s apart, and the share of time airborne stays under 25 % over a long simulated
  fight.
- A scripted bot beats it with ordinary commands. It jumps or climbs for charges, pogoes, and hits during
  openings. The bot runs for Fire Ant and Beetle at least.

**Checkpoint 1:** art contact sheet to you first, then the playable beetle. **I stop here for your playtest
before starting Swamp**, because the beetle sets the template for the other four.

---

## 4. Swamp — Mosquito queen "Mire Bell"

*Keeps the old Swamp boss name: the bloated abdomen is the bell.*

**Feel:** mostly airborne and hard to reach. She lands to feed, and that's when you hurt her. Health 40.

**Size and shape:**
- Head and thorax about 2.5 units. Proboscis 2.5 units, separate part so it extends and retracts.
- Six long dangling legs about 3 units.
- Wings span about 8 units: translucent, a 4-frame blur, drawn behind the body.
- **The blood abdomen** is the focus: a glossy, veined, dark-red sac. It starts about 2 units across and
  swells with each feeding, through 3 stages up to 3.5 units. The skin stretches and gets brighter and more
  translucent as it fills. It sloshes and wobbles in code (squash and stretch against her movement).

### Hurtboxes
| Box | Notes |
|---|---|
| Abdomen | Weak point, ×2. The bigger it gets, the bigger the target. |
| Thorax, head | ×1 |
| Wings, legs | No hurtbox. Hits pass through. |

### Air behaviour (about 75 % of the fight)
She hovers in the top third of the arena (y 11–15) with a lazy figure-eight drift and dips toward the player
before attacks. Most of the time only Spider, Dragonfly or the top platform can reach her.

1. **Proboscis Dive**
   - *Telegraph (0.7 s):* hovers in place and aims; a dotted red line runs from the proboscis to the target
     point.
   - *Attack:* dives along the line at 18 units/s and stabs into the floor or platform.
   - *Stuck (0.6 s):* proboscis in the ground, close to the ground. A short opening (abdomen in reach). Then she
     pulls out and climbs.
2. **Blood Spray**
   - *Telegraph (0.6 s):* the abdomen contracts and glows red; drops bead on its tip.
   - *Attack:* lobs a fan of blood globs with gravity: 3 in P1, 5 in P2, 7 in P3.
   - *Impact:* globs splash on landing and leave small puddles for 1.5 s that hurt on touch, marked by a
     bubbling red tint.
3. **Bog Bubbles** *(P2+, ties into Swamp's gas vents)*
   - *Telegraph:* green bubbles fizz at her spiracles (0.5 s).
   - *Attack:* releases 2–3 slow gas bubbles that drift toward the player and pop after 4 s, or when struck,
     in a small cloud. Hitting a bubble pops it safely in front of you.
4. **Larva Call** *(P3)*: shakes off 2 mosquito larvae (small 2-HP fliers that dart at the player), capped at 3
   alive.

### Landing and feeding (about 25 %)
After 2–3 air moves she lands.
- *Landing telegraph (0.6 s):* a shadow marker on the floor or the center platform where she'll touch down.
  The landing itself is gentle and deals no damage.
- **Feeding (2.5 s):** proboscis in the ground, the abdomen pulses and fills. Fully open: no contact damage,
  abdomen ×2. Feeding grows the abdomen one stage, capped at the stage for the current phase.

While grounded she has only **weak, small attacks that leave openings:**
- **Proboscis Jab:** short forward stab, 1.5 units, small box. Telegraph 0.45 s, recovery 0.8 s.
- **Wing Buffet:** a gust that pushes the player back about 3 units with no damage. Telegraph 0.4 s, recovery
  0.6 s.

She takes off after feeding plus one ground move, or right away if hit 6 times. *Take-off telegraph:* wings
blur faster for 0.4 s.

### Phases
| Phase | Abdomen | Changes |
|---|---|---|
| P1 | Stage 1 | Dive, Spray (3 globs) |
| P2 | Stage 2 | Bog Bubbles added; Spray 5 globs; lands slightly less often |
| P3 | Stage 3 | Larva Call added; Spray 7 globs; heavy, so she lands more often but attacks faster in the air |

### Death: the abdomen bursts
1. Last hit: she freezes, the abdomen swells fast (0.5 s, wobbling, veins glowing).
2. **Burst:** the sac ruptures in a big red splash. A burst sprite (3 frames), about 40 droplet particles
   arcing out, red splatter decals on nearby terrain that fade over 6 s, and a strong shake.
3. The emptied body (thorax, crumpled wings, the deflated sac part) falls, bounces once, and shrivels darker.
   Nektar drops. The reward appears.
4. The splash does no damage.

### Tests
- She spends 65–85 % of a simulated fight airborne.
- Grounded attacks have hitboxes no larger than 1.6 units and recoveries of at least 0.6 s.
- The abdomen takes ×2 and its hurtbox grows with each stage.
- A bot beats it with ordinary commands by hitting her when she lands or dives.

**Checkpoint 2:** contact sheet, then playable. Short playtest pause.

---

## 5. Caves — Centipede "Faultline"

**Feel:** it's in the walls. It erupts from the rock, crosses the arena, and dives back in. You read the
cracks to know where it's coming from. Health 50.

**Size and shape:** a chain of 16 segments behind a head.
- Head about 2.2 units with forcipules (venom claws) as separate opening parts.
- Each segment about 1.6 units wide and 1 unit tall, with a pair of legs. The tail ends in two long trailing
  cerci.
- Total length about 22 units.
- Slate-violet plates with crystal growths along the back, matching the Caves crystals. The legs ripple in a
  wave as it moves.

**Movement:**
- The head follows a path of waypoints. Segments follow the head's trail at fixed spacing, so the body traces
  exactly where the head has been.
- Inside rock, parts draw behind the terrain (sorting −5), so the rock hides them naturally. Where it enters
  or leaves rock: a crack decal, a debris burst and a small shake.
- It never digs real hexes, so the arena stays intact.

### Hurtboxes
| Box | Notes |
|---|---|
| Head | ×2 |
| Segments | ×1 each, while visible in the open (not inside rock) |
| Crystal growths on the back | Armored: clank. Still pogo-able, so you can pogo along its back. |

**Shrinking:** each 1/8 of its health lost, the tail-most segment breaks off. It cracks, falls, shatters into
crystal shards, and the centipede gets shorter. At 1 health it's the head plus 2 segments.

### Moves
1. **Burst Crossing** *(all phases)*
   - *Telegraph (0.8 s):* cracks spread and dust falls on a wall, floor or ceiling spot, picked near the
     player's line. The exit point on the far side also gets a faint crack, so you can read the line.
   - *Attack:* the head bursts out and crosses the arena in a straight line or shallow arc at 13 units/s, then
     dives into the far rock. The body streams after it for about 1.7 s; touching any segment hurts.
   - *Dodge:* step off the line, then hit segments as they pass.
2. **Wall Lunge** *(all phases)*
   - *Telegraph (0.6 s):* a crack at the player's height on the nearest side wall, with glowing eyes behind
     it.
   - *Attack:* the head and first 4 segments thrust 6 units out of the wall, and the forcipules snap.
   - *Recovery (1.2 s):* pulls back slowly. An opening on the head.
3. **Floor Lap** *(P2+)*
   - *Telegraph:* a trail of cracks runs along the floor from one wall.
   - *Attack:* it surfaces and runs the whole length of the floor, then up the far wall, across the ceiling
     and back into the rock. Its body lies low (about 1 unit) on the floor, so you jump over it as it passes;
     it's also an easy target for down-strikes.
4. **Ceiling Slam → Crystal Fall** *(P2+)*
   - *Telegraph:* the head emerges from the ceiling and hangs, swaying (0.9 s). Crystal shadows appear on the
     floor.
   - *Attack:* it slams its head into the ceiling. 4–6 crystal shards fall onto the shadow marks; each shard
     shatters.
   - *Hang recovery (1.0 s):* head dangling low enough to hit from the center platform.
5. **Coil Arc** *(P3)*: a fast double Burst Crossing, two lines in a row from different surfaces, with a 0.4 s
   gap.
6. **Rest:** after every 3 moves it pokes its head out of the floor and rests, low and vulnerable, for 1.5 s.
   This is the main opening.

### Phases
| Phase | Changes |
|---|---|
| P1 | Burst Crossing, Wall Lunge, Rest |
| P2 | Floor Lap and Crystal Fall added |
| P3 | Coil Arc added; telegraphs 15 % shorter. It's short by now, so crossings are quicker. |

### Death
The head bursts from the floor one last time, rears up, and freezes. Starting from the tail, each remaining
segment cracks and shatters in turn up the body, about 0.08 s apart. The head drops last: it hits the floor,
the forcipules splay, and its crystals dim. Shake on the head's fall. Nektar and reward.

### Tests
- Every crossing shows cracks at the entry and exit at least 0.8 s ahead.
- Segments inside rock have no active hurtbox or hitbox.
- Segment loss: 8 drops over the fight, never below the head plus 2.
- A bot beats it.

**Checkpoint 3:** contact sheet, then playable.

---

## 6. Desert — Sandworm (name TBD) and a dune arena

**Feel:** a giant you mostly see as ripples in the sand. It erupts, arcs over the arena and crashes back down.
Health 55.

### Arena changes for Desert
`BossArenaLayout.cs`

- Desert gets its own hex map: the shared hall, but the flat floor is replaced by **three rolling sand
  dunes**. These are Sand hexes 1–3 rows high on top of the bedrock floor, with gentle 1-row steps so you can
  walk over them, and troughs between them.
- Platforms stay (sandstone and sand), raised a row where a dune would crowd them.
- The worm's real path runs under the dunes and floor. When it erupts **through a dune** it digs out the sand
  hexes it breaks through (`HexTerrain.Excavate`), so the dunes get chewed up over the fight.
- **Optional extension:** a slow refill that regrows sand in craters, so the dunes don't disappear. This needs
  a new `HexTerrain.Place(cell, material)`, which doesn't exist yet. I'd build it only if the dunes flatten
  too much in play.

**Size and shape:**
- The head is a round maw about 4 units across, with three hinged jaw flaps lined with rings of inward teeth
  that open like a flower.
- The body is a chain of 10 ribbed segments tapering over about 14 units. Sun-bleached, sand-crusted hide with
  darker grooves.
- Underground it shows only as a **ripple and bulge sprite** moving along the surface, with sand trickling.

### Hurtboxes
| Box | Notes |
|---|---|
| Head | ×1 when closed |
| Open mouth (the gullet) | Weak point, ×2. Only exposed during Inhale. |
| Body segments | ×1 while above ground |
| Underground | Nothing. It can't be hit. |

### Moves
1. **Breach Arc** *(all phases)*
   - *Telegraph:* the ripple tracks to under the player, then stops; sand bubbles and a ring marker grows
     (0.8 s).
   - *Attack:* erupts straight up (mouth open, sand spray), arcs over to about 9 units high, and dives back in
     4–8 units away. The body follows the arc, and the whole arc hurts. It digs any dune hexes on the exit and
     entry points.
2. **Dune Wave** *(all phases)*
   - *Telegraph:* the ripple runs to one wall, and the sand there heaves (0.6 s).
   - *Attack:* it swims just under the surface across the arena, pushing up a travelling sand hump about 1.2
     units tall that you jump over. It reshapes the visual, not the hexes.
3. **Maw Inhale** *(P2+)*
   - *Telegraph:* surfaces near a wall with its head out and the jaws peeling open (0.7 s).
   - *Attack:* inhales for 2.5 s, pulling the player toward the mouth (a force on the motor, stronger when
     closer) with a sand stream visual. Then it chomps: a box in front of the mouth.
   - *Risk and reward:* the gullet is exposed the whole time (×2). Walk against the pull and hit it, or get
     clear.
   - *Recovery (1.0 s):* jaws hang slack. An opening.
4. **Sandstone Spit** *(P2+)*
   - *Telegraph:* the head half-surfaces and a bulge travels up its throat (0.6 s).
   - *Attack:* lobs 2–3 sandstone chunks that arc and shatter, digging a dune hex where they land.
5. **Beached** *(opening, all phases)*: about 1 in 3 Breach Arcs it lands on top of a dune instead of diving
   in, and thrashes on the surface for 1.8 s, fully hittable with no contact damage. It then wriggles back
   into the sand.
6. **Twin Breach** *(P3)*: two Breach Arcs back to back, the second aimed where the player dodged to.

### Death
A last full-height breach, frozen at the peak: the jaws spasm open, then it topples over sideways like a
felled tree and crashes across the dunes. Huge sand burst and shake. The segments sink into the sand one by
one from the tail, then the head settles half-buried and stays as scenery. Nektar and reward.

### Tests
- Underground, it has no hurtboxes and no hitboxes. The ripple shows its position at all times.
- Inhale pull is beatable by walking away for every insect (check against `RunSpeed`).
- The Desert map's dunes are walkable with ordinary jumps, and the spawn, exit and reward stand on sand.
- A bot beats it.

**Checkpoint 4:** contact sheet and arena render, then playable.

---

## 7. Alien — "The Blind Throne" (Azathoth-like)

*Name is a placeholder. Other options: "The Idiot Core", "The Piped Sovereign".*

**Feel:** cosmic horror. Its sheer bulk fills the top of the arena, and it can't really see you. It thrashes
blindly while its court of pipers keeps it placated. Health 70.

**Size and shape:**
- A seething amorphous mass hanging from the top-center of the arena, about 11 × 7 units (y ≈ 10–17), above
  the center platform. It's built from 10–14 overlapping blob parts that each pulse and wobble
  independently, so the outline never holds still.
- Dozens of **milky, blind eyes** of different sizes open and close at random across it.
- One **great central eye** stays shut until it's exposed.
- **Tentacles:** 4–6 chains of 8–12 segments each, curling with layered sine waves. They can reach the floor
  (about 14 units).
- A static aura: crackling violet sparks and occasional one-frame "glitch" slices offset sideways.
- **The Pipers:** 3 small, gangly servitors with flute-like proboscises. They drift in slow orbits around the
  mass and the platforms, playing: a note particle stream connects each to the mass.

### Core mechanic: the pipers' song (true to the Azathoth myth)
- While any piper lives, **the great eye stays shut** and the mass is armored all over: clank, no damage.
- Pipers have 3 health each and drift at reachable heights: around the platforms, the terraces and sometimes
  the floor. They don't attack, but they do dodge.
- **Kill all the pipers → the Throne wakes and convulses.** The great eye opens, and the mass sags down until
  the eye sits just above the center platform for 5 s (4 s in P3). The eye takes ×2 and the body ×1. It thrashes
  more wildly while awake (attacks speed up), so diving in is the gamble.
- Then the eye closes, it rises, and new pipers fade in. P1 has 2 pipers, P2 3, P3 4.

### Moves (fired blind; they track the player loosely because it "hears" you)
1. **Tentacle Slam** *(all phases)*
   - *Telegraph (0.8 s):* a tentacle rises and coils; its tip glows; a target marker appears on the floor or
     platform below.
   - *Attack:* it slams down vertically. The tentacle lies there 0.8 s (×1 damage, passed to the core) then
     withdraws.
2. **Tentacle Sweep** *(all phases)*
   - *Telegraph:* a tentacle draws back to one side with a glowing line along its sweep height (0.7 s). The
     height is either floor level (jump it) or platform level (drop down or stay on the floor).
   - *Attack:* it sweeps across the arena.
3. **Blind Gaze** *(all phases)*
   - *Telegraph:* 2–4 small eyes snap open and charge: a thin violet aim line toward where you were 0.5 s ago,
     0.7 s.
   - *Attack:* each fires a beam that stops at rock, reusing the Alien laser's line test and look.
4. **Static Orbs** *(P2+)*: it spits 3 slow orbs of crackling static that drift after the player for 4 s, then
   pop in a small burst. Strike one to pop it early.
5. **Unmaking Pulse** *(P3)*
   - *Telegraph:* every eye opens at once, and the palette inverts for 3 frames (1.0 s).
   - *Attack:* a ring of static expands from the mass with 2 gaps. Get to a gap, or be high on a platform
     above the ring's path.
6. **Convulse** *(when the pipers all die)*: the mass drops; for the first 0.4 s of the drop, tentacles flail
   (telegraphed by the drop itself). Then the 5 s window.

### Phases
| Phase | Pipers | Moves | Damage window |
|---|---|---|---|
| P1 | 2 | Slam, Sweep, Gaze | 5 s |
| P2 | 3 | Static Orbs added | 5 s |
| P3 | 4 | Unmaking Pulse added; attacks faster | 4 s |

### Death
The great eye stares wide, and every small eye opens. The mass shudders, then **implodes**: the blobs shrink
toward the eye while the tentacles whip and fall limp. A flash, then the inverted palette for 6 frames. A shower
of static sparks, and the great eye drops onto the center platform and dims to grey. The pipers stop
playing, drift down and dissolve. The reward appears, then the final exit opens.

### Tests
- The core absorbs every hit while a piper lives; killing all of them opens the window; pipers respawn after
  it.
- During the window the eye is reachable from the center platform with an ordinary jump and strike.
- Beam and ring telegraphs last at least 0.7 s; the ring always has 2 gaps wide enough for the player.
- A bot beats it.

**Checkpoint 5:** contact sheet, then playable.

---

## 8. Cleanup (Step 6)
- Delete the old boss styles, specs and art rows (`ChargeBoss`/`VolleyBoss`/`QuakeBoss`/`BurrowBoss`/`BeamBoss`,
  `Barkback`, `Faultback`, `Dune Maw`, `Crown Relay`, the `Actors.png` row 7 and row 2 of each world's actors
  sheet), plus the old boss code in `LevelEnemy`.
- Update `game/README.md` (each world's boss paragraph, double boss) and
  `docs/03_ITEMS_ECONOMY_AND_ENEMIES.md` (bosses section).
- Rewrite the boss parts of `FullRunVerification`, `ForestRunVerification` and the per-world verification
  files to use the new bots. Keep the full 20-level run on seed 1051.
- Each world's boss reward item stays as it is today (Forest random passive, Reed Flask, Prism Fang, Dune
  Mantle, Flux Core).

---

## 9. Order, commits, checkpoints

| Step | Work | Ends with |
|---|---|---|
| 0 | Hurtboxes; Boss, rig and effects base; projectiles; shake; boss bar and name card; double boss off; dev panel | Old bosses still work with the new bar |
| 1 | Stag beetle | **Contact sheet → your OK → playable → your playtest** |
| 2 | Mosquito | Contact sheet → playable |
| 3 | Centipede | Contact sheet → playable |
| 4 | Desert dune map and sandworm | Arena render and contact sheet → playable |
| 5 | Blind Throne | Contact sheet → playable |
| 6 | Cleanup, docs, test rewrite | Full verification green |

Commits stay small (one per piece within each step). Each commit message says what was and wasn't verified.

**Testing limits:** Unity's batch verification can't run while the editor has the project open. Compile
checks and map renders work regardless. For each boss, either close Unity so I can run the suite, or playtest
in the editor with the dev panel's BOSS row.

---

## 10. Open questions for you
1. **Names:** Old Oakhorn, Mire Bell, Faultline, (sandworm?), The Blind Throne. Keep, change, or leave to me?
2. **Fight length:** health targets aim for about 60 s (beetle) up to about 2 min (Throne) for a decent player.
   Shorter or longer?
3. **Mosquito in the air:** the plan lets her take damage in the air when you can reach her, but she's mostly
   out of reach. Should she instead be fully immune until she lands?
4. **Pipers:** is "kill the court to wake the god" the right core for the Alien fight, or do you want it to be
   damageable more directly?
5. **Desert dunes:** let the worm permanently chew them up, or build the sand-refill extension so they regrow?
