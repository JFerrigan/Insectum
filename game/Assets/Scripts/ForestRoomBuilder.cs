using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Insectum.Game
{
    // Templates carve a room in its canonical orientation, entered from the left (a start room leaves to the
    // right), using local units around the center of the suite's first chunk. The builder mirrors that into world
    // space.
    //
    // Every door sits at a fixed anchor, so any two templates can be joined: the layout carves a short link
    // between the anchors of neighboring rooms. A template must carve open floor at each of its door anchors.
    //
    // A suite spans one or more chunks, its members. Each member has its own landing and its own doors to
    // chunks outside the suite, and the template carves the space between members itself. The single-room
    // calls below act on the focused member: the first one, or the one a ForMember view is centered on.
    internal sealed class ForestRoomBuilder
    {
        internal const float DoorX = 5.2f;
        internal const float DoorY = 4.125f;

        private readonly ForestTunnelSpace space;
        private readonly RoomSuite suite;
        // The world center of the suite's first chunk, and every member's center in suite-local units.
        private readonly Vector2 anchor;
        private readonly Vector2[] offsets;
        private readonly ForestRoomProfile[] profiles;
        private readonly IReadOnlyCollection<RoomDoor>[] openings;
        private readonly Vector2[] landings;
        private readonly Dictionary<RoomDoor, (float clearance, Vector2? lip)>[] doors;
        // The member the single-room calls act on. Local units are measured from its center.
        private readonly int focus;

        // Centers are in world space, one per member. Openings name each member's doors to chunks outside the
        // suite in the suite's canonical orientation.
        internal ForestRoomBuilder(ForestTunnelSpace space, RoomSuite suite, IReadOnlyList<Vector2> centers,
            IReadOnlyList<ForestRoomProfile> profiles, IReadOnlyList<IReadOnlyCollection<RoomDoor>> openings, int level = 1)
        {
            this.space = space;
            this.suite = suite;
            int count = suite.Chunks.Count;
            anchor = centers[0];
            Level = level;
            offsets = new Vector2[count];
            this.profiles = new ForestRoomProfile[count];
            this.openings = new IReadOnlyCollection<RoomDoor>[count];
            landings = new Vector2[count];
            doors = new Dictionary<RoomDoor, (float clearance, Vector2? lip)>[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = centers[i] - anchor;
                offsets[i] = new Vector2(suite.Mirrored ? -offset.x : offset.x, offset.y);
                this.profiles[i] = profiles[i];
                this.openings[i] = openings[i];
                doors[i] = new Dictionary<RoomDoor, (float clearance, Vector2? lip)>();
            }
            TrapSpots = new List<Vector2>();
            TreasureSpots = new List<Vector2>();
        }

        // A view of the same suite centered on another member. Whatever it carves, lands, or opens belongs to
        // the whole suite.
        private ForestRoomBuilder(ForestRoomBuilder whole, int focus)
        {
            space = whole.space;
            suite = whole.suite;
            anchor = whole.anchor;
            offsets = whole.offsets;
            profiles = whole.profiles;
            openings = whole.openings;
            landings = whole.landings;
            doors = whole.doors;
            Level = whole.Level;
            TrapSpots = whole.TrapSpots;
            TreasureSpots = whole.TreasureSpots;
            this.focus = focus;
        }

        // The focused member's profile, for templates that carve a single chunk.
        internal ForestRoomProfile Profile => profiles[focus];
        // The level's place within its world, from 1 to 3, so a template can ease in on a world's first level.
        internal int Level { get; }
        internal Vector2 Center => World(Vector2.zero);
        internal int Members => offsets.Length;
        internal Vector2 Landing => landings[focus];
        internal IReadOnlyDictionary<RoomDoor, (float clearance, Vector2? lip)> Doors => doors[focus];
        internal List<Vector2> TrapSpots { get; }
        internal List<Vector2> TreasureSpots { get; }

        // Lets a suite template carve one member with a single-room template: the view's local units and
        // single-room calls are centered on that member.
        internal ForestRoomBuilder ForMember(int member) => member == focus ? this : new ForestRoomBuilder(this, member);

        // A member's center in local units.
        internal Vector2 MemberCenter(int member) => offsets[member] - offsets[focus];

        internal Vector2 LandingOf(int member) => landings[member];

        internal IReadOnlyDictionary<RoomDoor, (float clearance, Vector2? lip)> DoorsOf(int member) => doors[member];

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

        internal bool Has(RoomDoor door) => Has(focus, door);

        // True when the member has this door to a chunk outside the suite.
        internal bool Has(int member, RoomDoor door) => door != RoomDoor.None && openings[member].Contains(door);

        internal Vector2 World(Vector2 local)
        {
            Vector2 suiteLocal = offsets[focus] + local;
            return new Vector2(anchor.x + (suite.Mirrored ? -suiteLocal.x : suiteLocal.x), anchor.y + suiteLocal.y);
        }

        internal Vector2 WorldAnchor(RoomDoor door) => WorldAnchor(focus, door);

        internal Vector2 WorldAnchor(int member, RoomDoor door) => World(MemberCenter(member) + Anchor(door));

        // Carves a space with vertical end walls. The floor runs between the two points, and each end has its
        // own headroom, so the floor and ceiling can both slope.
        internal void Hall(Vector2 from, Vector2 to, float fromHeight, float toHeight)
        {
            Vector2 a = World(from);
            Vector2 b = World(to);
            if (a.x <= b.x)
                space.AddPassage(a, b, fromHeight, toHeight, suite.Anchor);
            else
                space.AddPassage(b, a, toHeight, fromHeight, suite.Anchor);
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

        // A floor point off the walking route where the level may place the world's trap.
        internal void Trap(Vector2 floor) => TrapSpots.Add(World(floor));

        // A floor point off the walking route, such as an island top or a pit, where the level may leave Nektar.
        internal void Treasure(Vector2 floor) => TreasureSpots.Add(World(floor));

        // A bramble bed growing up from a flat floor between two x positions.
        internal void Brambles(float from, float to, float floor) =>
            AddHazard(HazardKind.Bramble, from, to, floor, floor + LevelHazards.BrambleHeight, false);

        // Brambles hanging down from a flat roof between two x positions.
        internal void HangingBrambles(float from, float to, float roof) =>
            AddHazard(HazardKind.Bramble, from, to, roof - LevelHazards.BrambleHeight, roof, true);

        // A cactus on a flat floor, centered at x: a barrel 0.8 wide and 0.9 tall, or a tall one 0.6 wide and 1.3 tall.
        internal void Cactus(float x, float floor, bool tall)
        {
            float half = tall ? 0.3f : 0.4f;
            AddHazard(HazardKind.Cactus, x - half, x + half, floor, floor + (tall ? 1.3f : 0.9f), false);
        }

        // A spike bed on a flat floor between two x positions. Keep the approach flat too: a body walking down a
        // slope comes down fast enough to die on spikes.
        internal void Spikes(float from, float to, float floor) =>
            AddHazard(HazardKind.Spikes, from, to, floor, floor + LevelHazards.SpikeHeight, false);

        // A flytrap rooted in a flat floor, its open mouth a unit square centered at x.
        internal void Flytrap(float x, float floor) =>
            AddHazard(HazardKind.Flytrap, x - LevelHazards.MouthSize / 2f, x + LevelHazards.MouthSize / 2f, floor,
                floor + LevelHazards.MouthSize, false);

        // A flytrap hanging from a flat roof, its mouth opening downward.
        internal void HangingFlytrap(float x, float roof) =>
            AddHazard(HazardKind.Flytrap, x - LevelHazards.MouthSize / 2f, x + LevelHazards.MouthSize / 2f,
                roof - LevelHazards.MouthSize, roof, true);

        // A laser emitter floating at a point, with arms of the given reach evenly spaced around it. Phase is the
        // first arm's starting angle in degrees and spin its turn in degrees a second, counterclockwise when
        // positive, both in local orientation.
        internal void Laser(Vector2 emitter, float reach, float spin, int arms, float phase)
        {
            Vector2 at = World(emitter);
            space.Hazards.Add(new TunnelHazard(HazardKind.Laser, new Rect(at.x - 0.25f, at.y - 0.25f, 0.5f, 0.5f), false,
                suite.Anchor, reach, suite.Mirrored ? -spin : spin, arms, suite.Mirrored ? 180f - phase : phase));
        }

        // A standing point on the route where a walker heading in the given direction waits for a laser gap, then
        // runs the given distance in one go. The verification walker waits from exactly here.
        internal void Wait(Vector2 spot, int direction, float distance) =>
            space.Waits.Add((World(spot), suite.Mirrored ? -direction : direction, distance));

        // A take-off point on the route where a player heading in the given direction (1 right, -1 left) jumps
        // to clear a hazard. The verification walker leaps from exactly here.
        internal void Leap(Vector2 takeoff, int direction) =>
            space.Leaps.Add((World(takeoff), suite.Mirrored ? -direction : direction));

        // Hazards belong to the suite's first chunk, so a suite counts once toward a level's hazard rooms.
        private void AddHazard(HazardKind kind, float from, float to, float bottom, float top, bool hanging)
        {
            float a = World(new Vector2(from, 0f)).x;
            float b = World(new Vector2(to, 0f)).x;
            float y = Center.y;
            space.Hazards.Add(new TunnelHazard(kind,
                Rect.MinMaxRect(Mathf.Min(a, b), y + bottom, Mathf.Max(a, b), y + top), hanging, suite.Anchor));
        }

        // The room's main standing point, used for checkpoints, route markers, spawn and exit.
        internal void SetLanding(Vector2 local) => SetLanding(focus, local);

        // A member's standing point, in local units.
        internal void SetLanding(int member, Vector2 local) => landings[member] = World(local);

        // Declares a door with the headroom at its anchor. Lip marks the ledge a player must jump onto when
        // leaving the landing through this door.
        internal void Door(RoomDoor door, float clearance, Vector2? lip = null) => Door(focus, door, clearance, lip);

        // Declares one of a member's doors. Lip is in local units.
        internal void Door(int member, RoomDoor door, float clearance, Vector2? lip = null) =>
            doors[member].Add(door, (clearance, lip.HasValue ? World(lip.Value) : (Vector2?)null));
    }
}
