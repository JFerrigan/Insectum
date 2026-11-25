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
            session.Motor.Warp(OverPlug(session));
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
            session.Motor.Warp(OverPlug(session));
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
            Require(session.Content.NearbyShop() == null, "The shop offer appeared from too far away.");
            TunnelWalker.WalkTo(fixture, shop.Position, 2.5f);
            Require(session.Content.NearbyShop() == shop && session.Content.ClosestInteractable() != shop,
                "Approaching the shop did not reveal its offer before entering purchase range.");
            TunnelWalker.WalkTo(fixture, shop.Position, 1.2f);
            Require(run.Inventory.Nektar >= shop.Amount, "The route pickups before the shop did not cover its price.");
            Require(session.Content.ClosestInteractable() == shop &&
                session.InteractionPrompt == $"E  Buy {Items.Name(shop.Item)}  ({shop.Amount} Nektar)",
                "The shop prompt did not identify the item, price, and purchase key in range.");
            int wallet = run.Inventory.Nektar;
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(!shop.Available && run.Inventory.Active == shop.Item && run.Inventory.Nektar == wallet - shop.Amount,
                "The route pickups did not fund the first shop purchase through E.");
            Require(session.Content.NearbyShop() == shop && session.Content.ClosestInteractable() != shop,
                "The sold-out shop did not remain visible or still offered a purchase.");
            ItemId active = run.Inventory.Active;

            LevelFeature chest = NearestChest(session);
            TunnelWalker.WalkTo(fixture, chest.Position, 1.25f);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(!chest.Available && run.Inventory.Count(chest.Item) == 1, "E did not award the chest's passive.");
            BiomeVerification.Walk(fixture, () => session.AtExit && session.Motor.Grounded, 500);

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
                LevelFeature[] hearts = session.Content.Features.Where(feature => feature.Kind == LevelFeatureKind.HeartSap).ToArray();
                LevelFeature sap = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.EnergySap);
                BiomeVerification.Walk(fixture, () => session.AtExit && session.Motor.Grounded, 650);
                if (level == 2)
                {
                    Require(hearts.Any(heart => !heart.Available) && !sap.Available && run.Energy > 31f,
                        "The Forest route did not reach both restoration pickups.");
                    LevelFeature replacement = NearestChest(session);
                    Require(Items.IsActive(replacement.Item) && replacement.Item != active, "The second chest did not offer an alternate active.");
                    TunnelWalker.WalkTo(fixture, replacement.Position, 1.25f);
                    fixture.Step(new PlayerCommand(interactPressed: true));
                    Require(!replacement.Available && run.Inventory.Active == replacement.Item, "A normal chest interaction did not replace the active item.");
                    BiomeVerification.Walk(fixture, () => session.AtExit && session.Motor.Grounded, 500);
                }
                fixture.Step(new PlayerCommand(interactPressed: true));
                Require(run.LevelNumber == level + 1 && run.Energy == character.MaxEnergy,
                    $"Forest level {level} did not advance through the normal exit.");
            }

            Require(session.Content.HasBoss && !session.Content.ExitUnlocked, "Level four did not start with a boss gate.");
            LevelEnemy boss = session.Content.Enemies.Single(enemy => enemy.Boss);
            // Stand at the sealed hall's exit during the boss's entrance and try it, then come back to fight.
            Vector2 start = session.Motor.Body.position;
            session.Motor.Warp(session.Layout.Exit + Vector2.up * (run.Character.Radius + 0.06f));
            fixture.Step();
            Require(session.AtExit, "Could not stand at the boss arena's exit.");
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(session.Phase == RunPhase.Playing && run.LevelNumber == 4 && boss.Damage.Health.Alive,
                "The boss exit opened before victory.");
            session.Motor.Warp(start);

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
            Debug.Log($"Insectum Forest playthrough: seed {seed}, {character.Name}, all four levels and Old Oakhorn cleared, Swamp entered.");
        }

        // The chest vault that stands on the level's buried plug, or the nearest chest to the player.
        private static LevelFeature NearestChest(GameSession session) =>
            session.Content.Features.Where(feature => feature.Kind == LevelFeatureKind.Chest && feature.Available)
                .OrderBy(feature => (feature.Position - session.Motor.Body.position).sqrMagnitude).First();

        // Stands the player on the vault floor directly above the buried plug's top edge.
        private static Vector2 OverPlug(GameSession session)
        {
            LevelMarker plug = session.Layout.Markers.Single(marker => marker.Kind == LevelMarkerKind.Shortcut);
            Vector2 landing = session.Layout.Markers.Single(marker => marker.Kind == LevelMarkerKind.Branch
                && marker.Chunk == plug.Chunk).Position;
            return landing + Vector2.up * (session.Run.Character.Radius + 0.06f);
        }

        private static void FightBoss(PhysicsFixture fixture, LevelEnemy boss) =>
            BossFightBot.FightStagBeetle(fixture, boss, useHeavy: true);

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
                Require(session.Phase == RunPhase.Cleared, $"{character.Name} did not finish Forest after defeating Old Oakhorn.");
                if (replayPath != null)
                {
                    ForestReplayFile.Write(replayPath, character.Insect, seed, ReplayKind.Forest, fixture.Replay);
                    fixture.Replay = null;
                }
                Debug.Log($"Insectum Forest playthrough: seed {seed}, {character.Name}, all four levels and Old Oakhorn cleared.");
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
