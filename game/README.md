# Insectum

One run spans 20 levels across Forest, Swamp, Caves, Desert, and Alien.
Each world ends with a boss arena. Health, Nektar, and items carry between
levels, and ability energy refills on entry.

Open this directory as a project in Unity 6000.3.11f1. Open
`Assets/Scenes/Boot.unity` and press Play. Choose an insect with the mouse or
A/D, then press Enter. Use A/D to walk, Space to jump, and E at the marked exit.
Hold Space for a higher jump. Descend the Forest ledges toward the amber exit.
If there is a green marker above the exit, three ordinary jumps reach the side
pocket. Leave the seed field blank for a random layout, or enter the seed from
the HUD to replay it. Click the seed field or press Tab to edit it. From the
results screen, press Enter to restart or Esc to return to the roster.

Press J to strike forward or W + J to strike up. K uses the selected insect's
ability. As Spider, hold W + K while facing a wall to climb. As Beetle, K toggles
the shell. As Dragonfly, K toggles flight and A/D/W/S steer. Fire Ant's K uses a
heavier strike. Worm can hold K to dig ahead or S + K to dig down. Edit roster
and ability values in `Assets/Resources/CharacterTuning.asset` and material
values in `Assets/Resources/TerrainMaterials.asset`.

Use S + L to aim the Excavator Claw downward. Aim the Seed Bomb with W or S,
or face your target for a forward blast.

Defeat Barkback and take its reward to enter the four Swamp levels.
Watch for the green bubbles before a gas vent opens. The mud plug in a Swamp
side pocket can be jumped or excavated for five energy as Worm. Reed Flask
adds ten energy per stack to each energy sap pickup. Defeat Mire Bell and
claim its prize to enter Caves. Look for falling crystal warnings and jump
over Faultback's ground waves. Worm can clear the soft-stone side passage
for twelve energy. Prism Fang adds 0.35 world units per stack to melee reach.

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

The suite verifies the authored two-chunk crossing, the generated routes for
100 Forest seeds with all five insects, and the fallback. After changing Forest
templates, add `-insectum-bake-forest` to run every candidate through Unity
physics and regenerate `Assets/Resources/ForestCertificates.txt`. The Swamp
suite verifies 100 seeds, the mud shortcut and its ordinary jump route, and
the boss reward. Use `-insectum-bake-swamp` to regenerate its certificates
after editing the Swamp templates. The Caves suite likewise checks 100
seeds, its stone shortcut, and the boss encounter. Use
`-insectum-bake-caves` after editing Caves templates. Desert uses the same
100-seed sweep with `-insectum-bake-desert` to refresh its certificates.
Alien follows the same procedure with `-insectum-bake-alien`. The suite also
completes the 20-level run on seed 1051 with ordinary commands, buys Moth
Eyes, and verifies all three variants.

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

The output is `Builds/Insectum.app`. The scene renders to 320×180 and scales
in whole pixels. Larger windows use letterboxing when needed. With Pillow
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
