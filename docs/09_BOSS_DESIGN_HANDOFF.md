# Boss Design Handoff

**For:** whoever builds the next boss, person or agent.
**Status (November 2026):** Forest's Old Oakhorn (stag beetle) is the finished reference boss. Swamp, Caves,
Desert and Alien still run the old one-move bosses. Their agreed designs are in
`docs/08_BOSS_REWORK_PLAN.md`, sections 4–7. This doc covers **how** to build one so it comes out as
well as the beetle did. The plan covers **what** each boss does.

Read in this order:
1. Sections 1–2 here, so you know the designer's preferences and the workflow.
2. The plan's section for your boss.
3. The recipe (section 4) as you work.

---

## 1. The designer's preferences (decided)

These came from the designer while building the beetle. Treat them as rules.

- **A boss is tailored to its world**, with its own size, shape, and a few telegraphed key moves.
  Mostly grounded or mostly airborne is a per-boss choice. The beetle stays mostly on the ground; the
  mosquito is meant to be mostly in the air.
- **Full-width health bar** at the bottom, with only the boss's name above it.
- **No epithets or tag lines.** The name card shows the name and nothing else. ("Elder of the Hollow"
  was removed as cheesy.)
- **Hits must land where they look like they land.** Hurtboxes are fitted to the art and reach a little
  *past* it. Contact damage sits a little *inside* it. The designer prefers erring generous to the
  player.
- **Telegraphs should come from the boss itself:** its pose, its glowing attacking part, the white
  flash. Ground shadows and targets are fine. Both of the beetle's dotted lines (Air Gore's flight height
  and the charge's floor scuffs) were removed at the designer's request. **Ask before adding any abstract line or UI-style marker.** The plan still
  shows some (the mosquito's dotted dive line, the Throne's aim lines), so confirm those first.
- **The designer specifies death animations.** Ask if the plan doesn't say. The beetle drops to its
  knees, pauses, then falls flat onto its belly. The mosquito's blood-filled abdomen must burst.
- **Double boss is off** until it has its own design.

## 2. The workflow that worked

1. **Spitball first.** Offer 2–3 concepts per boss with shape, moves, an arena twist and art notes, and
   mark a pick. The designer chooses and adds details.
2. **Write a detailed plan for approval** (this is what `docs/08` is). Give each move a telegraph, an
   active window, a recovery, a dodge and what changes by phase. Then list the open questions.
3. **Build in this order** (section 4): art, then fitted hurtboxes, then logic, then a headless check,
   then the view, then wiring, then the Unity bot, then docs.
4. **Send the contact sheet as soon as it exists,** while you keep working, so the art can be
   redirected early.
5. **Stop at a playtest checkpoint** after each boss. Feedback so far was small and specific
   (hurtbox size, tag line, one marker), and each fix was a single commit.
6. **Commit small,** one piece per commit. Each message says what was and wasn't verified. Stage
   explicit paths only.

---

## 3. How a boss is built (architecture)

Boss logic is **headless**: no rendering types, so it runs under plain .NET in `tools/bosscheck.sh`. The
view reads the logic each fixed step and draws it.

| Piece | File | Role |
|---|---|---|
| Logic base | `Scripts/Bosses/Boss.cs` | See below |
| Boss logic | `Scripts/Bosses/StagBeetle.cs` | Acts, move picks, phases, poses, hitboxes, death |
| Boss view | `Scripts/Bosses/StagBeetleView.cs` | See below |
| Effects | `Scripts/Bosses/BossFx.cs` | Pooled dust, sparks and chips; shared sprites from `BossFx.png` |
| Wash shader | `Resources/BossFlash.shader` | Lerps a sprite toward a color: white for hits, amber for glows |
| Level handle | `Scripts/LevelEnemy.cs` | Wraps a boss's logic and view; the rest of the game sees a `LevelEnemy` |
| Level glue | `Scripts/LevelContent.cs` | `MainBoss`, shake forwarding, Nektar and reward after `DeathFinished` |
| Camera shake | `GameSession.Shake` / `ShakeOffset`, `GameBoot.LateUpdate` | |
| Arena | `Scripts/BossArenaLayout.cs` | The sealed hall's hex map, constants, spawn, exit and camera bounds |
| HUD | `Scripts/ForestScreen.cs` | `DrawBossBar`, `DrawNameCard`, `DrawHitboxes` (dev overlay) |
| Dev controls | `Scripts/DevMode.cs` | BOSS row: force move, phase 2/3, freeze, show boxes, kill |
| World hookup | `Scripts/ForestContent.cs` | `EnemySpec` with `EnemyStyle.ArenaBoss` and a `BossKind`; `AddEnemy(..., seed:)` |
| Art | `ArtSource/boss_forest_pixels.py` | See below |
| Headless check | `tools/MapGen/BossCheck.cs`, `tools/bosscheck.sh` | See below |
| Unity bot | `Editor/BossFightBot.cs` | Fights the boss with ordinary commands in Unity's verification |

**The logic base (`Boss.cs`)** handles:
- the modes: intro, fighting, dying, dead
- phases at ⅔ and ⅓ health
- hurtboxes, padded 3 px, and contact circles at 85 %
- hitboxes, and resolving them against the player
- guards during the intro, roars and death
- seeded picks, cues, shake requests, pogo-spam detection and dev hooks

**The boss view (`StagBeetleView.cs`)** handles:
- frame playback for each act
- the jaw glow, the white or amber wash, and the opening and death tints
- the shadow and the dive target
- the shockwaves and falling bark
- turning cues into effects

**The art script (`boss_forest_pixels.py`)** makes:
- the parametric beetle's frames and the glow mask
- `BossFx.png`
- the contact sheet
- the fitted hurtboxes and their overlay (`Builds/BossPreviews/`)

**The headless check (`BossCheck.cs`)** covers warnings, openings, padding, charge height, bounds, air
time and the scripted fights.

### Conventions inside a boss's logic
- **Acts.** An `Act` enum holds every state. `Enter(act, steps)`, `ActSteps`, `ActProgress` and `Done`
  drive them. Use `int.MaxValue` for "until a condition" acts (a run until the wall, a fall until the
  floor).
- **Moves** are picked in `Choose()` by `Pick(weights...)`. **Map the pick index to the dev move order
  explicitly.** (The beetle once had them out of step.)
- **Position** is the ground point under the body's middle, carried into the air when flying. Parts are
  placed in the boss's own frame with `AddPart(x forward, y up, radius, multiplier, armored)`.
- **Guards.** The player's strike runs *before* the boss's step each frame. So set `Invulnerable` and
  similar guards at the **end** of the step, for the steps after it.
- **Cues** (dust, a wall hit, a landing, a clank) pile up until the view takes them with `ClearCues()`.
  Don't clear them at the start of a step, or clanks from the player's strike are lost.
- **Determinism.** Use `NextRandom` and `Pick` only (they're seeded from the layout). Never use
  `Time.time` or `UnityEngine.Random`, and step only on the fixed 60 Hz step.
- **Phase changes** happen at the next `Choose()` with a roar (about 1 s, guarded), never mid-move.

---

## 4. Recipe for the next boss

**0. Generalize what the beetle left hard-coded.** Do this first, as its own commits.
- `LevelEnemy`'s constructor always builds a `StagBeetle`, and its view field is a `StagBeetleView`.
  Switch on `spec.Kind` and give views a small `IBossView { void Update(); }`.
- `BossCheck`'s attack and warning sets and its `Fighter` are beetle-only. Either add per-boss check
  classes, or give `Boss` generic flags (`InWarning`, `Attacking`, `Opening`, `Airborne`) so the
  warning, opening and bounds checks run for every boss.
- The art script mixes general helpers into the beetle code. Move `rotate`, `ellipse`, the `Pose`
  pattern, `contact_sheet`, `hurtbox_sheet`, `effects` and the shell-only coverage measure into
  `ArtSource/boss_pixels.py`.
- The mosquito needs `BossProjectile` (plan section 2.4). Its blood globs arc under gravity and splash.

**1. Design sheet.** Fill in, per move:
- warning steps (**at least 24**; `Boss.MinimumWarningSteps`)
- active steps and recovery steps
- the hitbox shape
- the dodge for every insect
- which phase it appears in, and what changes by phase

Also write down:
- the hurtbox layout, armor and weak points
- the openings: contact is off and the boss is tinted dull
- the death animation
- **The arithmetic for every jump-over** (section 5). Get the designer's OK.

**2. Art.** Write `ArtSource/boss_<world>_pixels.py`.
- Make a parametric pose, a list of frames per animation, a glow mask for the attacking part, and a
  contact sheet with the player at 1× for scale. Send the sheet at once.
- Draw in a y-up local frame and convert to PIL's y-down only at the end. The beetle's shell first
  rendered upside down from mixing the two up: top arcs are angles 0–180 in y-up.
- Keep the outline (INK), but lift the palette well off the dark backdrops. The first beetle pass
  vanished; legs and rims needed lighter colors.
- Draw every frame crisply; don't rotate sprites at runtime.

**3. Fit the hurtboxes to the art.** Print `AddPart` lines per logical pose from representative frames.
Measure coverage of the solid body (no legs or membranes) by the padded circles. **Aim for 95 % or
more.** Draw the overlay and paste the printed lines into the logic's `PoseParts`. Never place hurtboxes
before the art exists. The beetle's first set sat half a unit low and was hard to hit.

**4. Logic.** Write `Scripts/Bosses/<Name>.cs` on `Boss`. Give it intro, fight, death, `Hold`,
`MoveNames`, `ForceMove` and `PoseParts`, plus properties the view needs (act, progress, glow, markers,
hazards).

**5. Headless check.** Add the boss to `tools/MapGen/MapGen.csproj` and extend `BossCheck`. It must
check:
- warnings and nothing live during them
- safe openings and padding
- staying in the hall
- air time per phase
- a scripted fight on 12 seeds

Iterate there; it's seconds per run. Use the hits-by-move breakdown as the balance signal. When a dodge
seems impossible, write a throwaway experiment: sweep the jump timing against a forced move and print
which timings clear it. That's how the beetle's charge window was found and fixed.

**6. View.** Write `Scripts/Bosses/<Name>View.cs`.
- Its frame-range table must match the art script's frame order exactly; there's a comment on both.
- Hit flash is a white wash (4 steps); a telegraph is an amber glow on the attacking part, then a white
  wash on the last 3 warning steps.
- Openings get a dull tint. Death fades to a dim palette and the corpse stays.
- Sorting order: the body draws at 5, glow at 6, effects at 7 and floor markers at 3–4. Terrain is at 0
  and the backdrop at −20. Anything at −5 is hidden behind rock (for burrowers).

**7. Wire it in.**
- Add a `BossKind` value, the factory case, and the world's `EnemySpec`
  (`EnemyStyle.ArenaBoss`, health, Nektar, radius, tint, kind).
- In `Populate`, call `AddEnemy(spec, marker.Position, 0, seed: layout.Seeds.Encounters)`.
- Keep `spec.Name` the same as the logic's `Name`.

**8. Unity bot.** Add a fight method to `Editor/BossFightBot.cs`, ported from the headless fighter's
policy, and route the world's verification through it.

**9. Docs.** Add the boss's paragraph to `game/README.md` (moves and their warnings, openings, armor,
death), a status note in `docs/08`, and anything new for this doc.

**10. Run** `tools/compilecheck.sh` and `tools/bosscheck.sh` (and `tools/map.sh` if the arena changed),
then hand over for a playtest.

---

## 5. Numbers to design against

**Timing:** a fixed 60 Hz step (`PlayerMotor.StepSeconds`). 24 steps is 0.4 s.

**Screen:** 30 × 16.9 world units at 16 px per unit. The world renders at 480 × 270 and the HUD at
320 × 180.

**Player:**

| What | Value |
|---|---|
| Radius | 0.43 (Worm 0.34) |
| Run speed | 6.5 u/s |
| Jump | Speed 12.5, gravity 38, so the rise peaks at **2.06 units** |
| Spider's leap | About 4.1 units |
| Dragonfly flight | 8 u/s |
| Pogo bounce | 10.5 speed, about 1.45 units more |
| Hearts | Spider 3, Beetle 6, Dragonfly 2, Fire Ant 4, Worm 4 |
| Invulnerability after a hit | 36 steps |
| Strike | Reach 1.3, half-width 0.38, 1 damage, 16-step recovery |
| Fire Ant heavy strike | Reach 2.1, half-width 0.83, 2 damage, 24-step recovery |
| Fall damage | After about 7.4 units |

**Arena** (`BossArenaLayout`):

| What | Value |
|---|---|
| Floor surface | y = 1 |
| Lane ends (terrace faces) | x = ±17.32 |
| Ceiling underside | y = 18.5 |
| Boss start | (8, 1) |
| Player spawn | (−15.5, 1) |
| Exit | (16, 1.55) |
| Camera view | x ±27.2, y −0.9 to 20.6 |

The arena's platform tops (row *r*'s top is 1.5*r* + 1):

| Platform | Top (y) | x range |
|---|---|---|
| Terrace steps | 2.5, 4.0, 5.5 | against each wall |
| Tier A | 7.0 | ±11.3 to ±19.9 |
| Tier B | 8.5 | ±3.5 to ±10.4 |
| Bedrock perch | 10.0 | ±2.6 |
| High islands (Spider or Dragonfly only) | 11.5 | ±12.1 to ±15.6 |

**Boss constants (`Boss.cs`):**

| What | Value |
|---|---|
| Minimum warning | 24 steps |
| Hurt padding | 3/16 unit |
| Contact circles | 85 % of the art |
| Pogo-spam window | 90 steps |
| Boss invulnerability after a hit | 9 steps |

**Jump-over math.** A jump's height over time is h(t) = 12.5t − 19t². To clear a hitbox whose top is H
above the floor, the time in the air above it is **√(156.25 − 76H) / 19 seconds** (0.46 s at H = 1.04).
The time needed is (damaging length + 0.86) ÷ closing speed. Keep needed time ≤ 0.8 × airtime for a
standing jump.

Examples from the beetle:
- **Charge, head and jaws only.** (2.7 + 0.86) / 11 = 0.32 s against 0.46 s. Clearable standing,
  easier running toward it.
- **Charge, whole 6.3-unit body.** 0.65 s. Impossible standing, which is why only the leading part
  rams.
- **Floor-height dash.** 5.5 + 0.86 at 18 u/s is 0.35 s against about 0.37 s. Too tight, so it became
  a high bombing run.

**Budgets that felt right on the beetle:**
- Airborne 13 / 16 / 23 % by phase for a "mostly grounded" boss.
- 30 health for the first boss. A cautious bot that only strikes during openings needs about 130 s;
  players are faster.
- Openings of 0.6–1.6 s.
- Air moves at least 10 s apart.

---

## 6. Lessons (each cost a fix)

| Problem | Rule now |
|---|---|
| Hurtboxes placed before the art sat half a unit low | Fit from the art, measure coverage, pad 3 px |
| Contact matching the art made a long body unjumpable | Only the leading part rams; do the jump math |
| A floor-height dash couldn't be reliably jumped | Change the move's shape (bombing run), not just its numbers |
| The shell rendered upside down (y-up versus y-down arcs) | Draw in y-up local coordinates; check the contact sheet at 3–4× |
| Dev "force move" picked the wrong move | Map pick indices to `MoveNames` explicitly |
| The phase roar was hittable on its first frame | Set guards at the end of the step |
| Clank sparks from the player's strike vanished | Cues pile up until the view takes them |
| A `const Step` clashed with the `Step()` method | Name the constant `StepSeconds` |
| The scripted player stood too close and couldn't dodge a charge | Bots react to the warning: back off in the wind-up, jump on the flash, cross over toward the back |
| The fight ran long | Tune health from the bot's fight length; 36 became 30 |
| Abstract markers (the dotted line) annoyed the designer | Telegraph with the body, glow, flash and ground shadows; ask first |

---

## 7. Testing and playtesting

```bash
tools/compilecheck.sh
```

```bash
tools/bosscheck.sh
```

```bash
tools/map.sh 1234 4 10
```

- **`tools/compilecheck.sh`** compiles every game and editor script against Unity's DLLs. It works
  while the editor is open.
- **`tools/bosscheck.sh [seeds]`** runs the headless logic checks and scripted fights.
- **`tools/map.sh 1234 4 10`** renders an arena (levels 4, 8, 12, 16 and 20).
- **Unity's batch verification** (see `game/README.md`) needs the editor **closed**. It runs
  `BossFightBot`. **Not yet run for the beetle**: the editor was open throughout.
- **To playtest,** tick dev mode at character select, open the DEV panel, and press BOSS LVL. Use the
  BOSS row (cycle with `<` `>`, run with GO): force each move, jump to phase 2 or 3, freeze, SHOW BOXES
  (green hurtboxes, grey armor, yellow weak points, pink contact and live hitboxes) and KILL (to watch
  the death).
- New PNGs in `Resources/PixelArt` get their `.meta` from Unity on import; `ForestTextureImport` sets
  point filtering. Commit them once Unity has created them.

## 8. Definition of done (per boss)

- [ ] The designer signed off the design, the art contact sheet and the death animation.
- [ ] No tag line, and no abstract markers the designer hasn't approved.
- [ ] Hurtboxes are fitted from the art, cover at least 95 %, are padded 3 px, with contact at 85 %;
      the overlay image is regenerated.
- [ ] Every move warns for at least 24 steps with nothing live during the warning, and the jump-over math
      is written down.
- [ ] Openings exist after big moves, with contact off and the dull tint.
- [ ] Each of the five insects has a way through every move.
- [ ] `tools/bosscheck.sh` passes: 12/12 scripted wins, sensible fight length and hits by move, and air
      time on budget.
- [ ] `tools/compilecheck.sh` passes, and so does Unity's batch verification when the editor is free.
- [ ] Death animation, then Nektar and reward, then the exit.
- [ ] The dev BOSS row lists every move.
- [ ] The README paragraph, the `docs/08` status and this doc (if something new was learned) are
      updated.
- [ ] Small commits that say what was and wasn't verified, then a playtest checkpoint with the designer.

## 9. Open items

- Unity batch verification with `BossFightBot` has never run. (Unity's own editor compile of the beetle
  code is clean, and its textures imported with point filtering.)
- Swamp, Caves, Desert and Alien still use the old bosses (`EnemyStyle` ChargeBoss through BeamBoss, in
  `LevelEnemy`). The plan's cleanup step deletes them once all four are replaced.
- The plan's open questions:
  - boss names
  - whether the mosquito can be hurt in the air
  - the Throne's piper mechanic
  - whether the dunes regrow
- Possibly the Swamp dive line and the Throne aim lines, given the designer's feedback on markers.
