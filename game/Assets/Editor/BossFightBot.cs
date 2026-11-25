using System;
using UnityEngine;
using static Insectum.Game.Editor.ProductionVerification;

namespace Insectum.Game.Editor
{
    // Fights the reworked bosses with ordinary commands, the way tools/bosscheck.sh's scripted player does
    // headless: keep a little distance, jump charges and shockwaves, step out from under a locked dive and
    // falling bark, back off from snaps, pogo off the back, and strike during openings.
    internal static class BossFightBot
    {
        internal static void FightStagBeetle(PhysicsFixture fixture, LevelEnemy boss, bool useHeavy, int limit = 60 * 300)
        {
            GameSession session = fixture.Session;
            var beetle = (StagBeetle)boss.Brain;
            bool sawCharge = false, sawStuck = false;
            int held = 0;
            for (int tick = 0; tick < limit && !beetle.DeathFinished && session.Phase == RunPhase.Playing && session.Run.Alive; tick++)
            {
                Vector2 body = session.Motor.Body.position;
                Vector2 at = beetle.Position;
                float dx = at.x - body.x;
                float distance = Mathf.Abs(dx);
                int toward = dx < 0f ? -1 : 1;
                bool grounded = session.Motor.Grounded;
                StagBeetle.Act act = beetle.CurrentAct;
                sawCharge |= act == StagBeetle.Act.ChargeRun;
                sawStuck |= act == StagBeetle.Act.Stuck;
                int move = 0;
                bool jump = false;

                switch (act)
                {
                    case StagBeetle.Act.ChargeRun:
                    case StagBeetle.Act.ChargeSkid:
                        float ahead = (body.x - at.x) * beetle.Facing;
                        jump = ahead > 0f && ahead < 4.6f;
                        break;
                    case StagBeetle.Act.ChargeWindup:
                        move = distance < 4f ? -toward : 0;
                        break;
                    case StagBeetle.Act.SnapWindup:
                    case StagBeetle.Act.Snap:
                    case StagBeetle.Act.RearWindup:
                    case StagBeetle.Act.RearSnap:
                        move = distance < 4.2f ? -toward : 0;
                        break;
                    case StagBeetle.Act.HoverTrack:
                        break;
                    case StagBeetle.Act.DiveLock:
                    case StagBeetle.Act.DiveDrop:
                        int away = distance > 0.3f ? -toward : body.x > 0f ? -1 : 1;
                        move = distance < 4.6f ? away : 0;
                        break;
                    case StagBeetle.Act.GoreDash:
                        float front = (body.x - at.x) * beetle.Facing;
                        jump = !beetle.Bombing && front > 0f && front < 4.8f && Mathf.Abs(at.y + 1f - body.y) < 1.6f;
                        break;
                    default:
                        bool open = beetle.Opening && beetle.ActProgress < 0.75f;
                        float keep = open ? 2.2f : 4.5f;
                        move = distance > keep + 0.3f ? toward : distance < keep - 0.3f && !open ? -toward : 0;
                        break;
                }

                foreach (StagBeetle.Shard shard in beetle.Shards)
                {
                    float side = body.x - shard.Position.x;
                    if (Mathf.Abs(side) < 1.1f)
                        move = side < 0f ? -1 : 1;
                }
                if (!grounded && session.Motor.Body.linearVelocity.y < 0f && !beetle.Opening && distance < 3.2f)
                    move = -toward;
                foreach (StagBeetle.Shockwave wave in beetle.Shockwaves)
                {
                    float ahead = (body.x - wave.X) * wave.Direction;
                    if (ahead > 0f && ahead < 2.8f)
                        jump = true;
                    if (ahead > 0f && move == -wave.Direction)
                        move = 0;
                }
                // Stay on the boss lane rather than climbing the terraces.
                if (Mathf.Abs(body.x) > 16.2f && Math.Sign(move) == Math.Sign(body.x))
                    move = 0;

                bool jumpPressed = jump && grounded && held == 0;
                if (jumpPressed)
                    held = 20;
                bool falling = !grounded && session.Motor.Body.linearVelocity.y < 0f;
                bool pogo = falling && distance < 2.5f && body.y > at.y + 1.6f;
                bool strike = beetle.Opening && beetle.ActProgress < 0.9f && distance < 3.4f && grounded;
                bool heavy = strike && useHeavy && session.Run.Character.Insect == Insect.FireAnt
                    && session.Run.Energy >= CharacterTuning.Current.fireAntAttackEnergy;
                if (strike && session.Motor.Facing != toward)
                    move = toward;
                fixture.Step(new PlayerCommand(move, aim: pogo ? -1 : 0, jumpPressed: jumpPressed, jumpHeld: held > 0,
                    attackPressed: pogo || strike, abilityPressed: heavy));
                held = Mathf.Max(0, held - 1);
            }

            Require(beetle.DeathFinished && session.Run.Alive && sawCharge && sawStuck,
                $"Old Oakhorn fight for {session.Run.Character.Name} did not finish with ordinary commands: boss health "
                + $"{beetle.Health.Current}, player health {session.Run.Health}, charge seen {sawCharge}, stuck seen {sawStuck}, "
                + $"position {session.Motor.Body.position}.");
        }
    }
}
