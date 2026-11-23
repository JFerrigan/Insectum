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
        private HexTerrain terrain;
        private Material surface;
        private Texture2D texture;
        private Mesh liningMesh;

        public static void Attach(HexTerrain terrain, WorldId world = WorldId.Forest)
        {
            var view = terrain.Root.AddComponent<HexTerrainView>();
            view.terrain = terrain;
            view.texture = WorldArt.Terrain(world);
            view.surface = new Material(Shader.Find("Sprites/Default")) { mainTexture = view.texture };
            terrain.RegionChanged += view.DrawRegion;
            foreach (Vector2Int region in terrain.Regions)
                view.DrawRegion(region);
            if (terrain.Tunnel != null)
                view.DrawLining();
        }

        private void DrawRegion(Vector2Int region)
        {
            vertices.Clear();
            uv.Clear();
            indices.Clear();
            foreach (Vector2Int cell in terrain.CellsInRegion(region))
                AddRock(cell);
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
            mesh.SetTriangles(indices, 0);
        }

        private void AddRock(Vector2Int cell)
        {
            int variation = (cell.x * 17 + cell.y * 31) & 3;
            TerrainMaterial material = terrain.Cells[cell];
            int row = (int)(material == TerrainMaterial.Sandstone ? TerrainMaterial.HardStone : material);
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

        private void DrawLining()
        {
            vertices.Clear();
            uv.Clear();
            indices.Clear();
            Rect moss = Tile(0, 10);
            foreach (var edge in terrain.Tunnel.Lining)
            {
                if (edge.to.x - edge.from.x < 0.01f)
                    continue;
                AddStrip(edge.from, edge.to, moss);
            }

            var art = new GameObject("Tunnel moss");
            art.transform.SetParent(transform, false);
            liningMesh = new Mesh { name = art.name };
            liningMesh.SetVertices(vertices);
            liningMesh.SetUVs(0, uv);
            liningMesh.SetTriangles(indices, 0);
            art.AddComponent<MeshFilter>().sharedMesh = liningMesh;
            var renderer = art.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = surface;
            renderer.sortingOrder = 1;
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
        }

        private void OnDestroy()
        {
            terrain.RegionChanged -= DrawRegion;
            foreach (Mesh mesh in meshes.Values)
                Destroy(mesh);
            Destroy(liningMesh);
            Destroy(surface);
        }
    }
}
