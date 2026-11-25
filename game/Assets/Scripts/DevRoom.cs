using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal static class DevRoom
    {
        internal const int LevelNumber = 0;
        private const float FloorY = 1f;
        private const float FirstPedestalX = 5f;
        private const float PedestalSpacing = 4f;

        internal static LevelLayout Generate(int seed)
        {
            var spawn = new Vector2(0f, FloorY);
            var exit = new Vector2(PedestalX(DevMode.ItemList.Length - 1) + 6f, FloorY + 0.55f);
            Rect bounds = Rect.MinMaxRect(-6f, -2f, exit.x + 6f, 15f);
            var tiles = new Dictionary<Vector2Int, TerrainMaterial>();

            // A continuous row of shelf caps makes the floor flat between the walls.
            for (int row = -3; row <= 12; row++)
            {
                int first = Mathf.FloorToInt(bounds.xMin / HexGrid.ColumnSpacing - row * 0.5f) - 1;
                int last = Mathf.CeilToInt(bounds.xMax / HexGrid.ColumnSpacing - row * 0.5f) + 1;
                for (int column = first; column <= last; column++)
                {
                    var cell = new Vector2Int(column, row);
                    float x = HexGrid.Center(cell).x;
                    if (row <= 0 || row >= 10 || x < -4f || x > exit.x + 4f)
                        tiles.Add(cell, TerrainMaterial.Boundary);
                }
            }

            return new LevelLayout(seed, LevelNumber, new LevelSeeds(seed, LevelNumber), tiles,
                new List<MacroChunk>(), new List<RouteCheckpoint>(), new List<LevelMarker>(),
                spawn, exit, bounds, 1, false, cameraBounds: bounds);
        }

        internal static void Populate(LevelContent content)
        {
            for (int i = 0; i < DevMode.ItemList.Length; i++)
                content.AddFeature(LevelFeatureKind.Shop, new Vector2(PedestalX(i), FloorY + 0.52f),
                    item: DevMode.ItemList[i]);
        }

        private static float PedestalX(int index) => FirstPedestalX + index * PedestalSpacing;
    }
}
