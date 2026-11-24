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

        private static readonly ForestRoomTemplate[] Forest = { GreatRootStair };

        internal static IReadOnlyList<ForestRoomTemplate> For(WorldId world) =>
            world == WorldId.Forest ? Forest : Array.Empty<ForestRoomTemplate>();

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
