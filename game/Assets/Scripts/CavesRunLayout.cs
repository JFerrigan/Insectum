namespace Insectum.Game
{
    internal static class CavesRunLayout
    {
        internal static LevelLayout Generate(int seed, int level) =>
            RunWorlds.LocalLevel(level) == RunWorlds.LevelsPerWorld
                ? BossArenaLayout.Generate(seed, level, WorldId.Caves)
                : ForestTunnelLayout.Generate(seed, level, WorldId.Caves);
    }
}
