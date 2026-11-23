using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Insectum.Game.Editor.ProductionVerification;

namespace Insectum.Game.Editor
{
    internal static class ForestTunnelVerification
    {
        internal static void Verify()
        {
            using var fixture = new PhysicsFixture("Forest tunnel trial", enableAbilities: false, enableVariants: true);
            VerifySlopeRest(fixture);
            var layouts = new HashSet<string>();
            var times = new List<float>();
            int branches = 0;
            for (int seed = -16; seed < 24; seed++)
            {
                LevelLayout layout = ForestTunnelLayout.Generate(seed);
                LevelLayout duplicate = ForestTunnelLayout.Generate(seed);
                string signature = ForestGenerator.Signature(layout);
                Require(signature == ForestGenerator.Signature(duplicate) && GenerationVerification.SameMarkers(layout.Markers, duplicate.Markers),
                    $"Forest tunnel seed {seed} did not reproduce its layout.");
                layouts.Add(signature);
                VerifyGeometry(layout);

                foreach (Character character in Character.Roster)
                {
                    fixture.Session.Start(character, seed, layout);
                    bool explore = character.Insect == Insect.FireAnt;
                    int ticks = Cross(fixture, explore: explore);
                    if (explore)
                        branches += layout.Tunnel.Branches.Count;
                    else
                        times.Add(ticks * PlayerMotor.StepSeconds);
                    Require(fixture.Session.Run.Health == character.MaxHealth,
                        $"Forest tunnel seed {seed}: {character.Name} took damage using baseline movement.");
                    fixture.Step(new PlayerCommand(interactPressed: true));
                    Require(fixture.Session.Phase == RunPhase.Cleared, $"Forest tunnel seed {seed}: exit could not be used.");
                }
            }

            Require(layouts.Count >= 30, "Forest tunnel trials generated fewer than thirty distinct layouts.");
            LevelLayout fallback = ForestTunnelLayout.Generate(7, forceFallback: true);
            VerifyGeometry(fallback);
            Require(fallback.UsedFallback, "Forest tunnel test did not exercise its fallback.");
            foreach (Character character in Character.Roster)
            {
                fixture.Session.Start(character, 7, fallback);
                Cross(fixture, explore: true);
                Require(fixture.Session.Run.Health == character.MaxHealth, $"{character.Name} took damage on the tunnel fallback.");
            }

            VerifyTrialRun(fixture);
            VerifyDigging();
            VerifyExcavatorClaw();
            Debug.Log($"Insectum tunnel trial verified. {times.Count + branches / 3} damage-free baseline traversals, {layouts.Count} distinct layouts, "
                + $"{branches} side-tunnel round trips, a playable fallback for all five insects, and main routes of {times.Min():F1}–{times.Max():F1}s.");
        }

        private static void VerifySlopeRest(PhysicsFixture fixture)
        {
            const int seed = 12;
            LevelLayout layout = ForestTunnelLayout.Generate(seed);
            Character character = Character.Roster[(int)Insect.Beetle];
            foreach (int uphill in new[] { -1, 1 })
            {
                var (from, to) = layout.Tunnel.Lining.First(segment =>
                {
                    Vector2 span = segment.to - segment.from;
                    return span.x > 3f && span.y * uphill > 1.5f && span.y * uphill < span.x;
                });
                Vector2 tangent = (to - from).normalized;
                Vector2 normal = new(-tangent.y, tangent.x);
                fixture.Session.Start(character, seed, layout);
                PlayerMotor motor = fixture.Session.Motor;
                motor.Warp((from + to) / 2f + normal * (character.Radius + 0.04f));
                fixture.Advance(8);
                Vector2 start = motor.Body.position;
                fixture.Advance(11, new PlayerCommand(move: uphill));
                Require(motor.Grounded && motor.Body.position.y > start.y + 0.2f,
                    $"The player could not walk uphill toward {uphill}.");

                fixture.Advance(20);
                Vector2 stopped = motor.Body.position;
                fixture.Advance(180);
                Require(motor.Grounded && Vector2.Distance(stopped, motor.Body.position) < 0.01f,
                    $"The idle player slid on the slope facing uphill toward {uphill}: {stopped} to {motor.Body.position}.");
                fixture.Step(new PlayerCommand(jumpPressed: true, jumpHeld: true));
                Require(motor.Body.position.y > stopped.y + 0.1f,
                    $"The player could not jump from rest on the slope facing uphill toward {uphill}.");
            }
            Debug.Log("Insectum slope rest verified: walked uphill, stood still for three seconds, and jumped on both inclines.");
        }

        private static void VerifyGeometry(LevelLayout layout)
        {
            var chunks = layout.Chunks.ToDictionary(chunk => chunk.Coordinate);
            Require(chunks.Count == 37 && chunks.Values.Count(chunk => chunk.MainRoute) == 28
                && chunks.Values.Count(chunk => chunk.IsBranch) == 6 && layout.Spawn.y > layout.Exit.y,
                $"Forest tunnel seed {layout.RunSeed} missed its coverage or elevation targets.");
            foreach (MacroChunk chunk in chunks.Values)
            {
                for (int side = 0; side < HexGrid.SideCount; side++)
                {
                    if ((chunk.Ports & (MacroPort)(1 << side)) == 0)
                        continue;
                    Require(chunks.TryGetValue(HexGrid.Neighbor(chunk.Coordinate, side), out MacroChunk neighbor)
                        && (neighbor.Ports & (MacroPort)(1 << ((side + 3) % HexGrid.SideCount))) != 0,
                        $"Forest tunnel seed {layout.RunSeed} has an unpaired port at {chunk.Coordinate}.");
                }
            }

            ForestTunnelSpace tunnel = layout.Tunnel;
            Require(tunnel.Route.Zip(tunnel.Route.Skip(1), (from, to) => (from, to)).Count(tunnel.Jumps.ContainsKey) >= 2
                && layout.Markers.Count(marker => marker.Kind == LevelMarkerKind.Shortcut) == 1
                && layout.Tiles.Values.Count(material => material != TerrainMaterial.Reinforced) == 1
                && layout.Tiles[HexGrid.CellAt(layout.Markers.Single(marker => marker.Kind == LevelMarkerKind.Shortcut).Position)]
                    == TerrainMaterial.PackedDirt,
                $"Forest tunnel seed {layout.RunSeed} is missing uphill turns or its single packed-dirt plug under a chest vault.");
        }

        private static void VerifyTrialRun(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            var fireAnt = Character.Roster[(int)Insect.FireAnt];
            session.StartTunnelTrial(fireAnt, 1051);
            Require(session.Variant == LevelVariant.Night, "The tunnel trial did not use the seeded Night variant.");
            LevelFeature shop = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.Shop);
            string path = ForestReplayFile.Argument("-insectum-record-tunnel");
            if (path != null)
                fixture.Replay = new List<PlayerCommand>();
            int ticks = Cross(fixture, attack: true, explore: true);
            Require(!shop.Available && session.Run.Inventory.Active == shop.Item,
                "The ordinary tunnel trial did not earn enough Nektar and reach the side shop.");
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(session.Run is { LevelNumber: 2, Alive: true },
                "Clearing the Forest tunnel trial did not continue to Forest 2.");
            if (path != null)
                ForestReplayFile.Write(path, fireAnt.Insect, 1051, ReplayKind.Tunnel, fixture.Replay);
            fixture.Replay = null;

            int heavySeed = Enumerable.Range(0, 100).First(seed => RunWorlds.ChooseVariant(new LevelSeeds(seed, 1), 1) == LevelVariant.HeavySpawn);
            session.StartTunnelTrial(fireAnt, heavySeed);
            Require(session.Variant == LevelVariant.HeavySpawn && session.Content.Enemies.Count == 7,
                "The tunnel trial did not add two enemies for Heavy Spawn.");
            Debug.Log($"Insectum tunnel trial seed 1051 entered Forest 2 after {ticks * PlayerMotor.StepSeconds:F1}s with branch visits and ordinary attacks.");
        }

        private static void VerifyDigging()
        {
            using var fixture = new PhysicsFixture("Forest tunnel excavation");
            GameSession session = fixture.Session;
            var worm = Character.Roster[(int)Insect.Worm];
            foreach (int seed in Enumerable.Range(-16, 40).Append(1051))
            {
                session.StartTunnelTrial(worm, seed);
                Vector2 spawn = session.Layout.Spawn;
                int outward = -session.Layout.RouteDirection;
                fixture.Advance(8);
                fixture.Advance(35, new PlayerCommand(move: outward));
                Vector2 wall = session.Motor.Body.position;
                Require(Mathf.Abs(wall.x - spawn.x) < 1.85f && session.Terrain.Revision == 0,
                    $"Forest tunnel seed {seed}: the entrance is missing its solid wall.");
                for (int step = 0; step < 100 && (session.Motor.Body.position.x - wall.x) * outward < 4.5f; step++)
                    fixture.Step(new PlayerCommand(move: outward, abilityHeld: true));

                Vector2 dug = session.Motor.Body.position;
                Require((dug.x - wall.x) * outward >= 4.5f && session.Terrain.Revision >= 3
                    && Mathf.Approximately(session.Run.Energy, worm.MaxEnergy - 10f * session.Terrain.Revision),
                    $"Forest tunnel seed {seed}: Worm did not advance through reinforced rock from {wall} to {dug}, "
                    + $"after {session.Terrain.Revision} digs, energy {session.Run.Energy}. Health {session.Run.Health}.");

                session.StartTunnelTrial(worm, seed);
                fixture.Advance(8);
                float height = session.Motor.Body.position.y;
                fixture.Step(new PlayerCommand(aim: -1, abilityPressed: true));
                fixture.Advance(18);
                Require(session.Terrain.Revision == 1 && session.Motor.Body.position.y < height - 0.4f,
                    $"Forest tunnel seed {seed}: one downward dig did not remove the ground collider.");
            }
        }

        private static void VerifyExcavatorClaw()
        {
            using var fixture = new PhysicsFixture("Forest tunnel Excavator Claw");
            GameSession session = fixture.Session;
            Character beetle = Character.Roster[(int)Insect.Beetle];
            session.StartTunnelTrial(beetle, 1051);
            Require(session.Run.Inventory.Acquire(ItemId.ExcavatorClaw), "Could not equip the Excavator Claw.");
            fixture.Advance(8);

            float height = session.Motor.Body.position.y;
            fixture.Step(new PlayerCommand(aim: -1, itemPressed: true));
            fixture.Advance(18);
            Require(session.Terrain.Revision == 1 && session.Motor.Body.position.y < height - 0.4f
                && session.Run.Energy == beetle.MaxEnergy && session.Run.Inventory.ActiveCooldownSteps > 0,
                "The Excavator Claw could not open the reinforced floor without spending ability energy.");
        }

        private static int Cross(PhysicsFixture fixture, bool attack = false, bool explore = false)
        {
            GameSession session = fixture.Session;
            ForestTunnelSpace tunnel = session.Layout.Tunnel;
            fixture.Advance(8);
            int ticks = 8;
            for (int i = 0; i < tunnel.Route.Count; i++)
            {
                Vector2 here = tunnel.Route[i];
                if (explore)
                {
                    foreach (var branch in tunnel.Branches.Where(branch => branch.source == i))
                    {
                        ticks += Walk(fixture, here, branch.first, attack);
                        ticks += Walk(fixture, branch.first, branch.end, attack);
                        fixture.Step(new PlayerCommand(interactPressed: attack));
                        ticks += Walk(fixture, branch.end, branch.first, attack);
                        ticks += Walk(fixture, branch.first, here, attack);
                    }
                }

                if (i + 1 < tunnel.Route.Count)
                    ticks += Walk(fixture, here, tunnel.Route[i + 1], attack);
            }

            Require(session.AtExit, $"Forest tunnel seed {session.Run.Seed} ended beyond the exit trigger.");
            return ticks;
        }

        private static int Walk(PhysicsFixture fixture, Vector2 start, Vector2 target, bool attack)
        {
            GameSession session = fixture.Session;
            PlayerMotor player = session.Motor;
            bool jump = session.Layout.Tunnel.Jumps.TryGetValue((start, target), out Vector2 lip);
            if (jump)
                fixture.Advance(12);
            for (int step = 0; step < 310 && session.Run.Alive; step++)
            {
                Vector2 feet = player.Body.position - Vector2.up * session.Run.Character.Radius;
                if (Mathf.Abs(feet.x - target.x) <= 0.36f && Mathf.Abs(feet.y - target.y) <= 0.40f && player.Grounded)
                    return step + (jump ? 12 : 0);

                float delta = target.x - feet.x;
                int move = Mathf.Abs(delta) > 0.13f ? Math.Sign(delta) : 0;
                fixture.Step(new PlayerCommand(move: move, jumpPressed: jump && step == 0, jumpHeld: jump,
                    attackPressed: attack && step % 10 == 0));
            }

            throw new InvalidOperationException($"Forest tunnel seed {session.Run.Seed} {session.Run.Character.Name}: "
                + $"{start} to {target}, jump {jump} lip {lip}, position {player.Body.position}, hearts {session.Run.Health}.");
        }
    }
}
