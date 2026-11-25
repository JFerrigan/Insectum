using System;
using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    // Every world's fourth level: one sealed hall, about 1.6 screens wide and a screen tall, walled in bedrock
    // on all sides. The floor stays flat between the side terraces so bosses have a clear lane. Platforms rise
    // from the terraces one hex row at a time to a central perch, so an ordinary jump reaches every tier.
    internal static class BossArenaLayout
    {
        // The flat floor between the terraces, where ground bosses walk and charge.
        internal const float LaneHalfWidth = 16.2f;
        internal const float FloorY = 1f;
        // Where the lowest terrace step meets the floor on each side, and the ceiling's underside.
        internal const float TerraceFace = 17.32f;
        internal const float CeilingY = 18.5f;
        internal const float BossX = 8f;
        internal const float EchoX = -6f;
        private const float SpawnX = -15.5f;
        private const float ExitX = 16f;
        // Extend the rock above and below the hall by one terrain region. Keep four tile rows below
        // the fighting floor in the lowest camera view so the boss HUD sits over the rock.
        private const float VerticalRockPadding = HexGrid.RegionSize * HexGrid.RowSpacing;
        private const float ViewHalfWidth = 27.2f;
        private const float ViewBottom = FloorY - 4 * HexGrid.RowSpacing;
        private const float ViewTop = CeilingY + VerticalRockPadding;

        // One hex cell per character, two characters apart. Odd rows sit half a cell to the right, so they're
        // indented by one space, and the map reads the way it plays. The bottom line is hex row -1, and the floor
        // is row 0. The center cell of an even row sits at x = 0.
        //   #  bedrock (Reinforced)       =  the world's hard material      -  the world's soft material
        //   .  open air
        private static readonly string[] Map =
        {
            "# # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # #",
            " # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # #",
            "# # # # # . . . . # # # # . . . . . . . # # # . . . . . # # # # #",
            " # # # # . . . . . . # # . . . . . . . . . # . . . . . . # # # # #",
            "# # # . . . . . . . . . . . . . . . . . . . . . . . . . . . # # #",
            " # # . . . . . . . . . . . . . . . . . . . . . . . . . . . # # # #",
            "# # . . . . . . . . . . . . . . . . . . . . . . . . . . . # # # #",
            " # # # . . . . - - . . . . . . . . . . . . . . - - . . . . # # # #",
            "# # # . . . . . . . . . . . . # # # . . . . . . . . . . . . . # #",
            " # # . . . . . . . . - - - - . . . . - - - - . . . . . . . . # # #",
            "# # . . . = = = = = . . . . . . . . . . . . . = = = = = . . . # #",
            " # # = . . . . . . . . . . . . . . . . . . . . . . . . . . = # # #",
            "# # = = = . . . . . . . . . . . . . . . . . . . . . . . = = = # #",
            " # # = = = = . . . . . . . . . . . . . . . . . . . . = = = = # # #",
            "# # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # #",
            " # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # #"
        };
        private const int BottomRow = -1;
        private const int CenterColumn = 16;

        internal static LevelLayout Generate(int seed, int level, WorldId world)
        {
            (TerrainMaterial hard, TerrainMaterial soft) = Materials(world);
            var tiles = new Dictionary<Vector2Int, TerrainMaterial>();
            int topRow = BottomRow + Map.Length - 1;
            for (int line = 0; line < Map.Length; line++)
            {
                int r = topRow - line;
                int indent = r % 2 != 0 ? 1 : 0;
                string text = Map[line];
                for (int p = indent; p < text.Length; p += 2)
                {
                    TerrainMaterial? material = text[p] switch
                    {
                        '#' => TerrainMaterial.Reinforced,
                        '=' => hard,
                        '-' => soft,
                        '.' => null,
                        _ => throw new InvalidOperationException($"Boss arena row {r} has '{text[p]}' at {p}.")
                    };
                    int column = (p - indent) / 2;
                    if (material.HasValue)
                        tiles.Add(new Vector2Int(column - CenterColumn - FloorDiv(r, 2), r), material.Value);
                }
            }

            Rect hall = Footprint(tiles.Keys);
            Rect bounds = Rect.MinMaxRect(hall.xMin, hall.yMin - VerticalRockPadding, hall.xMax, hall.yMax + VerticalRockPadding);
            // Fill past the expanded bounds with unbreakable rock so the camera never reaches the terrain edge.
            int firstRow = Mathf.FloorToInt((bounds.yMin - PixelViewport.WorldHalfHeight) / HexGrid.RowSpacing) - 1;
            int lastRow = Mathf.CeilToInt((bounds.yMax + PixelViewport.WorldHalfHeight) / HexGrid.RowSpacing) + 1;
            for (int r = firstRow; r <= lastRow; r++)
            {
                int firstColumn = Mathf.FloorToInt((hall.xMin - PixelViewport.WorldHalfWidth) / HexGrid.ColumnSpacing - r / 2f) - 1;
                int lastColumn = Mathf.CeilToInt((hall.xMax + PixelViewport.WorldHalfWidth) / HexGrid.ColumnSpacing - r / 2f) + 1;
                for (int q = firstColumn; q <= lastColumn; q++)
                {
                    var cell = new Vector2Int(q, r);
                    if (!tiles.ContainsKey(cell) && !Inside(cell))
                        tiles.Add(cell, TerrainMaterial.Boundary);
                }
            }

            Rect view = Rect.MinMaxRect(-ViewHalfWidth, ViewBottom, ViewHalfWidth, ViewTop);
            var markers = new List<LevelMarker> { new(LevelMarkerKind.Boss, new Vector2(BossX, FloorY), Vector2Int.zero) };
            return new LevelLayout(seed, level, new LevelSeeds(seed, level), tiles, new List<MacroChunk>(),
                new List<RouteCheckpoint>(), markers, new Vector2(SpawnX, FloorY), new Vector2(ExitX, FloorY + 0.55f),
                bounds, 1, false, world, cameraBounds: view);
        }

        // Platforms are carved from each world's own diggable pair, so Worm and blasts can reshape them.
        private static (TerrainMaterial hard, TerrainMaterial soft) Materials(WorldId world) => world switch
        {
            WorldId.Swamp => (TerrainMaterial.Clay, TerrainMaterial.Mud),
            WorldId.Caves => (TerrainMaterial.HardStone, TerrainMaterial.SoftStone),
            WorldId.Desert => (TerrainMaterial.Sandstone, TerrainMaterial.Sand),
            WorldId.Alien => (TerrainMaterial.Metal, TerrainMaterial.AlienOrganic),
            _ => (TerrainMaterial.PackedDirt, TerrainMaterial.SoftDirt)
        };

        private static Rect Footprint(IEnumerable<Vector2Int> cells)
        {
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (Vector2Int cell in cells)
            {
                Vector2 center = HexGrid.Center(cell);
                xMin = Mathf.Min(xMin, center.x - HexGrid.HalfWidth);
                xMax = Mathf.Max(xMax, center.x + HexGrid.HalfWidth);
                yMin = Mathf.Min(yMin, center.y - 1f);
                yMax = Mathf.Max(yMax, center.y + 1f);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        // Whether a cell's center lies within the map's rows and its row's columns, open air included.
        private static bool Inside(Vector2Int cell)
        {
            int topRow = BottomRow + Map.Length - 1;
            if (cell.y < BottomRow || cell.y > topRow)
                return false;
            int column = cell.x + CenterColumn + FloorDiv(cell.y, 2);
            string text = Map[topRow - cell.y];
            int indent = cell.y % 2 != 0 ? 1 : 0;
            return column >= 0 && indent + column * 2 < text.Length;
        }

        private static int FloorDiv(int value, int divisor) =>
            value >= 0 ? value / divisor : -((-value + divisor - 1) / divisor);
    }
}
