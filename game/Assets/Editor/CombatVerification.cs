using System;
using System.Collections.Generic;
using UnityEngine;
using static Insectum.Game.Editor.ProductionVerification;

namespace Insectum.Game.Editor
{
    internal static class CombatVerification
    {
        private static CharacterTuning Tuning => CharacterTuning.Current;

        internal static void Verify()
        {
            VerifyHurtboxes();
            using var fixture = new PhysicsFixture("Combat verification");
            VerifyCommonMelee(fixture);
            VerifySharedDamage(fixture);
            VerifyEnemyContact(fixture);
            VerifySpider(fixture);
            VerifyBeetleMomentum(fixture);
            VerifyBeetleDamage(fixture);
            VerifyBeetleCrush(fixture);
            VerifyDragonfly(fixture);
            VerifyFireAnt(fixture);
            VerifyDisabledAbilities();
            Debug.Log("Insectum combat verified: boss hurtboxes, melee, enemy contact and shared damage, Spider high jump and automatic wall climb, Beetle momentum/absorption/crush, Dragonfly flight, Fire Ant heavy strike, and ability-disabled simulation.");
        }

        // A boss made of parts takes one hit per swing, on its most damaging part in reach. Armor absorbs a
        // hit but still counts as one, so a downward strike bounces off it, and a weak point doubles damage.
        private static void VerifyHurtboxes()
        {
            var armor = new Hurtbox(new Vector2(1f, 0f), 0.5f, armored: true);
            var plain = new Hurtbox(new Vector2(1.4f, 0f), 0.5f);
            var weak = new Hurtbox(new Vector2(3f, 0f), 0.5f, 2);
            IReadOnlyList<Hurtbox> parts = new[] { armor, plain, weak };
            var arena = new CombatArena();
            var boss = new DamageReceiver(new HealthPool(10), 1f, () => new Vector2(2f, 0f), 0, parts: () => parts);
            arena.AddTarget(boss);
            DamageOutcome outcome = DamageOutcome.Ignored;
            boss.Resolved += (_, result) => outcome = result;

            Require(arena.Melee(new MeleeStrike(Vector2.zero, Vector2.right, 1.3f, 0.38f, 1, 4f)) && boss.Health.Current == 9
                && outcome == DamageOutcome.Applied, "A swing over armor and a plain part did not land once on the plain part.");
            parts = new[] { armor };
            Require(arena.Melee(new MeleeStrike(Vector2.zero, Vector2.right, 1.3f, 0.38f, 1, 4f)) && boss.Health.Current == 9
                && outcome == DamageOutcome.Absorbed && boss.LastPart.Armored, "Armor did not absorb a hit that still counts.");
            parts = new[] { weak };
            Require(arena.Melee(new MeleeStrike(new Vector2(2f, 0f), Vector2.right, 1.3f, 0.38f, 1, 4f))
                && boss.Health.Current == 7, "A weak point did not double the damage.");
            parts = new[] { plain };
            Require(!arena.Melee(new MeleeStrike(new Vector2(5f, 0f), Vector2.right, 1.3f, 0.38f, 1, 4f))
                && boss.Health.Current == 7, "A swing hit a part behind it.");
        }

        private static void VerifyCommonMelee(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            foreach (Character character in Character.Roster)
            {
                session.Start(character, 301);
                fixture.Advance(8);
                Vector2 origin = session.Motor.Body.position;
                Vector2 knockback = Vector2.zero;
                DamageReceiver ahead = AddTarget(session, origin + Vector2.right * 1.1f,
                    applyImpulse: value => knockback = value);
                DamageReceiver behind = AddTarget(session, origin + Vector2.left);
                DamageReceiver above = AddTarget(session, origin + Vector2.up * 1.1f);

                fixture.Step(new PlayerCommand(move: 1, attackPressed: true));
                Require(ahead.Health.Current == 2 && behind.Health.Current == 3 && above.Health.Current == 3
                    && knockback.x > 0f && session.Motor.Body.linearVelocity.x > 0f,
                    $"{character.Name} did not strike forward while moving with knockback.");
                fixture.Step(new PlayerCommand(attackPressed: true));
                Require(ahead.Health.Current == 2, "Melee could repeat through its recovery.");

                fixture.Advance(Tuning.meleeRecoverySteps);
                fixture.Step(new PlayerCommand(aim: 1, attackPressed: true));
                Require(above.Health.Current == 2 && ahead.Health.Current == 2
                    && session.Run.Energy == character.MaxEnergy, $"{character.Name} could not perform a free upward attack.");
            }
        }

        private static void VerifySharedDamage(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            session.Start(Character.Roster[0], 302);
            fixture.Advance(8);
            DamageReceiver player = session.PlayerDamage;
            int startingHealth = session.Run.Health;
            Require(player.Receive(new DamageHit(DamageSource.Projectile, 1, new Vector2(3f, 2f))) == DamageOutcome.Applied
                && session.Run.Health == startingHealth - 1 && session.Motor.Body.linearVelocity.x > 2.9f,
                "A projectile hit did not cause heart damage and knockback.");
            Require(player.Receive(new DamageHit(DamageSource.Trap, 1, Vector2.left * 8f)) == DamageOutcome.Ignored
                && session.Run.Health == startingHealth - 1 && session.Motor.Body.linearVelocity.x > 2.9f,
                "A damage attempt during i-frames changed health or knockback.");
            fixture.Advance(Tuning.playerInvulnerabilitySteps);
            Require(player.Receive(new DamageHit(DamageSource.Trap, 1)) == DamageOutcome.Applied
                && session.Run.Health == startingHealth - 2, "A trap was still ignored after i-frames ended.");
        }

        private static void VerifySpider(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            Character spider = Character.Roster[(int)Insect.Spider];
            session.Start(spider, 303);
            fixture.Advance(8);
            session.Run.TrySpendEnergy(session.Run.Energy);
            fixture.Step(new PlayerCommand(abilityPressed: true));
            Require(session.Run.Energy == 0f && session.Motor.Grounded && Mathf.Abs(session.Motor.Body.linearVelocity.y) < 0.1f,
                "Spider high jumped without energy.");
            fixture.Step(new PlayerCommand(move: -1, jumpPressed: true, jumpHeld: true));
            Require(session.Run.Energy == 0f && session.Motor.Body.linearVelocity.x < 0f
                && session.Motor.Body.linearVelocity.y > 12f, "Spider's regular jump lacked height, steering, or free use.");

            session.Start(spider, 304);
            session.Run.TrySpendEnergy(session.Run.Energy);
            Vector2 start = new(-15.68f, -0.04f);
            session.Motor.Warp(start);
            Require(!session.Motor.HasWall(-1), "Spider started the climb fixture already against the wall.");
            for (int step = 0; step < 20; step++)
            {
                fixture.Step(new PlayerCommand(move: -1));
                if (session.Abilities.Climbing && session.Motor.Body.position.y > start.y + 0.15f)
                    break;
            }
            Require(session.Abilities.Climbing && session.Motor.Body.position.y > start.y + 0.15f && session.Run.Energy == 0f,
                $"Walking into a wall did not start a free climb. Position {session.Motor.Body.position}.");
            fixture.Step();
            Require(!session.Abilities.Climbing, "Spider kept climbing after movement was released.");

            session.Start(spider, 305);
            fixture.Advance(8);
            fixture.Step(new PlayerCommand(move: 1, abilityPressed: true, abilityHeld: true));
            fixture.Advance(8, new PlayerCommand(move: 1, abilityHeld: true));
            Require(Near(session.Run.Energy, spider.MaxEnergy - Tuning.spiderLeapEnergy)
                && session.Motor.Body.linearVelocity.x > 0f && session.Motor.Body.linearVelocity.y > 10f,
                "Holding Spider's high jump spent energy more than once or disabled air steering.");
        }

        private static void VerifyEnemyContact(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            Character character = Character.Roster[(int)Insect.FireAnt];
            session.Start(character, 314);
            fixture.Advance(8);
            Vector2 enemyFeet = session.Motor.Body.position + new Vector2(1.7f, -character.Radius);
            var enemy = new LevelEnemy(fixture.World.transform, null, ForestContent.BarkMite, enemyFeet, 0, null);
            DamageSource? hitSource = null;
            session.PlayerDamage.Resolved += (hit, outcome) =>
            {
                if (outcome == DamageOutcome.Applied)
                    hitSource = hit.Source;
            };
            for (int step = 0; step < 35 && hitSource == null; step++)
            {
                fixture.Step(new PlayerCommand(move: 1));
                enemy.Step(session.PlayerDamage);
            }
            Require(hitSource == DamageSource.Contact && session.Run.Health == character.MaxHealth - 1
                && enemy.AttackPhase == LevelEnemy.Phase.Warning && session.Motor.Body.linearVelocity.x < 0f,
                "Running into a live enemy should cost one heart and push the player back before any attack.");

            session.Motor.Warp(enemy.Position);
            enemy.Step(session.PlayerDamage);
            Require(session.Run.Health == character.MaxHealth - 1, "Enemy contact ignored the player's invulnerability.");

            fixture.Advance(Tuning.playerInvulnerabilitySteps);
            enemy.Damage.Receive(new DamageHit(DamageSource.Melee, 2));
            session.Motor.Warp(enemy.Position);
            enemy.Step(session.PlayerDamage);
            Require(session.Run.Health == character.MaxHealth - 1, "A defeated enemy dealt contact damage.");
        }

        private static void VerifyBeetleMomentum(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            session.Start(Character.Roster[1], 305);
            fixture.Advance(8);
            fixture.Advance(6, new PlayerCommand(move: 1));
            float speed = session.Motor.Body.linearVelocity.x;
            Vector2 start = session.Motor.Body.position;
            DamageReceiver enemy = AddTarget(session, start + Vector2.right);
            fixture.Step(new PlayerCommand(move: -1, jumpPressed: true, jumpHeld: true, attackPressed: true, abilityPressed: true));
            Require(session.Abilities.Shelled && Near(session.Motor.Body.linearVelocity.x, speed)
                && session.Motor.Body.position.y < start.y + 0.025f && enemy.Health.Current == 3 && session.Run.Energy == 100f,
                "Shell did not preserve momentum, suppress commands, or remain free until hit.");

            fixture.Step(new PlayerCommand(move: -1, abilityPressed: true));
            Require(!session.Abilities.Shelled && session.Motor.Body.linearVelocity.x < speed,
                "Opening the shell did not restore steering.");
        }

        private static void VerifyBeetleDamage(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            session.Start(Character.Roster[1], 306);
            fixture.Advance(8);
            int startingHealth = session.Run.Health;
            fixture.Step(new PlayerCommand(abilityPressed: true));
            var tap = new DamageHit(DamageSource.Trap, 1, Vector2.left * 3f);
            Require(session.PlayerDamage.Receive(tap) == DamageOutcome.Absorbed
                && Near(session.Run.Energy, 100f - Tuning.beetleEnergyPerHeart)
                && session.Run.Health == startingHealth && Mathf.Abs(session.Motor.Body.linearVelocity.x) < 0.01f,
                "Shell failed to absorb trap damage at the per-heart cost without knockback.");

            float balance = session.Run.Energy;
            Require(session.PlayerDamage.Receive(new DamageHit(DamageSource.Projectile, 2)) == DamageOutcome.Ignored
                && Near(session.Run.Energy, balance), "Shell paid for a hit discarded by i-frames.");
            fixture.Advance(Tuning.playerInvulnerabilitySteps);

            Require(session.Run.TrySpendEnergy(session.Run.Energy - Tuning.beetleEnergyPerHeart),
                "Could not set the exact-cost shell fixture.");
            Require(session.PlayerDamage.Receive(new DamageHit(DamageSource.Melee, 0)) == DamageOutcome.Ignored
                && Near(session.Run.Energy, Tuning.beetleEnergyPerHeart), "A zero-damage contact spent shell energy.");
            Require(session.PlayerDamage.Receive(new DamageHit(DamageSource.Melee, 1)) == DamageOutcome.Absorbed
                && session.Run.Energy == 0f && session.Abilities.Shelled && session.Run.Health == startingHealth,
                "A shell hit with exactly the required energy failed to absorb.");
            fixture.Advance(Tuning.playerInvulnerabilitySteps);
            Require(session.PlayerDamage.Receive(new DamageHit(DamageSource.Projectile, 2, Vector2.right * 4f))
                == DamageOutcome.Applied && !session.Abilities.Shelled && session.Run.Energy == 0f
                && session.Run.Health == startingHealth - 2 && session.Motor.Body.linearVelocity.x > 3.9f,
                "An unaffordable shell hit did not open the shell and apply full damage plus knockback.");

            session.Start(Character.Roster[1], 307);
            session.Motor.Warp(new Vector2(-0.2f, 4f));
            DamageSource? absorbedSource = null;
            session.PlayerDamage.Resolved += (hit, outcome) =>
            {
                if (outcome == DamageOutcome.Absorbed)
                    absorbedSource = hit.Source;
            };
            fixture.Step(new PlayerCommand(abilityPressed: true));
            fixture.Advance(60);
            Require(absorbedSource == DamageSource.Fall && session.Run.Health == startingHealth
                && Near(session.Run.Energy, 100f - Tuning.beetleEnergyPerHeart),
                "A real fast landing did not pass its would-be fall damage through the shell.");

            session.Start(Character.Roster[1], 308);
            session.Run.TrySpendEnergy(100f - (Tuning.beetleEnergyPerHeart - 1f));
            session.Motor.Warp(new Vector2(-0.2f, 4f));
            fixture.Step(new PlayerCommand(abilityPressed: true));
            fixture.Advance(60);
            Require(!session.Abilities.Shelled && session.Run.Health == startingHealth - 1
                && Near(session.Run.Energy, Tuning.beetleEnergyPerHeart - 1f),
                "An unaffordable fall block did not break the shell and retain unspent energy.");
        }

        private static void VerifyBeetleCrush(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            session.Start(Character.Roster[1], 309);
            session.Motor.Body.position = new Vector2(-0.2f, 2f);
            DamageReceiver underneath = AddTarget(session, new Vector2(-0.2f, -2.0f));
            DamageReceiver aside = AddTarget(session, new Vector2(0.9f, -2.0f));
            DamageReceiver tooHigh = AddTarget(session, new Vector2(-0.2f, 1.0f), radius: 0.15f);
            DamageSource? crushSource = null;
            underneath.Resolved += (hit, outcome) => crushSource = hit.Source;
            fixture.Step(new PlayerCommand(abilityPressed: true));
            // Observe the target crossing before the later landing on the Forest floor.
            fixture.Advance(31);
            Require(underneath.Health.Current == 3 - Tuning.beetleCrushDamage && crushSource == DamageSource.Crush
                && aside.Health.Current == 3 && tooHigh.Health.Current == 3 && session.Run.Energy == 100f,
                $"Only a sufficiently fast shell descent onto a target should crush for free. Below {underneath.Health.Current}, side {aside.Health.Current}, high {tooHigh.Health.Current}, energy {session.Run.Energy}.");

            session.Start(Character.Roster[1], 310);
            session.Motor.Body.position = new Vector2(-0.2f, 2f);
            DamageReceiver unshelled = AddTarget(session, new Vector2(-0.2f, -2.0f));
            fixture.Advance(35);
            Require(unshelled.Health.Current == 3, "An unshelled Beetle crushed a target.");
        }

        private static void VerifyDragonfly(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            session.Start(Character.Roster[2], 311);
            fixture.Advance(8);
            fixture.Step(new PlayerCommand(move: 1, aim: 1, abilityPressed: true));
            fixture.Advance(20, new PlayerCommand(move: 1, aim: 1));
            Require(session.Abilities.Flying && session.Motor.Body.linearVelocity.x > 5f
                && session.Motor.Body.linearVelocity.y > 5f
                && Near(session.Run.Energy, 100f - Tuning.dragonflyEnergyPerSecond * 21f * PlayerMotor.StepSeconds),
                "Dragonfly directional flight or per-second drain did not match tuning.");

            fixture.Step(new PlayerCommand(abilityPressed: true));
            Require(!session.Abilities.Flying, "Dragonfly could not toggle flight off.");
            float balance = session.Run.Energy;
            fixture.Advance(10);
            Require(Near(session.Run.Energy, balance), "Dragonfly energy drained with flight off.");
            session.Run.TrySpendEnergy(session.Run.Energy - 0.3f);
            fixture.Step(new PlayerCommand(aim: 1, abilityPressed: true));
            Require(session.Abilities.Flying, "Flight did not start with some energy remaining.");
            fixture.Step(new PlayerCommand(aim: 1));
            Require(session.Run.Energy == 0f && !session.Abilities.Flying,
                "Dragonfly flight did not disengage at exactly zero energy.");
            fixture.Step(new PlayerCommand(aim: 1, abilityPressed: true));
            Require(!session.Abilities.Flying, "Flight began at zero energy.");
        }

        private static void VerifyFireAnt(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            session.Start(Character.Roster[3], 312);
            fixture.Advance(8);
            Vector2 position = session.Motor.Body.position;
            DamageReceiver center = AddTarget(session, position + new Vector2(1f, 0f));
            DamageReceiver flank = AddTarget(session, position + new Vector2(1.58f, 0.65f));
            DamageReceiver outOfReach = AddTarget(session, position + new Vector2(2.8f, 0.65f));
            fixture.Step(new PlayerCommand(attackPressed: true));
            Require(center.Health.Current == 2 && flank.Health.Current == 3, "Ordinary melee hit the heavy-only flank.");
            fixture.Advance(Tuning.meleeRecoverySteps);
            fixture.Step(new PlayerCommand(move: 1, abilityPressed: true));
            Require(center.Health.Current == 0 && flank.Health.Current == 3 - Tuning.fireAntAttackDamage
                && outOfReach.Health.Current == 3 && Near(session.Run.Energy, 100f - Tuning.fireAntAttackEnergy)
                && session.Motor.Body.linearVelocity.x > 0f, "Fire Ant did not use its wider damaging strike while moving.");
            fixture.Step(new PlayerCommand(abilityPressed: true));
            Require(Near(session.Run.Energy, 100f - Tuning.fireAntAttackEnergy), "Heavy recovery still consumed energy.");
            fixture.Advance(Tuning.heavyRecoverySteps);
            session.Run.TrySpendEnergy(session.Run.Energy - 1f);
            fixture.Step(new PlayerCommand(abilityPressed: true));
            Require(session.Run.Energy == 1f && flank.Health.Current == 1, "An unaffordable heavy strike affected health or energy.");
        }

        private static void VerifyDisabledAbilities()
        {
            using var fixture = new PhysicsFixture("Abilities disabled", enableAbilities: false);
            GameSession session = fixture.Session;
            foreach (Character character in Character.Roster)
            {
                session.Start(character, 313);
                fixture.Advance(8);
                fixture.Step(new PlayerCommand(aim: -1, abilityPressed: true, abilityHeld: true));
                Require(session.Run.Energy == character.MaxEnergy && !session.Abilities.Shelled
                    && !session.Abilities.Flying && !session.Abilities.Climbing && session.Terrain.Revision == 0,
                    $"Disabled abilities still affected the fixture for {character.Name}.");
            }
        }

        private static DamageReceiver AddTarget(GameSession session, Vector2 position, float radius = 0.15f,
            Action<Vector2> applyImpulse = null)
        {
            var target = new DamageReceiver(new HealthPool(3), radius, () => position, 8, applyImpulse);
            session.Combat.AddTarget(target);
            return target;
        }

        private static bool Near(float actual, float expected) => Mathf.Abs(actual - expected) < 0.005f;
    }
}
