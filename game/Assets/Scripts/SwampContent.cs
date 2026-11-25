using UnityEngine;

namespace Insectum.Game
{
    internal static class SwampContent
    {
        internal static readonly EnemySpec ReedRunner = new("Reed runner", EnemyStyle.ReedRunner, 2, 2, 0.44f,
            new Color32(111, 182, 142, 255));
        internal static readonly EnemySpec MarshSkimmer = new("Marsh skimmer", EnemyStyle.Skimmer, 2, 1, 0.35f,
            new Color32(101, 190, 193, 255));
        internal static readonly EnemySpec Queen = new(MireLich.Title, EnemyStyle.ArenaBoss, MireLich.Hearts, 4, 0.8f,
            new Color32(202, 81, 103, 255), BossKind.MireLich);

        internal static void Populate(LevelContent content, LevelLayout layout)
        {
            var encounters = new SeededRandom(layout.Seeds.Encounters ^ 0x54923E65u);
            var loot = new SeededRandom(layout.Seeds.Loot ^ 0x174938CBu);
            Vector2 trail = Vector2.right * layout.RouteDirection;
            content.BossPrize = ItemId.ReedFlask;
            content.BossRewardPosition = layout.Exit - trail * 3.6f;
            foreach (LevelMarker marker in layout.Markers)
            {
                switch (marker.Kind)
                {
                    case LevelMarkerKind.Encounter:
                        EnemySpec enemy = EnemySpec.Find(marker.Enemy) ?? (encounters.Next(2) == 0 ? ReedRunner : MarshSkimmer);
                        content.AddEnemy(enemy, marker.Position, encounters.Next(96));
                        break;
                    case LevelMarkerKind.Boss:
                        content.AddEnemy(Queen, marker.Position, 0, seed: layout.Seeds.Encounters);
                        break;
                    case LevelMarkerKind.Shop:
                        content.AddFeature(LevelFeatureKind.Shop, marker.Position + Vector2.up * 0.52f, 5,
                            loot.Next(2) == 0 ? ItemId.ReedFlask : ItemId.Heartwood);
                        break;
                    case LevelMarkerKind.Branch:
                        content.AddFeature(LevelFeatureKind.Chest,
                            layout.Tunnel != null ? TunnelContent.ChestPosition(marker)
                                : marker.Position + trail * (HexGrid.ColumnSpacing * 3f) + Vector2.up * 0.49f,
                            item: layout.WorldLevel == 1 ? ItemId.ReedFlask : ItemId.BurrowSense);
                        break;
                    case LevelMarkerKind.Trap:
                        content.AddTrap(marker.Position, TrapStyle.Gas);
                        break;
                    case LevelMarkerKind.Shortcut:
                        content.BuryNektar(HexGrid.CellAt(marker.Position), 3);
                        break;
                }
            }

            if (content.HasBoss)
            {
                content.AddFeature(LevelFeatureKind.HeartSap, layout.Spawn + trail * 1.30f + Vector2.up * 0.46f, 1);
                content.AddFeature(LevelFeatureKind.EnergySap, layout.Spawn + trail * 2.60f + Vector2.up * 0.46f, 32);
                return;
            }

            if (layout.Tunnel != null)
            {
                TunnelContent.AddSupplies(content, layout, 23);
                return;
            }

            content.AddFeature(LevelFeatureKind.Nektar, layout.Spawn + trail * 1.00f + Vector2.up * 0.48f, 2);
            Vector2 landing = layout.Checkpoints[0].Landing;
            content.AddFeature(LevelFeatureKind.EnergySap, landing + trail * 1.3f + Vector2.up * 0.47f, 23);
            content.AddFeature(LevelFeatureKind.HeartSap, landing - trail * 0.20f + Vector2.up * 0.48f, 1);
            if (layout.UsedFallback)
                content.AddTrap(layout.Spawn - trail * 1.5f, TrapStyle.Gas);
        }
    }
}
