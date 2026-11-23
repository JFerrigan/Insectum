using System;
using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    public enum ExcavationResult
    {
        Empty,
        Reinforced,
        InsufficientEnergy,
        Removed
    }

    public enum TerrainEditCause
    {
        Excavation,
        Explosion
    }

    public readonly struct TerrainEdit
    {
        public Vector2Int Cell { get; }
        public TerrainMaterial Material { get; }
        public TerrainEditCause Cause { get; }

        public TerrainEdit(Vector2Int cell, TerrainMaterial material, TerrainEditCause cause)
        {
            Cell = cell;
            Material = material;
            Cause = cause;
        }
    }

    public sealed class HexTerrain
    {
        private sealed class Region
        {
            public readonly Vector2Int Position;
            public readonly SortedSet<Vector2Int> Cells = new(Comparer<Vector2Int>.Create(HexGrid.Compare));
            public readonly List<Edge> Edges = new();
            public readonly List<EdgeCollider2D> Colliders = new();

            public Region(Vector2Int position) => Position = position;
        }

        private readonly struct Edge
        {
            public readonly Vector2Int Start;
            public readonly Vector2Int End;
            public readonly Region Owner;

            public Edge(Vector2Int start, Vector2Int end, Region owner)
            {
                Start = start;
                End = end;
                Owner = owner;
            }
        }

        private readonly Dictionary<Vector2Int, TerrainMaterial> cells = new();
        private readonly Dictionary<Vector2Int, Region> regions = new();
        private readonly Dictionary<Vector2Int, Edge> startEdges = new();
        private readonly Dictionary<Vector2Int, Edge> endEdges = new();
        private readonly List<Vector2Int> dugTunnelCells = new();
        private readonly List<EdgeCollider2D> tunnelColliders = new();

        public GameObject Root { get; }
        public IReadOnlyDictionary<Vector2Int, TerrainMaterial> Cells { get; }
        public IEnumerable<Vector2Int> Regions => regions.Keys;
        public TerrainMaterials Materials { get; }
        public int Revision { get; private set; }
        public event Action<Vector2Int> RegionChanged;
        public event Action<IReadOnlyList<TerrainEdit>> Edited;
        internal ForestTunnelSpace Tunnel { get; }
        internal IReadOnlyList<(Vector2 from, Vector2 to)> TunnelLining { get; private set; }
        internal event Action TunnelLiningChanged;

        public HexTerrain(Transform parent, IReadOnlyDictionary<Vector2Int, TerrainMaterial> layout, TerrainMaterials materials)
            : this(parent, layout, materials, null)
        {
        }

        internal HexTerrain(Transform parent, IReadOnlyDictionary<Vector2Int, TerrainMaterial> layout, TerrainMaterials materials,
            ForestTunnelSpace tunnel)
        {
            Tunnel = tunnel;
            Materials = materials;
            Cells = new System.Collections.ObjectModel.ReadOnlyDictionary<Vector2Int, TerrainMaterial>(cells);
            Root = new GameObject("Hex terrain");
            Root.transform.SetParent(parent, false);
            Root.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;

            foreach (var tile in layout)
            {
                cells.Add(tile.Key, tile.Value);
                Vector2Int regionPosition = HexGrid.RegionAt(tile.Key);
                if (!regions.TryGetValue(regionPosition, out Region region))
                {
                    region = new Region(regionPosition);
                    regions.Add(regionPosition, region);
                }
                region.Cells.Add(tile.Key);
            }

            Rebuild(regions.Keys);
            if (Tunnel != null)
            {
                TunnelLining = Tunnel.Lining;
                ForestTunnelSpace.BuildCollision(Root, TunnelLining, tunnelColliders);
            }
        }

        public IEnumerable<Vector2Int> CellsInRegion(Vector2Int region) =>
            regions.TryGetValue(region, out Region found) ? found.Cells : Array.Empty<Vector2Int>();

        public bool HasShelfCap(Vector2Int cell) => cells.ContainsKey(cell)
            && Tunnel?.IsRock(cell) != true
            && cells.ContainsKey(HexGrid.Neighbor(cell, 1))
            && Tunnel?.IsRock(HexGrid.Neighbor(cell, 1)) != true
            && !cells.ContainsKey(HexGrid.Neighbor(cell, 0));

        public ExcavationResult ExcavateAdjacent(Vector2 origin, Vector2 direction, float reach, Func<float, bool> spendEnergy,
            bool allowReinforced = false)
        {
            Vector2 localOrigin = Root.transform.InverseTransformPoint(origin);
            Vector2 localDirection = Root.transform.InverseTransformVector(direction).normalized;
            if (localDirection == Vector2.zero || reach < 0f)
                return ExcavationResult.Empty;

            Vector2Int originCell = HexGrid.CellAt(localOrigin);
            Vector2Int? target = null;
            for (int side = -1; side < HexGrid.SideCount; side++)
            {
                Vector2Int cell = side < 0 ? originCell : HexGrid.Neighbor(originCell, side);
                if (!cells.ContainsKey(cell))
                    continue;
                float? entry = Tunnel == null ? HexGrid.RayEntry(cell, localOrigin, localDirection, reach)
                    : Tunnel.RayEntry(cell, localOrigin, localDirection, reach);
                if (entry is not float hit)
                    continue;
                if (target.HasValue && hit == reach && HexGrid.Compare(cell, target.Value) >= 0)
                    continue;

                reach = hit;
                target = cell;
            }

            return target.HasValue ? Excavate(target.Value, spendEnergy, allowReinforced) : ExcavationResult.Empty;
        }

        public ExcavationResult Excavate(Vector2Int cell, Func<float, bool> spendEnergy = null, bool allowReinforced = false)
        {
            if (!cells.TryGetValue(cell, out TerrainMaterial material))
                return ExcavationResult.Empty;
            if (material == TerrainMaterial.Boundary || material == TerrainMaterial.Reinforced && !allowReinforced)
                return ExcavationResult.Reinforced;
            if (spendEnergy != null && !spendEnergy(Materials.Get(material).excavationEnergy))
                return ExcavationResult.InsufficientEnergy;

            Remove(new[] { cell }, TerrainEditCause.Excavation);
            return ExcavationResult.Removed;
        }

        public int Explode(Vector2 origin, float radius, int power)
        {
            if (radius < 0f || power <= 0)
                return 0;

            Vector2 localOrigin = Root.transform.InverseTransformPoint(origin);
            var targets = new List<Vector2Int>();
            float radiusSquared = radius * radius;
            foreach (var tile in cells)
            {
                if (tile.Value != TerrainMaterial.Reinforced && tile.Value != TerrainMaterial.Boundary && Materials.Get(tile.Value).blastResistance <= power
                    && (HexGrid.Center(tile.Key) - localOrigin).sqrMagnitude <= radiusSquared)
                    targets.Add(tile.Key);
            }

            if (targets.Count == 0)
                return 0;
            targets.Sort(HexGrid.Compare);
            Remove(targets, TerrainEditCause.Explosion);
            return targets.Count;
        }

        private void Remove(IReadOnlyList<Vector2Int> targets, TerrainEditCause cause)
        {
            var dirty = new HashSet<Vector2Int>();
            var edits = new TerrainEdit[targets.Count];
            bool tunnelChanged = false;
            for (int i = 0; i < targets.Count; i++)
            {
                Vector2Int cell = targets[i];
                edits[i] = new TerrainEdit(cell, cells[cell], cause);
                if (Tunnel?.IsRock(cell) == true)
                {
                    dugTunnelCells.Add(cell);
                    tunnelChanged = true;
                }
                Vector2Int region = HexGrid.RegionAt(cell);
                cells.Remove(cell);
                regions[region].Cells.Remove(cell);
                dirty.Add(region);
                for (int side = 0; side < HexGrid.SideCount; side++)
                    dirty.Add(HexGrid.RegionAt(HexGrid.Neighbor(cell, side)));
            }

            Revision++;
            if (tunnelChanged)
            {
                TunnelLining = Tunnel.DigLining(dugTunnelCells, Cells);
                ForestTunnelSpace.BuildCollision(Root, TunnelLining, tunnelColliders);
            }
            Rebuild(dirty);
            if (tunnelChanged)
                TunnelLiningChanged?.Invoke();
            Edited?.Invoke(edits);
        }

        private void Rebuild(IEnumerable<Vector2Int> positions)
        {
            var dirty = new List<Region>();
            var colliders = new HashSet<Region>();
            foreach (Vector2Int position in positions)
            {
                if (!regions.TryGetValue(position, out Region region))
                    continue;
                dirty.Add(region);
                colliders.Add(region);
                MarkNeighbors(region, colliders);
            }

            dirty.Sort((a, b) => HexGrid.Compare(a.Position, b.Position));
            foreach (Region region in dirty)
            {
                foreach (Edge edge in region.Edges)
                {
                    startEdges.Remove(edge.Start);
                    endEdges.Remove(edge.End);
                }
                region.Edges.Clear();
            }

            foreach (Region region in dirty)
                RegisterEdges(region);

            foreach (Region region in dirty)
                MarkNeighbors(region, colliders);

            var orderedColliders = new List<Region>(colliders);
            orderedColliders.Sort((a, b) => HexGrid.Compare(a.Position, b.Position));
            foreach (Region region in orderedColliders)
                RebuildColliders(region);

            Physics2D.SyncTransforms();
            foreach (Region region in dirty)
                RegionChanged?.Invoke(region.Position);
        }

        private void RegisterEdges(Region region)
        {
            foreach (Vector2Int cell in region.Cells)
            {
                if (Tunnel?.IsRock(cell) == true)
                    continue;
                for (int side = 0; side < HexGrid.SideCount; side++)
                {
                    Vector2Int neighbor = HexGrid.Neighbor(cell, side);
                    if (cells.ContainsKey(neighbor) && Tunnel?.IsRock(neighbor) != true)
                        continue;

                    Vector2Int start = HexGrid.Corner(cell, side);
                    Vector2Int end = HexGrid.Corner(cell, (side + 1) % HexGrid.SideCount);
                    if (side == 0 && HasShelfCap(cell))
                        end = HexGrid.Corner(HexGrid.Neighbor(cell, 1), 0);
                    else if (side == 5 && HasShelfCap(HexGrid.Neighbor(cell, 4)))
                        continue;

                    var edge = new Edge(start, end, region);
                    region.Edges.Add(edge);
                    startEdges.Add(start, edge);
                    endEdges.Add(end, edge);
                }
            }
        }

        private void MarkNeighbors(Region region, HashSet<Region> colliders)
        {
            foreach (Edge edge in region.Edges)
            {
                if (endEdges.TryGetValue(edge.Start, out Edge previous))
                    colliders.Add(previous.Owner);
                if (startEdges.TryGetValue(edge.End, out Edge next))
                    colliders.Add(next.Owner);
            }
        }

        private void RebuildColliders(Region region)
        {
            var visited = new HashSet<Vector2Int>();
            var points = new List<Vector2>();
            int count = 0;

            foreach (Edge edge in region.Edges)
            {
                if ((!endEdges.TryGetValue(edge.Start, out Edge previous) || previous.Owner != region)
                    && !visited.Contains(edge.Start))
                    BuildChain(edge, points, visited, count++);
            }

            foreach (Edge edge in region.Edges)
                if (!visited.Contains(edge.Start))
                    BuildChain(edge, points, visited, count++);

            for (int i = count; i < region.Colliders.Count; i++)
                region.Colliders[i].enabled = false;
        }

        private void BuildChain(Edge first, List<Vector2> points, HashSet<Vector2Int> visited, int index)
        {
            Region region = first.Owner;
            points.Clear();
            points.Add(HexGrid.Vertex(first.Start));
            Edge last = first;
            visited.Add(last.Start);
            while (true)
            {
                points.Add(HexGrid.Vertex(last.End));
                if (!startEdges.TryGetValue(last.End, out Edge next) || next.Owner != region || !visited.Add(next.Start))
                    break;
                last = next;
            }

            if (index == region.Colliders.Count)
                region.Colliders.Add(Root.AddComponent<EdgeCollider2D>());
            EdgeCollider2D collider = region.Colliders[index];
            collider.SetPoints(points);
            collider.useAdjacentStartPoint = endEdges.TryGetValue(first.Start, out Edge previous);
            collider.useAdjacentEndPoint = startEdges.TryGetValue(last.End, out Edge following);
            if (collider.useAdjacentStartPoint)
                collider.adjacentStartPoint = HexGrid.Vertex(previous.Start);
            if (collider.useAdjacentEndPoint)
                collider.adjacentEndPoint = HexGrid.Vertex(following.End);
            collider.enabled = true;
        }
    }
}
