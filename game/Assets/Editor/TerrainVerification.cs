using System.Collections.Generic;
using UnityEngine;
using static Insectum.Game.Editor.ProductionVerification;

namespace Insectum.Game.Editor
{
    internal static class TerrainVerification
    {
        internal static void Verify()
        {
            using var fixture = new PhysicsFixture("Terrain verification");
            VerifyWormShortcut(fixture);
            VerifyWormEnergyAndAim(fixture);
            VerifyBoundariesAndExplosions(fixture);
            Debug.Log("Insectum terrain verified: Worm shortcut, downward aim, material costs, seam travel, boundary rebuild, and explosions.");
        }

        private static void VerifyWormShortcut(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            string replayPath = ForestReplayFile.Argument("-insectum-record-dig");
            if (replayPath != null)
                fixture.Replay = new List<PlayerCommand>();
            session.Start(Character.Roster[4], 552);
            fixture.Advance(8);

            Rigidbody2D worm = session.Motor.Body;
            for (int step = 0; step < 100 && worm.position.x > -12.75f; step++)
                fixture.Step(new PlayerCommand(move: -1));
            fixture.Advance(14);
            fixture.Advance(90, new PlayerCommand(move: 1));

            HexTerrain terrain = session.Terrain;
            Vector2Int plug = ForestRoute.ShortcutPlug;
            float plugX = HexGrid.Center(plug).x;
            Require(worm.position.x < plugX - 1f && worm.position.x > plugX - 1.4f,
                $"The authored shortcut entrance did not stop Worm at the packed dirt plug. Position {worm.position}.");
            Require(terrain.Cells[plug] == TerrainMaterial.PackedDirt && session.Run.Energy == 100f,
                "An intact shortcut consumed energy or changed its plug.");

            int cellsBefore = terrain.Cells.Count;
            int revisionBefore = terrain.Revision;
            fixture.Step(new PlayerCommand(move: 1, abilityPressed: true));

            Require(session.Run.Energy == 93f && terrain.Cells.Count == cellsBefore - 1 && !terrain.Cells.ContainsKey(plug)
                && terrain.Revision == revisionBefore + 1, "A single Worm command did not remove exactly the packed cell for 7 energy.");
            Require(worm.linearVelocity.x > 0f, "Worm remained blocked by the removed plug in the next physics step.");

            for (int step = 0; step < 300 && worm.position.x < 14.95f; step++)
                fixture.Step(new PlayerCommand(move: 1));
            Require(worm.position.x >= 14.95f, $"The shortcut stopped Worm before the final climb at {worm.position}.");

            fixture.Step(new PlayerCommand(move: 1, jumpPressed: true, jumpHeld: true));
            fixture.Advance(35, new PlayerCommand(move: 1, jumpHeld: true));
            for (int step = 0; step < 90 && !session.AtExit && session.Phase == RunPhase.Playing; step++)
                fixture.Step(new PlayerCommand(move: 1));
            Require(session.AtExit && session.Run.Health == session.Run.Character.MaxHealth,
                $"Worm could not reach the exit unharmed through the one-cell shortcut. Position {worm.position}.");
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(session.Phase == RunPhase.Cleared, "The shortcut did not lead to an operable exit.");
            if (replayPath != null)
            {
                ForestReplayFile.Write(replayPath, Insect.Worm, 552, ReplayKind.Authored, fixture.Replay);
                fixture.Replay = null;
            }
        }

        private static void VerifyWormEnergyAndAim(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            Character worm = Character.Roster[4];
            session.Start(worm, 553);
            fixture.Advance(8);
            fixture.Step(new PlayerCommand(abilityPressed: true));
            fixture.Advance(9);
            fixture.Step(new PlayerCommand(aim: -1, abilityPressed: true));
            Require(session.Terrain.Revision == 0 && session.Run.Energy == 100f,
                "Aiming into air or reinforced terrain changed Worm energy or occupancy.");

            session.Start(worm, 554);
            Require(session.Run.TrySpendEnergy(94f), "The insufficient-energy fixture could not spend 94 energy.");
            Vector2 block = HexGrid.Center(ForestRoute.ShortcutPlug);
            session.Motor.Body.position = new Vector2(block.x - 1.22f, -1.66f);
            fixture.Step(new PlayerCommand(abilityPressed: true));
            Require(session.Terrain.Revision == 0 && session.Run.Energy == 6f
                && session.Terrain.Cells.ContainsKey(ForestRoute.ShortcutPlug),
                "A Worm with only 6 energy altered a 7-energy tile.");

            session.Start(worm, 555);
            Vector2Int entry = ForestRoute.ShortcutEntry;
            session.Motor.Body.position = HexGrid.Center(entry) + Vector2.up * (1f + worm.Radius + 0.03f);
            fixture.Step(new PlayerCommand(aim: -1, abilityPressed: true));
            Require(session.Run.Energy == 96f && !session.Terrain.Cells.ContainsKey(entry),
                "Downward aim did not remove the soft dirt beneath Worm for 4 energy.");

            session.ReturnToSelection();
        }

        private static void VerifyBoundariesAndExplosions(PhysicsFixture fixture)
        {
            var layout = new Dictionary<Vector2Int, TerrainMaterial>();
            var splitNeighbor = new Vector2Int(7, 0);
            var cut = new Vector2Int(8, 0);
            for (int q = 4; q <= 12; q++)
                layout.Add(new Vector2Int(q, 0), TerrainMaterial.Reinforced);
            layout[cut] = TerrainMaterial.PackedDirt;

            var looseDirt = new Vector2Int(6, 4);
            var looseSand = new Vector2Int(7, 4);
            var floatingHardStone = new Vector2Int(8, 4);
            var floatingReinforced = new Vector2Int(9, 4);
            layout.Add(looseDirt, TerrainMaterial.SoftDirt);
            layout.Add(looseSand, TerrainMaterial.Sand);
            layout.Add(floatingHardStone, TerrainMaterial.HardStone);
            layout.Add(floatingReinforced, TerrainMaterial.Reinforced);

            var terrain = new HexTerrain(fixture.World.transform, layout, TerrainMaterials.Load());
            Require(HexGrid.RegionAt(splitNeighbor) != HexGrid.RegionAt(cut) && terrain.HasShelfCap(splitNeighbor),
                "The boundary fixture did not generate a smoothed shelf across two regions.");

            Character beetle = Character.Roster[1];
            var actor = new GameObject("Boundary walker");
            actor.transform.SetParent(fixture.World.transform, false);
            actor.transform.localPosition = HexGrid.Center(new Vector2Int(5, 0)) + Vector2.up * (1f + beetle.Radius + 0.02f);
            var motor = new PlayerMotor(actor, beetle.Radius, fixture.Surface);
            Physics2D.SyncTransforms();
            float worstHeightChange = 0f;
            float previousY = motor.Body.position.y;
            for (int step = 0; step < 88; step++)
            {
                motor.Step(new PlayerCommand(move: 1));
                fixture.Simulate();
                worstHeightChange = Mathf.Max(worstHeightChange, Mathf.Abs(previousY - motor.Body.position.y));
                previousY = motor.Body.position.y;
            }

            Require(motor.Body.position.x > HexGrid.Center(cut).x + 1.5f && worstHeightChange < 0.04f,
                $"The motor snagged or bobbed at the smoothed region join. Position {motor.Body.position}, change {worstHeightChange:F4}.");

            Vector2 ray = HexGrid.Center(cut) + Vector2.up * 3f;
            Require(fixture.Physics.Raycast(ray, Vector2.down, 5f).collider != null,
                "Before digging, the region boundary tile had no collider.");
            var updates = new HashSet<Vector2Int>();
            terrain.RegionChanged += position => updates.Add(position);

            IReadOnlyList<TerrainEdit> reported = null;
            terrain.Edited += edits => reported = edits;
            int energy = 7;
            bool Spend(float cost)
            {
                if (cost > energy)
                    return false;
                energy -= (int)cost;
                return true;
            }

            Require(terrain.Excavate(cut, Spend) == ExcavationResult.Removed && energy == 0,
                "An exactly affordable boundary dig did not succeed.");
            Require(updates.Contains(HexGrid.RegionAt(cut)) && updates.Contains(HexGrid.RegionAt(splitNeighbor))
                && !terrain.HasShelfCap(splitNeighbor), "The adjoining shelf cap remained after the boundary dig.");
            Require(fixture.Physics.Raycast(ray, Vector2.down, 5f).collider == null,
                "A ghost collider remained immediately after the boundary dig.");
            Require(reported != null && reported.Count == 1 && reported[0].Cell == cut
                && reported[0].Cause == TerrainEditCause.Excavation, "The boundary dig emitted an incorrect edit.");

            Vector2 blast = HexGrid.Center(floatingHardStone);
            RaycastHit2D before = fixture.Physics.Raycast(blast + Vector2.up * 2f, Vector2.down, 2f);
            int revision = terrain.Revision;
            Require(terrain.Explode(blast, 4f, 1) == 2 && terrain.Revision == revision + 1,
                "The blast did not remove both loose cells in one revision.");
            Require(reported.Count == 2 && reported[0].Cell == looseDirt && reported[1].Cell == looseSand
                && reported[0].Cause == TerrainEditCause.Explosion && reported[1].Cause == TerrainEditCause.Explosion,
                "Blast edits did not report the removed cells in a stable order.");
            Require(terrain.Cells.ContainsKey(floatingHardStone) && terrain.Cells.ContainsKey(floatingReinforced)
                && !terrain.Cells.ContainsKey(looseDirt) && !terrain.Cells.ContainsKey(looseSand),
                "Blast resistance or reinforcement was ignored.");
            Require(terrain.Explode(HexGrid.Center(floatingReinforced), 0.1f, int.MaxValue) == 0,
                "Reinforced terrain survived only because of its blast resistance.");

            for (int step = 0; step < 120; step++)
                fixture.Simulate();
            RaycastHit2D after = fixture.Physics.Raycast(blast + Vector2.up * 2f, Vector2.down, 2f);
            Require(before.collider != null && after.collider != null && Mathf.Abs(before.point.y - after.point.y) < 0.001f,
                "The floating hex moved or lost its collision after an adjacent explosion.");
        }
    }
}
