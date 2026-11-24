using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal sealed class ForestTunnelSpace
    {
        private const float Epsilon = 0.0001f;
        private readonly List<Passage> passages = new();
        private readonly Dictionary<Vector2Int, List<Vector2[]>> rock = new();
        private List<(Vector2 from, Vector2 to)> lining;
        internal readonly List<Vector2> Route = new();
        internal readonly Dictionary<(Vector2 from, Vector2 to), Vector2> Jumps = new();
        internal readonly List<(int source, Vector2 first, Vector2 end)> Branches = new();
        internal readonly Dictionary<Vector2Int, ForestRoomProfile> Rooms = new();
        // Hazards carved into rooms, and the take-off points where a walker heading in the given direction leaps
        // over them.
        internal readonly List<TunnelHazard> Hazards = new();
        internal readonly List<(Vector2 takeoff, int direction)> Leaps = new();
        internal Rect Bounds { get; private set; } = Rect.MinMaxRect(0, 0, 0, 0);

        private sealed class Passage
        {
            internal readonly Vector2[] Outline;
            internal readonly Rect Bounds;
            internal readonly object Owner;

            internal Passage(Vector2 a, Vector2 b, float left, float right, object owner)
            {
                Owner = owner;
                Outline = new[] { a, b, b + Vector2.up * right, a + Vector2.up * left };
                Bounds = Rect.MinMaxRect(a.x, Mathf.Min(a.y, b.y), b.x, Mathf.Max(a.y + left, b.y + right));
            }

            internal bool Covers(Vector2 start, Vector2 end, out float from, out float to)
            {
                from = 0f;
                to = 1f;
                return Mathf.Max(start.x, end.x) >= Bounds.xMin && Mathf.Min(start.x, end.x) <= Bounds.xMax
                    && Mathf.Max(start.y, end.y) >= Bounds.yMin && Mathf.Min(start.y, end.y) <= Bounds.yMax
                    && Clip(Outline, start, end, 1f, out from, out to);
            }
        }

        private static bool Clip(Vector2[] outline, Vector2 start, Vector2 end, float winding, out float from, out float to)
        {
            from = 0f;
            to = 1f;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 edge = outline[(i + 1) % outline.Length] - outline[i];
                float a = winding * Cross(edge, start - outline[i]);
                float b = winding * Cross(edge, end - outline[i]);
                if (a < 0f && b < 0f)
                    return false;
                if (a < 0f)
                    from = Mathf.Max(from, a / (a - b));
                else if (b < 0f)
                    to = Mathf.Min(to, a / (a - b));
                if (from >= to)
                    return false;
            }
            return true;
        }

        // Owner names what carved the passage (a room's chunk or a link) so checks can tell spaces apart.
        internal void AddPassage(Vector2 left, Vector2 right, float leftHeight, float rightHeight, object owner = null)
        {
            passages.Add(new Passage(left, right, leftHeight, rightHeight, owner));
        }

        internal IEnumerable<(object owner, IReadOnlyList<Vector2> outline)> Passages
        {
            get
            {
                foreach (Passage passage in passages)
                    yield return (passage.Owner, passage.Outline);
            }
        }

        internal bool AddRock(Vector2Int cell)
        {
            Vector2 center = HexGrid.Center(cell);
            Rect cellBounds = Rect.MinMaxRect(center.x - HexGrid.HalfWidth, center.y - 1f,
                center.x + HexGrid.HalfWidth, center.y + 1f);
            Bounds = Rect.MinMaxRect(Mathf.Min(Bounds.xMin, cellBounds.xMin), Mathf.Min(Bounds.yMin, cellBounds.yMin),
                Mathf.Max(Bounds.xMax, cellBounds.xMax), Mathf.Max(Bounds.yMax, cellBounds.yMax));
            List<Vector2[]> remaining = null;
            foreach (Passage passage in passages)
            {
                if (!passage.Bounds.Overlaps(cellBounds))
                    continue;

                remaining ??= new List<Vector2[]> { Hexagon(cell) };
                var outside = new List<Vector2[]>();
                foreach (Vector2[] piece in remaining)
                    Subtract(piece, passage.Outline, outside);
                remaining = outside;
                if (remaining.Count == 0)
                    return false;
            }

            rock.Add(cell, remaining);
            return true;
        }

        internal bool IsRock(Vector2Int cell) => rock.ContainsKey(cell);

        internal bool TryGetPieces(Vector2Int cell, out List<Vector2[]> pieces) =>
            rock.TryGetValue(cell, out pieces) && pieces != null;

        internal float? RayEntry(Vector2Int cell, Vector2 origin, Vector2 direction, float reach)
        {
            if (!TryGetPieces(cell, out List<Vector2[]> pieces))
                return HexGrid.RayEntry(cell, origin, direction, reach);

            float? hit = null;
            foreach (Vector2[] piece in pieces)
            {
                if (Clip(piece, origin, origin + direction * reach, -1f, out float start, out _))
                {
                    reach *= start;
                    hit = reach;
                }
            }
            return hit;
        }

        private static Vector2[] Hexagon(Vector2Int cell)
        {
            var points = new Vector2[6];
            for (int i = 0; i < points.Length; i++)
                points[i] = HexGrid.Vertex(HexGrid.Corner(cell, i));
            return points;
        }

        private static void Subtract(Vector2[] subject, Vector2[] hole, List<Vector2[]> outside)
        {
            for (int edge = 0; edge < hole.Length && subject.Length >= 3; edge++)
            {
                Vector2 origin = hole[edge];
                Vector2 direction = hole[(edge + 1) % hole.Length] - origin;
                var inside = new List<Vector2>(subject.Length + 1);
                var cut = new List<Vector2>(subject.Length + 1);
                Vector2 previous = subject[^1];
                float previousSide = Cross(direction, previous - origin);
                foreach (Vector2 point in subject)
                {
                    float side = Cross(direction, point - origin);
                    if ((side >= 0f) != (previousSide >= 0f))
                    {
                        Vector2 intersection = Vector2.LerpUnclamped(previous, point, previousSide / (previousSide - side));
                        inside.Add(intersection);
                        cut.Add(intersection);
                    }

                    (side >= 0f ? inside : cut).Add(point);
                    previous = point;
                    previousSide = side;
                }

                if (cut.Count >= 3 && Area(cut) > Epsilon)
                    outside.Add(cut.ToArray());
                subject = inside.ToArray();
            }
        }

        private static float Area(List<Vector2> points)
        {
            float twiceArea = 0f;
            for (int i = 1; i + 1 < points.Count; i++)
                twiceArea += Cross(points[i] - points[0], points[i + 1] - points[0]);
            return Mathf.Abs(twiceArea) * 0.5f;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        internal IReadOnlyList<(Vector2 from, Vector2 to)> Lining => lining ??= BuildLining();

        private List<(Vector2 from, Vector2 to)> BuildLining()
        {
            var segments = new List<(Vector2 from, Vector2 to)>();
            var overlaps = new List<(float start, float end)>();
            foreach (Passage passage in passages)
            {
                Vector2[] outline = passage.Outline;
                for (int index = 0; index < outline.Length; index++)
                {
                    Vector2 from = outline[index];
                    Vector2 to = outline[(index + 1) % outline.Length];
                    Vector2 direction = to - from;
                    Vector2 outward = new Vector2(direction.y, -direction.x).normalized * Epsilon;
                    foreach (Passage other in passages)
                    {
                        if (other != passage && other.Covers(from + outward, to + outward, out float start, out float end))
                            overlaps.Add((start, end));
                    }

                    AddUncovered(segments, overlaps, from, to);
                }
            }
            return segments;
        }

        internal List<(Vector2 from, Vector2 to)> DigLining(IReadOnlyCollection<Vector2Int> dugCells,
            IReadOnlyDictionary<Vector2Int, TerrainMaterial> remaining)
        {
            var segments = new List<(Vector2 from, Vector2 to)>(Lining.Count);
            var dugOutlines = new List<Vector2[]>(dugCells.Count);
            foreach (Vector2Int cell in dugCells)
                dugOutlines.Add(Hexagon(cell));

            var overlaps = new List<(float start, float end)>();
            // Keep the original smooth tunnel edges wherever the new hole has not cut into them.
            foreach (var (from, to) in Lining)
            {
                Vector2 span = to - from;
                Vector2 outward = new Vector2(span.y, -span.x).normalized * Epsilon;
                foreach (Vector2[] hex in dugOutlines)
                    if (Clip(hex, from + outward, to + outward, -1f, out float start, out float end))
                        overlaps.Add((start, end));
                AddUncovered(segments, overlaps, from, to);
            }

            // Add the rock-facing edges of each new hole, clipping away portions already in the tunnel.
            foreach (Vector2Int cell in dugCells)
            {
                for (int side = 0; side < HexGrid.SideCount; side++)
                {
                    if (!remaining.ContainsKey(HexGrid.Neighbor(cell, side)))
                        continue;
                    Vector2 from = HexGrid.Vertex(HexGrid.Corner(cell, (side + 1) % HexGrid.SideCount));
                    Vector2 to = HexGrid.Vertex(HexGrid.Corner(cell, side));
                    Vector2 span = to - from;
                    Vector2 outward = new Vector2(span.y, -span.x).normalized * Epsilon;
                    foreach (Passage passage in passages)
                        if (passage.Covers(from + outward, to + outward, out float start, out float end))
                            overlaps.Add((start, end));
                    AddUncovered(segments, overlaps, from, to);
                }
            }
            return segments;
        }

        private static void AddUncovered(List<(Vector2 from, Vector2 to)> segments,
            List<(float start, float end)> overlaps, Vector2 from, Vector2 to)
        {
            float length = Vector2.Distance(from, to);
            overlaps.Sort((a, b) => a.start.CompareTo(b.start));
            float cursor = 0f;
            foreach (var (start, end) in overlaps)
            {
                AddSegment(cursor, start);
                cursor = Mathf.Max(cursor, end);
            }
            AddSegment(cursor, 1f);
            overlaps.Clear();

            void AddSegment(float start, float end)
            {
                if ((end - start) * length > 0.002f)
                    segments.Add((Vector2.LerpUnclamped(from, to, start), Vector2.LerpUnclamped(from, to, end)));
            }
        }

        internal static void BuildCollision(GameObject owner, IReadOnlyList<(Vector2 from, Vector2 to)> edges,
            List<EdgeCollider2D> colliders)
        {
            // The air polygons share ports. Their outer contour is the rock surface.
            var starts = new Dictionary<Vector2Int, Vector2>();
            var ends = new Dictionary<Vector2Int, Vector2>();
            foreach (var segment in edges)
            {
                starts[Key(segment.from)] = segment.to;
                ends[Key(segment.to)] = segment.from;
            }
            var points = new List<Vector2>(2) { default, default };
            for (int i = 0; i < edges.Count; i++)
            {
                var segment = edges[i];
                if (i == colliders.Count)
                    colliders.Add(owner.AddComponent<EdgeCollider2D>());
                EdgeCollider2D collider = colliders[i];
                points[0] = segment.from;
                points[1] = segment.to;
                collider.SetPoints(points);
                collider.useAdjacentStartPoint = ends.TryGetValue(Key(segment.from), out Vector2 previous);
                collider.useAdjacentEndPoint = starts.TryGetValue(Key(segment.to), out Vector2 next);
                if (collider.useAdjacentStartPoint)
                    collider.adjacentStartPoint = previous;
                if (collider.useAdjacentEndPoint)
                    collider.adjacentEndPoint = next;
                collider.enabled = true;
            }
            for (int i = edges.Count; i < colliders.Count; i++)
                colliders[i].enabled = false;
        }

        private static Vector2Int Key(Vector2 position) => Vector2Int.RoundToInt(position * 500f);
    }
}
