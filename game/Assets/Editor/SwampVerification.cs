using System;
using System.Linq;
using UnityEngine;
using static Insectum.Game.Editor.ProductionVerification;
using static Insectum.Game.Editor.BiomeVerification;

namespace Insectum.Game.Editor
{
    internal static class SwampVerification
    {
        private const int FirstLevel = 5;

        internal static void VerifyGeneration() =>
            BiomeVerification.VerifyGeneration(WorldId.Swamp, TerrainMaterial.Mud, TerrainMaterial.Clay);

        internal static void VerifyPlaythrough()
        {
            using var fixture = new PhysicsFixture("Swamp playthrough");
            VerifyPocket(fixture, WorldId.Swamp, ItemId.ReedFlask, 1, excavate: true);
            VerifyPocket(fixture, WorldId.Swamp, ItemId.ReedFlask, -1, excavate: true);
            VerifyPocket(fixture, WorldId.Swamp, ItemId.ReedFlask, 1, excavate: false);
            VerifyPocket(fixture, WorldId.Swamp, ItemId.ReedFlask, -1, excavate: false);
            Require(fixture.Session.Content.Enemies.All(enemy => enemy.Style is EnemyStyle.ReedRunner or EnemyStyle.Skimmer)
                    && fixture.Session.Content.Traps.All(trap => trap.Style == TrapStyle.Gas),
                "Swamp generated a foreign encounter or hazard.");
            VerifyGas();
            VerifyProgression(fixture);
            Debug.Log("Insectum Swamp playthrough verified: Worm excavation and ordinary jumps in both pockets, readable gas, three routes, Mire Bell volley, Reed Flask, and gated exit.");
        }

        private static void VerifyGas()
        {
            var trap = new LevelTrap(null, null, Vector2.zero, TrapStyle.Gas);
            var target = new DamageReceiver(new HealthPool(3), 0.4f, () => trap.Position, 0);
            trap.Step(78, target);
            Require(trap.Warning && !trap.Extended && target.Health.Current == 3, "Swamp gas hurt during its warning.");
            trap.Step(102, target);
            trap.Step(103, target);
            Require(!trap.Warning && trap.Extended && target.Health.Current == 2, "Swamp gas did not strike exactly once.");
        }

        private static void VerifyProgression(PhysicsFixture fixture)
        {
            const int seed = 7126;
            GameSession session = fixture.Session;
            var ant = Character.Roster[(int)Insect.FireAnt];
            session.StartGenerated(ant, seed, FirstLevel);
            RunState run = session.Run;
            LevelFeature sap = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.EnergySap);
            run.Inventory.Acquire(ItemId.ReedFlask);
            run.TrySpendEnergy(70f);
            Walk(fixture, () => !sap.Available, 350);
            Require(run.Energy == 30f + sap.Amount + 10f, "Reed Flask did not augment a real energy pickup.");
            for (int level = FirstLevel; level < FirstLevel + 3; level++)
            {
                Walk(fixture, () => session.AtExit && session.Motor.Grounded, 770);
                fixture.Step(new PlayerCommand(interactPressed: true));
                Require(session.Run == run && run.World == WorldId.Swamp && run.LevelNumber == level + 1
                        && run.WorldLevel == level + 2 - FirstLevel && run.Energy == ant.MaxEnergy,
                    $"Swamp seed {seed}, level {level}: real exit interaction did not advance the run.");
            }

            LevelEnemy boss = session.Content.Enemies.Single(enemy => enemy.Boss);
            Require(boss.Style == EnemyStyle.VolleyBoss && !session.Content.ExitUnlocked && boss.Name == "Mire Bell",
                "Swamp did not enter its own boss arena.");
            Walk(fixture, () => session.AtExit, 360);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(session.Phase == RunPhase.Playing && run.LevelNumber == FirstLevel + 3,
                "Swamp's exit opened before defeating the boss.");

            bool sawVolley = false;
            bool sawWarning = false;
            for (int tick = 0; tick < 800 && boss.Damage.Health.Alive && run.Alive; tick++)
            {
                float dx = boss.Position.x - session.Motor.Body.position.x;
                sawVolley |= boss.AttackPhase == LevelEnemy.Phase.Attacking;
                sawWarning |= boss.AttackPhase == LevelEnemy.Phase.Warning;
                bool hit = Mathf.Abs(dx) < 1.95f && sawVolley;
                int move = Mathf.Abs(dx) > 1.25f || (hit && session.Motor.Facing * dx < 0f) ? Math.Sign(dx) : 0;
                fixture.Step(new PlayerCommand(move, attackPressed: hit, abilityPressed: hit));
            }
            Require(sawVolley && sawWarning && !boss.Damage.Health.Alive && run.Alive,
                $"Swamp seed {seed}: Mire Bell did not warn, volley and lose to normal attacks. Boss health {boss.Damage.Health.Current}, player health {run.Health}.");
            Require(!session.Content.ExitUnlocked, "Mire Bell's exit opened before collecting the boss prize.");
            LevelFeature prize = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.BossReward);
            MoveTo(fixture, prize.Position.x, () => Vector2.Distance(prize.Position, session.Motor.Body.position) < 1.2f, 220);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(prize.Item == ItemId.ReedFlask && !prize.Available && run.Inventory.Count(ItemId.ReedFlask) == 2
                    && session.Content.ExitUnlocked, "Swamp boss reward did not stack or unlock the exit.");
            MoveTo(fixture, session.Layout.Exit.x, () => session.AtExit, 180);
            run.Inventory.Acquire(ItemId.SeedBomb);
            int carriedHearts = run.Health;
            int carriedNektar = run.Inventory.Nektar;
            run.TrySpendEnergy(18f);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(session.Phase == RunPhase.Playing && session.Run == run && run.LevelNumber == 9
                    && run.World == WorldId.Caves && run.WorldLevel == 1 && session.Layout.World == WorldId.Caves
                    && run.Health == carriedHearts && run.Inventory.Nektar == carriedNektar
                    && run.Inventory.Active == ItemId.SeedBomb && run.Inventory.Count(ItemId.ReedFlask) == 2
                    && run.Energy == ant.MaxEnergy,
                "Swamp's unlocked exit did not carry the existing run into Caves.");
            Debug.Log($"Insectum Swamp playthrough: seed {seed}, Mire Bell cleared, Caves entered with run state intact.");
        }

    }
}
