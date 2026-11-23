using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal static class ForestRunLayout
    {
        internal const int LevelsPerWorld = RunWorlds.LevelsPerWorld;

        internal static LevelLayout Generate(int seed, int level) =>
            level == LevelsPerWorld ? BossArena(seed, level) : ForestTunnelLayout.Generate(seed, level, WorldId.Forest);

        private static LevelLayout BossArena(int seed, int level)
        {
            var tiles = new Dictionary<Vector2Int, TerrainMaterial>();
            for (int q = -8; q <= 8; q++)
            {
                tiles.Add(new Vector2Int(q, 0), TerrainMaterial.Reinforced);
                tiles.Add(new Vector2Int(q, -1), q <= -7 || q >= 7 ? TerrainMaterial.Reinforced : TerrainMaterial.PackedDirt);
            }

            // Outer roots define the room. Higher roots leave jump room above the charge lane.
            for (int row = 1; row <= 4; row++)
            {
                tiles.Add(new Vector2Int(-9 - row / 2, row), TerrainMaterial.Reinforced);
                tiles.Add(new Vector2Int(9 - (row + 1) / 2, row), TerrainMaterial.Reinforced);
            }
            for (int q = -8; q <= -6; q++)
                tiles.Add(new Vector2Int(q, 4), TerrainMaterial.Reinforced);
            for (int q = 2; q <= 5; q++)
                tiles.Add(new Vector2Int(q, 4), TerrainMaterial.Reinforced);

            var markers = new List<LevelMarker>
            {
                new(LevelMarkerKind.Boss, new Vector2(4.0f, 1f), Vector2Int.zero)
            };
            return new LevelLayout(seed, level, new LevelSeeds(seed, level), tiles, new List<MacroChunk>(),
                new List<RouteCheckpoint>(), markers, new Vector2(-10.6f, 1f), new Vector2(11.8f, 1.55f),
                Rect.MinMaxRect(-18f, -4f, 18f, 9.5f), 1, false);
        }
    }
}
