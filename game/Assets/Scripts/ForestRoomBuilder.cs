using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    // Templates carve a room in its canonical orientation, entered from the left (a start room leaves to the
    // right), using local units around the chunk center. The builder mirrors that into world space.
    internal sealed class ForestRoomBuilder
    {
        private readonly ForestTunnelSpace space;
        private readonly Dictionary<RoomDoor, (Vector2 foot, float clearance)> doors = new();

        internal ForestRoomBuilder(ForestTunnelSpace space, Vector2 center, ForestRoomProfile profile)
        {
            this.space = space;
            Center = center;
            Profile = profile;
        }

        internal ForestRoomProfile Profile { get; }
        internal Vector2 Center { get; }
        internal Vector2 Landing { get; private set; }
        internal IReadOnlyDictionary<RoomDoor, (Vector2 foot, float clearance)> Doors => doors;

        internal bool Has(RoomDoor door) =>
            door != RoomDoor.None && (Profile.Entry == door || Profile.Exit == door || Profile.Branch == door);

        internal Vector2 World(Vector2 local) => new(Center.x + (Profile.Mirrored ? -local.x : local.x), Center.y + local.y);

        // Carves a space with vertical end walls. The floor runs between the two points, and each end has its
        // own headroom, so the floor and ceiling can both slope.
        internal void Hall(Vector2 from, Vector2 to, float fromHeight, float toHeight)
        {
            Vector2 a = World(from);
            Vector2 b = World(to);
            if (a.x <= b.x)
                space.AddPassage(a, b, fromHeight, toHeight);
            else
                space.AddPassage(b, a, toHeight, fromHeight);
        }

        // The room's main standing point, used for checkpoints, route markers, spawn and exit.
        internal void SetLanding(Vector2 local) => Landing = World(local);

        // Where the link to the neighboring room attaches. Rise lifts the foot onto a step above the given point.
        internal void Door(RoomDoor door, Vector2 foot, float clearance, float rise = 0f) =>
            doors.Add(door, (World(foot) + Vector2.up * rise, clearance));
    }
}
