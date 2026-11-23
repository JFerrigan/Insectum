using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Insectum.Game.Editor.BiomeVerification;
using static Insectum.Game.Editor.ProductionVerification;

namespace Insectum.Game.Editor
{
    internal static class FullRunVerification
    {
        private const int FullRunSeed = 1051;

        internal static void Verify()
        {
            VerifyVariantIsolation();
            using var fixture = new PhysicsFixture("Full production run", enableVariants: true);
            Play(fixture, FullRunSeed);
            VerifyOtherDoubleBosses(fixture);
        }

        private static void VerifyVariantIsolation()
        {
            using var baseline = new PhysicsFixture("Base encounter and loot");
            using var variant = new PhysicsFixture("Variant walk", enableAbilities: false, enableVariants: true);
            Character beetle = Character.Roster[(int)Insect.Beetle];

            baseline.Session.StartGenerated(beetle, FullRunSeed, 1);
            variant.Session.StartGenerated(beetle, FullRunSeed, 1);
            GameSession night = variant.Session;
            Require(night.Variant == LevelVariant.Night && night.NightVisionRadius == 2.7f,
                "The seeded night level did not set its visible light radius.");
            night.Run.Inventory.Acquire(ItemId.MothEyes);
            night.Run.Inventory.Acquire(ItemId.MothEyes);
            Require(Mathf.Abs(night.NightVisionRadius - 5.7f) < 0.001f,
                "Moth Eyes did not extend the same Night radius used by the renderer.");
            Require(EquivalentBase(baseline.Session, night, requireExtraEnemies: false), "Night changed the base layout, encounters, or loot.");

            const int heavyLevel = 11;
            baseline.Session.StartGenerated(beetle, FullRunSeed, heavyLevel);
            variant.Session.StartGenerated(beetle, FullRunSeed, heavyLevel);
            GameSession heavy = variant.Session;
            Require(heavy.Variant == LevelVariant.HeavySpawn && EquivalentBase(baseline.Session, heavy, requireExtraEnemies: true),
                "Heavy spawn changed the base encounters, loot or geometry, or added no reinforcements.");
            LevelEnemy[] reinforcements = heavy.Content.Enemies.Skip(baseline.Session.Content.Enemies.Count).ToArray();
            var checkpoints = heavy.Layout.Checkpoints.Select(checkpoint => checkpoint.Landing).ToArray();
            Require(reinforcements.All(enemy => checkpoints.All(landing => Mathf.Abs(landing.x - enemy.Position.x) > 0.20f
                    || Mathf.Abs(landing.y - (enemy.Position.y - enemy.Spec.Radius)) > 0.2f)),
                "A reinforcement occupied a safe landing's checkpoint center.");
            var damage = new List<DamageSource>();
            heavy.PlayerDamage.Resolved += (hit, outcome) => { if (outcome == DamageOutcome.Applied) damage.Add(hit.Source); };
            Walk(variant, () => heavy.AtExit && heavy.Motor.Grounded, 800);
            Require(damage.Count == 0 && reinforcements.All(enemy => enemy.Damage.Health.Alive),
                "An ordinary Heavy spawn traversal needed combat, abilities, items, or damage.");
            Debug.Log("Insectum variants verified: Night and stacked Moth Eyes radius, Heavy spawn with unchanged geometry and damage-free ordinary bypass.");
        }

        private static bool EquivalentBase(GameSession baseline, GameSession variant, bool requireExtraEnemies)
        {
            int count = baseline.Content.Enemies.Count;
            return ForestGenerator.Signature(baseline.Layout) == ForestGenerator.Signature(variant.Layout)
                && (!requireExtraEnemies || variant.Content.Enemies.Count > count)
                && baseline.Content.Enemies.Select(enemy => (enemy.Style, enemy.Position))
                    .SequenceEqual(variant.Content.Enemies.Take(count).Select(enemy => (enemy.Style, enemy.Position)))
                && baseline.Content.Features.Select(feature => (feature.Kind, feature.Item, feature.Amount, feature.Position))
                    .SequenceEqual(variant.Content.Features.Select(feature => (feature.Kind, feature.Item, feature.Amount, feature.Position)));
        }

        private static void Play(PhysicsFixture fixture, int seed)
        {
            GameSession session = fixture.Session;
            Character ant = Character.Roster[(int)Insect.FireAnt];
            fixture.Replay = new List<PlayerCommand>();
            session.StartGenerated(ant, seed);
            RunState run = session.Run;
            var variants = new HashSet<LevelVariant>();
            for (int level = 1; level <= RunWorlds.LevelCount; level++)
            {
                Require(session.Run == run && run.LevelNumber == level && run.Alive && run.Energy == ant.MaxEnergy,
                    $"Seed {seed}: run state did not reach level {level} with replenished ability energy.");
                variants.Add(session.Variant);
                if (level == 3)
                {
                    LevelFeature shop = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.Shop);
                    Require(shop.Item == ItemId.MothEyes, $"Seed {seed} no longer sells Moth Eyes in Forest 3.");
                    Walk(fixture, () => Vector2.Distance(shop.Position, session.Motor.Body.position) < 1.2f, 360);
                    fixture.Step(new PlayerCommand(interactPressed: true));
                    Require(run.Inventory.Count(ItemId.MothEyes) == 1 && !shop.Available,
                        "Normal shop interaction did not acquire Moth Eyes.");
                }
                if (session.Variant == LevelVariant.Night)
                    Require(Mathf.Abs(session.NightVisionRadius - (level < 3 ? 2.7f : 4.2f)) < 0.001f,
                        "The actual light radius did not retain the Moth Eyes purchase.");
                if (session.Content.HasBoss)
                    ClearArena(fixture);
                else
                    Walk(fixture, () => session.AtExit && session.Motor.Grounded, 850);

                int health = run.Health;
                int nektar = run.Inventory.Nektar;
                WorldId world = run.World;
                LevelVariant variant = session.Variant;
                fixture.Step(new PlayerCommand(interactPressed: true));
                Require(run.Health == health && run.Inventory.Nektar == nektar,
                    $"Seed {seed}: portal {world} {level} changed carried health or Nektar.");
                Debug.Log($"Insectum full run seed {seed}: cleared {world} {RunWorlds.LocalLevel(level)} ({variant}), health {health}.");
            }

            Require(session.Phase == RunPhase.Cleared && run.LevelNumber == RunWorlds.LevelCount && run.World == WorldId.Alien
                    && variants.Contains(LevelVariant.Night) && variants.Contains(LevelVariant.HeavySpawn)
                    && variants.Contains(LevelVariant.DoubleBoss),
                $"Seed {seed}: the full run did not complete all 20 levels and all variants.");
            string path = ForestReplayFile.Argument("-insectum-record-run");
            if (path != null)
                ForestReplayFile.Write(path, ant.Insect, seed, ReplayKind.Run, fixture.Replay);
            Debug.Log($"Insectum full run verified: seed {seed}, all 20 levels, all variants, {fixture.Replay.Count} normal commands.");
        }

        private static void VerifyOtherDoubleBosses(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            Character ant = Character.Roster[(int)Insect.FireAnt];
            fixture.Replay = null;
            foreach (WorldId world in new[] { WorldId.Swamp, WorldId.Caves, WorldId.Desert, WorldId.Alien })
            {
                int level = RunWorlds.FirstLevel(world) + RunWorlds.LevelsPerWorld - 1;
                int seed = Enumerable.Range(1000, 128).First(candidate =>
                    RunWorlds.ChooseVariant(new LevelSeeds(candidate, level), RunWorlds.LevelsPerWorld) == LevelVariant.DoubleBoss);
                session.StartGenerated(ant, seed, level);
                ClearArena(fixture);
                Require(session.Content.ExitUnlocked && session.Run.Alive, $"{world}'s double boss arena did not unlock.");
                fixture.Step(new PlayerCommand(interactPressed: true));
                Require(world == WorldId.Alien ? session.Phase == RunPhase.Cleared : session.Run.LevelNumber == level + 1,
                    $"{world}'s double boss arena could not advance through its exit.");
                Debug.Log($"Insectum double boss verified: {world}, seed {seed}, both telegraphs and one collected reward.");
            }
        }

        private static void ClearArena(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            LevelEnemy[] bosses = session.Content.Enemies.Where(enemy => enemy.Boss)
                .OrderBy(enemy => Vector2.Distance(enemy.Position, session.Motor.Body.position)).ToArray();
            Require(bosses.Length == (session.Variant == LevelVariant.DoubleBoss ? 2 : 1)
                && session.Content.BossesRemaining == bosses.Length && !session.Content.ExitUnlocked,
                "Arena started with the wrong boss count or an unlocked exit.");

            // Reserve heavy strikes before the second fight. Energy otherwise carries across both.
            for (int index = 0; index < bosses.Length; index++)
            {
                Fight(fixture, bosses[index], index == bosses.Length - 1);
                int remaining = bosses.Length - index - 1;
                int prizes = session.Content.Features.Count(feature => feature.Kind == LevelFeatureKind.BossReward);
                Require(!session.Content.ExitUnlocked && session.Content.BossesRemaining == remaining
                        && prizes == (remaining == 0 ? 1 : 0),
                    "A boss defeat generated an early or duplicate arena reward.");
            }

            LevelFeature reward = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.BossReward);
            MoveTo(fixture, reward.Position.x, () => Vector2.Distance(reward.Position, session.Motor.Body.position) < 1.2f, 290);
            int previous = session.Run.Inventory.Count(reward.Item);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(!reward.Available && session.Content.ExitUnlocked && session.Run.Inventory.Count(reward.Item) == previous + 1,
                "Normal interaction did not collect exactly one boss prize or unlock the exit.");
            MoveTo(fixture, session.Layout.Exit.x, () => session.AtExit, 290);
        }

        private static void Fight(PhysicsFixture fixture, LevelEnemy boss, bool useHeavy)
        {
            GameSession session = fixture.Session;
            bool warned = false;
            bool attacked = false;
            int jump = 0;
            int retreat = 0;
            int beamSide = 0;
            bool doubleBeam = boss.Style == EnemyStyle.BeamBoss && session.Variant == LevelVariant.DoubleBoss;
            float burrowMark = 0f;
            LevelEnemy.Phase last = LevelEnemy.Phase.Waiting;
            for (int tick = 0; tick < 1450 && boss.Damage.Health.Alive && session.Run.Alive; tick++)
            {
                Vector2 body = session.Motor.Body.position;
                float dx = boss.Position.x - body.x;
                float distance = Mathf.Abs(dx);
                LevelEnemy.Phase phase = boss.AttackPhase;
                warned |= phase == LevelEnemy.Phase.Warning;
                attacked |= phase == LevelEnemy.Phase.Attacking;
                bool dodgable = boss.Style is EnemyStyle.ChargeBoss or EnemyStyle.QuakeBoss or EnemyStyle.VolleyBoss;
                bool dodge = dodgable && phase == LevelEnemy.Phase.Attacking && session.Motor.Grounded && jump == 0 && distance < 3f;
                if (dodge)
                    jump = 22;

                if (boss.Style == EnemyStyle.BurrowBoss && phase == LevelEnemy.Phase.Warning && last != phase)
                {
                    burrowMark = body.x;
                    retreat = body.x < 0f ? 1 : -1;
                }
                if (doubleBeam && phase == LevelEnemy.Phase.Warning && last != phase)
                    beamSide = body.x < boss.Position.x ? -1 : 1;
                last = phase;
                bool underground = boss.Style == EnemyStyle.BurrowBoss && boss.Position.y < 0f;
                bool crossBeam = boss.Style == EnemyStyle.BeamBoss && phase == LevelEnemy.Phase.Attacking && distance < 2.8f;
                bool open = boss.Style != EnemyStyle.BeamBoss || phase == LevelEnemy.Phase.Recovery;
                float normalReach = 1.66f + session.Run.Inventory.MeleeReachBonus;
                float heavyReach = 2.02f + session.Run.Inventory.MeleeReachBonus;
                bool heavy = useHeavy && session.Run.Energy >= CharacterTuning.Current.fireAntAttackEnergy;
                bool hit = warned && open && !underground && jump == 0 && distance < (heavy ? heavyReach : normalReach);
                int move = distance > 1.17f || hit && session.Motor.Facing * dx < 0f ? Math.Sign(dx) : 0;
                if (doubleBeam && phase is LevelEnemy.Phase.Warning or LevelEnemy.Phase.Attacking)
                {
                    float behindBeam = boss.Position.x - beamSide * 1.30f - body.x;
                    move = Mathf.Abs(behindBeam) > 0.12f ? Math.Sign(behindBeam) : 0;
                }
                else if (crossBeam)
                    move = distance < 0.23f ? 0 : Math.Sign(dx);
                if (underground)
                    move = Mathf.Abs(body.x - burrowMark) < 2f ? retreat : 0;
                if (jump > 0)
                    move = 0;

                fixture.Step(new PlayerCommand(move, jumpPressed: dodge, jumpHeld: jump > 0, attackPressed: hit,
                    abilityPressed: hit && heavy));
                jump = Mathf.Max(0, jump - 1);
            }
            Require(warned && attacked && !boss.Damage.Health.Alive && session.Run.Alive,
                $"Level {session.Run.LevelNumber} {boss.Name} fight did not finish: boss {boss.Damage.Health.Current}, player {session.Run.Health}, warned {warned}, attacked {attacked}, position {session.Motor.Body.position}.");
        }
    }
}
