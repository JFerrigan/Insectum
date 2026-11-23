# Insectum movement lab

In Unity Hub, add this `prototype` directory from disk and launch it from the **Projects** list using Unity **6000.3.11f1**. Launching from the signed-in Hub lets it pass the current licensing connection to the Editor. Open [MovementLab.unity](Assets/MovementLab.unity) and press **Play**. The scene uses keyboard controls and placeholder pixel graphics.

| Key | Action |
| --- | --- |
| A / D or Left / Right | Move |
| Space | Jump. Hold for more height |
| X | Dig forward as Worm. Hold to repeat |
| Down + X or S + X | Dig below as Worm |
| C | Switch between Worm and the baseline mover |
| Tab | Toggle raw hexes and smoothed shelves |
| F1 | Show the generated collision outline |
| R | Reset terrain, hearts and energy |

Walk right and stop at the orange packed-dirt cell. Dig it to spend seven energy, then walk through the opening. Try digging into the purple reinforced floor to check that the energy stays unchanged. The platforms on the left let you try jump height, material costs and fall damage.

**Tools → Insectum → Verify Movement Lab** runs a scripted physics check in the editor. It checks movement across joins, the packed-dirt cost, one-cell clearance, jump height and fall damage.

## Implementation notes

- **Movement.** The motor uses a 60 Hz physics step with a circle collider, acceleration, air control, jump buffering, coyote time and a short jump on release. Treat the movement values and the two test collider sizes as tuning inputs.
- **Terrain.** Pointy-top axial cells give this version convenient horizontal rows. The smoothed mode fills valleys between adjacent, equal-height tops and merges the polygons into one collider. The overlay lets you compare that shape with the raw hex outlines. The lab rebuilds the room collider synchronously when a cell is removed. Before expanding to full levels, test region boundaries and measure rebuild time after repeated excavations.
- **Excavation.** The dig ray selects the first logical solid cell ahead or below. A successful dig removes one cell, spends its material cost and refreshes collision in the same physics step. The authored plug gives a repeatable clearance test while keeping the reinforced cells above it in place.

The next mechanical test is Beetle shell. Use the same motor and isolate impact speed, fall damage, crush hits, and charging energy only when the shell absorbs damage. For procedural terrain, start with two authored Forest chunks and validate the crossing with the shared motor before expanding the template set. Each port should be checked against the smoothed collision shape, with abilities disabled.
