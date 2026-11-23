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
        private const float HalfWidth = 1.85f;
        private const float TurnStep = 1.4f;
        private const float LowerDoor = 1.33f;

        private static readonly RoomDoor[] DoorOrder =
        {
            RoomDoor.HighRight, RoomDoor.LowRight, RoomDoor.LowLeft, RoomDoor.HighLeft
        };

        // The original tunnel room. It fits every shape, so a shape without authored templates still generates.
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

        // A short flat room. Two doors on one side make a turn: the floor drops half a step and the upper door
        // sits a jump ledge above it.
        private static void CarveGeneric(ForestRoomBuilder room)
        {
            int left = (room.Has(RoomDoor.HighLeft) ? 1 : 0) + (room.Has(RoomDoor.LowLeft) ? 1 : 0);
            int right = (room.Has(RoomDoor.HighRight) ? 1 : 0) + (room.Has(RoomDoor.LowRight) ? 1 : 0);
            bool turn = left == 2 && right == 0 || right == 2 && left == 0;
            float floor = turn ? -TurnStep / 2f : 0f;
            room.SetLanding(new Vector2(0f, floor));
            float height = left + right == 1 ? 3.8f
                : ForestTunnelLayout.Height + (turn || left + right == 3 ? TurnStep : 0f);
            room.Hall(new Vector2(-HalfWidth, floor), new Vector2(HalfWidth, floor), height, height);

            foreach (RoomDoor door in DoorOrder)
            {
                if (!room.Has(door))
                    continue;
                bool rightward = door is RoomDoor.HighRight or RoomDoor.LowRight;
                bool split = (rightward ? right : left) == 2;
                bool upper = split && (door is RoomDoor.HighLeft or RoomDoor.HighRight);
                room.Door(door, new Vector2(rightward ? HalfWidth : -HalfWidth, floor),
                    split && !upper ? LowerDoor : ForestTunnelLayout.Height, upper ? TurnStep : 0f);
            }
        }
    }
}
