using UnityEngine;

namespace Insectum.Game
{
    internal static class ForestContent
    {
        private const uint ShopSalt = 0xE12D64A5u;
        internal static readonly EnemySpec BarkMite = new("Bark mite", EnemyStyle.Crawler, 2, 1, 0.45f,
            new Color32(198, 121, 78, 255));
        internal static readonly EnemySpec SporeMoth = new("Spore moth", EnemyStyle.Flyer, 2, 1, 0.37f,
            new Color32(188, 158, 227, 255));
        private static readonly EnemySpec Barkback = new("Barkback", EnemyStyle.ChargeBoss, 8, 4, 0.80f,
            new Color32(174, 99, 60, 255));

        private static readonly ItemId[] Passives =
        {
            ItemId.MothEyes, ItemId.BurrowSense, ItemId.Heartwood, ItemId.FlameGlands, ItemId.VolatileBlood,
            ItemId.EmergencyReboot
        };

        internal static void Populate(LevelContent content, LevelLayout layout)
        {
            var encounters = new SeededRandom(layout.Seeds.Encounters ^ 0x16AB42D9u);
            var shops = new SeededRandom(layout.Seeds.Loot ^ ShopSalt);
            var chests = new SeededRandom(layout.Seeds.Loot ^ 0x56882C71u);
            var prizes = new SeededRandom(layout.Seeds.Loot ^ 0x17CEC3D1u);
            var trail = Vector2.right * layout.RouteDirection;
            ItemId shopItem = layout.LevelNumber == 1
                ? (shops.Next(2) == 0 ? ItemId.ExcavatorClaw : ItemId.SeedBomb)
                : Passives[shops.Next(Passives.Length)];
            ItemId chestItem = layout.LevelNumber == 2
                ? AlternateActive(layout.RunSeed)
                : Passives[chests.Next(Passives.Length)];
            content.BossPrize = Passives[prizes.Next(Passives.Length)];
            content.BossRewardPosition = layout.Exit - trail * 3.6f;
            bool trapPlaced = false;
            bool chestPlaced = false;

            foreach (LevelMarker marker in layout.Markers)
            {
                switch (marker.Kind)
                {
                    case LevelMarkerKind.Encounter:
                        EnemySpec enemy = EnemySpec.Find(marker.Enemy) ?? (encounters.Next(2) == 0 ? BarkMite : SporeMoth);
                        content.AddEnemy(enemy, marker.Position, encounters.Next(96));
                        break;
                    case LevelMarkerKind.Shop:
                        content.AddFeature(LevelFeatureKind.Shop, marker.Position + Vector2.up * 0.52f, 4, shopItem);
                        break;
                    case LevelMarkerKind.Branch:
                        content.AddFeature(LevelFeatureKind.Chest, marker.Position + Vector2.up * 0.51f, item: chestItem);
                        if (layout.Tunnel == null)
                        {
                            Vector2 buried = marker.Position - Vector2.up + trail * HexGrid.ColumnSpacing;
                            content.BuryNektar(HexGrid.CellAt(buried), 2);
                        }
                        chestPlaced = true;
                        break;
                    case LevelMarkerKind.Trap:
                        content.AddTrap(marker.Position);
                        trapPlaced = true;
                        break;
                    case LevelMarkerKind.Shortcut:
                        content.BuryNektar(HexGrid.CellAt(marker.Position), 2);
                        break;
                    case LevelMarkerKind.Boss:
                        content.AddEnemy(Barkback, marker.Position, 0);
                        break;
                }
            }

            if (content.HasBoss)
            {
                content.AddFeature(LevelFeatureKind.HeartSap, layout.Spawn + trail * 1.3f + Vector2.up * 0.46f, 1);
                content.AddFeature(LevelFeatureKind.EnergySap, layout.Spawn + trail * 2.6f + Vector2.up * 0.46f, 35);
                return;
            }

            if (layout.Tunnel != null)
            {
                TunnelContent.AddSupplies(content, layout, 32);
                return;
            }

            content.AddFeature(LevelFeatureKind.Nektar, layout.Spawn + trail * 0.95f + Vector2.up * 0.50f, 2);
            if (layout.Checkpoints.Count > 0)
                content.AddFeature(LevelFeatureKind.Nektar, layout.Checkpoints[0].Landing - trail * 0.32f + Vector2.up * 0.49f, 2);
            if (layout.Checkpoints.Count > 1)
            {
                Vector2 landing = layout.Checkpoints[1].Landing + Vector2.up * 0.48f;
                content.AddFeature(LevelFeatureKind.HeartSap, landing + trail * 0.72f, 1);
                content.AddFeature(LevelFeatureKind.EnergySap, landing + trail * 1.50f, 32);
            }

            if (!chestPlaced)
                content.AddFeature(LevelFeatureKind.Chest, layout.Exit + trail * -2.65f, item: chestItem);
            if (!trapPlaced)
                content.AddTrap(layout.Spawn - trail * 1.50f);
        }

        private static ItemId AlternateActive(int seed)
        {
            var firstShop = new SeededRandom(new LevelSeeds(seed, 1).Loot ^ ShopSalt);
            return firstShop.Next(2) == 0 ? ItemId.SeedBomb : ItemId.ExcavatorClaw;
        }
    }
}
