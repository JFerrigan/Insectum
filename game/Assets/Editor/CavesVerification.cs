using System;
using System.Linq;
using UnityEngine;
using static Insectum.Game.Editor.BiomeVerification;
using static Insectum.Game.Editor.ProductionVerification;

namespace Insectum.Game.Editor
{
    internal static class CavesVerification
    {
        private const int FirstLevel = 9;

        internal static void VerifyGeneration() =>
            BiomeVerification.VerifyGeneration(WorldId.Caves, TerrainMaterial.SoftStone);

        internal static void VerifyPlaythrough()
        {
            using var fixture = new PhysicsFixture("Caves playthrough");
            VerifyVault(fixture, WorldId.Caves, ItemId.PrismFang, excavate: true);
            VerifyVault(fixture, WorldId.Caves, ItemId.PrismFang, excavate: false);
            Require(fixture.Session.Content.Enemies.All(enemy => enemy.Style is EnemyStyle.CaveHopper or EnemyStyle.GlowFly)
                    && fixture.Session.Content.Traps.All(trap => trap.Style == TrapStyle.Crystal),
                "Caves generated a foreign encounter or hazard.");
            VerifyCrystal();
            VerifyQuake();
            VerifyFang(fixture);
            VerifyProgression(fixture);
            Debug.Log("Insectum Caves playthrough verified: vault chest and a 12-energy Worm soft-stone plug, warning and crystal drop, Prism Fang reach, Faultback shockwave, reward, and gated exit.");
        }

        private static void VerifyCrystal()
        {
            var trap = new LevelTrap(null, null, Vector2.zero, TrapStyle.Crystal);
            var target = new DamageReceiver(new HealthPool(3), 0.4f, () => trap.Position, 0);
            trap.Step(73, target);
            Require(trap.Warning && !trap.Extended && target.Health.Current == 3, "Caves crystal hurt during its warning.");
            trap.Step(100, target);
            trap.Step(101, target);
            Require(!trap.Warning && trap.Extended && target.Health.Current == 2, "Caves crystal did not strike exactly once.");
        }

        private static void VerifyQuake()
        {
            foreach (float height in new[] { 0.43f, 2.0f })
            {
                var style = new EnemySpec("Faultback", EnemyStyle.QuakeBoss, 9, 0, 0.83f, Color.white);
                var boss = new LevelEnemy(null, null, style, Vector2.zero, 0, null);
                var target = new DamageReceiver(new HealthPool(3), 0.43f, () => new Vector2(1.6f, height), 0);
                for (int step = 0; step < 83; step++)
                    boss.Step(target);
                int expected = height < 1f ? 2 : 3;
                Require(target.Health.Current == expected,
                    $"Faultback's ground wave resolved incorrectly at height {height}.");
            }
        }

        private static void VerifyFang(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            session.StartGenerated(Character.Roster[(int)Insect.FireAnt], 7126, FirstLevel);
            fixture.Advance(8);
            Vector2 forward = Vector2.right * session.Motor.Facing;
            Vector2 targetPosition = session.Motor.Body.position + forward * 1.63f;
            var target = new DamageReceiver(new HealthPool(3), 0.10f, () => targetPosition, 0);
            session.Combat.AddTarget(target);
            fixture.Step(new PlayerCommand(attackPressed: true));
            Require(target.Health.Current == 3, "The target was already inside ordinary melee range.");
            fixture.Advance(CharacterTuning.Current.meleeRecoverySteps + 1);
            session.Run.Inventory.Acquire(ItemId.PrismFang);
            fixture.Step(new PlayerCommand(attackPressed: true));
            Require(target.Health.Current == 2, "Prism Fang did not extend a normal commanded melee strike.");
        }

        private static void VerifyProgression(PhysicsFixture fixture)
        {
            const int seed = 7126;
            GameSession session = fixture.Session;
            var ant = Character.Roster[(int)Insect.FireAnt];
            session.StartGenerated(ant, seed, FirstLevel);
            RunState run = session.Run;
            for (int level = FirstLevel; level < FirstLevel + 3; level++)
            {
                Walk(fixture, () => session.AtExit && session.Motor.Grounded, 770);
                fixture.Step(new PlayerCommand(interactPressed: true));
                Require(session.Run == run && run.World == WorldId.Caves && run.LevelNumber == level + 1
                        && run.WorldLevel == level + 2 - FirstLevel && run.Energy == ant.MaxEnergy,
                    $"Caves seed {seed}, level {level}: real exit interaction did not advance the run.");
            }

            LevelEnemy boss = session.Content.Enemies.Single(enemy => enemy.Boss);
            Require(boss.Style == EnemyStyle.QuakeBoss && !session.Content.ExitUnlocked && boss.Name == "Faultback",
                "Caves did not enter the Faultback arena.");
            Walk(fixture, () => session.AtExit, 360);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(session.Phase == RunPhase.Playing && run.LevelNumber == FirstLevel + 3,
                "Caves exit opened before defeating the boss.");

            bool sawQuake = false;
            bool sawWarning = false;
            int jump = 0;
            for (int tick = 0; tick < 800 && boss.Damage.Health.Alive && run.Alive; tick++)
            {
                float dx = boss.Position.x - session.Motor.Body.position.x;
                bool quake = boss.AttackPhase == LevelEnemy.Phase.Attacking;
                sawQuake |= quake;
                sawWarning |= boss.AttackPhase == LevelEnemy.Phase.Warning;
                bool dodge = quake && session.Motor.Grounded && jump == 0 && Mathf.Abs(dx) < 3f;
                if (dodge)
                    jump = 21;
                bool hit = Mathf.Abs(dx) < 1.96f && sawQuake && jump == 0;
                int move = jump == 0 && (Mathf.Abs(dx) > 1.85f || hit && session.Motor.Facing * dx < 0f)
                    ? Math.Sign(dx) : 0;
                fixture.Step(new PlayerCommand(move, jumpPressed: dodge, jumpHeld: jump > 0, attackPressed: hit, abilityPressed: hit));
                jump = Mathf.Max(0, jump - 1);
            }
            Require(sawQuake && sawWarning && !boss.Damage.Health.Alive && run.Alive,
                $"Caves seed {seed}: Faultback did not warn, quake and lose to ordinary commands. Boss health {boss.Damage.Health.Current}, player health {run.Health}.");
            Require(!session.Content.ExitUnlocked, "Faultback's exit opened before collecting the boss prize.");
            LevelFeature prize = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.BossReward);
            MoveTo(fixture, prize.Position.x, () => Vector2.Distance(prize.Position, session.Motor.Body.position) < 1.2f, 220);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(prize.Item == ItemId.PrismFang && !prize.Available && run.Inventory.Count(ItemId.PrismFang) == 1
                    && session.Content.ExitUnlocked, "Caves boss reward did not award the Fang or unlock the exit.");
            MoveTo(fixture, session.Layout.Exit.x, () => session.AtExit, 180);
            int carriedHearts = run.Health;
            int carriedNektar = run.Inventory.Nektar;
            run.Inventory.Acquire(ItemId.ExcavatorClaw);
            run.TrySpendEnergy(16f);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(session.Phase == RunPhase.Playing && session.Run == run && run.LevelNumber == 13
                    && run.World == WorldId.Desert && run.WorldLevel == 1 && session.Layout.World == WorldId.Desert
                    && run.Health == carriedHearts && run.Inventory.Nektar == carriedNektar
                    && run.Inventory.Active == ItemId.ExcavatorClaw && run.Inventory.Count(ItemId.PrismFang) == 1
                    && run.Energy == ant.MaxEnergy,
                "Caves' unlocked exit did not carry the existing run into Desert.");
            Debug.Log($"Insectum Caves playthrough: seed {seed}, Faultback cleared, Desert entered with run state intact.");
        }
    }
}
