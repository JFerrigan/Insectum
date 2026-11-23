using System;
using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    // Templates carve a room in its canonical orientation, entered from the left (a start room leaves to the
    // right), using local units around the chunk center. The builder mirrors that into world space.
    //
    // Every door sits at a fixed anchor, so any two templates can be joined: the layout carves a short link
    // between the anchors of neighboring rooms. A template must carve open floor at each of its door anchors.
    internal sealed class ForestRoomBuilder
    {
        internal const float DoorX = 5.2f;
        internal const float DoorY = 4.125f;

        private readonly ForestTunnelSpace space;
        private readonly Vector2Int chunk;
        private readonly Dictionary<RoomDoor, (float clearance, Vector2? lip)> doors = new();

        internal ForestRoomBuilder(ForestTunnelSpace space, Vector2Int chunk, Vector2 center, ForestRoomProfile profile)
        {
            this.space = space;
            this.chunk = chunk;
            Center = center;
            Profile = profile;
        }

        internal ForestRoomProfile Profile { get; }
        internal Vector2 Center { get; }
        internal Vector2 Landing { get; private set; }
        internal IReadOnlyDictionary<RoomDoor, (float clearance, Vector2? lip)> Doors => doors;

        internal static Vector2 Anchor(RoomDoor door) => door switch
        {
            RoomDoor.HighLeft => new Vector2(-DoorX, DoorY),
            RoomDoor.LowLeft => new Vector2(-DoorX, -DoorY),
            RoomDoor.HighRight => new Vector2(DoorX, DoorY),
            RoomDoor.LowRight => new Vector2(DoorX, -DoorY),
            _ => throw new ArgumentOutOfRangeException(nameof(door))
        };

        internal static bool IsHigh(RoomDoor door) => door is RoomDoor.HighLeft or RoomDoor.HighRight;
        internal static int SideOf(RoomDoor door) => door is RoomDoor.HighRight or RoomDoor.LowRight ? 1 : -1;

        internal bool Has(RoomDoor door) =>
            door != RoomDoor.None && (Profile.Entry == door || Profile.Exit == door || Profile.Branch == door);

        internal Vector2 World(Vector2 local) => new(Center.x + (Profile.Mirrored ? -local.x : local.x), Center.y + local.y);

        internal Vector2 WorldAnchor(RoomDoor door) => World(Anchor(door));

        // Carves a space with vertical end walls. The floor runs between the two points, and each end has its
        // own headroom, so the floor and ceiling can both slope.
        internal void Hall(Vector2 from, Vector2 to, float fromHeight, float toHeight)
        {
            Vector2 a = World(from);
            Vector2 b = World(to);
            if (a.x <= b.x)
                space.AddPassage(a, b, fromHeight, toHeight, chunk);
            else
                space.AddPassage(b, a, toHeight, fromHeight, chunk);
        }

        // The room's main standing point, used for checkpoints, route markers, spawn and exit.
        internal void SetLanding(Vector2 local) => Landing = World(local);

        // Declares a door with the headroom at its anchor. Lip marks the ledge a player must jump onto when
        // leaving the landing through this door.
        internal void Door(RoomDoor door, float clearance, Vector2? lip = null) =>
            doors.Add(door, (clearance, lip.HasValue ? World(lip.Value) : (Vector2?)null));
    }
}
