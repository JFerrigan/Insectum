using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal static class DesertTemplates
    {
        internal static readonly Vector2Int Plug = new(1, -4);

        internal static string DescentId(MacroPort ports, bool ledgestone)
        {
            string route = (ports & MacroPort.UpperLeft) == 0 ? "Rim"
                : (ports & MacroPort.LowerRight) != 0 ? "Wadi"
                : (ports & MacroPort.UpperRight) != 0 ? "Oasis"
                : "Basin";
            return $"Desert.{route}.{(ledgestone ? "Sandstone" : "Sand")}";
        }

        internal static void StampDescent(Dictionary<Vector2Int, TerrainMaterial> tiles, Vector2Int macro,
            bool mirrored, bool ledgestone, bool pocketStairs)
        {
            ForestTemplates.StampDescent(tiles, macro, mirrored, ledgestone, pocketStairs);
            Add(tiles, macro, new Vector2Int(-3, 0), mirrored, TerrainMaterial.Sandstone);
            Add(tiles, macro, new Vector2Int(-2, -1), mirrored, TerrainMaterial.Sand);
            Add(tiles, macro, new Vector2Int(-1, -1), mirrored, TerrainMaterial.Sand);
            Add(tiles, macro, new Vector2Int(-1, -2), mirrored, TerrainMaterial.Sandstone);
            if (ledgestone)
                Add(tiles, macro, new Vector2Int(0, -2), mirrored, TerrainMaterial.Sandstone);
        }

        internal static void StampPocket(Dictionary<Vector2Int, TerrainMaterial> tiles, Vector2Int macro, bool mirrored)
        {
            for (int q = 0; q <= 5; q++)
                Add(tiles, macro, new Vector2Int(q, -5), mirrored, q < 3 ? TerrainMaterial.Reinforced : TerrainMaterial.Sandstone);
            Add(tiles, macro, Plug, mirrored, TerrainMaterial.Sand);
            Add(tiles, macro, new Vector2Int(4, -4), mirrored, TerrainMaterial.Sandstone);
        }

        internal static void StampClosed(Dictionary<Vector2Int, TerrainMaterial> tiles, Vector2Int macro, bool mirrored)
        {
            Add(tiles, macro, new Vector2Int(-2, -1), mirrored, TerrainMaterial.Sand);
            Add(tiles, macro, new Vector2Int(-1, -1), mirrored, TerrainMaterial.Sand);
            Add(tiles, macro, new Vector2Int(0, -1), mirrored, TerrainMaterial.Sandstone);
            Add(tiles, macro, new Vector2Int(1, -1), mirrored, TerrainMaterial.Sand);
            Add(tiles, macro, new Vector2Int(0, -2), mirrored, TerrainMaterial.Sandstone);
        }

        private static void Add(Dictionary<Vector2Int, TerrainMaterial> tiles, Vector2Int macro, Vector2Int local,
            bool mirrored, TerrainMaterial material)
        {
            if (HexGrid.Distance(Vector2Int.zero, local) > HexGrid.MacroRadius)
                throw new System.InvalidOperationException($"Desert template tile {local} is outside its macro chunk.");
            tiles.Add(ForestTemplates.Global(macro, local, mirrored), material);
        }
    }
}
