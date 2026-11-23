using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    public static class ForestRoute
    {
        public static readonly Vector2 Spawn = new(-6.5f, 1f);
        public static readonly Vector2 Exit = new(21.3f, -4.4f);
        public static readonly Rect Bounds = Rect.MinMaxRect(-19f, -7f, 24f, 3.5f);
        public static readonly Vector2Int ShortcutEntry = new(-8, -1);
        public static readonly Vector2Int ShortcutPlug = new(-5, -1);
        public const float KillHeight = -9f;

        public static HexTerrain Build(Transform parent, Sprite pixel)
        {
            var tiles = new Dictionary<Vector2Int, TerrainMaterial>();
            AddShelf(tiles, -6, -1, 0);
            AddShelf(tiles, 1, 3, 0);
            AddShelf(tiles, 5, 6, -1);
            AddShelf(tiles, 8, 9, -2);
            AddShelf(tiles, 11, 12, -3);
            AddShelf(tiles, -7, 16, -4);

            // The side entrance drops under the start, where one packed cell blocks the shortcut.
            AddShelf(tiles, -8, -7, -1);
            tiles[ShortcutEntry] = TerrainMaterial.SoftDirt;
            AddShelf(tiles, -8, -3, -2);
            AddShelf(tiles, -2, -1, -3);
            tiles.Add(ShortcutPlug, TerrainMaterial.PackedDirt);

            // The outside face near the optional side entrance is a wall-climb practice surface.
            tiles.Add(new Vector2Int(-9, -1), TerrainMaterial.Reinforced);
            tiles.Add(new Vector2Int(-10, 0), TerrainMaterial.Reinforced);
            tiles.Add(new Vector2Int(-10, 1), TerrainMaterial.Reinforced);
            tiles.Add(new Vector2Int(-11, 2), TerrainMaterial.Reinforced);

            var terrain = new HexTerrain(parent, tiles, TerrainMaterials.Load());
            if (pixel != null)
                HexTerrainView.Attach(terrain);

            MarkExit(parent, pixel, Exit);
            return terrain;
        }

        internal static SpriteRenderer MarkExit(Transform parent, Sprite pixel, Vector2 exit) =>
            pixel == null ? null : ForestArt.Place(parent, "Exit", exit + Vector2.up * 0.15f,
                ForestArt.Prop(ForestProp.Exit), 2);

        private static void AddShelf(Dictionary<Vector2Int, TerrainMaterial> tiles, int first, int last, int row)
        {
            for (int column = first; column <= last; column++)
                tiles.Add(new Vector2Int(column, row), TerrainMaterial.Reinforced);
        }

        internal static SpriteRenderer CreateMarker(Transform parent, string name, Vector2 position, Vector2 size,
            Sprite sprite, Color color, int order = 0)
        {
            if (sprite == null)
                return null;

            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = position;
            marker.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
