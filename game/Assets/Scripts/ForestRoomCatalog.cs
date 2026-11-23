using System;
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

    internal static class ForestRoomCatalog
    {
        private const float Clearance = ForestTunnelLayout.Height;
        private const float LedgeStep = 1.4f;
        private const float HubFloor = -LedgeStep / 2f;
        private const float HubHalfWidth = 1.2f;
        private const float HubWall = 3.2f;
        private const float HubHeight = LedgeStep + Clearance;
        private const float LowerDoor = 1.33f;
        private const float DeadEndDepth = 2.6f;
        private const float DeadEndHeight = 3.8f;

        private static readonly RoomDoor[] DoorOrder =
        {
            RoomDoor.HighRight, RoomDoor.LowRight, RoomDoor.LowLeft, RoomDoor.HighLeft
        };

        // Plain ramps and halls that fit every shape, so a shape without authored templates still generates.
        internal static readonly ForestRoomTemplate Generic = new("Forest.Room.Generic", 1, _ => true, CarveGeneric);

        private static readonly ForestRoomTemplate[] Templates = { Generic };

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
                CarveLevelHall(room, high == 2 ? ForestRoomBuilder.DoorY : -ForestRoomBuilder.DoorY);
            else
                CarveHub(room);
        }

        // One door: a flat chamber at the door's height. The landing sits near the back wall, where Worm can
        // dig straight out of the start room.
        private static void CarveDeadEnd(ForestRoomBuilder room, RoomDoor door)
        {
            Vector2 anchor = ForestRoomBuilder.Anchor(door);
            int side = ForestRoomBuilder.SideOf(door);
            room.Hall(anchor, new Vector2(-side * DeadEndDepth, anchor.y), DeadEndHeight, DeadEndHeight);
            room.SetLanding(new Vector2(-side * (DeadEndDepth - 1f), anchor.y));
            room.Door(door, Clearance);
        }

        // Two doors at the same height on opposite sides: one level hall between them.
        private static void CarveLevelHall(ForestRoomBuilder room, float floor)
        {
            room.Hall(new Vector2(-ForestRoomBuilder.DoorX, floor), new Vector2(ForestRoomBuilder.DoorX, floor), Clearance, Clearance);
            room.SetLanding(new Vector2(0f, floor));
            foreach (RoomDoor door in DoorOrder)
                if (room.Has(door))
                    room.Door(door, Clearance);
        }

        // A central landing with a ramp to each door. High doors start from a jump ledge above the landing and
        // low doors leave from its floor, so a high and a low door can share one side without their ramps meeting.
        private static void CarveHub(ForestRoomBuilder room)
        {
            bool left = room.Has(RoomDoor.HighLeft) || room.Has(RoomDoor.LowLeft);
            bool right = room.Has(RoomDoor.HighRight) || room.Has(RoomDoor.LowRight);
            room.Hall(new Vector2(left ? -HubHalfWidth : -HubWall, HubFloor), new Vector2(right ? HubHalfWidth : HubWall, HubFloor),
                HubHeight, HubHeight);
            room.SetLanding(new Vector2(0f, HubFloor));

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
    }
}
