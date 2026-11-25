# Insectum

One run spans 20 levels across Forest, Swamp, Caves, Desert, and Alien.
Each world ends with a boss arena. Health, Nektar, and items carry between
levels, and ability energy refills on entry.

Open this directory as a project in Unity 6000.3.11f1. Open
`Assets/Scenes/Boot.unity` and press Play. Choose an insect with the mouse or
A/D, then press Enter. Use A/D to walk, Space to jump, and E at the marked exit.
Hold Space for a higher jump.
Touching a live enemy costs one heart and knocks you back. Hard landings after
a fall of about 7.4 world units cost one heart.

Levels 1 to 3 of every world are winding tunnels. Each 37-chunk board has a
28-room main route and three side tunnels: one leads to a shop and two to
chest vaults. Every room is built from a template that matches where its doors
are, so the same seed always gives the same rooms. A suite template can carve
several route rooms as one big set piece, at most one per level. Forest has
two: the Great Root Stair joins two straight descents into a huge sloping
cavern of root terraces, and the Sunken Glade Valley turns a dip in the route
into a wide basin between two ordinary rooms. Each world has its own
themed set (Forest hollows and fungus shelves, Swamp fens and lily pads, Caves
geodes and stalactites, Desert dunes and temple halls, Alien bubbles and
pods), with traps and Nektar caches placed off the walking route. Every world
also puts its own hazard on the route: up to two hazard rooms on a world's
first level, four on its second, and six on its third, with the first one near
the start.

- Forest brambles cost a heart and throw you clear. Hop over floor beds, walk
  under hanging thorns, leap a thorny bowl, or take a cliff at a run.
- Swamp flytraps kill whatever enters their open mouths, enemies included, then
  stay shut for three seconds. Some hang from low roofs, so do not jump under
  them.
- Caves spikes kill you if you land on them, but you can walk through them.
  Falling into the spike chasm is fatal.
- Desert cacti cost a heart and throw you hard. A pit full of them bounces you
  around until you climb out.
- Alien lasers sweep around their emitters and kill what they touch, enemies
  included. Their beams stop at rock. Wait for a gap, then run.

A downward strike (S + J in the air) bounces off brambles and cacti unhurt, and
Dune Mantle blocks any hazard hit, deadly ones included.

The first chest vault in each level stands on a plug of the world's soft material with
Nektar buried in it. Level 4 is the world's boss arena: one sealed hall, about 50 units wide and 18 tall,
walled in bedrock on every side. The camera can show more rock above the ceiling and below the floor.
A flat floor runs between two stepped terraces, and platforms of the world's diggable materials climb
one hex row at a time to a bedrock perch in the middle, so an ordinary jump reaches every tier.
Two higher islands need Spider's leap or Dragonfly's flight. Edit the hall's hex map in
`Assets/Scripts/BossArenaLayout.cs`. Press Esc during a run to pause and see the controls,
current item, and seed. Press Esc again or click Resume to continue. Leave
the seed field blank for a random layout, or enter a seed to replay it. Type
digits at character select to enter one, and Backspace to erase. Click the seed
field or press Tab to select it. From the
results screen, press Enter to restart or Esc to return to the roster. Tick
Boss rush at character select to play only the five boss arenas, Forest through
Alien. Health and items carry from one fight to the next. Press Esc or click
Back to return to the main menu.

Press J to strike forward or W + J to strike up. K uses the selected insect's
ability. As Spider, press K to jump twice as high for 16 energy. Walk into a
wall to climb automatically for free. Release the move key to drop. As Beetle,
K toggles the shell. As Dragonfly, K toggles flight and A/D/W/S steer. Fire Ant's
K uses a heavier strike. Worm can hold K to dig ahead or S + K to dig down. Edit roster
and ability values in `Assets/Resources/CharacterTuning.asset` and material
values in `Assets/Resources/TerrainMaterials.asset`.

Use S + L to aim the Excavator Claw downward. Aim the Seed Bomb with W or S,
or face your target for a forward blast.

Forest's boss is Old Oakhorn, a stag beetle (30 health), with a full-width
health bar along the bottom of the screen. The bar has ticks where its
second and third phases begin, and its name card shows while it rears up
at the start. Every attack has a warning first:

- **Charge:** it scrapes a foot, crouches low, its jaws glow amber, and it
  flashes white just before it runs. Only its head and jaws hurt while it charges, so
  back off and jump over them, pogo off its back, or get up a terrace.
  If it reaches the end of the floor, its jaws stick in the terrace for a
  moment. That's the safe time to hit it, and its head takes double damage.
- **Snap:** it snaps at anyone close in front.
- **Rearing snap:** it rears up and snaps overhead at anyone above it, or
  after two quick pogos.
- **Body slam:** if you dig into the floor under it, or strike up at it from
  beneath, it rises high on straight legs, quivers and glows amber all over,
  then belly-flops. The slam reaches 3.4 units down into the floor under its
  whole body, so digging under it is no hiding place.
- **Flying** (at least 10 s apart): it either hovers over you behind a red
  target and slams down, sending a jumpable shockwave each way, or makes
  Air Gore passes. On the platforms it settles to your height, its jaws
  glow, and it rams across; on the floor it flies high and drops bark with
  shadows beneath.

Touching it hurts, except while it's stuck or recovering, when it looks
dull. Its hurtboxes are fitted to each pose's art and reach a little past it,
while contact stays a little inside, so a hit that looks like it lands does.
`boss_forest_pixels.py` prints the fitted circles and draws them over the art
in `Builds/BossPreviews/forest_hurtboxes.png`. Its jaws are armored, so hits
there clank (a downward strike still bounces off them). When it dies it falls to its knees, then onto its belly,
before the reward appears. Take the reward to enter the four Swamp levels.
Watch for the green bubbles before a gas vent opens. The mud plug under a
Swamp chest vault costs Worm five energy.

Mire Lich guards level 8 with 40 health. You can hit her whenever you can
reach her. Her abdomen takes double damage and grows when she finishes
feeding. The dive and landing lock a shadow on the terrain. Run out of the
dive path, use platforms as cover from blood globs, and jump the puddles.
Strike Bog Bubbles to pop them safely, and step out of each larva's warned
dart. On the ground, back away from the jab, gust, or Subsurface Probe.
Touching her is safe while she feeds or recovers. Once her abdomen bursts
and her body settles, collect four Nektar near the body and claim the Reed
Flask to unlock the exit to Caves. Reed Flask adds ten energy per stack to
each energy sap pickup.

Look for falling crystal warnings and jump
over Faultback's ground waves. The soft-stone plug costs Worm twelve energy.
Prism Fang adds 0.35 world units per stack to melee reach.

Faultback's reward opens Desert. Watch the cinder jets for an amber warning
and step off the mark before Dune Maw resurfaces. The sand plug costs Worm
three energy. Dune Mantle blocks one trap hit per stack on each level.

Dune Maw's reward opens Alien. Watch for sparks before a static column fires,
and strike Crown Relay's exposed core after the beam sweep. The alien-organic
plug costs Worm ten energy. Flux Core restores six energy per stack on each
enemy defeat. Take Crown Relay's reward and enter the final exit to finish
the run.

The seed also selects level variants. Night limits visibility around the
insect. Each Moth Eyes adds 1.5 world units to the starting 2.7-unit light
radius. Heavy spawn adds enemies on traversable shelves. Boss arenas never get
a variant: double boss is switched off until it has a design for the new
bosses.

Run the production verification from this directory on macOS.

```sh
/Applications/Unity/Hub/Editor/6000.3.11f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "$PWD" \
  -executeMethod Insectum.Game.Editor.ProductionVerification.Run \
  -logFile /private/tmp/insectum-bootstrap.log
```

Add `-insectum-tunnel-only` to run only the room-template checks, the
Forest 1 tunnel checks, the hazard checks, and the Forest 1 to 3 crossings.
The room checks generate 200 Forest 1 seeds and 300
levels across all worlds, and require every room shape, every template, the
buried plug, and at least 1.5 units of rock between unlinked rooms. The
hazard checks generate 60 tunnels per world and require each world to use only
its own hazard, floor and roof hazards to rest on rock clear of the landings,
lasers to float clear of rock, at most two hazard rooms per world level, and
every hazard room in use. They also check bramble and cactus hits and pogo
bounces, walking through and landing on spikes, flytraps snapping and
reopening, lasers, and Dune Mantle. Walkers jump at each room's marked
take-off points and wait at each laser room's marked spot for a clear run. Each
world's suite crosses 12 of its tunnels and the fallback with all five insects
damage-free, opens a vault chest, digs the plug as Worm, and fights the boss.
The suite also completes the 20-level run on seed 1051 with ordinary
commands, buys Moth Eyes, and verifies the night and heavy-spawn variants.

If Unity waits for licensing, copy the `LicenseClient` channel name from the
signed-in editor log and pass it with `-licensingIpc`.


## macOS build

Install macOS Build Support for Unity 6000.3.11f1. Run this from `game/`.

```sh
/Applications/Unity/Hub/Editor/6000.3.11f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath "$PWD" -buildTarget StandaloneOSX \
  -executeMethod Insectum.Game.Editor.BuildInsectum.Mac \
  -logFile /private/tmp/insectum-mac-build.log
```

The output is `Builds/Insectum.app`. Add `-insectum-tunnel-trial-build` to
write `Builds/Insectum Tunnel Trial.app`. The game opens in fullscreen. The
world renders at 480×270 and fills the available 16:9 area with point filtering.
Other aspect ratios use letterboxing. With Pillow
installed, run the matching `*_pixels.py` script in `ArtSource` to
regenerate that world's textures in `Assets/Resources/PixelArt`.
`item_pixels.py` generates the icons used in shops and the pause menu.
`sprite_style.py` provides shared shading and outlines, and `sprite_preview.py`
writes a comparison sheet to `Builds/SpritePreviews/`.
`boss_forest_pixels.py` draws Old Oakhorn's frames, its jaw glow and the
shared boss effects, and writes a contact sheet to `Builds/BossPreviews/`.
`boss_swamp_pixels.py` draws Mire Lich's three stages, generates the fitted
circles, and writes a contact sheet, overlay, and death preview to that folder.

## Boss check (no Unity needed)

`docs/09_BOSS_DESIGN_HANDOFF.md` is the recipe for building a boss. `tools/compilecheck.sh` compiles every
game and editor script against the installed Unity's DLLs, which works while the editor has the project
open.

`tools/bosscheck.sh [seeds]` steps the reworked bosses' fight logic
(`Assets/Scripts/Bosses`) under plain .NET, like the map generator. It
checks that each attack's warning lasts at least 24 steps with nothing live
during it, and that openings never hurt on contact. It reports the time
airborne per phase, checks the boss stays in the hall, and has a scripted
player win a fight per seed (default 12), reporting fight length and hits
taken by move. Pass `mire` after the seed count to run just the Swamp
boss checks. In the game, the dev panel's BOSS row forces any move, jumps
to a phase, freezes the AI, draws hurtboxes and hitboxes, or kills the boss.

## Map generator (no Unity needed)

`tools/map.sh` renders any seed's levels to PNG plus a text listing, using
the game's own generator source compiled under plain .NET. It works while the
Unity editor has the project open, takes about a second per level and needs
only the `dotnet` SDK (9.0+). Agents can use it to check generation changes
without launching Unity. Run it from the repo root:

```sh
tools/map.sh                  # random seed, level 1
tools/map.sh 1234             # seed 1234, level 1
tools/map.sh 1234 7           # seed 1234, level 7 (1-20; 4, 8, 12, 16, 20 are boss arenas)
tools/map.sh random all       # random seed, all 20 levels
tools/map.sh 1234 13 10       # level 13 at 10 px per world unit (default 6)
```

For each level it writes `game/Builds/Maps/seed<N>_level<NN>.png` and `.txt`,
prints the text listing and opens the PNGs. Agents that only need the data
can skip the image viewer and call the program directly:

```sh
dotnet build tools/MapGen/MapGen.csproj -v q -o tools/MapGen/bin/out
dotnet tools/MapGen/bin/out/MapGen.dll <seed|random> [level|all] [zoom] [outDir]
```

`tools/chunks.sh [world|all] [filter] [zoom]` renders every room template
(once per room shape it fits) and every multi-chunk suite on its own. Each is
cropped from a real generated level into `game/Builds/Chunks/<World>/`, with
an `index.txt` giving the template, shape, seed, level and chunks for each, so
any example can be reopened in full with `tools/map.sh <seed> <level>`. It
exits 2 if a template never appears in 400 seeds, which makes it a check
that new templates are reachable. `tools/map.sh -h` and `tools/chunks.sh -h`
print usage, as does `MapGen.dll -h`.

`MapGen.dll stats [seedCount] [firstSeed]` (default 200 seeds from 1) skips
the PNGs. It prints how often multi-chunk rooms (suites) spawn per world,
the suites-per-level and suite-size distributions, and which suite templates
were picked. 200 seeds takes about 2 minutes. Each level's `.txt` also has a
`MULTI-CHUNK ROOMS` section.

It exits non-zero when a level fails to generate and prints the exception to
stderr, so it can also serve as a quick generation smoke test (for example
`random all` in a loop).

**Image:** up is up. Dark is open air (cells with no tile, or inside a
tunnel passage). Muted colors are rock, one hue per `TerrainMaterial`. Grey
outlines are the tunnel lining, yellow is the main route and white the side
branches. Pale lines are chunk borders, and a pink outline surrounds each multi-chunk room, with no line between its members. Green dot is spawn, red is exit, small blue dots are checkpoints.
Markers are encounter red, trap orange, shop cyan, treasure gold, boss
magenta, branch/shortcut white.

**Text listing:** a header with seed, level name, variant, fallback flag,
size and marker counts, then spawn, exit, bounds, route direction and tile
count. `CHUNKS` lists each macro chunk as `(q, r)`, its role
(main/branch/filler), template id and ports, plus its tunnel room
role/shape. Under each chunk are its checkpoint, markers and hazards with
world positions. `ROUTE`, `BRANCH` and `LEAP` lines follow for tunnel
levels. Boss arenas have no chunks, so their markers appear under
`(no chunk)`.

**How it works:** `tools/MapGen/MapGen.csproj` links the generator files from
`Assets/Scripts` and defines `MAPGEN`. `tools/MapGen/Unity.cs` supplies small
stand-ins for the UnityEngine types they use (`Vector2`, `Vector2Int`,
`Rect`, `Mathf`, `Resources.Load` reading `Assets/Resources` from disk).
Unity-only code in `RunWorlds`, `LevelHazards`, `ForestTunnelSpace` and
`PixelViewport` is fenced with `#if !MAPGEN`. When a generator change pulls
in a new file, add it to the csproj. If that file touches Unity runtime
types, fence those parts with `#if !MAPGEN` or add the missing member to
`Unity.cs`. Keep stand-ins faithful to Unity's math, because the output
must match the game seed for seed.

The Unity editor has the same view under **Insectum → Map Viewer**.

## Chunk Editor

**Insectum → Chunk Editor** makes and edits rooms (one chunk) and suites (2–4
chunks) without writing code. Rooms are saved as JSON in
`Assets/Resources/Rooms/<World>/<id>.json`. The generator merges them into that
world's rooms or suites, and they show up in levels, `tools/map.sh` and
`tools/chunks.sh`. Commit the JSON files like any other asset.

- **New chunk:** pick a world, a name, and either the profiles (room shapes)
  the room fits or a suite size. A suite starts as a chain of switchbacks,
  the most common route run, and its footprint (entry doors, steps, exit
  doors) can be changed under Room settings.
- **Edit a saved chunk:** reopens any JSON file in `Resources/Rooms`.
- **Edit a built-in chunk:** opens any code room (one of its profiles) or
  suite as data. It's captured by recording the builder calls it makes in a
  generated level. Saving with the same id replaces the built-in room in the
  game; give it a new id to make a new room from it.

**Tools:**
- Select (drag handles; ctrl-click a cave to add a point; Delete removes)
- Cave (floor and ceiling lines)
- Platform (Islet, Pad, Slab, Crystal, Pod)
- Hall
- Landing, Treasure, Trap
- Enemy (a named regular, or the world's usual pick)
- Hazard (bramble, spikes, flytrap, cactus; shift-click to hang it from the roof)
- Laser
- Door (click a door circle to toggle it)
- Tile (paints the rock material of hex cells)
- Erase

Scroll zooms and right- or middle-drag pans. Undo and redo cover every change,
and Cmd/Ctrl-S saves.

The canvas replays the room through the real `ForestRoomBuilder`, so the dark
air is exactly what the game carves. The checks panel flags:
- missing landings
- landings or door anchors not in open air
- doors the chosen profiles or footprint need
- floors steeper than 42°
- drops of 5 units or more
- single-room caves outside the hexagon (x ±8.3, y ±7.2)

Walkability still needs a playtest or the production verification run.
**Preview in level** saves, finds the first seed whose generation uses the
room and opens it with `tools/map.sh`. **Play here** saves, then enters Play
mode with dev mode on at that seed and level.

**Format:** `RoomData` (`Assets/Scripts/RoomData.cs`) is a list of ops, each
one `ForestRoomBuilder` call in its chunk's local units: `cave`, `hall`,
`door`, `landing`, `treasure`, `trap`, `enemy`, `hazard`, `laser`, `wait`,
`leap`, `tile`. Two commands help scripts and agents:
- `dotnet tools/MapGen/bin/out/MapGen.dll export <world> <templateId> [shape]`
  prints a built-in room as this JSON.
- `MapGen.dll roundtrip` checks that every built-in room converts to data and
  carves identically. All 185 currently do.

## Desktop replay and terrain profile

Add `-insectum-record-run /private/tmp/run.replay` to the production verification
command to save its full 20-level playthrough. Build with
`-insectum-development` to produce `Builds/Insectum Dev.app`. From `game/`,
launch the recorded commands in the graphical development player.

```sh
open -n -a "$PWD/Builds/Insectum Dev.app" --args \
  -insectum-replay /private/tmp/run.replay -insectum-render-fps 30 \
  -logFile /private/tmp/insectum-replay-30.log
open -n -a "$PWD/Builds/Insectum Dev.app" --args \
  -insectum-replay /private/tmp/run.replay -insectum-render-fps 120 \
  -logFile /private/tmp/insectum-replay-120.log
```

The replay feeds normal commands at 60 Hz, independent of the render target.
It logs each level and the final result. For smaller replays, the verification
command also accepts `-insectum-record-forest /private/tmp/forest.replay` and
`-insectum-record-dig /private/tmp/dig.replay`.

For the 60 FPS terrain benchmark, close any replay players first and launch
the graphical development player with the sample count.

```sh
open -n -a "$PWD/Builds/Insectum Dev.app" --args \
  -insectum-profile-terrain 80 -logFile /private/tmp/insectum-terrain-profile.log
```

The benchmark uses generated scenes with their normal art, collision, and
mesh rebuilding. It reports separate CPU timings for cross-region digs and
blasts, plus rendered frame intervals with and without edits. Frame intervals
include rendering, frame pacing and OS scheduling.
