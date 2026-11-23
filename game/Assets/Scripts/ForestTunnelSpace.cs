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
        internal Rect Bounds { get; private set; } = Rect.MinMaxRect(0, 0, 0, 0);

        private sealed class Passage
        {
            internal readonly Vector2[] Outline;
            internal readonly Rect Bounds;

            internal Passage(Vector2 a, Vector2 b, float left, float right)
            {
                Outline = new[] { a, b, b + Vector2.up * right, a + Vector2.up * left };
                Bounds = Rect.MinMaxRect(a.x, Mathf.Min(a.y, b.y), b.x, Mathf.Max(a.y + left, b.y + right));
            }

            internal bool Covers(Vector2 start, Vector2 end, out float from, out float to)
            {
                from = 0f;
                to = 1f;
                for (int i = 0; i < Outline.Length; i++)
                {
                    Vector2 edge = Outline[(i + 1) % Outline.Length] - Outline[i];
                    float a = Cross(edge, start - Outline[i]);
                    float b = Cross(edge, end - Outline[i]);
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
        }

        internal void AddPassage(Vector2 left, Vector2 right, float leftHeight, float rightHeight)
        {
            passages.Add(new Passage(left, right, leftHeight, rightHeight));
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
            foreach (Passage passage in passages)
            {
                Vector2[] outline = passage.Outline;
                for (int index = 0; index < outline.Length; index++)
                {
                    Vector2 from = outline[index];
                    Vector2 to = outline[(index + 1) % outline.Length];
                    Vector2 direction = to - from;
                    Vector2 outward = new Vector2(direction.y, -direction.x).normalized * Epsilon;
                    var overlaps = new List<(float start, float end)>();
                    foreach (Passage other in passages)
                    {
                        if (other != passage && other.Covers(from + outward, to + outward, out float start, out float end))
                            overlaps.Add((start, end));
                    }

                    overlaps.Sort((a, b) => a.start.CompareTo(b.start));
                    float cursor = 0f;
                    foreach (var clip in overlaps)
                    {
                        AddSegment(cursor, clip.start);
                        cursor = Mathf.Max(cursor, clip.end);
                    }
                    AddSegment(cursor, 1f);

                    void AddSegment(float start, float end)
                    {
                        if ((end - start) * direction.magnitude > 0.002f)
                            segments.Add((Vector2.LerpUnclamped(from, to, start), Vector2.LerpUnclamped(from, to, end)));
                    }
                }
            }
            return segments;
        }

        internal void BuildCollision(GameObject owner)
        {
            // The air polygons share ports. Their outer contour is the rock surface.
            var starts = new Dictionary<Vector2Int, Vector2>();
            var ends = new Dictionary<Vector2Int, Vector2>();
            foreach (var segment in Lining)
            {
                starts[Key(segment.from)] = segment.to;
                ends[Key(segment.to)] = segment.from;
            }
            foreach (var segment in Lining)
            {
                var collider = owner.AddComponent<EdgeCollider2D>();
                collider.SetPoints(new List<Vector2> { segment.from, segment.to });
                collider.useAdjacentStartPoint = ends.TryGetValue(Key(segment.from), out Vector2 previous);
                collider.useAdjacentEndPoint = starts.TryGetValue(Key(segment.to), out Vector2 next);
                if (collider.useAdjacentStartPoint)
                    collider.adjacentStartPoint = previous;
                if (collider.useAdjacentEndPoint)
                    collider.adjacentEndPoint = next;
            }
        }

        private static Vector2Int Key(Vector2 position) => Vector2Int.RoundToInt(position * 500f);
    }
}
