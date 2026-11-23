using System;
using System.Linq;
using UnityEngine;
using static Insectum.Game.Editor.BiomeVerification;
using static Insectum.Game.Editor.ProductionVerification;

namespace Insectum.Game.Editor
{
    internal static class DesertVerification
    {
        private const int FirstLevel = 13;

        internal static void VerifyGeneration() =>
            BiomeVerification.VerifyGeneration(WorldId.Desert, TerrainMaterial.Sand);

        internal static void VerifyPlaythrough()
        {
            using var fixture = new PhysicsFixture("Desert playthrough");
            VerifyVault(fixture, WorldId.Desert, ItemId.DuneMantle, excavate: true);
            VerifyVault(fixture, WorldId.Desert, ItemId.DuneMantle, excavate: false);
            Require(fixture.Session.Content.Enemies.All(enemy => enemy.Style is EnemyStyle.DuneBeetle or EnemyStyle.SandWasp)
                    && fixture.Session.Content.Traps.All(trap => trap.Style == TrapStyle.Heat),
                "Desert generated a foreign encounter or hazard.");
            VerifyHeat();
            VerifyBurrow();
            VerifyMantle(fixture);
            VerifyProgression(fixture);
            Debug.Log("Insectum Desert playthrough verified: vault chest and a 3-energy Worm sand plug, cinder warning and knockback, Dune Mantle per-level guards, Dune Maw burrow, reward, and gated exit.");
        }

        private static void VerifyHeat()
        {
            var trap = new LevelTrap(null, null, Vector2.zero, TrapStyle.Heat);
            Vector2 knockback = Vector2.zero;
            var target = new DamageReceiver(new HealthPool(3), 0.4f,
                () => trap.Position + Vector2.right * 0.32f, 0, impulse => knockback = impulse);
            trap.Step(78, target);
            Require(trap.Warning && !trap.Extended && target.Health.Current == 3, "Desert cinder jet hurt during its warning.");
            trap.Step(96, target);
            trap.Step(97, target);
            Require(!trap.Warning && trap.Extended && target.Health.Current == 2 && knockback.x > 5f,
                "Desert cinder jet did not push away and strike exactly once.");
        }

        private static void VerifyBurrow()
        {
            foreach (bool sidestep in new[] { true, false })
            {
                var spec = new EnemySpec("Dune Maw", EnemyStyle.BurrowBoss, 9, 0, 0.84f, Color.white);
                var boss = new LevelEnemy(null, null, spec, Vector2.zero, 0, null);
                Vector2 position = new(1.6f, 0.43f);
                var target = new DamageReceiver(new HealthPool(3), 0.43f, () => position,
                    CharacterTuning.Current.playerInvulnerabilitySteps);
                boss.Step(target);
                Require(boss.AttackPhase == LevelEnemy.Phase.Warning && target.Health.Current == 3,
                    "Dune Maw's target marker was already damaging.");
                if (sidestep)
                    position.x += 3f;
                for (int step = 0; step < 42; step++)
                    boss.Step(target);
                Require(boss.AttackPhase == LevelEnemy.Phase.Attacking && boss.Position.y < 0f,
                    "Dune Maw did not leave the combat plane while burrowing.");
                for (int step = 0; step < 30; step++)
                    boss.Step(target);
                Require(Mathf.Abs(boss.Position.x - 1.6f) < 0.1f
                        && target.Health.Current == (sidestep ? 3 : 2),
                    "Dune Maw did not erupt at its telegraphed location.");
            }
        }

        private static void VerifyMantle(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            session.StartGenerated(Character.Roster[(int)Insect.FireAnt], 7126, FirstLevel);
            RunState run = session.Run;
            run.Inventory.Acquire(ItemId.DuneMantle);
            run.Inventory.Acquire(ItemId.DuneMantle);
            var heat = new DamageHit(DamageSource.Trap, 1);
            int invulnerability = CharacterTuning.Current.playerInvulnerabilitySteps + 1;
            Require(session.PlayerDamage.Receive(heat) == DamageOutcome.Absorbed
                    && session.PlayerDamage.Receive(heat) == DamageOutcome.Ignored,
                "Dune Mantle didn't absorb the first trap or lost a charge to i-frames.");
            fixture.Advance(invulnerability);
            Require(session.PlayerDamage.Receive(heat) == DamageOutcome.Absorbed, "Dune Mantle's second stack did not absorb a trap.");
            fixture.Advance(invulnerability);
            Require(session.PlayerDamage.Receive(heat) == DamageOutcome.Applied, "Dune Mantle absorbed more traps than its stacks.");
            Walk(fixture, () => session.AtExit && session.Motor.Grounded, 760);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(session.Run == run && run.LevelNumber == FirstLevel + 1
                    && session.PlayerDamage.Receive(heat) == DamageOutcome.Absorbed,
                "Dune Mantle charges did not reset on actual level entry.");
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
                Require(session.Run == run && run.World == WorldId.Desert && run.LevelNumber == level + 1
                        && run.WorldLevel == level + 2 - FirstLevel && run.Energy == ant.MaxEnergy,
                    $"Desert seed {seed}, level {level}: real exit interaction did not advance the run.");
            }

            LevelEnemy boss = session.Content.Enemies.Single(enemy => enemy.Boss);
            Require(boss.Style == EnemyStyle.BurrowBoss && !session.Content.ExitUnlocked && boss.Name == "Dune Maw",
                "Desert did not enter the Dune Maw arena.");
            Walk(fixture, () => session.AtExit, 360);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(session.Phase == RunPhase.Playing && run.LevelNumber == FirstLevel + 3,
                "Desert exit opened before defeating the boss.");

            bool sawBurrow = false;
            bool sawWarning = false;
            int retreat = -1;
            float warningX = session.Motor.Body.position.x;
            LevelEnemy.Phase previousPhase = boss.AttackPhase;
            for (int tick = 0; tick < 1000 && boss.Damage.Health.Alive && run.Alive; tick++)
            {
                Vector2 body = session.Motor.Body.position;
                float dx = boss.Position.x - body.x;
                bool underground = boss.Position.y < 0f;
                bool warned = boss.AttackPhase == LevelEnemy.Phase.Warning;
                if (warned && previousPhase != LevelEnemy.Phase.Warning)
                {
                    warningX = body.x;
                    retreat = body.x < 0f ? 1 : -1;
                }
                previousPhase = boss.AttackPhase;
                sawBurrow |= underground;
                sawWarning |= warned;
                bool evade = underground && Mathf.Abs(body.x - warningX) < 2.0f;
                bool hit = !underground && sawBurrow && Mathf.Abs(dx) < 1.96f;
                int move = evade ? retreat : underground ? 0
                    : Mathf.Abs(dx) > 1.85f || (hit && session.Motor.Facing * dx < 0f) ? Math.Sign(dx) : 0;
                fixture.Step(new PlayerCommand(move, attackPressed: hit, abilityPressed: hit));
            }
            Require(sawBurrow && sawWarning && !boss.Damage.Health.Alive && run.Alive,
                $"Desert seed {seed}: Dune Maw did not warn, burrow and lose to ordinary commands. Boss health {boss.Damage.Health.Current}, player health {run.Health}.");
            Require(!session.Content.ExitUnlocked, "Dune Maw's exit opened before collecting the boss prize.");
            LevelFeature prize = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.BossReward);
            MoveTo(fixture, prize.Position.x, () => Vector2.Distance(prize.Position, session.Motor.Body.position) < 1.2f, 220);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(prize.Item == ItemId.DuneMantle && !prize.Available && run.Inventory.Count(ItemId.DuneMantle) == 1
                    && session.Content.ExitUnlocked, "Desert boss reward did not award the Mantle or unlock the exit.");
            MoveTo(fixture, session.Layout.Exit.x, () => session.AtExit, 180);
            int carriedHearts = run.Health;
            int carriedNektar = run.Inventory.Nektar;
            run.Inventory.Acquire(ItemId.SeedBomb);
            run.TrySpendEnergy(14f);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(session.Phase == RunPhase.Playing && session.Run == run && run.LevelNumber == 17
                    && run.World == WorldId.Alien && run.WorldLevel == 1 && session.Layout.World == WorldId.Alien
                    && run.Health == carriedHearts && run.Inventory.Nektar == carriedNektar
                    && run.Inventory.Active == ItemId.SeedBomb && run.Inventory.Count(ItemId.DuneMantle) == 1
                    && run.Energy == ant.MaxEnergy,
                "Desert's unlocked exit did not carry the existing run into Alien.");
            Debug.Log($"Insectum Desert playthrough: seed {seed}, Dune Maw cleared, Alien entered with run state intact.");
        }
    }
}
