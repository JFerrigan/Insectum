using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal static class SwampRunLayout
    {
        internal static LevelLayout Generate(int seed, int level) =>
            RunWorlds.LocalLevel(level) == RunWorlds.LevelsPerWorld
                ? BossArena(seed, level)
                : ForestTunnelLayout.Generate(seed, level, WorldId.Swamp);

        private static LevelLayout BossArena(int seed, int level)
        {
            var tiles = new Dictionary<Vector2Int, TerrainMaterial>();
            for (int q = -8; q <= 8; q++)
            {
                tiles.Add(new Vector2Int(q, 0), TerrainMaterial.Reinforced);
                tiles.Add(new Vector2Int(q, -1), q % 3 == 0 ? TerrainMaterial.Clay : TerrainMaterial.Mud);
            }

            // Piled reeds and backed clay leave the entire projectile lane open.
            for (int r = 1; r <= 3; r++)
            {
                tiles.Add(new Vector2Int(-9 - r / 2, r), TerrainMaterial.Reinforced);
                tiles.Add(new Vector2Int(9 - (r + 1) / 2, r), TerrainMaterial.Reinforced);
            }
            for (int q = -8; q <= -5; q++)
                tiles.Add(new Vector2Int(q, 4), TerrainMaterial.Clay);
            for (int q = 2; q <= 5; q++)
                tiles.Add(new Vector2Int(q, 4), TerrainMaterial.Mud);

            var markers = new List<LevelMarker> { new(LevelMarkerKind.Boss, new Vector2(4.0f, 1f), Vector2Int.zero) };
            return new LevelLayout(seed, level, new LevelSeeds(seed, level), tiles, new List<MacroChunk>(),
                new List<RouteCheckpoint>(), markers, new Vector2(-10.6f, 1f), new Vector2(11.8f, 1.55f),
                Rect.MinMaxRect(-18f, -4f, 18f, 9.5f), 1, false, WorldId.Swamp);
        }
    }
}
