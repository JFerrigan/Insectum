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

        // Carves the space between a floor line and a ceiling line, both sampled left to right at the profile
        // points. Repeating an x makes a vertical step. Islands are solid rock left floating inside the cave,
        // each outlined by a bottom line and a top line sampled left to right in the same way.
        internal void Cave((float x, float floor, float ceiling)[] profile,
            params (float x, float bottom, float top)[][] islands)
        {
            var breaks = new List<float>(profile.Length + islands.Length * 5);
            foreach (var point in profile)
                breaks.Add(point.x);
            foreach (var island in islands)
                foreach (var point in island)
                    breaks.Add(point.x);
            breaks.Sort();

            var stack = new List<(float bottom0, float bottom1, float top0, float top1)>(islands.Length);
            for (int i = 1; i < breaks.Count; i++)
            {
                float x0 = breaks[i - 1];
                float x1 = breaks[i];
                if (x1 - x0 < 0.001f)
                    continue;
                if (!Sample(profile, x0, x1, out float floor0, out float floor1, out float ceiling0, out float ceiling1))
                    throw new ArgumentException($"Cave interval {x0}..{x1} lies outside its profile.");

                stack.Clear();
                foreach (var island in islands)
                    if (Sample(island, x0, x1, out float bottom0, out float bottom1, out float top0, out float top1))
                        stack.Add((bottom0, bottom1, top0, top1));
                stack.Sort((p, q) => (p.bottom0 + p.bottom1).CompareTo(q.bottom0 + q.bottom1));
                foreach (var (bottom0, bottom1, top0, top1) in stack)
                {
                    Gap(x0, x1, floor0, floor1, bottom0, bottom1);
                    floor0 = top0;
                    floor1 = top1;
                }
                Gap(x0, x1, floor0, floor1, ceiling0, ceiling1);
            }
        }

        private static bool Sample((float x, float low, float high)[] outline, float x0, float x1,
            out float low0, out float low1, out float high0, out float high1)
        {
            low0 = low1 = high0 = high1 = 0f;
            int segment = Segment(outline, x0, x1);
            if (segment < 0)
                return false;
            var a = outline[segment];
            var b = outline[segment + 1];
            float t0 = (x0 - a.x) / (b.x - a.x);
            float t1 = (x1 - a.x) / (b.x - a.x);
            low0 = Mathf.Lerp(a.low, b.low, t0);
            low1 = Mathf.Lerp(a.low, b.low, t1);
            high0 = Mathf.Lerp(a.high, b.high, t0);
            high1 = Mathf.Lerp(a.high, b.high, t1);
            return true;
        }

        private void Gap(float x0, float x1, float floor0, float floor1, float ceiling0, float ceiling1)
        {
            if (ceiling0 - floor0 < 0.3f || ceiling1 - floor1 < 0.3f)
                throw new ArgumentException($"Cave space at x {x0}..{x1} is too thin or inverted.");
            Hall(new Vector2(x0, floor0), new Vector2(x1, floor1), ceiling0 - floor0, ceiling1 - floor1);
        }

        private static int Segment((float x, float floor, float ceiling)[] profile, float x0, float x1)
        {
            for (int i = 0; i + 1 < profile.Length; i++)
                if (profile[i].x <= x0 + 0.001f && profile[i + 1].x >= x1 - 0.001f && profile[i + 1].x - profile[i].x > 0.001f)
                    return i;
            return -1;
        }

        // The room's main standing point, used for checkpoints, route markers, spawn and exit.
        internal void SetLanding(Vector2 local) => Landing = World(local);

        // Declares a door with the headroom at its anchor. Lip marks the ledge a player must jump onto when
        // leaving the landing through this door.
        internal void Door(RoomDoor door, float clearance, Vector2? lip = null) =>
            doors.Add(door, (clearance, lip.HasValue ? World(lip.Value) : (Vector2?)null));
    }
}
