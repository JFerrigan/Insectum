using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Insectum.Game.ForestRoomCatalog;

namespace Insectum.Game
{
    // Suites are set pieces that carve several route chunks as one room. Each member keeps its own landing, so
    // checkpoints, encounters and the verification walker still move landing to landing. Away from the edge it
    // shares with another member, a suite stays inside each member's hexagon, as single rooms do.
    internal static class ForestSuites
    {
        // Two straight descents in a row: a huge sloping cavern with root terraces stepping down through both.
        internal static readonly ForestRoomTemplate GreatRootStair = new("Forest.Suite.GreatRootStair", 1,
            new SuiteFootprint(RoomDoor.HighLeft, new[] { 1 }, RoomDoor.LowRight), CarveGreatRootStair);

        // Down into a chunk and back up out of it: a wide sunken glade between two ordinary rooms. The way in
        // may come from either left door and the way out may leave by either right door.
        internal static readonly ForestRoomTemplate SunkenGladeValley = new("Forest.Suite.SunkenGladeValley", 1,
            new SuiteFootprint(new[] { RoomDoor.HighLeft, RoomDoor.LowLeft }, new[] { 1, 0 },
                new[] { RoomDoor.HighRight, RoomDoor.LowRight }), CarveSunkenGladeValley);

        // Chains of switchbacks stacked down and back, the commonest runs on any route: two chunks fit about 97%
        // of levels, three about 95% and four about 60%. Built only from caves and platforms, so every world
        // carves them, each with its own platforms and name.
        private static ForestRoomTemplate Gorge(WorldId world) => new($"{world}.Suite.SwitchbackGorge", 1,
            new SuiteFootprint(RoomDoor.HighLeft, new[] { 3 }, RoomDoor.LowRight),
            room => CarveChain(room, 2, RoomDoor.LowRight, world));

        private static ForestRoomTemplate Cascade(WorldId world, string name) => new($"{world}.Suite.{name}", 1,
            new SuiteFootprint(RoomDoor.HighLeft, new[] { 3, 1 }, RoomDoor.LowLeft),
            room => CarveChain(room, 3, RoomDoor.LowLeft, world));

        private static ForestRoomTemplate Descent(WorldId world, string name) => new($"{world}.Suite.{name}", 1,
            new SuiteFootprint(RoomDoor.HighLeft, new[] { 3, 1, 3 }, RoomDoor.LowRight),
            room => CarveChain(room, 4, RoomDoor.LowRight, world));

        private static readonly ForestRoomTemplate[] Forest =
        {
            GreatRootStair, SunkenGladeValley, Gorge(WorldId.Forest),
            Cascade(WorldId.Forest, "RootCascade"), Descent(WorldId.Forest, "GreatRootDescent")
        };
        private static readonly ForestRoomTemplate[] Swamp =
            { Gorge(WorldId.Swamp), Cascade(WorldId.Swamp, "LilyCascade"), Descent(WorldId.Swamp, "MireDescent") };
        private static readonly ForestRoomTemplate[] Caves =
            { Gorge(WorldId.Caves), Cascade(WorldId.Caves, "CrystalCascade"), Descent(WorldId.Caves, "CrystalDescent") };
        private static readonly ForestRoomTemplate[] Desert =
            { Gorge(WorldId.Desert), Cascade(WorldId.Desert, "RuinCascade"), Descent(WorldId.Desert, "SunkenStairDescent") };
        private static readonly ForestRoomTemplate[] Alien =
            { Gorge(WorldId.Alien), Cascade(WorldId.Alien, "PodCascade"), Descent(WorldId.Alien, "HiveDescent") };

        internal static IReadOnlyList<ForestRoomTemplate> For(WorldId world) => world switch
        {
            WorldId.Forest => Forest,
            WorldId.Swamp => Swamp,
            WorldId.Caves => Caves,
            WorldId.Desert => Desert,
            WorldId.Alien => Alien,
            _ => Array.Empty<ForestRoomTemplate>()
        };

        // Enter high in the upper chunk and walk off one root terrace after another: down to the first landing,
        // down again, along a long root slope across into the lower chunk, off its top terrace to the second
        // landing, and off a last ledge to the lower door. Every drop lands where running off it carries the
        // player, and a root islet floats over each landing with Nektar for anyone who jumps up to it.
        private static void CarveGreatRootStair(ForestRoomBuilder room)
        {
            Vector2 b = room.MemberCenter(1);
            // The lower chunk sits 1.5 units higher in one mirroring than the other, so the middle terrace follows
            // it halfway and the root slope stays walkable either way.
            float middle = -0.9f + 0.5f * (b.y + 7.5f);
            Vector2[] floor =
            {
                new(-DoorX, DoorY), new(-3.0f, DoorY), new(-3.0f, 1.6f), new(0.8f, 1.6f), new(0.8f, middle),
                new(3.6f, middle), new(b.x - 5.0f, b.y + 3.0f), new(b.x - 1.8f, b.y + 3.0f), new(b.x - 1.8f, b.y - 0.5f),
                new(b.x + 2.2f, b.y - 0.5f), new(b.x + 2.2f, b.y - DoorY), new(b.x + DoorX, b.y - DoorY)
            };
            // The roof stays under each member's hexagon and dips beneath the corner the two share with their
            // upper neighbor.
            Vector2[] roof =
            {
                new(-DoorX, 7.2f), new(4.15f, 7.2f), new(6.0f, 3.9f), new(7.6f, 1.0f), new(b.x - 4.6f, b.y + 6.6f),
                new(b.x + 2.2f, b.y + 6.9f), new(b.x + 4.2f, b.y + 4.0f), new(b.x + DoorX, b.y + 1.0f)
            };
            room.Cave(Profile(floor, roof), Islet(-1.6f, 1.0f, 5.4f, 0.7f), Islet(b.x - 1.0f, b.x + 1.6f, b.y + 4.6f, 0.6f));
            room.SetLanding(0, new Vector2(-0.4f, 1.6f));
            room.SetLanding(1, new Vector2(b.x + 0.8f, b.y - 0.5f));
            room.Treasure(new Vector2(-0.3f, 5.4f));
            room.Treasure(new Vector2(b.x + 0.3f, b.y + 4.6f));
            room.Door(0, RoomDoor.HighLeft, Clearance);
            room.Door(1, RoomDoor.LowRight, Clearance);
        }

        // The first chunk is the way in: the Tumbling Glade from a high door or a level hall from a low one, both
        // ending at the floor of the door toward the glade. From there a short ledge drops into a wide basin
        // across the middle chunk, with a root islet floating over it holding Nektar for climbers and fliers.
        // A long bank climbs out into the last chunk, which is the way out: the Sunlit Rise to a high door, with
        // its jump from the landing, or a level hall to a low one. Each end reuses its single-room template on
        // that member.
        private static void CarveSunkenGladeValley(ForestRoomBuilder room)
        {
            Vector2 b = room.MemberCenter(1);
            Vector2 c = room.MemberCenter(2);
            if (room.Has(0, RoomDoor.HighLeft))
                CarveTumblingGlade(room);
            else
                CarveLevelHall(room, false);

            // The middle chunk sits 1.5 units lower in one mirroring, so the basin follows it halfway. The drop
            // stays under 5 units and the climb out under 37 degrees either way.
            float basin = -8.2f + 0.5f * (b.y + 7.5f);
            Vector2[] floor =
            {
                new(DoorX, -DoorY), new(7.0f, -DoorY), new(7.0f, basin), new(b.x + 1.0f, basin), new(c.x - DoorX, c.y - DoorY)
            };
            // The roof stays under the chunk the three members wrap around, and rises to meet the last chunk.
            Vector2[] roof =
            {
                new(DoorX, 0.5f), new(7.0f, 0f), new(8.4f, b.y + 6.6f), new(b.x + 3.5f, b.y + 6.6f),
                new(c.x - 6.5f, c.y + 1.0f), new(c.x - DoorX, c.y + 1.0f)
            };
            room.Cave(Profile(floor, roof), Islet(b.x - 3.4f, b.x - 1.4f, basin + 4.6f, 0.7f));
            room.SetLanding(1, new Vector2(b.x - 1.5f, basin));
            room.Treasure(new Vector2(b.x - 2.4f, basin + 4.6f));

            ForestRoomBuilder exit = room.ForMember(2);
            if (exit.Has(RoomDoor.HighRight))
                CarveSunlitRise(exit);
            else
                CarveLevelHall(exit, false);
        }

        // A walkable platform with a flat top, and a smaller perch off the route, in each world's style.
        private static (float x, float bottom, float top)[] Platform(WorldId world, float left, float right, float top,
            float depth) => world switch
        {
            WorldId.Swamp or WorldId.Alien => Pad(left, right, top),
            WorldId.Desert => Slab(left, right, top - depth, top),
            _ => Islet(left, right, top, depth)
        };

        private static (float x, float bottom, float top)[] Perch(WorldId world, float left, float right, float top) => world switch
        {
            WorldId.Swamp => Pad(left, right, top),
            WorldId.Caves => Crystal(left, right, top - 0.3f, 0.3f),
            WorldId.Desert => Slab(left, right, top - 0.6f, top),
            WorldId.Alien => Pod((left + right) / 2f, top - (right - left) / 2f, (right - left) / 2f),
            _ => Islet(left, right, top, 0.6f)
        };

        private const float Chamber = -DoorY;
        private const float SeamRoof = Chamber + 3.1f;

        // Members alternate down and back: even members are upper chambers, odd members the pits below and to
        // their left. Each upper chamber is entered high on the left: walk its shelf, drop onto a platform, drop
        // again to the chamber floor and landing, then turn back along the floor under the shelf. Across the seam
        // the floor ends over the pit's long platform: run left along it and drop to the pit's landing, then walk
        // back right beneath it. A pit with a member after it runs on into that member's high door. The last
        // member leaves by the suite's exit: a pit by its low right door, an upper chamber by its low left one.
        // Every drop stays under 5 units. Positions come from MemberCenter, because members sit about 1.5 units
        // higher or lower depending on the mirroring.
        private static void CarveChain(ForestRoomBuilder room, int members, RoomDoor exit, WorldId world)
        {
            for (int i = 0; i < members; i++)
            {
                ForestRoomBuilder member = room.ForMember(i);
                Vector2? next = i + 1 < members ? member.MemberCenter(i + 1) : null;
                if (i % 2 == 0)
                    CarveUpper(member, next, world);
                else
                {
                    CarvePit(member, member.MemberCenter(i - 1), world);
                    if (next.HasValue)
                        member.Hall(new Vector2(DoorX, -DoorY), next.Value + new Vector2(-DoorX, DoorY), Clearance, Clearance);
                }
            }
            room.Door(0, RoomDoor.HighLeft, Clearance);
            room.Door(members - 1, exit, Clearance);
        }

        // An upper chamber in its own local units. With a pit after it, the floor runs left to the seam over the
        // pit; without one, it ends at the low left door.
        private static void CarveUpper(ForestRoomBuilder room, Vector2? pit, WorldId world)
        {
            room.Cave(new[] { (-DoorX, DoorY, 7.2f), (-3.4f, DoorY, 7.15f), (-1.2f, DoorY, 7.1f), (0.6f, DoorY, 7.0f) });
            var chamber = new List<(float x, float floor, float ceiling)>();
            if (pit.HasValue)
                chamber.Add((pit.Value.x + 6.3f - 0.4f, Chamber, SeamRoof));
            chamber.AddRange(new[]
            {
                (-DoorX, Chamber, SeamRoof + (pit.HasValue ? 0.4f : 0f)), (-3.2f, Chamber, 1.4f), (-1.2f, Chamber, 2.5f),
                (0.6f, Chamber, 3.1f), (0.6f, Chamber, 7.0f), (1.8f, Chamber, 7.15f), (3.2f, Chamber, 7.05f),
                (4.6f, Chamber, 6.4f), (5.4f, -4.1f, 5.3f), (6.0f, -3.85f, 3.9f), (6.4f, -3.6f, 1.6f), (6.6f, -3.3f, -0.2f)
            });
            room.Cave(chamber.ToArray(), Platform(world, -0.6f, 4.6f, 0.6f, 1.3f));
            room.SetLanding(new Vector2(5.0f, Chamber));
            room.Treasure(new Vector2(2.0f, 0.6f));
        }

        // A pit in its own local units, below and left of the upper chamber at the given center. Its roof rises
        // to meet the chamber at the seam, and a perch over the landing holds Nektar.
        private static void CarvePit(ForestRoomBuilder room, Vector2 upper, WorldId world)
        {
            float meet = SeamRoof + upper.y;
            room.Cave(new (float x, float floor, float ceiling)[]
            {
                (-6.0f, -1.6f, 3.6f), (-4.6f, -1.6f, 5.6f), (-2.4f, -1.6f, 6.6f), (0.4f, -1.9f, 6.8f),
                (2.0f, -2.6f, Mathf.Max(6.8f, meet - 0.6f)), (DoorX, -DoorY, meet), (6.3f, -DoorY, meet)
            }, Platform(world, -1.5f, 6.3f, 2.5f, 0.8f), Perch(world, -4.4f, -2.8f, 4.2f));
            room.SetLanding(new Vector2(-3.0f, -1.6f));
            room.Treasure(new Vector2(-5.0f, -1.6f));
            room.Treasure(new Vector2(-3.6f, 4.2f));
        }

        // Merges a floor line and a roof line, each listed left to right, into the profile Cave takes. The floor
        // may repeat an x to make a vertical step. The roof may not.
        private static (float x, float floor, float ceiling)[] Profile(Vector2[] floor, Vector2[] roof)
        {
            var profile = new List<(float x, float floor, float ceiling)>();
            foreach (float x in floor.Select(point => point.x).Concat(roof.Select(point => point.x)).Distinct().OrderBy(x => x))
            {
                float ceiling = Along(roof, x);
                bool step = false;
                foreach (Vector2 point in floor)
                {
                    if (point.x != x)
                        continue;
                    profile.Add((x, point.y, ceiling));
                    step = true;
                }
                if (!step)
                    profile.Add((x, Along(floor, x), ceiling));
            }
            return profile.ToArray();
        }

        private static float Along(Vector2[] line, float x)
        {
            for (int i = 0; i + 1 < line.Length; i++)
                if (line[i].x <= x && x <= line[i + 1].x && line[i + 1].x > line[i].x)
                    return Mathf.Lerp(line[i].y, line[i + 1].y, (x - line[i].x) / (line[i + 1].x - line[i].x));
            throw new ArgumentException($"x {x} lies outside the line from {line[0].x} to {line[^1].x}.");
        }
    }
}
