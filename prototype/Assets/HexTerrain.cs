using System;
using System.Collections.Generic;
using UnityEngine;

namespace Insectum
{
    public sealed class HexTerrain
    {
        public const float ColumnSpacing = 1.7320508f;
        public const float HalfWidth = ColumnSpacing / 2f;
        public static readonly Vector2Int TunnelCell = new(2, 1);

        // Pointy-top axial hexes. Corners are clockwise in world space.
        public static readonly Vector2[] Corners =
        {
            new(0f, 1f), new(HalfWidth, 0.5f), new(HalfWidth, -0.5f),
            new(0f, -1f), new(-HalfWidth, -0.5f), new(-HalfWidth, 0.5f)
        };

        public enum Material { SoftDirt, PackedDirt, SoftStone, Metal, Reinforced }

        private readonly Dictionary<Vector2Int, Material> tiles = new();
        private readonly PolygonCollider2D source;

        public GameObject Root { get; }
        public CompositeCollider2D Collider { get; }
        public IReadOnlyDictionary<Vector2Int, Material> Tiles => tiles;
        public bool Smooth { get; private set; } = true;
        public int Revision { get; private set; }

        public HexTerrain()
        {
            Root = new GameObject("Hex terrain");
            Root.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            Collider = Root.AddComponent<CompositeCollider2D>();
            Collider.geometryType = CompositeCollider2D.GeometryType.Polygons;
            Collider.generationType = CompositeCollider2D.GenerationType.Manual;
            Collider.offsetDistance = 0.001f;
            source = Root.AddComponent<PolygonCollider2D>();
            source.compositeOperation = Collider2D.CompositeOperation.Merge;
            Reset();
        }

        public static Vector2 Center(Vector2Int cell) =>
            new(ColumnSpacing * (cell.x + 0.5f * cell.y), 1.5f * cell.y);

        public static int? DigCost(Material material) => material switch
        {
            Material.SoftDirt => 4,
            Material.PackedDirt => 7,
            Material.SoftStone => 12,
            Material.Metal => 25,
            Material.Reinforced => null,
            _ => throw new ArgumentOutOfRangeException(nameof(material))
        };

        public void Reset()
        {
            tiles.Clear();
            for (int q = -9; q <= 9; q++)
            {
                tiles.Add(new Vector2Int(q, -1), Material.Reinforced);
                tiles.Add(new Vector2Int(q, 0), Material.Reinforced);
            }

            tiles.Add(TunnelCell, Material.PackedDirt);
            tiles.Add(new Vector2Int(1, 2), Material.Reinforced);
            tiles.Add(new Vector2Int(1, 3), Material.Reinforced);
            tiles.Add(new Vector2Int(0, 4), Material.Reinforced);

            tiles.Add(new Vector2Int(-7, 1), Material.SoftDirt);
            tiles.Add(new Vector2Int(-8, 2), Material.PackedDirt);
            tiles.Add(new Vector2Int(-7, 2), Material.SoftStone);
            tiles.Add(new Vector2Int(-7, 3), Material.SoftDirt);
            tiles.Add(new Vector2Int(-6, 3), Material.Metal);
            tiles.Add(new Vector2Int(-8, 6), Material.SoftStone);
            tiles.Add(new Vector2Int(-7, 6), Material.SoftStone);
            tiles.Add(new Vector2Int(-6, 6), Material.PackedDirt);
            Rebuild();
        }

        public void SetSmoothing(bool enabled)
        {
            if (Smooth == enabled)
                return;

            Smooth = enabled;
            Rebuild();
        }

        public string TryDig(Vector2Int cell, ref int energy)
        {
            if (!tiles.TryGetValue(cell, out var material))
                return "No tile in reach";
            if (DigCost(material) is not int cost)
                return "Reinforced terrain";
            if (energy < cost)
                return $"Need {cost} energy";

            tiles.Remove(cell);
            energy -= cost;
            Rebuild();
            return $"Excavated, spent {cost} energy";
        }

        public Vector2Int? FindDigTarget(Vector2 origin, Vector2 direction, float reach)
        {
            Vector2Int? target = null;
            foreach (var cell in tiles.Keys)
            {
                if (RayEntry(origin - Center(cell), direction, reach) is not float distance)
                    continue;
                if (distance == reach && target.HasValue)
                    continue;

                target = cell;
                reach = distance;
            }
            return target;
        }

        private static float? RayEntry(Vector2 origin, Vector2 direction, float reach)
        {
            float entry = 0f;
            for (int i = 0; i < Corners.Length; i++)
            {
                Vector2 start = Corners[i];
                Vector2 edge = Corners[(i + 1) % Corners.Length] - start;
                Vector2 outward = new(-edge.y, edge.x);
                float separation = Vector2.Dot(outward, origin - start);
                float speed = Vector2.Dot(outward, direction);
                if (Mathf.Abs(speed) < 0.00001f)
                {
                    if (separation > 0f)
                        return null;
                    continue;
                }

                float t = -separation / speed;
                if (speed < 0f)
                    entry = Mathf.Max(entry, t);
                else
                    reach = Mathf.Min(reach, t);
                if (entry > reach)
                    return null;
            }
            return entry;
        }

        public bool HasShelfCap(Vector2Int cell) => Smooth
            && tiles.ContainsKey(cell + Vector2Int.right)
            && !tiles.ContainsKey(cell + Vector2Int.up);

        private void Rebuild()
        {
            var paths = new List<Vector2[]>();
            foreach (var cell in tiles.Keys)
            {
                Vector2 center = Center(cell);
                var hex = new Vector2[Corners.Length];
                for (int i = 0; i < Corners.Length; i++)
                    hex[i] = center + Corners[i];
                paths.Add(hex);

                // Join equal-height peaks. A small overlap lets the composite merge the paths.
                if (HasShelfCap(cell))
                {
                    paths.Add(new[]
                    {
                        center + Vector2.up,
                        center + new Vector2(ColumnSpacing, 1f),
                        center + new Vector2(HalfWidth, 0.48f)
                    });
                }
            }

            source.pathCount = paths.Count;
            for (int i = 0; i < paths.Count; i++)
                source.SetPath(i, paths[i]);
            Collider.GenerateGeometry();
            Revision++;
        }
    }
}
