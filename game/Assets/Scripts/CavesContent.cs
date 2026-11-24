using UnityEngine;

namespace Insectum.Game
{
    internal static class CavesContent
    {
        internal static readonly EnemySpec Hopper = new("Slate hopper", EnemyStyle.CaveHopper, 2, 1, 0.44f,
            new Color32(143, 166, 219, 255));
        internal static readonly EnemySpec GlowFly = new("Geode gnat", EnemyStyle.GlowFly, 2, 2, 0.34f,
            new Color32(169, 114, 243, 255));
        private static readonly EnemySpec Faultback = new("Faultback", EnemyStyle.QuakeBoss, 9, 5, 0.83f,
            new Color32(173, 138, 236, 255));

        internal static void Populate(LevelContent content, LevelLayout layout)
        {
            var encounters = new SeededRandom(layout.Seeds.Encounters ^ 0xBF711448u);
            var loot = new SeededRandom(layout.Seeds.Loot ^ 0x84BAD317u);
            Vector2 trail = Vector2.right * layout.RouteDirection;
            content.BossPrize = ItemId.PrismFang;
            content.BossRewardPosition = layout.Exit - trail * 3.6f;
            foreach (LevelMarker marker in layout.Markers)
            {
                switch (marker.Kind)
                {
                    case LevelMarkerKind.Encounter:
                        content.AddEnemy(EnemySpec.Find(marker.Enemy) ?? (encounters.Next(2) == 0 ? Hopper : GlowFly), marker.Position, encounters.Next(96));
                        break;
                    case LevelMarkerKind.Boss:
                        content.AddEnemy(Faultback, marker.Position, 0);
                        break;
                    case LevelMarkerKind.Shop:
                        content.AddFeature(LevelFeatureKind.Shop, marker.Position + Vector2.up * 0.52f, 5,
                            loot.Next(2) == 0 ? ItemId.PrismFang : ItemId.MothEyes);
                        break;
                    case LevelMarkerKind.Branch:
                        content.AddFeature(LevelFeatureKind.Chest,
                            layout.Tunnel != null ? TunnelContent.ChestPosition(marker)
                                : marker.Position + trail * (HexGrid.ColumnSpacing * 4f) + Vector2.up * 0.49f,
                            item: layout.WorldLevel == 1 ? ItemId.PrismFang : ItemId.MothEyes);
                        break;
                    case LevelMarkerKind.Trap:
                        content.AddTrap(marker.Position, TrapStyle.Crystal);
                        break;
                    case LevelMarkerKind.Shortcut:
                        content.BuryNektar(HexGrid.CellAt(marker.Position), 4);
                        break;
                }
            }

            if (content.HasBoss)
            {
                content.AddFeature(LevelFeatureKind.HeartSap, layout.Spawn + trail * 1.3f + Vector2.up * 0.46f, 1);
                content.AddFeature(LevelFeatureKind.EnergySap, layout.Spawn + trail * 2.6f + Vector2.up * 0.46f, 30);
                return;
            }

            if (layout.Tunnel != null)
            {
                TunnelContent.AddSupplies(content, layout, 28);
                return;
            }

            content.AddFeature(LevelFeatureKind.Nektar, layout.Spawn + trail * 1.05f + Vector2.up * 0.48f, 2);
            Vector2 landing = layout.Checkpoints[0].Landing;
            content.AddFeature(LevelFeatureKind.EnergySap, landing + trail * 1.32f + Vector2.up * 0.47f, 28);
            content.AddFeature(LevelFeatureKind.HeartSap, landing - trail * 0.20f + Vector2.up * 0.48f, 1);
            if (layout.UsedFallback)
                content.AddTrap(layout.Spawn - trail * 1.48f, TrapStyle.Crystal);
        }
    }
}
