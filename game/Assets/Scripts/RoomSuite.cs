using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    // The chunks one template carves as a single room, listed in route order. The first chunk anchors the
    // template's coordinates and owns every passage the suite carves, and the whole suite is mirrored together
    // so that it is entered from the left. Most rooms are a suite of one chunk.
    internal sealed class RoomSuite
    {
        private readonly Vector2Int[] chunks;

        internal RoomSuite(ForestRoomTemplate template, IReadOnlyList<Vector2Int> chunks, bool mirrored)
        {
            Template = template;
            this.chunks = new Vector2Int[chunks.Count];
            for (int i = 0; i < chunks.Count; i++)
                this.chunks[i] = chunks[i];
            Mirrored = mirrored;
        }

        internal ForestRoomTemplate Template { get; }
        internal IReadOnlyList<Vector2Int> Chunks => chunks;
        internal Vector2Int Anchor => chunks[0];
        internal bool Mirrored { get; }

        internal bool Contains(Vector2Int chunk) => System.Array.IndexOf(chunks, chunk) >= 0;
    }

    // The run of consecutive main-route chunks a suite template covers, in the suite's canonical orientation
    // (entered from the left): the door its first chunk is entered through, the side each chunk steps to the
    // next (0 upper right, 1 lower right, 3 lower left, 4 upper left), and the door its last chunk leaves
    // through.
    //
    // The chunk grid is skewed, so a mirrored suite's later chunks do not sit exactly where the unmirrored
    // ones do. Templates place them with ForestRoomBuilder.MemberCenter rather than fixed offsets.
    internal sealed class SuiteFootprint
    {
        private readonly int[] steps;

        internal SuiteFootprint(RoomDoor entry, int[] steps, RoomDoor exit)
        {
            Entry = entry;
            this.steps = (int[])steps.Clone();
            Exit = exit;
        }

        internal RoomDoor Entry { get; }
        internal IReadOnlyList<int> Steps => steps;
        internal RoomDoor Exit { get; }
        internal int Length => steps.Length + 1;
    }
}
