using System;
using UnityEngine;

namespace Insectum.Game
{
    public static class HexGrid
    {
        public const float ColumnSpacing = 1.7320508f;
        public const float HalfWidth = ColumnSpacing / 2f;
        public const float RowSpacing = 1.5f;
        public const int RegionSize = 8;
        public const int MacroRadius = 5;

        private static readonly Vector2Int[] Corners =
        {
            new(0, 2), new(1, 1), new(1, -1), new(0, -2), new(-1, -1), new(-1, 1)
        };

        private static readonly Vector2Int[] Neighbors =
        {
            new(0, 1), new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1)
        };

        public const int SideCount = 6;

        public static Vector2Int Neighbor(Vector2Int cell, int side) => cell + Neighbors[side];

        public static Vector2Int Corner(Vector2Int cell, int side) =>
            new Vector2Int(2 * cell.x + cell.y, 3 * cell.y) + Corners[side];

        public static Vector2 Vertex(Vector2Int vertex) => new(vertex.x * HalfWidth, vertex.y * 0.5f);

        public static Vector2 Center(Vector2Int cell) =>
            new(ColumnSpacing * (cell.x + 0.5f * cell.y), RowSpacing * cell.y);

        public static Vector2Int CellAt(Vector2 position)
        {
            float r = position.y / RowSpacing;
            float q = position.x / ColumnSpacing - r / 2f;
            float s = -q - r;
            int roundedQ = Mathf.RoundToInt(q);
            int roundedR = Mathf.RoundToInt(r);
            int roundedS = Mathf.RoundToInt(s);
            float qError = Mathf.Abs(roundedQ - q);
            float rError = Mathf.Abs(roundedR - r);
            float sError = Mathf.Abs(roundedS - s);
            if (qError > rError && qError > sError)
                roundedQ = -roundedR - roundedS;
            else if (rError > sError)
                roundedR = -roundedQ - roundedS;
            return new Vector2Int(roundedQ, roundedR);
        }

        public static int Distance(Vector2Int a, Vector2Int b)
        {
            Vector2Int delta = a - b;
            return Math.Max(Math.Abs(delta.x + delta.y), Math.Max(Math.Abs(delta.x), Math.Abs(delta.y)));
        }

        public static Vector2Int RegionAt(Vector2Int cell) => new(FloorRegion(cell.x), FloorRegion(cell.y));

        internal static int Compare(Vector2Int a, Vector2Int b)
        {
            int row = a.y.CompareTo(b.y);
            return row != 0 ? row : a.x.CompareTo(b.x);
        }

        private static int FloorRegion(int coordinate)
        {
            int quotient = coordinate / RegionSize;
            return coordinate < 0 && coordinate % RegionSize != 0 ? quotient - 1 : quotient;
        }

        internal static float? RayEntry(Vector2Int cell, Vector2 origin, Vector2 direction, float reach)
        {
            float enter = 0f;
            for (int side = 0; side < SideCount; side++)
            {
                Vector2 start = Vertex(Corner(cell, side));
                Vector2 end = Vertex(Corner(cell, (side + 1) % SideCount));
                Vector2 edge = end - start;
                Vector2 normal = new(-edge.y, edge.x);
                float separation = Vector2.Dot(normal, origin - start);
                float speed = Vector2.Dot(normal, direction);
                if (Mathf.Abs(speed) < 0.00001f)
                {
                    if (separation > 0f)
                        return null;
                    continue;
                }

                float t = -separation / speed;
                if (speed < 0f)
                    enter = Mathf.Max(enter, t);
                else
                    reach = Mathf.Min(reach, t);
                if (enter > reach)
                    return null;
            }

            return enter;
        }
    }
}
