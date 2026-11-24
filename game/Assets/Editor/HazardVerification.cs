using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Insectum.Game.Editor.ProductionVerification;

namespace Insectum.Game.Editor
{
    internal static class HazardVerification
    {
        // Enemies sway about half a unit around a landing, and Heavy Spawn adds one about a unit to its side.
        private const float LandingClearance = 1.9f;

        internal static void Verify()
        {
            int beds = VerifyPlacement();
            VerifyBrambles();
            Debug.Log($"Insectum brambles verified: {beds} bramble patches across 90 Forest tunnels rest on rock clear of "
                + "every landing, one hazard room per world level at most, all four bramble rooms in use, and contact, "
                + "invulnerability, pogo, roof thorns and Dune Mantle behave.");
        }

        // Each Forest tunnel level sets aside at most one hazard room per world level, all on the main route,
        // and a world's first level uses only the gentlest. Every bramble rests on rock, and floor brambles stay
        // clear of the landings where checkpoints and enemies stand.
        private static int VerifyPlacement()
        {
            using var fixture = new PhysicsFixture("Bramble placement", enableAbilities: false);
            var used = new HashSet<string>();
            int beds = 0;
            for (int seed = -15; seed < 15; seed++)
            {
                for (int level = 1; level < RunWorlds.LevelsPerWorld; level++)
                {
                    LevelLayout layout = RunWorlds.Generate(seed, level);
                    ForestTunnelSpace tunnel = layout.Tunnel;
                    string where = $"Forest seed {seed}/{level}";
                    Vector2Int[] rooms = tunnel.Hazards.Select(hazard => hazard.Chunk).Distinct().ToArray();
                    Require(rooms.Length <= level && rooms.All(chunk => tunnel.Rooms[chunk].Role == RoomRole.Main),
                        $"{where} has {rooms.Length} hazard rooms, or one off the main route.");
                    foreach (Vector2Int chunk in rooms)
                    {
                        string id = layout.Chunks.Single(candidate => candidate.Coordinate == chunk).TemplateId;
                        ForestRoomTemplate template = ForestBrambleRooms.Templates.SingleOrDefault(candidate => candidate.Id == id);
                        Require(template != null && template.HazardLevel <= level,
                            $"{where} carved hazards in {id}, which is not a bramble room open on this level.");
                        used.Add(id);
                    }

                    fixture.Session.Start(Character.Roster[(int)Insect.Beetle], seed, layout);
                    Physics2D.SyncTransforms();
                    var landings = tunnel.Route.Concat(tunnel.Branches.SelectMany(branch => new[] { branch.first, branch.end }));
                    foreach (TunnelHazard hazard in tunnel.Hazards)
                    {
                        RequireRock(fixture, hazard, where);
                        beds++;
                        if (hazard.Hanging)
                            continue;
                        Rect area = hazard.Area;
                        foreach (Vector2 landing in landings)
                        {
                            float apart = Mathf.Max(area.xMin - landing.x, landing.x - area.xMax);
                            Require(Mathf.Abs(landing.y - area.yMin) > 1f || apart >= LandingClearance,
                                $"{where}: brambles at {area} sit {apart:F2} units from the landing at {landing}.");
                        }
                    }
                }
            }

            string[] missing = ForestBrambleRooms.Templates.Select(template => template.Id).Except(used).ToArray();
            Require(missing.Length == 0, $"Forest tunnels never used {string.Join(", ", missing)}.");
            foreach (WorldId world in new[] { WorldId.Swamp, WorldId.Caves, WorldId.Desert, WorldId.Alien })
                Require(RunWorlds.Generate(5, RunWorlds.FirstLevel(world) + 2).Tunnel.Hazards.Count == 0,
                    $"{world} carved brambles, which belong to the Forest.");
            return beds;
        }

        // A floor bramble must stand on rock at both ends and its middle, and a hanging one must hang from it.
        private static void RequireRock(PhysicsFixture fixture, TunnelHazard hazard, string where)
        {
            Rect area = hazard.Area;
            Vector2 toward = hazard.Hanging ? Vector2.up : Vector2.down;
            float rootY = hazard.Hanging ? area.yMax : area.yMin;
            foreach (float x in new[] { area.xMin + 0.1f, area.center.x, area.xMax - 0.1f })
            {
                RaycastHit2D hit = fixture.Physics.Raycast(new Vector2(x, rootY - toward.y * 0.3f), toward, 0.6f);
                Require(hit.collider != null && Mathf.Abs(hit.distance - 0.3f) < 0.06f,
                    $"{where}: brambles at {area} do not {(hazard.Hanging ? "hang from" : "rest on")} rock at x {x:F2}.");
            }
        }

        private static void VerifyBrambles()
        {
            using var fixture = new PhysicsFixture("Bramble contact", enableAbilities: false);
            GameSession session = fixture.Session;
            Character beetle = Character.Roster[(int)Insect.Beetle];
            LevelLayout layout = Enumerable.Range(0, 200).Select(candidate => RunWorlds.Generate(candidate, 3))
                .First(candidate => FloorBed(candidate) != null && RoofBed(candidate) != null);
            int seed = layout.RunSeed;
            Rect bed = FloorBed(layout).Value;
            Rect roof = RoofBed(layout).Value;
            Vector2 inBed = new(bed.center.x, bed.yMin + beetle.Radius + 0.02f);

            // Touching a bed costs one heart and throws the player up, then invulnerability covers a second touch.
            session.Start(beetle, seed, layout);
            session.Motor.Warp(inBed);
            fixture.Step();
            Require(session.Run.Health == beetle.MaxHealth - 1 && session.Motor.Body.linearVelocity.y > 5f,
                $"A bramble bed did not cost a heart and throw the player up: {session.Run.Health} hearts, "
                + $"velocity {session.Motor.Body.linearVelocity}.");
            session.Motor.Warp(inBed);
            fixture.Advance(10);
            Require(session.Run.Health == beetle.MaxHealth - 1, "Brambles ignored the player's invulnerability.");
            fixture.Advance(40);
            Require(session.Run.Health < beetle.MaxHealth - 1, "Standing in brambles stopped hurting after invulnerability ended.");

            // A downward strike that reaches the bed bounces the player off it unhurt.
            session.Start(beetle, seed, layout);
            session.Motor.Warp(new Vector2(bed.center.x, bed.yMax + beetle.Radius + 0.6f));
            fixture.Step();
            fixture.Step(new PlayerCommand(aim: -1, attackPressed: true));
            Require(session.Motor.Body.linearVelocity.y > 8f, "A downward strike did not pogo off the brambles.");
            fixture.Advance(12);
            Require(session.Run.Health == beetle.MaxHealth && session.Motor.Body.position.y > bed.yMax + beetle.Radius + 0.6f,
                "The pogo off the brambles still hurt or did not rise.");

            // Thorns hanging from a roof throw the player down and away.
            session.Start(beetle, seed, layout);
            session.Motor.Warp(new Vector2(roof.center.x, roof.yMin - beetle.Radius + 0.12f));
            fixture.Step();
            Require(session.Run.Health == beetle.MaxHealth - 1 && session.Motor.Body.linearVelocity.y < 0f,
                "Hanging brambles did not cost a heart and throw the player down.");

            // Dune Mantle blocks a bramble as it blocks any trap.
            session.Start(beetle, seed, layout);
            Require(session.Run.Inventory.Acquire(ItemId.DuneMantle), "Could not equip Dune Mantle.");
            session.Motor.Warp(inBed);
            fixture.Step();
            Require(session.Run.Health == beetle.MaxHealth, "Dune Mantle did not block the brambles.");
        }

        private static Rect? FloorBed(LevelLayout layout) => layout.Tunnel.Hazards
            .Where(hazard => !hazard.Hanging && hazard.Area.width >= 1.4f).Select(hazard => (Rect?)hazard.Area).FirstOrDefault();

        private static Rect? RoofBed(LevelLayout layout) => layout.Tunnel.Hazards
            .Where(hazard => hazard.Hanging).Select(hazard => (Rect?)hazard.Area).FirstOrDefault();
    }
}
