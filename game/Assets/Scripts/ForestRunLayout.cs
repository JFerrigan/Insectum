namespace Insectum.Game
{
    internal static class ForestRunLayout
    {
        internal const int LevelsPerWorld = RunWorlds.LevelsPerWorld;

        internal static LevelLayout Generate(int seed, int level) =>
            level == LevelsPerWorld
                ? BossArenaLayout.Generate(seed, level, WorldId.Forest)
                : ForestTunnelLayout.Generate(seed, level, WorldId.Forest);
    }
}
