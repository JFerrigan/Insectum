using UnityEngine;

namespace Insectum.Game
{
    internal static class WorldArt
    {
        internal static Texture2D Terrain(WorldId world) => world switch
        {
            WorldId.Swamp => SwampArt.Terrain,
            WorldId.Caves => CavesArt.Terrain,
            WorldId.Desert => DesertArt.Terrain,
            WorldId.Alien => AlienArt.Terrain,
            _ => ForestArt.Terrain
        };

        internal static Sprite Backdrop(WorldId world) => world switch
        {
            WorldId.Swamp => SwampArt.Backdrop,
            WorldId.Caves => CavesArt.Backdrop,
            WorldId.Desert => DesertArt.Backdrop,
            WorldId.Alien => AlienArt.Backdrop,
            _ => ForestArt.Backdrop
        };
    }
}
