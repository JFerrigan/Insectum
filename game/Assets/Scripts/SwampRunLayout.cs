namespace Insectum.Game
{
    internal static class SwampRunLayout
    {
        internal static LevelLayout Generate(int seed, int level) =>
            RunWorlds.LocalLevel(level) == RunWorlds.LevelsPerWorld
                ? BossArenaLayout.Generate(seed, level, WorldId.Swamp)
                : ForestTunnelLayout.Generate(seed, level, WorldId.Swamp);
    }
}
