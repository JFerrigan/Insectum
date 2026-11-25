using UnityEngine;

namespace Insectum.Game
{
    internal static class AlienContent
    {
        internal static readonly EnemySpec LatticeTick = new("Lattice tick", EnemyStyle.LatticeTick, 3, 2, 0.45f,
            new Color32(201, 102, 182, 255));
        internal static readonly EnemySpec ArcMote = new("Arc mote", EnemyStyle.ArcMote, 2, 2, 0.36f,
            new Color32(102, 226, 183, 255));
        internal static readonly EnemySpec CrownRelay = new("Crown Relay", EnemyStyle.BeamBoss, 10, 6, 0.86f,
            new Color32(146, 223, 236, 255));

        internal static void Populate(LevelContent content, LevelLayout layout)
        {
            var encounters = new SeededRandom(layout.Seeds.Encounters ^ 0x902FE411u);
            var loot = new SeededRandom(layout.Seeds.Loot ^ 0xC83ADB27u);
            Vector2 trail = Vector2.right * layout.RouteDirection;
            content.BossPrize = ItemId.FluxCore;
            content.BossRewardPosition = layout.Exit - trail * 3.6f;
            foreach (LevelMarker marker in layout.Markers)
            {
                switch (marker.Kind)
                {
                    case LevelMarkerKind.Encounter:
                        content.AddEnemy(EnemySpec.Find(marker.Enemy) ?? (encounters.Next(2) == 0 ? LatticeTick : ArcMote), marker.Position, encounters.Next(96));
                        break;
                    case LevelMarkerKind.Boss:
                        content.AddEnemy(CrownRelay, marker.Position, 0);
                        break;
                    case LevelMarkerKind.Shop:
                        content.AddFeature(LevelFeatureKind.Shop, marker.Position + Vector2.up * 0.52f, 5,
                            loot.Next(2) == 0 ? ItemId.FluxCore : ItemId.VolatileBlood);
                        break;
                    case LevelMarkerKind.Branch:
                        content.AddFeature(LevelFeatureKind.Chest,
                            layout.Tunnel != null ? TunnelContent.ChestPosition(marker)
                                : marker.Position + trail * (HexGrid.ColumnSpacing * 3.4f) + Vector2.up * 0.49f,
                            item: layout.WorldLevel is 1 or 3 ? ItemId.FluxCore : ItemId.EmergencyReboot);
                        break;
                    case LevelMarkerKind.Trap:
                        content.AddTrap(marker.Position, TrapStyle.Arc);
                        break;
                    case LevelMarkerKind.Shortcut:
                        content.BuryNektar(HexGrid.CellAt(marker.Position), 5);
                        break;
                }
            }

            if (content.HasBoss)
            {
                content.AddFeature(LevelFeatureKind.HeartSap, layout.Spawn + trail * 1.3f + Vector2.up * 0.46f, 1);
                content.AddFeature(LevelFeatureKind.EnergySap, layout.Spawn + trail * 2.6f + Vector2.up * 0.46f, 34);
                return;
            }

            if (layout.Tunnel != null)
            {
                TunnelContent.AddSupplies(content, layout, 28);
                return;
            }

            content.AddFeature(LevelFeatureKind.Nektar, layout.Spawn + trail * 1.00f + Vector2.up * 0.48f, 2);
            Vector2 landing = layout.Checkpoints[0].Landing;
            content.AddFeature(LevelFeatureKind.EnergySap, landing + trail * 1.32f + Vector2.up * 0.47f, 28);
            content.AddFeature(LevelFeatureKind.HeartSap, landing - trail * 0.20f + Vector2.up * 0.48f, 1);
            if (layout.UsedFallback)
                content.AddTrap(layout.Spawn - trail * 1.52f, TrapStyle.Arc);
        }
    }
}
