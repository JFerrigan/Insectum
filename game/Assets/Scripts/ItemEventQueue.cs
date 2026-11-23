using System;
using System.Collections.Generic;

namespace Insectum.Game
{
    internal enum ItemEventKind
    {
        EnemyHit,
        EnemyDefeated,
        TileRemoved
    }

    internal readonly struct ItemEvent
    {
        public ItemEventKind Kind { get; }
        public LevelEnemy Enemy { get; }
        public DamageHit Hit { get; }
        public TerrainEdit Tile { get; }

        private ItemEvent(ItemEventKind kind, LevelEnemy enemy, DamageHit hit, TerrainEdit tile)
        {
            Kind = kind;
            Enemy = enemy;
            Hit = hit;
            Tile = tile;
        }

        public static ItemEvent EnemyHit(LevelEnemy enemy, DamageHit hit) => new(ItemEventKind.EnemyHit, enemy, hit, default);
        public static ItemEvent EnemyDefeated(LevelEnemy enemy) => new(ItemEventKind.EnemyDefeated, enemy, default, default);
        public static ItemEvent TileRemoved(TerrainEdit tile) => new(ItemEventKind.TileRemoved, null, default, tile);
    }

    internal sealed class ItemEventQueue
    {
        internal const int WorkLimit = 24;
        private readonly Queue<ItemEvent> pending = new();
        private bool draining;

        internal int PendingCount => pending.Count;
        internal void Publish(ItemEvent itemEvent) => pending.Enqueue(itemEvent);

        internal void Drain(Action<ItemEvent> consume)
        {
            if (draining)
                return;

            draining = true;
            try
            {
                for (int processed = 0; processed < WorkLimit && pending.Count > 0; processed++)
                    consume(pending.Dequeue());
            }
            finally
            {
                draining = false;
            }
        }
    }
}
