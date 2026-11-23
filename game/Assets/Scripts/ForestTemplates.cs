using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal static class ForestTemplates
    {
        internal static readonly Vector2Int Entry = new(-4, 2);
        internal static readonly Vector2Int Exit = new(5, -2);
        internal static readonly Vector2Int PocketLanding = new(0, -5);

        // Adjacent rows overlap the rear of each landing. The two authored
        // variants share their entry and exit ledges and vary their inner shelf.
        private static readonly Vector2Int[] AlderRows =
        {
            new(-5, -3), new(-3, -1), new(-1, 1), new(1, 3), new(3, 5)
        };

        private static readonly Vector2Int[] BirchRows =
        {
            new(-5, -3), new(-4, -1), new(0, 1), new(0, 3), new(3, 5)
        };

        internal static Vector2Int MacroCenter(Vector2Int macro) =>
            new(11 * macro.x + 5 * macro.y, -5 * macro.x + 6 * macro.y);

        internal static Vector2Int Reflect(Vector2Int cell, bool mirrored) =>
            mirrored ? new Vector2Int(-cell.x - cell.y, cell.y) : cell;

        internal static Vector2Int Global(Vector2Int macro, Vector2Int local, bool mirrored) =>
            Reflect(MacroCenter(macro) + local, mirrored);

        internal static Vector2 Landing(Vector2Int macro, Vector2Int local, bool mirrored) =>
            HexGrid.Center(Global(macro, local, mirrored)) + Vector2.up;

        internal static string DescentId(MacroPort ports, bool birch)
        {
            bool entrance = (ports & MacroPort.UpperLeft) != 0;
            bool exit = (ports & MacroPort.LowerRight) != 0;
            string role = !entrance ? "Crown" : exit ? "RootStairs" : "Hollow";
            if ((ports & MacroPort.UpperRight) != 0)
                role += "Pocket";
            return $"Forest.{role}.{(birch ? "Birch" : "Alder")}";
        }

        internal static void StampDescent(Dictionary<Vector2Int, TerrainMaterial> tiles, Vector2Int macro, bool mirrored,
            bool birch = false, bool pocketStairs = false)
        {
            Vector2Int[] stairs = birch ? BirchRows : AlderRows;
            for (int i = 0; i < stairs.Length; i++)
            {
                Vector2Int span = stairs[i];
                for (int q = span.x; q <= span.y; q++)
                    Add(tiles, macro, new Vector2Int(q, 2 - i), mirrored, TerrainMaterial.Reinforced);
            }

            if (pocketStairs)
            {
                Add(tiles, macro, new Vector2Int(5, -1), mirrored, TerrainMaterial.Reinforced);
                Add(tiles, macro, new Vector2Int(5, 0), mirrored, TerrainMaterial.Reinforced);
            }
        }

        internal static void StampPocket(Dictionary<Vector2Int, TerrainMaterial> tiles, Vector2Int macro, bool mirrored)
        {
            Add(tiles, macro, PocketLanding, mirrored, TerrainMaterial.Reinforced);
            Add(tiles, macro, PocketLanding + Vector2Int.right, mirrored, TerrainMaterial.PackedDirt);
            Add(tiles, macro, PocketLanding + Vector2Int.right * 2, mirrored, TerrainMaterial.SoftDirt);
        }

        internal static void StampClosed(Dictionary<Vector2Int, TerrainMaterial> tiles, Vector2Int macro, bool mirrored)
        {
            Add(tiles, macro, new Vector2Int(-1, -1), mirrored, TerrainMaterial.SoftDirt);
            Add(tiles, macro, new Vector2Int(0, -1), mirrored, TerrainMaterial.PackedDirt);
            Add(tiles, macro, new Vector2Int(1, -1), mirrored, TerrainMaterial.SoftDirt);
            Add(tiles, macro, new Vector2Int(0, -2), mirrored, TerrainMaterial.SoftStone);
        }

        private static void Add(Dictionary<Vector2Int, TerrainMaterial> tiles, Vector2Int macro, Vector2Int local,
            bool mirrored, TerrainMaterial material)
        {
            if (HexGrid.Distance(Vector2Int.zero, local) > HexGrid.MacroRadius)
                throw new System.InvalidOperationException($"Forest template tile {local} is outside its macro chunk.");
            tiles.Add(Global(macro, local, mirrored), material);
        }

        internal static MacroPort Reflect(MacroPort ports, bool mirrored)
        {
            if (!mirrored)
                return ports;

            MacroPort result = ports & (MacroPort.Top | MacroPort.Bottom);
            if ((ports & MacroPort.UpperRight) != 0) result |= MacroPort.UpperLeft;
            if ((ports & MacroPort.LowerRight) != 0) result |= MacroPort.LowerLeft;
            if ((ports & MacroPort.LowerLeft) != 0) result |= MacroPort.LowerRight;
            if ((ports & MacroPort.UpperLeft) != 0) result |= MacroPort.UpperRight;
            return result;
        }
    }
}
