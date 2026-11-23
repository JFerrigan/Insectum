using System;
using System.Linq;
using UnityEngine;
using static Insectum.Game.Editor.BiomeVerification;
using static Insectum.Game.Editor.ProductionVerification;

namespace Insectum.Game.Editor
{
    internal static class AlienVerification
    {
        private const int FirstLevel = 17;

        internal static void VerifyGeneration() =>
            BiomeVerification.VerifyGeneration(WorldId.Alien, TerrainMaterial.AlienOrganic);

        internal static void VerifyPlaythrough()
        {
            using var fixture = new PhysicsFixture("Alien playthrough");
            VerifyVault(fixture, WorldId.Alien, ItemId.FluxCore, excavate: true);
            VerifyVault(fixture, WorldId.Alien, ItemId.FluxCore, excavate: false);
            Require(fixture.Session.Content.Enemies.All(enemy => enemy.Style is EnemyStyle.LatticeTick or EnemyStyle.ArcMote)
                    && fixture.Session.Content.Traps.All(trap => trap.Style == TrapStyle.Arc),
                "Alien generated a foreign encounter or hazard.");
            VerifyArc();
            VerifyBeam();
            VerifyFluxCore(fixture);
            VerifyProgression(fixture);
            Debug.Log("Insectum Alien playthrough verified: vault chest and a 10-energy Worm organic plug, static warning and strike, Flux Core on kills, Crown Relay shield and sweep, reward, and final victory.");
        }

        private static void VerifyArc()
        {
            var trap = new LevelTrap(null, null, Vector2.zero, TrapStyle.Arc);
            var target = new DamageReceiver(new HealthPool(3), 0.4f,
                () => trap.Position + Vector2.up * 1.60f, 0);
            trap.Step(75, target);
            Require(trap.Warning && !trap.Extended && target.Health.Current == 3,
                "Alien static column hurt during its warning.");
            trap.Step(102, target);
            trap.Step(103, target);
            Require(!trap.Warning && trap.Extended && target.Health.Current == 2,
                "Alien static column did not strike exactly once high above the floor.");
        }

        private static void VerifyBeam()
        {
            var spec = new EnemySpec("Crown Relay", EnemyStyle.BeamBoss, 10, 0, 0.86f, Color.white);
            var boss = new LevelEnemy(null, null, spec, Vector2.zero, 0, null);
            var target = new DamageReceiver(new HealthPool(3), 0.43f, () => new Vector2(3.5f, 0.43f), 0);
            var hit = new DamageHit(DamageSource.Melee, 2);
            Require(boss.Damage.Receive(hit) == DamageOutcome.Absorbed && boss.Damage.Health.Current == 10,
                "Crown Relay did not block an early strike with its shield.");
            boss.Step(target);
            Require(boss.AttackPhase == LevelEnemy.Phase.Warning && target.Health.Current == 3,
                "Crown Relay did not telegraph its beam.");
            for (int step = 0; step < 95; step++)
            {
                boss.Damage.Step();
                boss.Step(target);
            }
            Require(boss.AttackPhase == LevelEnemy.Phase.Recovery && target.Health.Current == 2,
                "Crown Relay's sweep did not cross the target exactly once.");
            Require(boss.Damage.Receive(hit) == DamageOutcome.Applied && boss.Damage.Health.Current == 8,
                "Crown Relay's core did not become vulnerable during recovery.");
        }

        private static void VerifyFluxCore(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            session.StartGenerated(Character.Roster[(int)Insect.FireAnt], 7126, FirstLevel);
            fixture.Advance(8);
            RunState run = session.Run;
            run.Inventory.Acquire(ItemId.FluxCore);
            run.Inventory.Acquire(ItemId.FluxCore);
            run.TrySpendEnergy(30f);

            Vector2 floor = session.Motor.Body.position - Vector2.up * run.Character.Radius;
            var spec = new EnemySpec("Flux target", EnemyStyle.LatticeTick, 2, 0, 0.45f, Color.white);
            session.Content.AddEnemy(spec, floor + Vector2.right * session.Motor.Facing * 1.1f, 0);
            LevelEnemy target = session.Content.Enemies[^1];
            fixture.Step(new PlayerCommand(abilityPressed: true));
            float expected = run.Character.MaxEnergy - 30f - CharacterTuning.Current.fireAntAttackEnergy + 12f;
            Require(!target.Damage.Health.Alive && Mathf.Abs(run.Energy - expected) < 0.01f,
                "Two Flux Cores did not restore twelve energy on a real commanded enemy defeat.");
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
                Require(session.Run == run && run.World == WorldId.Alien && run.LevelNumber == level + 1
                        && run.WorldLevel == level + 2 - FirstLevel && run.Energy == ant.MaxEnergy,
                    $"Alien seed {seed}, level {level}: real exit interaction did not advance the run.");
            }

            LevelEnemy boss = session.Content.Enemies.Single(enemy => enemy.Boss);
            Require(boss.Style == EnemyStyle.BeamBoss && !session.Content.ExitUnlocked && boss.Name == "Crown Relay",
                "Alien did not enter the Crown Relay arena.");
            Walk(fixture, () => session.AtExit, 360);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(session.Phase == RunPhase.Playing && run.LevelNumber == FirstLevel + 3,
                "Alien final exit opened before defeating the boss.");

            bool sawWarning = false;
            bool sawSweep = false;
            int beamSide = 0;
            LevelEnemy.Phase last = LevelEnemy.Phase.Waiting;
            for (int tick = 0; tick < 950 && boss.Damage.Health.Alive && run.Alive; tick++)
            {
                Vector2 body = session.Motor.Body.position;
                float dx = boss.Position.x - body.x;
                LevelEnemy.Phase phase = boss.AttackPhase;
                bool sweep = phase == LevelEnemy.Phase.Attacking;
                sawWarning |= phase == LevelEnemy.Phase.Warning;
                sawSweep |= sweep;
                if (phase == LevelEnemy.Phase.Warning && last != phase)
                    beamSide = Math.Sign(dx);
                last = phase;
                bool hit = phase == LevelEnemy.Phase.Recovery && Mathf.Abs(dx) < 1.96f;
                int move = Mathf.Abs(dx) > 1.85f || hit && session.Motor.Facing * dx < 0f ? Math.Sign(dx) : 0;
                fixture.Step(beamSide != 0 && phase is LevelEnemy.Phase.Warning or LevelEnemy.Phase.Attacking
                    ? MoveBehindBeam(session, dx, beamSide)
                    : new PlayerCommand(move, attackPressed: hit, abilityPressed: hit));
            }
            Require(sawWarning && sawSweep && !boss.Damage.Health.Alive && run.Alive,
                $"Alien seed {seed}: Crown Relay did not warn, sweep and lose to ordinary commands. Boss health {boss.Damage.Health.Current}, player health {run.Health}.");
            Require(!session.Content.ExitUnlocked, "Crown Relay's exit opened before collecting the boss prize.");
            LevelFeature prize = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.BossReward);
            MoveTo(fixture, prize.Position.x, () => Vector2.Distance(prize.Position, session.Motor.Body.position) < 1.2f, 220);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(prize.Item == ItemId.FluxCore && !prize.Available && run.Inventory.Count(ItemId.FluxCore) == 1
                    && session.Content.ExitUnlocked, "Alien boss reward did not award the Core or unlock the exit.");
            MoveTo(fixture, session.Layout.Exit.x, () => session.AtExit, 180);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(session.Phase == RunPhase.Cleared && run.LevelNumber == FirstLevel + 3
                    && run.LevelNumber == RunWorlds.LevelCount && !session.Motor.Body.simulated,
                "Alien's unlocked exit did not complete the run through normal interaction.");
        }

        internal static PlayerCommand MoveBehindBeam(GameSession session, float dx, int beamSide)
        {
            float behind = dx + beamSide * 1.8f;
            int move = Mathf.Abs(behind) > 0.12f ? Math.Sign(behind) : 0;
            bool jump = dx * beamSide > 0f && Mathf.Abs(dx) < 2.35f && session.Motor.Grounded;
            return new PlayerCommand(move, jumpPressed: jump, jumpHeld: true);
        }
    }
}
