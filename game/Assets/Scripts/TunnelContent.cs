using UnityEngine;

namespace Insectum.Game
{
    // Content shared by every world's tunnel levels. Route landings are always open floor, so supplies sit
    // on them rather than at offsets that could fall inside rock.
    internal static class TunnelContent
    {
        // A chest stands on its vault's landing.
        internal static Vector2 ChestPosition(LevelMarker marker) => marker.Position + Vector2.up * 0.49f;

        internal static void AddSupplies(LevelContent content, LevelLayout layout, int energySap)
        {
            content.AddFeature(LevelFeatureKind.Nektar, layout.Spawn + Vector2.up * 0.50f, 2);
            for (int i = 0; i < layout.Checkpoints.Count; i += 4)
            {
                Vector2 pickup = layout.Checkpoints[i].Landing + Vector2.up * 0.5f;
                content.AddFeature(LevelFeatureKind.Nektar, pickup, 2);
                if (i % 8 == 4)
                    content.AddFeature(LevelFeatureKind.HeartSap, pickup + Vector2.up * 0.6f, 1);
            }

            if (layout.Checkpoints.Count > 2)
                content.AddFeature(LevelFeatureKind.EnergySap, layout.Checkpoints[2].Landing + Vector2.up * 0.47f, energySap);
        }
    }
}
