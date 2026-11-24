using UnityEngine;

namespace Insectum.Game
{
    internal static class PixelViewport
    {
        internal const int Width = 320;
        internal const int Height = 180;
        internal const int WorldWidth = Width * 3 / 2;
        internal const int WorldHeight = Height * 3 / 2;
        internal const float PixelsPerUnit = 16f;
        internal const float WorldHalfWidth = WorldWidth / (2f * PixelsPerUnit);
        internal const float WorldHalfHeight = WorldHeight / (2f * PixelsPerUnit);
#if !MAPGEN

        internal static Rect ScreenRect
        {
            get
            {
                float scale = Mathf.Min((float)Screen.width / Width, (float)Screen.height / Height);
                float width = Width * scale;
                float height = Height * scale;
                return new Rect(Mathf.Floor((Screen.width - width) * 0.5f), Mathf.Floor((Screen.height - height) * 0.5f),
                    width, height);
            }
        }

        internal static Vector2 Pointer
        {
            get
            {
                Rect viewport = ScreenRect;
                return (new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) - viewport.position)
                    / (viewport.width / Width);
            }
        }

        internal static Vector2 Snap(Vector2 position) =>
            new(Mathf.Round(position.x * PixelsPerUnit) / PixelsPerUnit,
                Mathf.Round(position.y * PixelsPerUnit) / PixelsPerUnit);
#endif
    }
}
