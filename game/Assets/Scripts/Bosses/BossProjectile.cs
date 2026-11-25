using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    // Queries the current tiles directly, so a dig during the same fixed step takes effect immediately.
    internal sealed class BossTerrain
    {
        private readonly IReadOnlyDictionary<Vector2Int, TerrainMaterial> tiles;

        internal BossTerrain(IReadOnlyDictionary<Vector2Int, TerrainMaterial> tiles) => this.tiles = tiles;

        internal bool Sweep(Vector2 from, Vector2 to, out Vector2 impact)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            Vector2 direction = delta.normalized;
            float nearest = length;
            bool hit = false;
            // Each sample and its six neighbors cover the entire segment, including thin corner crossings.
            int samples = Mathf.Max(1, Mathf.CeilToInt(length));
            for (int i = 0; i <= samples; i++)
            {
                Vector2Int sample = HexGrid.CellAt(from + delta * (i / (float)samples));
                for (int side = -1; side < HexGrid.SideCount; side++)
                {
                    Vector2Int cell = side < 0 ? sample : HexGrid.Neighbor(sample, side);
                    if (tiles.ContainsKey(cell) && HexGrid.RayEntry(cell, from, direction, nearest) is float entry)
                    {
                        nearest = entry;
                        hit = true;
                    }
                }
                if (hit && length * (i / (float)samples) >= nearest)
                    break;
            }
            impact = from + direction * nearest;
            return hit;
        }
    }

    // A ballistic point that sweeps from its previous position to its next, even at high speed.
    internal sealed class BossProjectile
    {
        internal Vector2 Position { get; private set; }
        internal Vector2 Velocity { get; private set; }

        internal BossProjectile(Vector2 position, Vector2 velocity)
        {
            Position = position;
            Velocity = velocity;
        }

        internal bool Step(BossTerrain terrain, float gravity)
        {
            const float dt = 1f / 60f;
            Vector2 next = Position + (Velocity + Vector2.down * (gravity * dt * 0.5f)) * dt;
            Velocity += Vector2.down * (gravity * dt);
            bool hit = terrain.Sweep(Position, next, out Vector2 impact);
            Position = hit ? impact : next;
            return hit;
        }
    }
}
