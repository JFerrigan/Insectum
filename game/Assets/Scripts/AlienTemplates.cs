using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal static class AlienTemplates
    {
        internal static readonly Vector2Int Plug = new(1, -4);

        internal static string DescentId(MacroPort ports, bool lattice)
        {
            string route = (ports & MacroPort.UpperLeft) == 0 ? "Membrane"
                : (ports & MacroPort.LowerRight) != 0 ? "Conduit"
                : (ports & MacroPort.UpperRight) != 0 ? "Nursery"
                : "Relay";
            return $"Alien.{route}.{(lattice ? "Lattice" : "Mycelium")}";
        }

        internal static void StampDescent(Dictionary<Vector2Int, TerrainMaterial> tiles, Vector2Int macro,
            bool mirrored, bool lattice, bool pocketStairs)
        {
            ForestTemplates.StampDescent(tiles, macro, mirrored, lattice, pocketStairs);
            Add(tiles, macro, new Vector2Int(-3, -1), mirrored, TerrainMaterial.AlienOrganic);
            Add(tiles, macro, new Vector2Int(-2, -2), mirrored, TerrainMaterial.AlienOrganic);
            Add(tiles, macro, new Vector2Int(-1, -2), mirrored, lattice ? TerrainMaterial.Metal : TerrainMaterial.AlienOrganic);
            Add(tiles, macro, new Vector2Int(-1, -3), mirrored, TerrainMaterial.Metal);
            if (lattice)
                Add(tiles, macro, new Vector2Int(0, -3), mirrored, TerrainMaterial.Metal);
        }

        internal static void StampPocket(Dictionary<Vector2Int, TerrainMaterial> tiles, Vector2Int macro, bool mirrored)
        {
            for (int q = 0; q <= 5; q++)
                Add(tiles, macro, new Vector2Int(q, -5), mirrored, q < 3 ? TerrainMaterial.Reinforced : TerrainMaterial.Metal);
            Add(tiles, macro, Plug, mirrored, TerrainMaterial.AlienOrganic);
            Add(tiles, macro, new Vector2Int(3, -2), mirrored, TerrainMaterial.Metal);
            Add(tiles, macro, new Vector2Int(4, -2), mirrored, TerrainMaterial.AlienOrganic);
        }

        internal static void StampClosed(Dictionary<Vector2Int, TerrainMaterial> tiles, Vector2Int macro, bool mirrored)
        {
            Add(tiles, macro, new Vector2Int(-1, 1), mirrored, TerrainMaterial.AlienOrganic);
            Add(tiles, macro, new Vector2Int(0, 0), mirrored, TerrainMaterial.Metal);
            Add(tiles, macro, new Vector2Int(1, 0), mirrored, TerrainMaterial.AlienOrganic);
            Add(tiles, macro, new Vector2Int(1, -1), mirrored, TerrainMaterial.Metal);
            Add(tiles, macro, new Vector2Int(2, -2), mirrored, TerrainMaterial.AlienOrganic);
        }

        private static void Add(Dictionary<Vector2Int, TerrainMaterial> tiles, Vector2Int macro,
            Vector2Int local, bool mirrored, TerrainMaterial material)
        {
            if (HexGrid.Distance(Vector2Int.zero, local) > HexGrid.MacroRadius)
                throw new System.InvalidOperationException($"Alien template tile {local} is outside its macro chunk.");
            tiles.Add(ForestTemplates.Global(macro, local, mirrored), material);
        }
    }
}
