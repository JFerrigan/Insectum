using UnityEngine;

namespace Insectum.Game
{
    internal static class DesertContent
    {
        internal static readonly EnemySpec DuneBeetle = new("Dune beetle", EnemyStyle.DuneBeetle, 3, 2, 0.48f,
            new Color32(219, 157, 93, 255));
        private static readonly EnemySpec SandWasp = new("Sand wasp", EnemyStyle.SandWasp, 2, 1, 0.36f,
            new Color32(218, 152, 93, 255));
        private static readonly EnemySpec DuneMaw = new("Dune Maw", EnemyStyle.BurrowBoss, 9, 5, 0.84f,
            new Color32(218, 125, 83, 255));

        internal static void Populate(LevelContent content, LevelLayout layout)
        {
            var encounters = new SeededRandom(layout.Seeds.Encounters ^ 0xA6DF3117u);
            var loot = new SeededRandom(layout.Seeds.Loot ^ 0xD07BAB52u);
            Vector2 trail = Vector2.right * layout.RouteDirection;
            content.BossPrize = ItemId.DuneMantle;
            content.BossRewardPosition = layout.Exit - trail * 3.6f;
            foreach (LevelMarker marker in layout.Markers)
            {
                switch (marker.Kind)
                {
                    case LevelMarkerKind.Encounter:
                        content.AddEnemy(encounters.Next(2) == 0 ? DuneBeetle : SandWasp, marker.Position, encounters.Next(96));
                        break;
                    case LevelMarkerKind.Boss:
                        content.AddEnemy(DuneMaw, marker.Position, 0);
                        break;
                    case LevelMarkerKind.Shop:
                        content.AddFeature(LevelFeatureKind.Shop, marker.Position + Vector2.up * 0.52f, 5,
                            loot.Next(2) == 0 ? ItemId.DuneMantle : ItemId.BurrowSense);
                        break;
                    case LevelMarkerKind.Branch:
                        content.AddFeature(LevelFeatureKind.Chest,
                            marker.Position + trail * (HexGrid.ColumnSpacing * 3f) + Vector2.up * 0.49f,
                            item: layout.WorldLevel == 1 ? ItemId.DuneMantle : ItemId.SeedBomb);
                        break;
                    case LevelMarkerKind.Trap:
                        content.AddTrap(marker.Position, TrapStyle.Heat);
                        break;
                    case LevelMarkerKind.Shortcut:
                        content.BuryNektar(HexGrid.CellAt(marker.Position), 4);
                        break;
                }
            }

            if (content.HasBoss)
            {
                content.AddFeature(LevelFeatureKind.HeartSap, layout.Spawn + trail * 1.3f + Vector2.up * 0.46f, 1);
                content.AddFeature(LevelFeatureKind.EnergySap, layout.Spawn + trail * 2.6f + Vector2.up * 0.46f, 32);
                return;
            }

            content.AddFeature(LevelFeatureKind.Nektar, layout.Spawn + trail * 1.00f + Vector2.up * 0.48f, 2);
            Vector2 landing = layout.Checkpoints[0].Landing;
            content.AddFeature(LevelFeatureKind.EnergySap, landing + trail * 1.31f + Vector2.up * 0.47f, 26);
            content.AddFeature(LevelFeatureKind.HeartSap, landing - trail * 0.19f + Vector2.up * 0.48f, 1);
            if (layout.UsedFallback)
                content.AddTrap(layout.Spawn - trail * 1.48f, TrapStyle.Heat);
        }
    }
}
