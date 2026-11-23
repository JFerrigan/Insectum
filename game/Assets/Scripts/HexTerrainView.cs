using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal sealed class HexTerrainView : MonoBehaviour
    {
        private readonly Dictionary<Vector2Int, Mesh> meshes = new();
        private readonly List<Vector3> vertices = new();
        private readonly List<Vector2> uv = new();
        private readonly List<int> indices = new();
        private readonly List<Color32> shades = new();
        private Color32 shade = new(255, 255, 255, 255);
        private HexTerrain terrain;
        private Material surface;
        private Texture2D texture;
        private Mesh liningMesh;
        private readonly Dictionary<Vector2Int, Mesh> flavorMeshes = new();
        private readonly List<Color32> colors = new();
        private FlavorSet flavor;
        private Material flavorSurface;
        private int worldSalt;

        // Decals are placed once from the untouched level, then every cell draws only its own slice of
        // each decal overlapping it. Digging a cell removes just that slice; neighbours never change.
        private readonly struct Decal
        {
            public readonly Rect Bounds;
            public readonly Rect Uv;
            public readonly bool Flip;
            public readonly Color32 Tint;

            public Decal(Rect bounds, Rect uv, bool flip, Color32 tint)
            {
                Bounds = bounds; Uv = uv; Flip = flip; Tint = tint;
            }
        }

        private readonly Dictionary<Vector2Int, List<Decal>> decalsByCell = new();
        private readonly List<Vector2> clipA = new();
        private readonly List<Vector2> clipB = new();

        public static void Attach(HexTerrain terrain, WorldId world = WorldId.Forest)
        {
            var view = terrain.Root.AddComponent<HexTerrainView>();
            view.terrain = terrain;
            view.texture = WorldArt.Terrain(world);
            view.surface = new Material(Shader.Find("Sprites/Default")) { mainTexture = view.texture };
            view.flavor = FlavorArt.Get(world);
            view.flavorSurface = new Material(Shader.Find("Sprites/Default")) { mainTexture = view.flavor.Texture };
            view.worldSalt = (int)world * 104729;
            terrain.RegionChanged += view.OnRegionChanged;
            foreach (Vector2Int region in terrain.Regions)
                view.DrawRegion(region);
            foreach (Vector2Int region in terrain.Regions)
                foreach (Vector2Int cell in terrain.CellsInRegion(region))
                    view.PlaceDecal(cell);
            foreach (Vector2Int region in terrain.Regions)
                view.DrawFlavor(region);
            if (terrain.Tunnel != null)
            {
                terrain.TunnelLiningChanged += view.DrawLining;
                view.DrawLining();
            }
        }

        private void OnRegionChanged(Vector2Int region)
        {
            DrawRegion(region);
            DrawFlavor(region);
        }

        private void DrawRegion(Vector2Int region)
        {
            vertices.Clear();
            uv.Clear();
            indices.Clear();
            shades.Clear();
            foreach (Vector2Int cell in terrain.CellsInRegion(region))
                AddRock(cell);
            shade = new Color32(255, 255, 255, 255);
            foreach (Vector2Int cell in terrain.CellsInRegion(region))
                AddGrowth(cell);

            if (!meshes.TryGetValue(region, out Mesh mesh))
            {
                if (vertices.Count == 0)
                    return;
                var art = new GameObject($"Terrain art {region.x}, {region.y}");
                art.transform.SetParent(transform, false);
                mesh = new Mesh { name = art.name };
                mesh.MarkDynamic();
                art.AddComponent<MeshFilter>().sharedMesh = mesh;
                art.AddComponent<MeshRenderer>().sharedMaterial = surface;
                meshes.Add(region, mesh);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetColors(shades);
            mesh.SetTriangles(indices, 0);
        }

        private void AddRock(Vector2Int cell)
        {
            int variation = (cell.x * 17 + cell.y * 31) & 3;
            TerrainMaterial material = terrain.Cells[cell];
            int row = (int)(material switch
            {
                TerrainMaterial.Sandstone => TerrainMaterial.HardStone,
                TerrainMaterial.Boundary => TerrainMaterial.Reinforced,
                _ => material
            });
            shade = material == TerrainMaterial.Boundary ? new Color32(96, 88, 100, 255) : new Color32(255, 255, 255, 255);
            Rect tile = Tile(variation, row);
            Vector2 center = HexGrid.Center(cell);
            if (terrain.Tunnel?.TryGetPieces(cell, out List<Vector2[]> pieces) == true)
            {
                foreach (Vector2[] piece in pieces)
                    for (int i = 1; i + 1 < piece.Length; i++)
                        AddTriangle(piece[0], piece[i], piece[i + 1], RockUv(tile, piece[0] - center),
                            RockUv(tile, piece[i] - center), RockUv(tile, piece[i + 1] - center));
                return;
            }
            for (int side = 0; side < HexGrid.SideCount; side++)
            {
                Vector2 a = HexGrid.Vertex(HexGrid.Corner(cell, side));
                Vector2 b = HexGrid.Vertex(HexGrid.Corner(cell, (side + 1) % HexGrid.SideCount));
                AddTriangle(center, a, b, tile.center, RockUv(tile, a - center), RockUv(tile, b - center));
            }

            if (terrain.HasShelfCap(cell))
            {
                Vector2 a = HexGrid.Vertex(HexGrid.Corner(cell, 0));
                Vector2 b = HexGrid.Vertex(HexGrid.Corner(HexGrid.Neighbor(cell, 1), 0));
                Vector2 valley = HexGrid.Vertex(HexGrid.Corner(cell, 1));
                AddTriangle(a, b, valley, new Vector2(tile.xMin, tile.yMax),
                    new Vector2(tile.xMax, tile.yMax), new Vector2(tile.center.x, tile.yMax - tile.height / 4f));
            }
        }

        private void AddGrowth(Vector2Int cell)
        {
            if (terrain.Tunnel?.IsRock(cell) == true)
                return;
            Vector2 top = HexGrid.Vertex(HexGrid.Corner(cell, 0));
            bool cap = terrain.HasShelfCap(cell);
            Rect moss = Tile(0, 10);
            if (cap)
                AddStrip(top, HexGrid.Vertex(HexGrid.Corner(HexGrid.Neighbor(cell, 1), 0)), moss);
            else if (!terrain.Cells.ContainsKey(HexGrid.Neighbor(cell, 0)))
                AddStrip(top, HexGrid.Vertex(HexGrid.Corner(cell, 1)), moss);

            if (!terrain.HasShelfCap(HexGrid.Neighbor(cell, 4)) && !terrain.Cells.ContainsKey(HexGrid.Neighbor(cell, 5)))
                AddStrip(HexGrid.Vertex(HexGrid.Corner(cell, 5)), top, moss);

            int variation = (cell.x * 19 + cell.y * 7) & 15;
            if (!cap || variation > 3)
                return;

            Vector2 root = top + Vector2.right * 0.7f;
            Vector2 left = root - Vector2.right * 0.45f;
            Vector2 right = root + Vector2.right * 0.45f;
            Vector2 height = Vector2.up * 0.94f;
            AddQuad(left + height, right + height, left, right, Tile(1 + variation % 3, 10));
        }

        private void DrawFlavor(Vector2Int region)
        {
            vertices.Clear();
            shades.Clear();
            uv.Clear();
            indices.Clear();
            colors.Clear();
            foreach (Vector2Int cell in terrain.CellsInRegion(region))
                DrawCellFlavor(cell);

            if (!flavorMeshes.TryGetValue(region, out Mesh mesh))
            {
                if (vertices.Count == 0)
                    return;
                var art = new GameObject($"Terrain flavor {region.x}, {region.y}");
                art.transform.SetParent(transform, false);
                art.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                mesh = new Mesh { name = art.name };
                mesh.MarkDynamic();
                art.AddComponent<MeshFilter>().sharedMesh = mesh;
                art.AddComponent<MeshRenderer>().sharedMaterial = flavorSurface;
                flavorMeshes.Add(region, mesh);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
        }

        private int Hash(Vector2Int cell)
        {
            unchecked
            {
                int h = cell.x * 73856093 ^ cell.y * 19349663 ^ worldSalt;
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return h & 0x7fffffff;
            }
        }

        // Fully intact solid cell: present and not partially carved by a tunnel.
        private bool Solid(Vector2Int cell) =>
            terrain.Cells.ContainsKey(cell) && terrain.Tunnel?.TryGetPieces(cell, out _) != true;

        private bool Interior(Vector2Int cell)
        {
            if (!Solid(cell))
                return false;
            for (int side = 0; side < HexGrid.SideCount; side++)
                if (!Solid(HexGrid.Neighbor(cell, side)))
                    return false;
            return true;
        }

        private bool LargeCandidate(Vector2Int cell) => Hash(cell) % 40 == 0;

        private bool HugeCandidate(Vector2Int cell) => Hash(cell) % 70 == 7;

        private static IEnumerable<Vector2Int> Within(Vector2Int cell, int radius)
        {
            int span = radius * 2;
            for (int dx = -span; dx <= span; dx++)
            for (int dy = -span; dy <= span; dy++)
            {
                var other = new Vector2Int(cell.x + dx, cell.y + dy);
                if (other != cell && HexGrid.Distance(other, cell) <= radius)
                    yield return other;
            }
        }

        // A huge decal (6x4 units) needs every cell within two steps solid, interior and of one material,
        // and yields to any earlier-sorting huge candidate within five cells.
        private bool HasHuge(Vector2Int cell)
        {
            if (!HugeCandidate(cell) || !Interior(cell))
                return false;
            TerrainMaterial material = terrain.Cells[cell];
            if (flavor.Huge[material].Count == 0)
                return false;
            foreach (Vector2Int other in Within(cell, 2))
                if (!Interior(other) || terrain.Cells[other] != material)
                    return false;
            foreach (Vector2Int other in Within(cell, 5))
                if (HugeCandidate(other) && HexGrid.Compare(other, cell) < 0)
                    return false;
            return true;
        }

        private bool CoveredByHuge(Vector2Int cell)
        {
            foreach (Vector2Int other in Within(cell, 3))
                if (HugeCandidate(other) && HasHuge(other))
                    return true;
            return false;
        }

        // A large decal needs its whole hex ring solid and of one material, and yields to any
        // candidate within two cells that sorts earlier, so big decals never overlap.
        private bool HasLarge(Vector2Int cell)
        {
            if (!LargeCandidate(cell) || !Interior(cell))
                return false;
            TerrainMaterial material = terrain.Cells[cell];
            if (flavor.Large[material].Count == 0)
                return false;
            for (int side = 0; side < HexGrid.SideCount; side++)
            {
                Vector2Int ring = HexGrid.Neighbor(cell, side);
                if (terrain.Cells[ring] != material)
                    return false;
            }
            foreach (Vector2Int other in Nearby(cell))
                if (LargeCandidate(other) && HexGrid.Compare(other, cell) < 0)
                    return false;
            return true;
        }

        private static IEnumerable<Vector2Int> Nearby(Vector2Int cell)
        {
            var seen = new HashSet<Vector2Int> { cell };
            for (int a = 0; a < HexGrid.SideCount; a++)
            {
                Vector2Int n = HexGrid.Neighbor(cell, a);
                if (seen.Add(n))
                    yield return n;
                for (int b = 0; b < HexGrid.SideCount; b++)
                {
                    Vector2Int m = HexGrid.Neighbor(n, b);
                    if (seen.Add(m))
                        yield return m;
                }
            }
        }

        private void PlaceDecal(Vector2Int cell)
        {
            if (!Interior(cell))
                return;
            TerrainMaterial material = terrain.Cells[cell];
            if (material == TerrainMaterial.Boundary)
                return;
            int hash = Hash(cell);
            Color32 tint = Color32.Lerp(new Color32(255, 255, 255, 255), terrain.Materials.Get(material).tint, 0.3f);
            tint.a = 255;
            Vector2 center = HexGrid.Center(cell);
            bool flip = (hash >> 8 & 1) == 1;

            if (HasHuge(cell))
            {
                List<Rect> huge = flavor.Huge[terrain.Cells[cell]];
                AddDecal(center, FlavorArt.HugeWidth / 16f, FlavorArt.HugeHeight / 16f, huge[(hash >> 10) % huge.Count], flip, tint);
                return;
            }
            if ((LargeCandidate(cell) || hash % 6 == 1) && CoveredByHuge(cell))
                return;

            if (HasLarge(cell))
            {
                List<Rect> large = flavor.Large[material];
                Rect rect = large[(hash >> 10) % large.Count];
                float w = FlavorArt.LargeWidth / 16f, h = FlavorArt.LargeHeight / 16f;
                AddDecal(center, w, h, rect, flip, tint);
                return;
            }

            if (hash % 6 != 1)
                return;
            foreach (Vector2Int n in Nearby(cell))
                if (n == cell || HexGrid.Distance(n, cell) <= 1)
                    if (LargeCandidate(n) && HasLarge(n))
                        return;
            List<Rect> small = flavor.Small[material];
            if (small.Count == 0)
                return;
            Rect tile = small[(hash >> 10) % small.Count];
            Vector2 jitter = new(((hash >> 4) & 7) / 7f - 0.5f, ((hash >> 12) & 7) / 7f - 0.5f);
            AddDecal(center + jitter * 0.3f, 1f, 1f, tile, flip, tint);
        }

        private void AddDecal(Vector2 center, float width, float height, Rect rect, bool flip, Color32 tint)
        {
            var decal = new Decal(new Rect(center.x - width / 2f, center.y - height / 2f, width, height), rect, flip, tint);
            Vector2Int anchor = HexGrid.CellAt(center);
            int reach = Mathf.CeilToInt(Mathf.Max(width, height) / 1.5f) + 1;
            foreach (Vector2Int other in Within(anchor, reach))
                Register(other, decal);
            Register(anchor, decal);
        }

        private void Register(Vector2Int cell, Decal decal)
        {
            Vector2 c = HexGrid.Center(cell);
            var hex = new Rect(c.x - HexGrid.HalfWidth, c.y - 1f, HexGrid.ColumnSpacing, 2f);
            if (!hex.Overlaps(decal.Bounds))
                return;
            if (!decalsByCell.TryGetValue(cell, out List<Decal> list))
                decalsByCell[cell] = list = new List<Decal>();
            list.Add(decal);
        }

        private void DrawCellFlavor(Vector2Int cell)
        {
            if (!decalsByCell.TryGetValue(cell, out List<Decal> decals))
                return;
            if (terrain.Tunnel?.TryGetPieces(cell, out List<Vector2[]> pieces) == true)
            {
                foreach (Vector2[] piece in pieces)
                    foreach (Decal decal in decals)
                        AddClipped(piece, decal);
                return;
            }
            var hex = new Vector2[HexGrid.SideCount];
            for (int side = 0; side < HexGrid.SideCount; side++)
                hex[side] = HexGrid.Vertex(HexGrid.Corner(cell, side));
            foreach (Decal decal in decals)
                AddClipped(hex, decal);
        }

        // Clip a convex polygon to the decal's rectangle and emit it with UVs mapped into the decal.
        private void AddClipped(Vector2[] polygon, Decal decal)
        {
            clipA.Clear();
            clipA.AddRange(polygon);
            Rect b = decal.Bounds;
            ClipAxis(clipA, clipB, 0, b.xMin, true);
            ClipAxis(clipB, clipA, 0, b.xMax, false);
            ClipAxis(clipA, clipB, 1, b.yMin, true);
            ClipAxis(clipB, clipA, 1, b.yMax, false);
            if (clipA.Count < 3)
                return;
            Rect r = decal.Uv;
            Vector2 Uv(Vector2 p)
            {
                float u = (p.x - b.xMin) / b.width, v = (p.y - b.yMin) / b.height;
                if (decal.Flip)
                    u = 1f - u;
                return new Vector2(Mathf.Lerp(r.xMin, r.xMax, u), Mathf.Lerp(r.yMin, r.yMax, v));
            }
            for (int i = 1; i + 1 < clipA.Count; i++)
            {
                AddTriangle(clipA[0], clipA[i], clipA[i + 1], Uv(clipA[0]), Uv(clipA[i]), Uv(clipA[i + 1]));
                colors.Add(decal.Tint);
                colors.Add(decal.Tint);
                colors.Add(decal.Tint);
            }
        }

        private static void ClipAxis(List<Vector2> input, List<Vector2> output, int axis, float limit, bool keepAbove)
        {
            output.Clear();
            for (int i = 0; i < input.Count; i++)
            {
                Vector2 a = input[i], c = input[(i + 1) % input.Count];
                bool aIn = keepAbove ? a[axis] >= limit : a[axis] <= limit;
                bool cIn = keepAbove ? c[axis] >= limit : c[axis] <= limit;
                if (aIn)
                    output.Add(a);
                if (aIn != cIn)
                {
                    float t = (limit - a[axis]) / (c[axis] - a[axis]);
                    output.Add(Vector2.Lerp(a, c, t));
                }
            }
        }

        private void DrawLining()
        {
            vertices.Clear();
            shades.Clear();
            uv.Clear();
            indices.Clear();
            Rect moss = Tile(0, 10);
            foreach (var edge in terrain.TunnelLining)
            {
                if (edge.to.x - edge.from.x < 0.01f)
                    continue;
                AddStrip(edge.from, edge.to, moss);
            }

            if (liningMesh == null)
            {
                var art = new GameObject("Tunnel moss");
                art.transform.SetParent(transform, false);
                liningMesh = new Mesh { name = art.name };
                liningMesh.MarkDynamic();
                art.AddComponent<MeshFilter>().sharedMesh = liningMesh;
                var renderer = art.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = surface;
                renderer.sortingOrder = 1;
            }
            liningMesh.Clear();
            liningMesh.SetVertices(vertices);
            liningMesh.SetUVs(0, uv);
            liningMesh.SetTriangles(indices, 0);
        }

        private void AddStrip(Vector2 a, Vector2 b, Rect tile)
        {
            Vector2 inward = Vector2.down * 0.22f;
            AddQuad(a, b, a + inward, b + inward, tile);
        }

        private void AddQuad(Vector2 topLeft, Vector2 topRight, Vector2 bottomLeft, Vector2 bottomRight, Rect tile)
        {
            Vector2 topLeftUv = new(tile.xMin, tile.yMax);
            Vector2 topRightUv = new(tile.xMax, tile.yMax);
            Vector2 bottomLeftUv = new(tile.xMin, tile.yMin);
            Vector2 bottomRightUv = new(tile.xMax, tile.yMin);
            AddTriangle(topLeft, topRight, bottomLeft, topLeftUv, topRightUv, bottomLeftUv);
            AddTriangle(topRight, bottomRight, bottomLeft, topRightUv, bottomRightUv, bottomLeftUv);
        }

        private Rect Tile(int column, int row)
        {
            Rect rect = ForestArt.TexCoords(texture, column * 28, row * 32, 28, 32);
            float insetX = 0.5f / texture.width;
            float insetY = 0.5f / texture.height;
            return Rect.MinMaxRect(rect.xMin + insetX, rect.yMin + insetY, rect.xMax - insetX, rect.yMax - insetY);
        }

        private static Vector2 RockUv(Rect tile, Vector2 offset) =>
            new(Mathf.Lerp(tile.xMin, tile.xMax, offset.x / HexGrid.ColumnSpacing + 0.5f),
                Mathf.Lerp(tile.yMin, tile.yMax, offset.y * 0.5f + 0.5f));

        private void AddTriangle(Vector2 a, Vector2 b, Vector2 c, Vector2 uvA, Vector2 uvB, Vector2 uvC)
        {
            for (int i = 0; i < 3; i++)
                indices.Add(vertices.Count + i);
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            uv.Add(uvA);
            uv.Add(uvB);
            uv.Add(uvC);
            shades.Add(shade);
            shades.Add(shade);
            shades.Add(shade);
        }

        private void OnDestroy()
        {
            terrain.RegionChanged -= OnRegionChanged;
            terrain.TunnelLiningChanged -= DrawLining;
            foreach (Mesh mesh in meshes.Values)
                Destroy(mesh);
            Destroy(liningMesh);
            foreach (Mesh mesh in flavorMeshes.Values)
                Destroy(mesh);
            Destroy(flavorSurface);
            Destroy(surface);
        }
    }
}
