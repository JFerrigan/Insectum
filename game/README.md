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
are, so the same seed always gives the same rooms. Each world has its own
themed set (Forest hollows and fungus shelves, Swamp fens and lily pads, Caves
geodes and stalactites, Desert dunes and temple halls, Alien bubbles and
pods), with traps and Nektar caches placed off the walking route. Forest
levels also put brambles on the route: up to two bramble rooms on Forest 1,
four on Forest 2, and six on Forest 3, with the first one near the start.
Touching brambles costs a heart and throws you clear, and a downward strike
(S + J in the air) bounces off them unhurt. Bramble rooms hop over floor beds,
walk under hanging thorns, leap across a thorny bowl, hop beds on both floors
of a tall hollow, or take a cliff at a run so you clear the bed below without
jumping into the thorns above. The first
chest vault in each level stands on a plug of the world's soft material with
Nektar buried in it. Level 4 is the world's boss arena. Press Esc during a run to pause and see the controls,
current item, and seed. Press Esc again or click Resume to continue. Leave
the seed field blank for a random layout, or enter a seed to replay it. Click
the seed field or press Tab to edit it. From the
results screen, press Enter to restart or Esc to return to the roster.

Press J to strike forward or W + J to strike up. K uses the selected insect's
ability. As Spider, press K to jump twice as high for 16 energy. Walk into a
wall to climb automatically for free. Release the move key to drop. As Beetle,
K toggles the shell. As Dragonfly, K toggles flight and A/D/W/S steer. Fire Ant's
K uses a heavier strike. Worm can hold K to dig ahead or S + K to dig down. Edit roster
and ability values in `Assets/Resources/CharacterTuning.asset` and material
values in `Assets/Resources/TerrainMaterials.asset`.

Use S + L to aim the Excavator Claw downward. Aim the Seed Bomb with W or S,
or face your target for a forward blast.

Defeat Barkback and take its reward to enter the four Swamp levels.
Watch for the green bubbles before a gas vent opens. The mud plug under a Swamp
chest vault costs Worm five energy. Reed Flask
adds ten energy per stack to each energy sap pickup. Defeat Mire Bell and
claim its prize to enter Caves. Look for falling crystal warnings and jump
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
radius. Heavy spawn adds enemies on traversable shelves. Double boss arenas
have two active bosses, each with its own attack warning. Defeat both to reveal
one reward, then claim it to unlock the exit.

Run the production verification from this directory on macOS.

```sh
/Applications/Unity/Hub/Editor/6000.3.11f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "$PWD" \
  -executeMethod Insectum.Game.Editor.ProductionVerification.Run \
  -logFile /private/tmp/insectum-bootstrap.log
```

Add `-insectum-tunnel-only` to run only the room-template checks, the
Forest 1 tunnel checks, the bramble checks, and the Forest 1 to 3 crossings.
The room checks generate 200 Forest 1 seeds and 300
levels across all worlds, and require every room shape, every template, the
buried plug, and at least 1.5 units of rock between unlinked rooms. The
bramble checks generate 90 Forest tunnels and require every bramble to rest on
rock clear of the landings, at most two bramble rooms per world level, and all
five bramble rooms in use. They also check bramble damage, invulnerability,
the pogo bounce, hanging thorns, and Dune Mantle. Each
world's suite crosses 12 of its tunnels and the fallback with all five insects
damage-free, opens a vault chest, digs the plug as Worm, and fights the boss.
The suite also completes the 20-level run on seed 1051 with ordinary
commands, buys Moth Eyes, and verifies all three variants.

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
