using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal static class SwampTemplates
    {
        internal static readonly Vector2Int Plug = new(1, -4);

        internal static string DescentId(MacroPort ports, bool clayBank)
        {
            string route = (ports & MacroPort.UpperLeft) == 0 ? "Inlet"
                : (ports & MacroPort.LowerRight) != 0 ? "FloodSteps"
                : (ports & MacroPort.UpperRight) != 0 ? "Reedbank"
                : "Outflow";
            return $"Swamp.{route}.{(clayBank ? "Clay" : "Silt")}";
        }

        internal static void StampDescent(Dictionary<Vector2Int, TerrainMaterial> tiles, Vector2Int macro, bool mirrored,
            bool clayBank, bool pocketStairs)
        {
            ForestTemplates.StampDescent(tiles, macro, mirrored, clayBank, pocketStairs);
            Add(tiles, macro, new Vector2Int(1, -4), mirrored, TerrainMaterial.Mud);
            Add(tiles, macro, new Vector2Int(2, -4), mirrored, clayBank ? TerrainMaterial.Clay : TerrainMaterial.Mud);
            if (clayBank)
                Add(tiles, macro, new Vector2Int(1, -5), mirrored, TerrainMaterial.Clay);
        }

        internal static void StampPocket(Dictionary<Vector2Int, TerrainMaterial> tiles, Vector2Int macro, bool mirrored)
        {
            for (int q = 0; q <= 5; q++)
                Add(tiles, macro, new Vector2Int(q, -5), mirrored,
                    q < 3 ? TerrainMaterial.Reinforced : TerrainMaterial.Clay);
            Add(tiles, macro, Plug, mirrored, TerrainMaterial.Mud);
        }

        internal static void StampClosed(Dictionary<Vector2Int, TerrainMaterial> tiles, Vector2Int macro, bool mirrored)
        {
            Add(tiles, macro, new Vector2Int(-1, -1), mirrored, TerrainMaterial.Clay);
            Add(tiles, macro, new Vector2Int(0, -1), mirrored, TerrainMaterial.Mud);
            Add(tiles, macro, new Vector2Int(1, -1), mirrored, TerrainMaterial.Mud);
            Add(tiles, macro, new Vector2Int(2, -1), mirrored, TerrainMaterial.Clay);
            Add(tiles, macro, new Vector2Int(0, -2), mirrored, TerrainMaterial.Clay);
        }

        private static void Add(Dictionary<Vector2Int, TerrainMaterial> tiles, Vector2Int macro,
            Vector2Int local, bool mirrored, TerrainMaterial material)
        {
            if (HexGrid.Distance(Vector2Int.zero, local) > HexGrid.MacroRadius)
                throw new System.InvalidOperationException($"Swamp template tile {local} is outside its macro chunk.");
            tiles.Add(ForestTemplates.Global(macro, local, mirrored), material);
        }
    }
}
