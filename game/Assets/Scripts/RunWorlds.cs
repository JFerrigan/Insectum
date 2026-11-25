using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Insectum.Game
{
    public enum WorldId
    {
        Forest,
        Swamp,
        Caves,
        Desert,
        Alien
    }

    internal static class RunWorlds
    {
        internal const int LevelsPerWorld = 4;
        private static readonly WorldId[] Order = { WorldId.Forest, WorldId.Swamp, WorldId.Caves, WorldId.Desert, WorldId.Alien };
        internal static int LevelCount => LevelsPerWorld * Order.Length;

        internal static int FirstLevel(WorldId world) => Array.IndexOf(Order, world) * LevelsPerWorld + 1;

        internal static WorldId AtLevel(int level) => Order[(Math.Max(1, level) - 1) / LevelsPerWorld];
        internal static int LocalLevel(int level) => level == 0 ? 0 : (Math.Max(1, level) - 1) % LevelsPerWorld + 1;

        // The first boss level after this one: every world's last level holds its boss.
        internal static int NextBossLevel(int level) => (Math.Max(0, level) / LevelsPerWorld + 1) * LevelsPerWorld;

        internal static LevelVariant ChooseVariant(LevelSeeds seeds, int worldLevel)
        {
            // The dev room and boss arenas have no variants.
            if (worldLevel == 0 || worldLevel == LevelsPerWorld)
                return LevelVariant.Standard;
            var random = new SeededRandom(seeds.Variants);
            return random.Next(8) switch
            {
                0 => LevelVariant.Night,
                1 => LevelVariant.HeavySpawn,
                _ => LevelVariant.Standard
            };
        }

#if !MAPGEN
        internal static void ApplyVariant(LevelContent content, LevelLayout layout, HexTerrain terrain, LevelVariant variant)
        {
            if (variant == LevelVariant.DoubleBoss)
            {
                for (int i = 0; i < content.Enemies.Count; i++)
                {
                    LevelEnemy enemy = content.Enemies[i];
                    if (!enemy.Boss)
                        continue;

                    // Each boss telegraphs independently from its own part of the arena.
                    content.AddEnemy(enemy.Spec, new Vector2(BossArenaLayout.EchoX, BossArenaLayout.FloorY), 0, true);
                    break;
                }
                content.ShowStatus("Double boss. Defeat both.");
            }
            else if (variant == LevelVariant.HeavySpawn)
            {
                AddReinforcements(content, layout, terrain);
                content.ShowStatus("Heavy spawn. More enemies ahead.");
            }
            else if (variant == LevelVariant.Night)
            {
                content.ShowStatus("Night. Watch the edges of your light.");
            }
        }

        private static void AddReinforcements(LevelContent content, LevelLayout layout, HexTerrain terrain)
        {
            EnemySpec spec = layout.World switch
            {
                WorldId.Swamp => SwampContent.ReedRunner,
                WorldId.Caves => CavesContent.Hopper,
                WorldId.Desert => DesertContent.DuneBeetle,
                WorldId.Alien => AlienContent.LatticeTick,
                _ => ForestContent.BarkMite
            };
            var landings = new List<Vector2>();
            foreach (RouteCheckpoint checkpoint in layout.Checkpoints)
            {
                Vector2 landing = checkpoint.Landing;
                if ((layout.Spawn - landing).sqrMagnitude <= 9f || (layout.Exit - landing).sqrMagnitude <= 9f)
                    continue;

                if (layout.Tunnel != null)
                {
                    if (!content.Enemies.Any(enemy => (enemy.Position - landing).sqrMagnitude < 4f))
                        landings.Add(landing + Vector2.right * 0.98f);
                    continue;
                }

                Vector2Int support = HexGrid.CellAt(landing - Vector2.up);
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector2Int left = sign < 0 ? support + Vector2Int.left : support;
                    if (!terrain.HasShelfCap(left))
                        continue;
                    Vector2 seat = landing + Vector2.right * (sign * 1.18f);
                    float leftX = HexGrid.Center(left).x;
                    if (seat.x >= leftX + 0.40f && seat.x <= leftX + HexGrid.ColumnSpacing - 0.40f)
                        landings.Add(seat);
                }
            }

            var random = new SeededRandom(layout.Seeds.Variants ^ 0xD1B54A35u);
            for (int count = 0; count < 2 && landings.Count > 0; count++)
            {
                int choice = random.Next(landings.Count);
                content.AddEnemy(spec, landings[choice], random.Next(96));
                landings.RemoveAt(choice);
            }
        }

#endif
        internal static LevelLayout Generate(int seed, int level) => AtLevel(level) switch
        {
            WorldId.Forest => ForestRunLayout.Generate(seed, level),
            WorldId.Swamp => SwampRunLayout.Generate(seed, level),
            WorldId.Caves => CavesRunLayout.Generate(seed, level),
            WorldId.Desert => DesertRunLayout.Generate(seed, level),
            WorldId.Alien => AlienRunLayout.Generate(seed, level),
            _ => throw new ArgumentOutOfRangeException(nameof(level))
        };

#if !MAPGEN
        internal static void Populate(LevelContent content, LevelLayout layout)
        {
            switch (layout.World)
            {
                case WorldId.Forest:
                    ForestContent.Populate(content, layout);
                    break;
                case WorldId.Swamp:
                    SwampContent.Populate(content, layout);
                    break;
                case WorldId.Caves:
                    CavesContent.Populate(content, layout);
                    break;
                case WorldId.Desert:
                    DesertContent.Populate(content, layout);
                    break;
                case WorldId.Alien:
                    AlienContent.Populate(content, layout);
                    break;
            }
        }
#endif
    }
}
