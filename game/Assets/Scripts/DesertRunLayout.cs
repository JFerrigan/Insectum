namespace Insectum.Game
{
    internal static class DesertRunLayout
    {
        internal static LevelLayout Generate(int seed, int level) =>
            RunWorlds.LocalLevel(level) == RunWorlds.LevelsPerWorld
                ? BossArenaLayout.Generate(seed, level, WorldId.Desert)
                : ForestTunnelLayout.Generate(seed, level, WorldId.Desert);
    }
}
