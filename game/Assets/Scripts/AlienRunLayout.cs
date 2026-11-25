namespace Insectum.Game
{
    internal static class AlienRunLayout
    {
        internal static LevelLayout Generate(int seed, int level) =>
            RunWorlds.LocalLevel(level) == RunWorlds.LevelsPerWorld
                ? BossArenaLayout.Generate(seed, level, WorldId.Alien)
                : ForestTunnelLayout.Generate(seed, level, WorldId.Alien);
    }
}
