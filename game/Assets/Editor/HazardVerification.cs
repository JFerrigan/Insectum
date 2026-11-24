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

        // Each world's own hazard, in its hazard rooms.
        private static readonly Dictionary<WorldId, HazardKind> Kinds = new()
        {
            [WorldId.Forest] = HazardKind.Bramble,
            [WorldId.Swamp] = HazardKind.Flytrap,
            [WorldId.Caves] = HazardKind.Spikes,
            [WorldId.Desert] = HazardKind.Cactus,
            [WorldId.Alien] = HazardKind.Laser
        };

        internal static void Verify()
        {
            int hazards = 0;
            foreach (WorldId world in Kinds.Keys)
                hazards += VerifyPlacement(world);
            VerifyBrambles();
            VerifyCacti();
            VerifySpikes();
            VerifyFlytraps();
            VerifyLasers();
            Debug.Log($"Insectum hazards verified: {hazards} hazards across 300 tunnels, each world using only its own, "
                + "floor and roof hazards resting on rock clear of every landing, lasers floating in open air, at most "
                + "two hazard rooms per world level, every hazard room in use, and brambles, cacti, spikes, flytraps "
                + "and lasers behaving.");
        }

        // Each tunnel level sets aside at most two hazard rooms per world level, all on the main route, and a
        // world's first level uses only the gentlest. Every hazard is the world's own kind. Floor and roof hazards
        // rest on rock, floor ones stay clear of the landings where checkpoints and enemies stand, and lasers
        // float clear of rock.
        private static int VerifyPlacement(WorldId world)
        {
            using var fixture = new PhysicsFixture($"{world} hazard placement", enableAbilities: false);
            ForestRoomTemplate[] rooms = ForestRoomCatalog.For(world).Where(template => template.HazardLevel > 0).ToArray();
            var used = new HashSet<string>();
            int count = 0;
            for (int seed = -10; seed < 10; seed++)
            {
                for (int level = 1; level < RunWorlds.LevelsPerWorld; level++)
                {
                    LevelLayout layout = RunWorlds.Generate(seed, RunWorlds.FirstLevel(world) + level - 1);
                    ForestTunnelSpace tunnel = layout.Tunnel;
                    string where = $"{world} seed {seed}/{level}";
                    Vector2Int[] chunks = tunnel.Hazards.Select(hazard => hazard.Chunk).Distinct().ToArray();
                    Require(chunks.Length <= ForestTunnelLayout.HazardRooms(level) && chunks.All(chunk => tunnel.Rooms[chunk].Role == RoomRole.Main),
                        $"{where} has {chunks.Length} hazard rooms, or one off the main route.");
                    foreach (Vector2Int chunk in chunks)
                    {
                        string id = layout.Chunks.Single(candidate => candidate.Coordinate == chunk).TemplateId;
                        ForestRoomTemplate template = rooms.SingleOrDefault(candidate => candidate.Id == id);
                        Require(template != null && template.HazardLevel <= level,
                            $"{where} carved hazards in {id}, which is not a {world} hazard room open on this level.");
                        used.Add(id);
                    }

                    fixture.Session.Start(Character.Roster[(int)Insect.Beetle], layout.RunSeed, layout);
                    Physics2D.SyncTransforms();
                    var landings = tunnel.Route.Concat(tunnel.Branches.SelectMany(branch => new[] { branch.first, branch.end })).ToArray();
                    foreach (TunnelHazard hazard in tunnel.Hazards)
                    {
                        count++;
                        Require(hazard.Kind == Kinds[world], $"{where} carved {hazard.Kind}, which is not a {world} hazard.");
                        if (hazard.Kind == HazardKind.Laser)
                        {
                            Require(fixture.Physics.OverlapCircle(hazard.Area.center, 0.3f) == null,
                                $"{where}: the laser at {hazard.Area.center} is set in rock.");
                            continue;
                        }
                        RequireRock(fixture, hazard, where);
                        if (hazard.Hanging)
                            continue;
                        Rect area = hazard.Area;
                        foreach (Vector2 landing in landings)
                        {
                            float apart = Mathf.Max(area.xMin - landing.x, landing.x - area.xMax);
                            Require(Mathf.Abs(landing.y - area.yMin) > 1f || apart >= LandingClearance,
                                $"{where}: {hazard.Kind} at {area} sits {apart:F2} units from the landing at {landing}.");
                        }
                    }
                }
            }

            string[] missing = rooms.Select(template => template.Id).Except(used).ToArray();
            Require(missing.Length == 0, $"{world} tunnels never used {string.Join(", ", missing)}.");
            return count;
        }

        // A floor hazard must stand on rock at both ends and its middle, and a hanging one must hang from it.
        private static void RequireRock(PhysicsFixture fixture, TunnelHazard hazard, string where)
        {
            Rect area = hazard.Area;
            Vector2 toward = hazard.Hanging ? Vector2.up : Vector2.down;
            float rootY = hazard.Hanging ? area.yMax : area.yMin;
            foreach (float x in new[] { area.xMin + 0.1f, area.center.x, area.xMax - 0.1f })
            {
                RaycastHit2D hit = fixture.Physics.Raycast(new Vector2(x, rootY - toward.y * 0.3f), toward, 0.6f);
                Require(hit.collider != null && Mathf.Abs(hit.distance - 0.3f) < 0.06f,
                    $"{where}: {hazard.Kind} at {area} does not {(hazard.Hanging ? "hang from" : "rest on")} rock at x {x:F2}.");
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

        // A cactus costs a heart and throws the player hard, and a downward strike bounces off it unhurt.
        private static void VerifyCacti()
        {
            using var fixture = new PhysicsFixture("Cactus contact", enableAbilities: false);
            GameSession session = fixture.Session;
            Character beetle = Character.Roster[(int)Insect.Beetle];
            (LevelLayout layout, int index) = Find(WorldId.Desert, 3, hazard => hazard.Kind == HazardKind.Cactus);
            Rect cactus = layout.Tunnel.Hazards[index].Area;

            session.Start(beetle, layout.RunSeed, layout);
            session.Motor.Warp(new Vector2(cactus.center.x, cactus.yMin + beetle.Radius + 0.02f));
            fixture.Step();
            Vector2 thrown = session.Motor.Body.linearVelocity;
            Require(session.Run.Health == beetle.MaxHealth - 1 && Mathf.Abs(thrown.x) > 5f && thrown.y > 4f,
                $"A cactus did not cost a heart and throw the player hard: {session.Run.Health} hearts, velocity {thrown}.");

            session.Start(beetle, layout.RunSeed, layout);
            session.Motor.Warp(new Vector2(cactus.center.x, cactus.yMax + beetle.Radius + 0.6f));
            fixture.Step();
            fixture.Step(new PlayerCommand(aim: -1, attackPressed: true));
            Require(session.Motor.Body.linearVelocity.y > 8f, "A downward strike did not pogo off the cactus.");
            fixture.Advance(12);
            Require(session.Run.Health == beetle.MaxHealth, "The pogo off the cactus still hurt.");
        }

        // Walking through a spike bed is safe. Landing on one is fatal.
        private static void VerifySpikes()
        {
            using var fixture = new PhysicsFixture("Spike contact", enableAbilities: false);
            GameSession session = fixture.Session;
            Character beetle = Character.Roster[(int)Insect.Beetle];
            (LevelLayout layout, int index) = Find(WorldId.Caves, 3,
                hazard => hazard.Kind == HazardKind.Spikes && hazard.Area.width < 2f);
            Rect bed = layout.Tunnel.Hazards[index].Area;

            session.Start(beetle, layout.RunSeed, layout);
            session.Motor.Warp(new Vector2(bed.xMin - beetle.Radius - 0.3f, bed.yMin + beetle.Radius + 0.02f));
            fixture.Advance(10);
            for (int step = 0; step < 90 && session.Motor.Body.position.x < bed.xMax + beetle.Radius + 0.1f; step++)
                fixture.Step(new PlayerCommand(move: 1));
            Require(session.Phase == RunPhase.Playing && session.Run.Health == beetle.MaxHealth
                    && session.Motor.Body.position.x >= bed.xMax + beetle.Radius + 0.1f,
                $"Walking through spikes was not safe: {session.Phase}, {session.Run.Health} hearts, at {session.Motor.Body.position}.");

            session.Start(beetle, layout.RunSeed, layout);
            session.Motor.Warp(new Vector2(bed.center.x, bed.yMin + beetle.Radius + 1.5f));
            fixture.Advance(40);
            Require(session.Phase == RunPhase.Defeated, "Landing on spikes did not kill the player.");
        }

        // An open flytrap kills what enters its mouth, then stays shut a while. Dune Mantle saves the player but
        // the trap still snaps.
        private static void VerifyFlytraps()
        {
            using var fixture = new PhysicsFixture("Flytrap contact", enableAbilities: false);
            GameSession session = fixture.Session;
            Character beetle = Character.Roster[(int)Insect.Beetle];
            (LevelLayout layout, int index) = Find(WorldId.Swamp, 3, hazard => hazard.Kind == HazardKind.Flytrap && !hazard.Hanging);
            Rect mouth = layout.Tunnel.Hazards[index].Area;
            Vector2 inside = new(mouth.center.x, mouth.yMin + beetle.Radius + 0.05f);

            session.Start(beetle, layout.RunSeed, layout);
            session.Motor.Warp(inside);
            fixture.Step();
            Require(session.Phase == RunPhase.Defeated, "An open flytrap did not kill the player.");

            session.Start(beetle, layout.RunSeed, layout);
            Require(session.Run.Inventory.Acquire(ItemId.DuneMantle), "Could not equip Dune Mantle.");
            session.Motor.Warp(inside);
            fixture.Step();
            Require(session.Phase == RunPhase.Playing && session.Run.Health == beetle.MaxHealth && session.Hazards.Shut(index),
                "Dune Mantle did not save the player from a flytrap, or the flytrap did not snap shut.");
            session.Motor.Warp(layout.Spawn + Vector2.up * (beetle.Radius + 0.06f));
            fixture.Advance(200);
            Require(!session.Hazards.Shut(index), "The flytrap did not open again after chewing.");
        }

        // A laser kills what its beam touches.
        private static void VerifyLasers()
        {
            using var fixture = new PhysicsFixture("Laser contact", enableAbilities: false);
            GameSession session = fixture.Session;
            Character beetle = Character.Roster[(int)Insect.Beetle];
            (LevelLayout layout, int index) = Find(WorldId.Alien, 1, hazard => hazard.Kind == HazardKind.Laser);
            session.Start(beetle, layout.RunSeed, layout);
            session.Motor.Warp(layout.Tunnel.Hazards[index].Area.center);
            fixture.Step();
            Require(session.Phase == RunPhase.Defeated, "A laser beam did not kill the player.");
        }

        // The first generated level of the world level with a hazard that matches, and that hazard's index.
        private static (LevelLayout layout, int index) Find(WorldId world, int level, System.Predicate<TunnelHazard> wanted)
        {
            for (int seed = 0; seed < 200; seed++)
            {
                LevelLayout layout = RunWorlds.Generate(seed, RunWorlds.FirstLevel(world) + level - 1);
                int index = layout.Tunnel.Hazards.FindIndex(wanted);
                if (index >= 0)
                    return (layout, index);
            }
            throw new System.InvalidOperationException($"No {world} level {level} in 200 seeds has the wanted hazard.");
        }

        private static Rect? FloorBed(LevelLayout layout) => layout.Tunnel.Hazards
            .Where(hazard => !hazard.Hanging && hazard.Area.width >= 1.4f).Select(hazard => (Rect?)hazard.Area).FirstOrDefault();

        private static Rect? RoofBed(LevelLayout layout) => layout.Tunnel.Hazards
            .Where(hazard => hazard.Hanging).Select(hazard => (Rect?)hazard.Area).FirstOrDefault();
    }
}
