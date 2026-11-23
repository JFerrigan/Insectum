using UnityEngine;

namespace Insectum.Game
{
    internal static class AlienArt
    {
        private static readonly Sprite[,] actors = new Sprite[3, 6];
        private static readonly Sprite[] hazards = new Sprite[4];
        private static Texture2D terrain;
        private static Texture2D actorAtlas;
        private static Texture2D hazardAtlas;
        private static Sprite backdrop;

        internal static Texture2D Terrain => terrain ??= ForestArt.Load("AlienTerrain");

        internal static Sprite Backdrop
        {
            get
            {
                if (backdrop == null)
                {
                    Texture2D texture = ForestArt.Load("AlienBackdrop");
                    backdrop = ForestArt.Frame(texture, 0, 0, texture.width, texture.height, Vector2.one * 0.5f);
                }
                return backdrop;
            }
        }

        internal static Sprite Actor(int row, int frame) =>
            actors[row, frame] ??= ForestArt.Frame(actorAtlas ??= ForestArt.Load("AlienActors"),
                frame, row, 32, 24, new Vector2(0.5f, 0.18f));

        internal static Sprite Arc(int frame) =>
            hazards[frame] ??= ForestArt.Frame(hazardAtlas ??= ForestArt.Load("AlienHazards"),
                frame, 0, 32, 32, Vector2.one * 0.5f);

        internal static Sprite Beam => Arc(3);
    }
}
