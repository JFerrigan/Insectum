using System;
using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal sealed class ForestRoomTemplate
    {
        private readonly Func<ForestRoomProfile, bool> fits;
        private readonly Action<ForestRoomBuilder> carve;

        internal ForestRoomTemplate(string id, int weight, Func<ForestRoomProfile, bool> fits, Action<ForestRoomBuilder> carve)
        {
            Id = id;
            Weight = weight;
            this.fits = fits;
            this.carve = carve;
        }

        internal string Id { get; }
        internal int Weight { get; }
        internal bool Fits(ForestRoomProfile profile) => fits(profile);
        internal void Carve(ForestRoomBuilder room) => carve(room);
    }

    // Rooms are authored in local units around the chunk center, entered from the left. Doors sit at
    // (±5.2, ±4.125). Away from its doors a room should stay inside a hexagon reaching x ±8.3 at mid height
    // and y ±7.2 near the middle, so neighboring rooms keep rock between them.
    //
    // Until the verification walker follows authored paths, every route through a room must suit it: walking
    // toward the landing never turns back, the only jump is from the landing onto a door's ledge, slopes stay
    // under 42 degrees, and drops stay under 5 units.
    internal static class ForestRoomCatalog
    {
        private const float Clearance = ForestTunnelLayout.Height;
        private const float LedgeStep = 1.4f;
        private const float HubFloor = -LedgeStep / 2f;
        private const float HubHalfWidth = 1.2f;
        private const float HubTop = HubFloor + LedgeStep + Clearance;
        private const float PocketDepth = 1.8f;
        private const float LowerDoor = 1.33f;
        private const float DoorX = ForestRoomBuilder.DoorX;
        private const float DoorY = ForestRoomBuilder.DoorY;

        private static readonly RoomDoor[] DoorOrder =
        {
            RoomDoor.HighRight, RoomDoor.LowRight, RoomDoor.LowLeft, RoomDoor.HighLeft
        };

        // Plain ramps and halls that fit every shape, so a shape without authored templates still generates.
        internal static readonly ForestRoomTemplate Generic = new("Forest.Room.Generic", 1, _ => true, CarveGeneric);

        private static readonly ForestRoomTemplate[] Templates =
        {
            Generic,
            new("Forest.Open.GreatHollow", 1, Shape("Main.Switchback"), CarveGreatHollow),
            new("Forest.Open.RootChimney", 1, Shape("Main.Switchback", "Main.ClimbBack", "SideTunnel.ClimbBack"), CarveRootChimney),
            new("Forest.Open.TumblingGlade", 1, Shape("Main.Descent"), CarveTumblingGlade),
            new("Forest.Open.MossyBasin", 1, Shape("Main.Span"), CarveMossyBasin),
            new("Forest.Open.RootCathedral", 1, Shape("Main.Crossing", "SideTunnel.Crossing"), CarveRootCathedral),
            new("Forest.Open.SunlitRise", 1, Shape("Main.Ascent", "SideTunnel.Ascent"), CarveSunlitRise)
        };

        internal static IReadOnlyList<ForestRoomTemplate> All => Templates;

        internal static ForestRoomTemplate Choose(ForestRoomProfile profile, ref SeededRandom random)
        {
            int total = 0;
            foreach (ForestRoomTemplate template in Templates)
                if (template.Fits(profile))
                    total += template.Weight;
            if (total == 0)
                return Generic;

            int pick = random.Next(total);
            foreach (ForestRoomTemplate template in Templates)
            {
                if (!template.Fits(profile))
                    continue;
                if (pick < template.Weight)
                    return template;
                pick -= template.Weight;
            }
            return Generic;
        }

        private static Func<ForestRoomProfile, bool> Shape(params string[] shapes) =>
            profile => Array.IndexOf(shapes, profile.Shape) >= 0;

        private static void CarveGeneric(ForestRoomBuilder room)
        {
            int count = 0;
            int high = 0;
            RoomDoor only = RoomDoor.None;
            foreach (RoomDoor door in DoorOrder)
            {
                if (!room.Has(door))
                    continue;
                count++;
                only = door;
                if (ForestRoomBuilder.IsHigh(door))
                    high++;
            }

            if (count == 1)
                CarveDeadEnd(room, only);
            else if (count == 2 && (high == 0 || high == 2))
                CarveLevelHall(room, high == 2);
            else
                CarveHub(room);
        }

        // One door: a domed chamber at the door's height whose back curves down to the floor. The landing sits
        // near the back, where Worm can dig straight out of the start room.
        private static void CarveDeadEnd(ForestRoomBuilder room, RoomDoor door)
        {
            float floor = ForestRoomBuilder.Anchor(door).y;
            int side = ForestRoomBuilder.SideOf(door);
            (float toward, float height)[] shape =
            {
                (-2.8f, 0.9f), (-2.4f, 2.3f), (-1.8f, 3.3f), (-0.6f, 4.0f), (1.8f, 4.2f), (3.8f, 3.7f), (DoorX, Clearance)
            };
            var profile = new List<(float x, float floor, float ceiling)>();
            foreach (var (toward, height) in shape)
                profile.Add((side * toward, floor, floor + height));
            room.Cave(Sorted(profile));
            room.SetLanding(new Vector2(side * -1.6f, floor));
            room.Door(door, Clearance);
        }

        // Two doors at the same height on opposite sides: one hall between them. A low hall gets an arched roof.
        private static void CarveLevelHall(ForestRoomBuilder room, bool high)
        {
            float floor = high ? DoorY : -DoorY;
            float rise = high ? 0f : 1f;
            room.Cave(new[]
            {
                (-DoorX, floor, floor + Clearance),
                (-2.6f, floor, floor + Clearance + rise * 0.7f),
                (0f, floor, floor + Clearance + rise),
                (2.6f, floor, floor + Clearance + rise * 0.7f),
                (DoorX, floor, floor + Clearance)
            });
            room.SetLanding(new Vector2(0f, floor));
            foreach (RoomDoor door in DoorOrder)
                if (room.Has(door))
                    room.Door(door, Clearance);
        }

        // A domed landing with a ramp to each door. A side without doors ends in a rounded pocket.
        private static void CarveHub(ForestRoomBuilder room)
        {
            bool left = room.Has(RoomDoor.HighLeft) || room.Has(RoomDoor.LowLeft);
            bool right = room.Has(RoomDoor.HighRight) || room.Has(RoomDoor.LowRight);
            var profile = new List<(float x, float floor, float ceiling)>
            {
                (-HubHalfWidth, HubFloor, HubTop), (0f, HubFloor, HubTop + 0.6f), (HubHalfWidth, HubFloor, HubTop)
            };
            if (!left)
                AddPocket(profile, -1);
            if (!right)
                AddPocket(profile, 1);
            room.Cave(Sorted(profile));
            room.SetLanding(new Vector2(0f, HubFloor));
            CarveHubRamps(room);
        }

        // High doors start from a jump ledge above the hub floor and low doors leave from the floor, so a high
        // and a low door can share one side without their ramps meeting.
        private static void CarveHubRamps(ForestRoomBuilder room)
        {
            foreach (RoomDoor door in DoorOrder)
            {
                if (!room.Has(door))
                    continue;
                int side = ForestRoomBuilder.SideOf(door);
                Vector2 anchor = ForestRoomBuilder.Anchor(door);
                if (ForestRoomBuilder.IsHigh(door))
                {
                    var lip = new Vector2(side * HubHalfWidth, HubFloor + LedgeStep);
                    room.Hall(lip, anchor, Clearance, Clearance);
                    room.Door(door, Clearance, lip);
                    continue;
                }

                RoomDoor upper = side > 0 ? RoomDoor.HighRight : RoomDoor.HighLeft;
                float throat = room.Has(upper) ? LowerDoor : Clearance;
                room.Hall(new Vector2(side * HubHalfWidth, HubFloor), anchor, throat, Clearance);
                room.Door(door, Clearance);
            }
        }

        // The ceiling eases from full hub height down to a low lip, like the curved back of a cave pocket.
        private static void AddPocket(List<(float x, float floor, float ceiling)> profile, int side)
        {
            for (int step = 1; step <= 4; step++)
            {
                float t = step / 4f;
                float height = 1f + (HubTop - HubFloor - 1f) * Mathf.Cos(t * Mathf.PI / 2f);
                profile.Add((side * (HubHalfWidth + PocketDepth * t), HubFloor, HubFloor + height));
            }
        }

        private static (float x, float floor, float ceiling)[] Sorted(List<(float x, float floor, float ceiling)> profile)
        {
            profile.Sort((a, b) => a.x.CompareTo(b.x));
            return profile.ToArray();
        }

        // A floating rock with a flat, walkable top and a rounded underside.
        private static (float x, float bottom, float top)[] Islet(float left, float right, float top, float depth)
        {
            float width = right - left;
            return new[]
            {
                (left, top - 0.35f, top), (left + width * 0.2f, top - depth * 0.8f, top), ((left + right) / 2f, top - depth, top),
                (right - width * 0.2f, top - depth * 0.8f, top), (right, top - 0.35f, top)
            };
        }

        // Switchback. A tall hollow: walk the upper shelf, drop onto a floating island, drop again into the
        // chamber, then leave along the floor beneath the island and the overhanging shelf.
        private static void CarveGreatHollow(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, -DoorY, 0f), (-3.2f, -DoorY, 1.4f), (-1.2f, -DoorY, 2.5f), (0.6f, -DoorY, 3.1f),
                (0.6f, -DoorY, 7.0f), (1.8f, -DoorY, 7.15f), (3.2f, -DoorY, 7.05f), (4.6f, -DoorY, 6.4f),
                (5.4f, -4.1f, 5.3f), (6.0f, -3.85f, 3.9f), (6.4f, -3.6f, 1.6f), (6.6f, -3.3f, -0.2f)
            }, Islet(-0.6f, 4.6f, 0.6f, 1.3f));
            room.Cave(new[] { (-DoorX, DoorY, 7.2f), (-3.4f, DoorY, 7.15f), (-1.2f, DoorY, 7.1f), (0.6f, DoorY, 7.0f) });
            room.SetLanding(new Vector2(5.0f, -DoorY));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.LowLeft, Clearance);
        }

        // Switchback or climb. A hub with both ramps on the left that opens into a domed chamber with a shallow
        // pit and a floating island.
        private static void CarveRootChimney(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-HubHalfWidth, HubFloor, HubTop), (-0.2f, HubFloor, 5.6f), (1.6f, HubFloor, 6.9f), (2.0f, HubFloor, 7.0f),
                (4.0f, -2.2f, 6.2f), (5.0f, -2.2f, 5.3f), (6.2f, -1.3f, 3.6f)
            }, Islet(2.4f, 4.6f, 3.6f, 1.0f));
            room.SetLanding(new Vector2(0f, HubFloor));
            CarveHubRamps(room);
        }

        // Descent. Broad terraces breaking down under a dome, then a ramp to the low door. A high island above
        // the first drop is a perch for anyone who jumps from the top terrace.
        private static void CarveTumblingGlade(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, DoorY, 7.2f), (-3.0f, DoorY, 7.2f), (-2.6f, 3.9f, 7.2f), (-2.2f, 1.7f, 7.2f), (-1.0f, 1.5f, 7.1f),
                (0.8f, 1.5f, 7.0f), (1.2f, 1.3f, 6.95f), (1.5f, -1.5f, 6.9f), (1.9f, -1.65f, 6.8f), (3.2f, -2.62f, 6.4f),
                (4.2f, -3.38f, 4.6f), (DoorX, -DoorY, 1.0f)
            }, Islet(-1.8f, 0.4f, 5.5f, 0.6f), Islet(1.6f, 3.4f, 4.4f, 0.8f));
            room.SetLanding(new Vector2(-0.6f, 1.5f));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.LowRight, Clearance);
        }

        // Span. Both doors are high, so the room opens downward into a wide mossy basin with an island over it.
        private static void CarveMossyBasin(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, DoorY, 7.2f), (-4.6f, DoorY, 7.2f), (-1.4f, 1.5f, 7.1f), (0f, 1.3f, 7.15f), (1.4f, 1.5f, 7.1f),
                (4.6f, DoorY, 7.2f), (DoorX, DoorY, 7.2f)
            }, Islet(-1.4f, 1.4f, 5.0f, 0.7f));
            room.SetLanding(new Vector2(0f, 1.3f));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.HighRight, Clearance);
        }

        // Crossing. Both doors are low, so the room rises into a tall domed hall with floating islands. The walls
        // bulge out above the doors into shallow side shelves.
        private static void CarveRootCathedral(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-7.0f, 0.5f, 1.9f), (-6.2f, 0.1f, 3.7f), (-DoorX, 0f, 4.6f), (-DoorX, -DoorY, 4.6f), (-4.2f, -DoorY, 6.0f),
                (-2.2f, -DoorY, 7.0f), (0f, -3.6f, 7.2f), (2.2f, -DoorY, 7.0f), (4.2f, -DoorY, 6.0f), (DoorX, -DoorY, 4.6f),
                (DoorX, 0f, 4.6f), (6.2f, 0.1f, 3.7f), (7.0f, 0.5f, 1.9f)
            }, Islet(-3.4f, -1.2f, 1.3f, 0.8f), Islet(1.0f, 3.2f, 2.9f, 0.8f));
            room.SetLanding(new Vector2(-3.2f, -DoorY));
            room.Door(RoomDoor.LowLeft, Clearance);
            room.Door(RoomDoor.LowRight, Clearance);
        }

        // Ascent. A long slope up to a landing, a jump onto the upper slope, and a high dome over both.
        private static void CarveSunlitRise(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, -DoorY, 2.5f), (-3.6f, -2.91f, 4.4f), (-0.7f, HubFloor, 6.6f), (0.7f, HubFloor, 7.0f),
                (0.7f, HubFloor + LedgeStep, 7.0f), (3.4f, 2.75f, 6.9f), (DoorX, DoorY, 7.2f)
            }, Islet(-3.0f, -1.0f, 3.0f, 0.7f));
            room.SetLanding(new Vector2(0f, HubFloor));
            room.Door(RoomDoor.LowLeft, Clearance);
            room.Door(RoomDoor.HighRight, Clearance, new Vector2(0.7f, HubFloor + LedgeStep));
        }
    }
}
