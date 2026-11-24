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
}
