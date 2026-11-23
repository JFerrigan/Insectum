using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Insectum.Game.Editor.ProductionVerification;

namespace Insectum.Game.Editor
{
    internal static class ForestRunVerification
    {
        internal static void Verify()
        {
            using var fixture = new PhysicsFixture("Forest run verification");
            VerifyInventory();
            VerifyEventBudget();
            VerifyRepeatability(fixture);
            VerifyTrap();
            VerifyActiveAndBuriedNektar(fixture);
            VerifyCombatItems(fixture);
            VerifyCompleteRun(fixture);
            VerifyOtherCharacters(fixture);
            VerifyDefeatAndRevive(fixture);
            Debug.Log("Insectum Forest run verified: four runtime levels, optional chest and shop, restoration, repeated seeds, item cascades, boss reward, defeat and restart.");
        }

        private static void VerifyInventory()
        {
            var run = new RunState(Character.Roster[0], 9);
            run.EnterNextLevel();
            RunInventory inventory = run.Inventory;
            inventory.AddNektar(5);
            Require(!inventory.TryBuy(ItemId.ExcavatorClaw, 6) && !inventory.TryBuy(ItemId.None, 1)
                && !inventory.TryBuy(ItemId.SeedBomb, -1) && inventory.Nektar == 5 && inventory.Active == ItemId.None,
                "A failed purchase changed Nektar or items.");
            Require(inventory.TryBuy(ItemId.ExcavatorClaw, 4) && inventory.Nektar == 1 &&
                inventory.Active == ItemId.ExcavatorClaw, "The active-item purchase did not commit both changes.");
            Require(!inventory.TryBuy(ItemId.ExcavatorClaw, 1) && inventory.Nektar == 1 &&
                inventory.TryBuy(ItemId.SeedBomb, 1) && inventory.Nektar == 0 && inventory.Active == ItemId.SeedBomb,
                "Active replacement or duplicate-purchase atomicity failed.");

            int maximum = run.MaxHealth;
            run.TakeDamage(1);
            for (int index = 0; index < 20; index++)
                Require(inventory.Acquire(ItemId.Heartwood), "A legal passive stack was rejected.");
            Require(inventory.Count(ItemId.Heartwood) == 20 && run.MaxHealth == maximum + 20 &&
                run.Health == run.MaxHealth - 1, "Heartwood did not stack or preserve the missing heart.");
            float vision = inventory.NightVisionRadius;
            inventory.Acquire(ItemId.MothEyes);
            inventory.Acquire(ItemId.MothEyes);
            Require(Mathf.Abs(inventory.NightVisionRadius - (vision + 3f)) < 0.001f && inventory.Count(ItemId.MothEyes) == 2
                && inventory.Active == ItemId.SeedBomb, $"Passive vision did not stack independently of the active slot. Radius {inventory.NightVisionRadius}, previous {vision}, count {inventory.Count(ItemId.MothEyes)}, active {inventory.Active}.");
        }

        private static void VerifyEventBudget()
        {
            var queue = new ItemEventQueue();
            var observed = new List<int>();
            for (int index = 0; index <= ItemEventQueue.WorkLimit; index++)
                Enqueue(index);

            queue.Drain(itemEvent =>
            {
                int current = itemEvent.Tile.Cell.x;
                observed.Add(current);
                if (current == 0)
                {
                    Enqueue(1000);
                    queue.Drain(_ => throw new InvalidOperationException("The event limit could be bypassed recursively."));
                }
            });
            Require(observed.SequenceEqual(Enumerable.Range(0, ItemEventQueue.WorkLimit)) && queue.PendingCount == 2,
                "Item events exceeded their fixed-step budget or were reordered in a chain.");
            queue.Drain(itemEvent => observed.Add(itemEvent.Tile.Cell.x));
            Require(observed.TakeLast(2).SequenceEqual(new[] { ItemEventQueue.WorkLimit, 1000 }) && queue.PendingCount == 0,
                "Deferred item events were lost or reordered on the next step.");

            void Enqueue(int number) => queue.Publish(ItemEvent.TileRemoved(
                new TerrainEdit(new Vector2Int(number, 0), TerrainMaterial.SoftDirt, TerrainEditCause.Explosion)));
        }

        private static void VerifyRepeatability(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            const int seed = 7123;
            session.StartGenerated(Character.Roster[1], seed);
            string geometry = ForestGenerator.Signature(session.Layout);
            var features = session.Content.Features.Select(f => (f.Kind, f.Item, f.Position, f.Amount)).ToArray();
            var enemies = session.Content.Enemies.Select(e => (e.Name, e.Position)).ToArray();

            var encounterRandom = new SeededRandom(session.Layout.Seeds.Encounters);
            for (int i = 0; i < 250; i++)
                encounterRandom.NextUInt();

            session.StartGenerated(Character.Roster[1], seed);
            Require(geometry == ForestGenerator.Signature(session.Layout)
                && features.SequenceEqual(session.Content.Features.Select(f => (f.Kind, f.Item, f.Position, f.Amount)))
                && enemies.SequenceEqual(session.Content.Enemies.Select(e => (e.Name, e.Position))),
                "The three Forest streams did not reproduce layout, encounters, and loot.");
        }

        private static void VerifyTrap()
        {
            var trap = new LevelTrap(null, null, Vector2.zero);
            var player = new DamageReceiver(new HealthPool(3), 0.43f, () => Vector2.up * 0.42f, 20);
            DamageSource? hitSource = null;
            player.Resolved += (hit, _) => hitSource = hit.Source;
            bool warned = false;
            for (int tick = 0; tick < LevelTrap.Cycle; tick++)
            {
                trap.Step(tick, player);
                if (trap.Warning)
                {
                    warned = true;
                    Require(player.Health.Current == 3, "The trap attacked during its warning.");
                }
                if (trap.Extended)
                    Require(warned, "A Forest trap extended before a visible warning.");
            }
            Require(player.Health.Current == 2 && hitSource == DamageSource.Trap,
                "The extended thorns did not resolve through shared trap damage.");
        }

        private static void VerifyActiveAndBuriedNektar(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            session.StartGenerated(Character.Roster[4], 7124);
            Vector2Int cell = session.Content.BuriedNektar.Keys.Single();
            Vector2 center = HexGrid.Center(cell);
            session.Motor.Warp(center + Vector2.up * (1.08f + session.Run.Character.Radius));
            float energy = session.Run.Energy;
            session.Run.Inventory.Acquire(ItemId.ExcavatorClaw);
            fixture.Step(new PlayerCommand(aim: -1, itemPressed: true));
            Require(!session.Terrain.Cells.ContainsKey(cell) && session.Run.Energy == energy && session.Run.Inventory.ActiveCooldownSteps > 0
                && !session.Content.BuriedNektar.ContainsKey(cell)
                && session.Content.Features.Any(f => f.Kind == LevelFeatureKind.Nektar && f.Position == center),
                "Excavator Claw did not expose buried Nektar through the terrain event without spending ability energy.");

            session.StartGenerated(Character.Roster[0], 7124);
            cell = session.Content.BuriedNektar.Keys.Single();
            center = HexGrid.Center(cell);
            session.Run.Inventory.Acquire(ItemId.SeedBomb);
            session.Motor.Warp(center + Vector2.up * (1.08f + session.Run.Character.Radius));
            fixture.Step(new PlayerCommand(aim: -1, itemPressed: true));
            Require(!session.Terrain.Cells.ContainsKey(cell) && session.Run.Inventory.ActiveCooldownSteps > 0
                && session.Content.Features.Any(f => f.Kind == LevelFeatureKind.Nektar && f.Position == center),
                "Seed Bomb did not destroy packed dirt through the shared terrain blast.");
        }

        private static void VerifyCombatItems(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            session.StartGenerated(Character.Roster[0], 7125);
            session.Run.Inventory.Acquire(ItemId.FlameGlands);
            session.Run.Inventory.Acquire(ItemId.VolatileBlood);
            var testSpec = new EnemySpec("Fixture mite", EnemyStyle.Crawler, 2, 2, 0.35f, default);
            Vector2 point = new(session.Layout.Spawn.x + 55f, session.Layout.Spawn.y + 16f);
            session.Content.AddEnemy(testSpec, point, 0);
            session.Content.AddEnemy(testSpec, point + Vector2.right, 0);
            LevelEnemy primary = session.Content.Enemies[^2];
            LevelEnemy secondary = session.Content.Enemies[^1];
            primary.Damage.Receive(new DamageHit(DamageSource.Melee, 1));
            secondary.Damage.Receive(new DamageHit(DamageSource.Trap, 1));
            fixture.Advance(36);
            Require(!primary.Damage.Health.Alive && !secondary.Damage.Health.Alive &&
                session.Content.Features.Count(feature => feature.Kind == LevelFeatureKind.Nektar && feature.Amount == 2
                    && feature.Position.y > point.y - 1f) == 2,
                "The melee burn failed to trigger a Volatile Blood kill chain and both Nektar drops.");
        }

        private static void VerifyCompleteRun(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            const int seed = 7126;
            Character character = Character.Roster[3];
            session.StartGenerated(character, seed);
            RunState run = session.Run;
            fixture.Advance(8);
            LevelFeature shop = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.Shop);
            BiomeVerification.Walk(fixture, () => run.Inventory.Nektar >= shop.Amount && Near(session, shop.Position, 1.2f), 350);
            int wallet = run.Inventory.Nektar;
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(!shop.Available && run.Inventory.Active == shop.Item && run.Inventory.Nektar == wallet - shop.Amount,
                "The route pickups did not fund the first shop purchase through E.");
            ItemId active = run.Inventory.Active;

            BiomeVerification.Walk(fixture, () => session.AtExit && session.Motor.Grounded, 500);
            LevelFeature chest = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.Chest);
            if (session.Layout.Markers.Any(marker => marker.Kind == LevelMarkerKind.Branch))
                ReachPocketChest(fixture);
            Require(Near(session, chest.Position, 1.25f), "The optional chest could not be reached from the Forest route.");
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(!chest.Available && run.Inventory.Count(chest.Item) == 1, "E did not award the chest's passive.");

            MoveTo(fixture, session.Layout.Exit.x - 0.32f * session.Layout.RouteDirection,
                () => session.AtExit && session.Motor.Grounded, 180);

            run.TrySpendEnergy(47f);
            run.TakeDamage(1);
            run.Inventory.AddNektar(5);
            int carriedHealth = run.Health;
            int carriedNektar = run.Inventory.Nektar;
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(session.Run == run && run.LevelNumber == 2 && session.Layout.LevelNumber == 2 &&
                run.Energy == character.MaxEnergy && run.Health == carriedHealth &&
                run.Inventory.Nektar == carriedNektar && run.Inventory.Active == active && run.Inventory.Count(chest.Item) == 1,
                "The next Forest level failed to retain the run or refill only ability energy.");

            for (int level = 2; level <= 3; level++)
            {
                run.TrySpendEnergy(70f);
                LevelFeature heart = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.HeartSap);
                LevelFeature sap = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.EnergySap);
                BiomeVerification.Walk(fixture, () => session.AtExit && session.Motor.Grounded, 650);
                if (level == 2)
                {
                    Require(!heart.Available && !sap.Available && run.Energy > 31f,
                        "The Forest route did not reach both restoration pickups.");
                    LevelFeature replacement = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.Chest);
                    Require(Items.IsActive(replacement.Item) && replacement.Item != active, "The second chest did not offer an alternate active.");
                    ReachPocketChest(fixture);
                    fixture.Step(new PlayerCommand(interactPressed: true));
                    Require(!replacement.Available && run.Inventory.Active == replacement.Item, "A normal chest interaction did not replace the active item.");
                    MoveTo(fixture, session.Layout.Exit.x - 0.32f * session.Layout.RouteDirection,
                        () => session.AtExit && session.Motor.Grounded, 180);
                }
                fixture.Step(new PlayerCommand(interactPressed: true));
                Require(run.LevelNumber == level + 1 && run.Energy == character.MaxEnergy,
                    $"Forest level {level} did not advance through the normal exit.");
            }

            Require(session.Content.HasBoss && !session.Content.ExitUnlocked, "Level four did not start with a boss gate.");
            LevelEnemy boss = session.Content.Enemies.Single(enemy => enemy.Boss);
            BiomeVerification.Walk(fixture, () => session.AtExit, 350);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(session.Phase == RunPhase.Playing && run.LevelNumber == 4 && boss.Damage.Health.Alive,
                "The boss exit opened before victory.");

            FightBoss(fixture, boss);
            Require(!session.Content.ExitUnlocked, "The exit opened before claiming the reward.");
            LevelFeature reward = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.BossReward);
            MoveTo(fixture, reward.Position.x, () => Near(session, reward.Position, 1.20f), 200);
            int before = run.Inventory.Count(reward.Item);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(!reward.Available && session.Content.BossRewardClaimed && session.Content.ExitUnlocked
                && run.Inventory.Count(reward.Item) == before + 1, "The boss did not award its item and unlock the exit.");
            MoveTo(fixture, session.Layout.Exit.x, () => session.AtExit, 150);
            int carriedHearts = run.Health;
            int carriedCurrency = run.Inventory.Nektar;
            ItemId carriedActive = run.Inventory.Active;
            run.TrySpendEnergy(19f);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(session.Phase == RunPhase.Playing && session.Run == run && run.LevelNumber == 5
                && run.World == WorldId.Swamp && run.WorldLevel == 1 && session.Layout.World == WorldId.Swamp
                && run.Health == carriedHearts && run.Inventory.Nektar == carriedCurrency
                && run.Inventory.Active == carriedActive && run.Inventory.Count(reward.Item) == before + 1
                && run.Energy == character.MaxEnergy,
                "The Forest exit did not carry the existing run into Swamp with refreshed energy.");
            Debug.Log($"Insectum Forest playthrough: seed {seed}, {character.Name}, all four levels and Barkback cleared, Swamp entered.");
        }

        private static void ReachPocketChest(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            LevelLayout layout = session.Layout;
            Vector2 pocket = layout.Markers.Single(marker => marker.Kind == LevelMarkerKind.Branch).Position;
            fixture.Advance(8);
            for (int rise = 2; rise >= 0; rise--)
            {
                Vector2 landing = pocket - new Vector2(rise * HexGrid.HalfWidth * layout.RouteDirection, rise * HexGrid.RowSpacing);
                bool reached = false;
                for (int tick = 0; tick < 80; tick++)
                {
                    float dx = landing.x - session.Motor.Body.position.x;
                    int move = Mathf.Abs(dx) > 0.18f ? Math.Sign(dx) : 0;
                    fixture.Step(new PlayerCommand(move: move, jumpPressed: tick == 0, jumpHeld: true));
                    Vector2 feet = session.Motor.Body.position - Vector2.up * session.Run.Character.Radius;
                    if (session.Motor.Grounded && Vector2.Distance(feet, landing) < 0.58f)
                    {
                        reached = true;
                        break;
                    }
                }
                Require(reached, $"Could not reach seed {layout.RunSeed} pocket landing {landing}. Body {session.Motor.Body.position}.");
            }
        }

        private static void FightBoss(PhysicsFixture fixture, LevelEnemy boss)
        {
            GameSession session = fixture.Session;
            bool warned = boss.AttackPhase == LevelEnemy.Phase.Warning;
            int jump = 0;
            for (int tick = 0; tick < 850 && boss.Damage.Health.Alive && session.Phase == RunPhase.Playing; tick++)
            {
                float dx = boss.Position.x - session.Motor.Body.position.x;
                warned |= boss.AttackPhase == LevelEnemy.Phase.Warning;
                bool dodge = session.Motor.Grounded && jump == 0 && boss.AttackPhase == LevelEnemy.Phase.Attacking
                    && Mathf.Abs(dx) < 3.4f;
                if (dodge)
                    jump = 22;
                float reach = 1.3f + boss.Damage.Radius;
                bool hit = Mathf.Abs(dx) < reach && jump == 0
                    && Mathf.Abs(session.Motor.Body.position.y - boss.Position.y) < 0.38f + boss.Damage.Radius;
                int move = Mathf.Abs(dx) > reach - 0.12f || (hit && session.Motor.Facing * dx < 0f) ? Math.Sign(dx) : 0;
                if (jump > 0)
                    move = 0;
                bool heavy = hit && session.Run.Character.Insect == Insect.FireAnt;
                fixture.Step(new PlayerCommand(move, jumpPressed: dodge, jumpHeld: jump > 0, attackPressed: hit, abilityPressed: heavy));
                jump = Mathf.Max(0, jump - 1);
            }
            Require(warned && !boss.Damage.Health.Alive && session.Run.Alive,
                $"Barkback combat for {session.Run.Character.Name} did not resolve through telegraphed attacks and ordinary commands. Boss health {boss.Damage.Health.Current}, player health {session.Run.Health}, position {session.Motor.Body.position}.");
        }

        private static void VerifyOtherCharacters(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            const int seed = 7126;
            foreach (Character character in Character.Roster)
            {
                if (character.Insect == Insect.FireAnt)
                    continue;

                string replayPath = character.Insect == Insect.Beetle
                    ? ForestReplayFile.Argument("-insectum-record-forest") : null;
                if (replayPath != null)
                    fixture.Replay = new List<PlayerCommand>();
                session.StartForest(character, seed);
                for (int level = 1; level <= 3; level++)
                {
                    BiomeVerification.Walk(fixture, () => session.AtExit && session.Motor.Grounded, 650);
                    fixture.Step(new PlayerCommand(interactPressed: true));
                    Require(session.Run.LevelNumber == level + 1, $"{character.Name} did not advance to Forest {level + 1}.");
                }

                LevelEnemy boss = session.Content.Enemies.Single(enemy => enemy.Boss);
                FightBoss(fixture, boss);
                LevelFeature reward = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.BossReward);
                MoveTo(fixture, reward.Position.x, () => Near(session, reward.Position, 1.20f), 250);
                fixture.Step(new PlayerCommand(interactPressed: true));
                MoveTo(fixture, session.Layout.Exit.x, () => session.AtExit, 250);
                fixture.Step(new PlayerCommand(interactPressed: true));
                Require(session.Phase == RunPhase.Cleared, $"{character.Name} did not finish Forest after defeating Barkback.");
                if (replayPath != null)
                {
                    ForestReplayFile.Write(replayPath, character.Insect, seed, ReplayKind.Forest, fixture.Replay);
                    fixture.Replay = null;
                }
                Debug.Log($"Insectum Forest playthrough: seed {seed}, {character.Name}, all four levels and Barkback cleared.");
            }
        }

        private static void VerifyDefeatAndRevive(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            session.StartGenerated(Character.Roster[2], 7127);
            RunState run = session.Run;
            run.Inventory.AddNektar(7);
            run.Inventory.Acquire(ItemId.EmergencyReboot);
            run.Inventory.Acquire(ItemId.EmergencyReboot);
            run.TrySpendEnergy(32f);
            Vector2 spawn = session.Layout.Spawn;
            session.Motor.Warp(new Vector2(spawn.x, session.Layout.KillHeight - 0.5f));
            fixture.Step();
            Require(session.Phase == RunPhase.Playing && run.Alive && run.Inventory.ReviveUsed
                && run.Inventory.Nektar == 7 && run.Energy == 68f && Near(session, spawn, 0.6f),
                "Emergency Reboot did not revive once at the checkpoint without resetting run resources.");
            fixture.Advance(12);
            Require(session.Phase == RunPhase.Playing && run.Alive && session.Motor.Grounded,
                "Emergency Reboot placed the player on an unsafe checkpoint.");
            session.Motor.Warp(new Vector2(spawn.x, session.Layout.KillHeight - 0.5f));
            fixture.Step();
            Require(session.Phase == RunPhase.Defeated && !run.Alive, "A stacked Emergency Reboot revived twice in the same run.");
            session.StartGenerated(Character.Roster[2], 7127);
            Require(session.Phase == RunPhase.Playing && session.Run != run && session.Run.LevelNumber == 1
                && session.Run.Inventory.Nektar == 0 && session.Run.Inventory.Passives.Count == 0 && !session.Run.Inventory.ReviveUsed,
                "Restart retained the old run's inventory or revive flag.");
        }

        private static void MoveTo(PhysicsFixture fixture, float x, Func<bool> reached, int limit)
        {
            GameSession session = fixture.Session;
            for (int tick = 0; tick < limit && session.Phase == RunPhase.Playing && !reached(); tick++)
            {
                float dx = x - session.Motor.Body.position.x;
                fixture.Step(new PlayerCommand(move: Mathf.Abs(dx) > 0.16f ? Math.Sign(dx) : 0));
            }
            Require(reached(), $"Forest seed {session.Run.Seed}/{session.Run.LevelNumber} could not reach x={x:F2} from {session.Motor.Body.position}.");
        }

        private static bool Near(GameSession session, Vector2 point, float radius) =>
            (session.Motor.Body.position - point).sqrMagnitude < radius * radius;
    }
}
