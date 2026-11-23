using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Insectum.Game
{
    internal enum LevelFeatureKind
    {
        Nektar,
        HeartSap,
        EnergySap,
        Shop,
        Chest,
        BossReward
    }

    internal sealed class LevelFeature
    {
        public LevelFeatureKind Kind { get; }
        public Vector2 Position { get; }
        public ItemId Item { get; }
        public int Amount { get; }
        public bool Available { get; private set; } = true;
        public bool Interactable => Kind >= LevelFeatureKind.Shop;
        private readonly SpriteRenderer art;

        public LevelFeature(LevelFeatureKind kind, Vector2 position, ItemId item, int amount, SpriteRenderer art)
        {
            Kind = kind;
            Position = position;
            Item = item;
            Amount = amount;
            this.art = art;
        }

        public void Consume()
        {
            Available = false;
            if (art != null)
                art.gameObject.SetActive(false);
        }
    }

    internal sealed class LevelContent
    {
        private readonly GameSession session;
        private readonly Transform parent;
        private readonly Sprite pixel;
        private readonly PhysicsScene2D physics;
        private readonly List<LevelEnemy> enemies = new();
        private readonly List<LevelTrap> traps = new();
        private readonly List<LevelDart> darts = new();
        private readonly List<LevelFeature> features = new();
        private readonly Dictionary<Vector2Int, int> buriedNektar = new();
        private readonly Dictionary<Vector2Int, SpriteRenderer> buriedArt = new();
        private readonly ItemEventQueue events = new();
        private readonly SpriteRenderer blastArt;
        private float blastUntil;
        private int tick;
        private int statusUntil;
        private bool rewardPlaced;

        internal IReadOnlyList<LevelEnemy> Enemies => enemies;
        internal IReadOnlyList<LevelTrap> Traps => traps;
        internal IReadOnlyList<LevelFeature> Features => features;
        internal IReadOnlyDictionary<Vector2Int, int> BuriedNektar => buriedNektar;
        internal bool HasBoss { get; private set; }
        internal string BossName { get; private set; }
        internal ItemId BossPrize { private get; set; }
        internal Vector2 BossRewardPosition { private get; set; }
        internal bool BossRewardClaimed { get; private set; }
        internal bool ExitUnlocked => !HasBoss || BossRewardClaimed;
        internal int BossesRemaining
        {
            get
            {
                int count = 0;
                foreach (LevelEnemy enemy in enemies)
                    if (enemy.Boss && enemy.Damage.Health.Alive)
                        count++;
                return count;
            }
        }
        internal string BossGatePrompt
        {
            get
            {
                int remaining = BossesRemaining;
                if (remaining == 0)
                    return "Claim the boss reward";
                return session.Variant == LevelVariant.DoubleBoss ? $"Defeat bosses ({remaining} left)"
                    : $"Defeat {BossName} and claim its reward";
            }
        }
        internal string Status { get; private set; }
        private RunState Run => session.Run;

        internal LevelContent(GameSession session, Transform parent, Sprite pixel)
        {
            this.session = session;
            this.parent = parent;
            this.pixel = pixel;
            physics = parent.gameObject.scene.GetPhysicsScene2D();
            blastArt = pixel == null ? null : ForestArt.Place(parent, "Blast", Vector2.zero,
                ForestArt.Prop(ForestProp.Blast), 6);
            if (blastArt != null)
                blastArt.enabled = false;
            session.Terrain.Edited += OnTerrainEdited;
        }

        internal void AddEnemy(EnemySpec spec, Vector2 landing, int patrolOffset, bool echo = false)
        {
            var enemy = new LevelEnemy(parent, pixel, spec, landing, patrolOffset, FireDart, echo);
            enemies.Add(enemy);
            session.Combat.AddTarget(enemy.Damage);
            enemy.Damage.Resolved += (hit, outcome) =>
            {
                if (outcome != DamageOutcome.Applied)
                    return;
                events.Publish(ItemEvent.EnemyHit(enemy, hit));
                if (!enemy.Damage.Health.Alive)
                    events.Publish(ItemEvent.EnemyDefeated(enemy));
            };
            if (enemy.Boss)
            {
                HasBoss = true;
                BossName ??= spec.Name;
            }
        }

        internal void AddTrap(Vector2 landing, TrapStyle style = TrapStyle.Thorn) =>
            traps.Add(new LevelTrap(parent, pixel, landing, style));

        internal void AddFeature(LevelFeatureKind kind, Vector2 position, int amount = 0, ItemId item = ItemId.None)
        {
            var art = pixel == null ? null : ForestArt.Place(parent, kind.ToString(), position,
                ForestArt.Prop((ForestProp)kind), 3);
            if (art != null && kind < LevelFeatureKind.Shop)
                art.transform.localScale = Vector3.one * (kind == LevelFeatureKind.Nektar ? 0.52f : 0.75f);
            features.Add(new LevelFeature(kind, position, item, amount, art));
        }

        internal void BuryNektar(Vector2Int cell, int amount)
        {
            if (!buriedNektar.TryAdd(cell, amount))
                return;
            Vector2 position = HexGrid.Center(cell);
            if (pixel == null)
                return;
            var marker = ForestArt.Place(parent, "Buried Nektar", position, ForestArt.Prop(ForestProp.Nektar), 4);
            marker.transform.localScale = Vector3.one * 0.54f;
            marker.enabled = false;
            buriedArt.Add(cell, marker);
        }

        internal void Step(PlayerCommand command)
        {
            tick++;
            if (tick >= statusUntil)
                Status = null;
            Run.Inventory.Step();
            if (command.ItemPressed)
                UseActive(command);
            foreach (LevelEnemy enemy in enemies)
                if (Run.Alive)
                    enemy.Step(session.PlayerDamage);
            foreach (LevelTrap trap in traps)
                if (Run.Alive)
                    trap.Step(tick, session.PlayerDamage);

            for (int i = 0; i < darts.Count;)
            {
                if (!Run.Alive || darts[i].Step(physics, session.PlayerDamage))
                    i++;
                else
                    darts.RemoveAt(i);
            }
        }

        internal void FinishStep()
        {
            events.Drain(React);
            if (!Run.Alive)
                return;

            Vector2 position = session.Motor.Body.position;
            foreach (LevelFeature feature in features)
            {
                if (feature.Available && !feature.Interactable && (feature.Position - position).sqrMagnitude < 0.74f * 0.74f)
                    PickUp(feature);
            }
            UpdateBuriedArt(position);
            if (blastArt != null && Time.time >= blastUntil)
                blastArt.enabled = false;
        }

        internal void ShowStatus(string message)
        {
            Status = message;
            statusUntil = tick + 145;
        }

        internal string InteractPrompt()
        {
            LevelFeature feature = ClosestInteractable();
            if (feature == null)
                return null;
            return feature.Kind switch
            {
                LevelFeatureKind.Shop => $"E  Buy {Items.Name(feature.Item)}  ({feature.Amount} Nektar)",
                LevelFeatureKind.Chest => "E  Open chest",
                _ => $"E  Claim {Items.Name(feature.Item)}"
            };
        }

        internal bool TryInteract()
        {
            LevelFeature feature = ClosestInteractable();
            if (feature == null)
                return false;

            if (feature.Kind == LevelFeatureKind.Shop)
            {
                if (!Run.Inventory.TryBuy(feature.Item, feature.Amount))
                {
                    ShowStatus(Run.Inventory.Nektar < feature.Amount ? "Need more Nektar" : "Already equipped");
                    return true;
                }
            }
            else if (!Run.Inventory.Acquire(feature.Item))
                return true;

            if (feature.Kind == LevelFeatureKind.BossReward)
                BossRewardClaimed = true;
            feature.Consume();
            ShowStatus($"Acquired {Items.Name(feature.Item)}");
            return true;
        }

        private LevelFeature ClosestInteractable()
        {
            float best = 1.32f * 1.32f;
            LevelFeature closest = null;
            Vector2 position = session.Motor.Body.position;
            foreach (LevelFeature feature in features)
            {
                if (!feature.Available || !feature.Interactable)
                    continue;
                float distance = (feature.Position - position).sqrMagnitude;
                if (distance >= best)
                    continue;
                best = distance;
                closest = feature;
            }
            return closest;
        }

        private void PickUp(LevelFeature feature)
        {
            switch (feature.Kind)
            {
                case LevelFeatureKind.Nektar:
                    Run.Inventory.AddNektar(feature.Amount);
                    break;
                case LevelFeatureKind.HeartSap:
                    if (Run.Health == Run.MaxHealth)
                        return;
                    Run.RestoreHealth(feature.Amount);
                    ShowStatus("Recovered a heart");
                    break;
                case LevelFeatureKind.EnergySap:
                    if (Run.Energy >= Run.Character.MaxEnergy)
                        return;
                    Run.RestoreEnergy(feature.Amount + Run.Inventory.EnergySapBonus);
                    ShowStatus("Recovered ability energy");
                    break;
            }
            feature.Consume();
        }

        private void UseActive(PlayerCommand command)
        {
            if (Run.Inventory.ActiveCooldownSteps > 0)
            {
                ShowStatus("Active item is recharging");
                return;
            }

            Vector2 origin = session.Motor.Body.position;
            Vector2 forward = Vector2.right * session.Motor.FacingFor(command);
            switch (Run.Inventory.Active)
            {
                case ItemId.ExcavatorClaw:
                    Vector2 digDirection = command.Aim < 0 ? Vector2.down : forward;
                    if (session.Terrain.ExcavateAdjacent(origin, digDirection, Run.Character.Radius + 0.92f, null)
                        != ExcavationResult.Removed)
                    {
                        ShowStatus("No diggable tile in reach");
                        return;
                    }
                    Run.Inventory.BeginCooldown(108);
                    break;
                case ItemId.SeedBomb:
                    Vector2 aim = command.Aim > 0 ? Vector2.up : command.Aim < 0 ? Vector2.down : forward;
                    Blast(origin + aim * 1.45f, 2.15f, 2, 2);
                    Run.Inventory.BeginCooldown(228);
                    break;
                default:
                    ShowStatus("Active slot empty");
                    break;
            }
        }

        private void OnTerrainEdited(IReadOnlyList<TerrainEdit> edits)
        {
            foreach (TerrainEdit edit in edits)
                events.Publish(ItemEvent.TileRemoved(edit));
        }

        private void React(ItemEvent itemEvent)
        {
            switch (itemEvent.Kind)
            {
                case ItemEventKind.EnemyHit:
                    if (itemEvent.Hit.Source == DamageSource.Melee)
                        itemEvent.Enemy.Ignite(Run.Inventory.Count(ItemId.FlameGlands));
                    break;
                case ItemEventKind.EnemyDefeated:
                    EnemyDefeated(itemEvent.Enemy);
                    break;
                case ItemEventKind.TileRemoved:
                    Vector2Int cell = itemEvent.Tile.Cell;
                    if (buriedNektar.Remove(cell, out int amount))
                        AddFeature(LevelFeatureKind.Nektar, HexGrid.Center(cell), amount);
                    if (buriedArt.Remove(cell, out SpriteRenderer art))
                        art.gameObject.SetActive(false);
                    break;
            }
        }

        private void EnemyDefeated(LevelEnemy enemy)
        {
            Run.RestoreEnergy(Run.Inventory.Count(ItemId.FluxCore) * 6f);
            if (enemy.Nektar > 0)
                AddFeature(LevelFeatureKind.Nektar, enemy.Position, enemy.Nektar);
            if (enemy.Boss && !rewardPlaced && BossesRemaining == 0)
            {
                rewardPlaced = true;
                AddFeature(LevelFeatureKind.BossReward, BossRewardPosition, item: BossPrize);
                ShowStatus(session.Variant == LevelVariant.DoubleBoss ? "Both bosses defeated. Claim the reward."
                    : $"{BossName} defeated. Claim the reward.");
            }

            int volatility = Run.Inventory.Count(ItemId.VolatileBlood);
            if (volatility > 0)
                Blast(enemy.Position, 1.65f + Mathf.Min(volatility, 4) * 0.14f, volatility, volatility);
        }

        private void Blast(Vector2 position, float radius, int power, int damage)
        {
            session.Terrain.Explode(position, radius, power);
            float radiusSquared = radius * radius;
            foreach (LevelEnemy enemy in enemies)
            {
                Vector2 offset = enemy.Position - position;
                if (offset.sqrMagnitude <= radiusSquared)
                    enemy.Damage.Receive(new DamageHit(DamageSource.Explosion, damage, offset.normalized * 4f));
            }
            if (blastArt != null)
            {
                blastArt.transform.position = PixelViewport.Snap(position);
                blastArt.transform.localScale = Vector3.one * radius;
                blastArt.enabled = true;
                blastUntil = Time.time + 0.13f;
            }
        }

        private void FireDart(Vector2 origin, Vector2 direction)
        {
            if (direction.sqrMagnitude > 0f)
                darts.Add(new LevelDart(parent, pixel, origin, direction));
        }

        private void UpdateBuriedArt(Vector2 position)
        {
            float range = Run.Inventory.Count(ItemId.BurrowSense) * 1.2f;
            if (range > 0f)
                range += 3f;
            foreach (var marker in buriedArt.Values)
                marker.enabled = (position - (Vector2)marker.transform.position).sqrMagnitude < range * range;
        }
    }
}
