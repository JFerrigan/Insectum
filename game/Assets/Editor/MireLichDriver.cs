using UnityEngine;

namespace Insectum.Game.Editor
{
    // Shared between the editor's command bot and the headless motor fixture.
    internal sealed class MireLichDriver
    {
        internal readonly struct Input
        {
            internal readonly int Move;
            internal readonly bool Jump, HoldJump, Strike, Heavy;

            internal Input(int move, bool jump, bool holdJump, bool strike, bool heavy)
            {
                Move = move;
                Jump = jump;
                HoldJump = holdJump;
                Strike = strike;
                Heavy = heavy;
            }
        }

        private MireLich.Act lastAct;
        private MireLich.Move lastMove;
        private int dodgeSide;
        private int jumpTime;

        internal Input Step(MireLich queen, Vector2 at, float vx, bool grounded, float reachBonus, bool heavyReady)
        {
            MireLich.Act act = queen.CurrentAct;
            MireLich.Move move = queen.CurrentMove;
            if (act != lastAct || move != lastMove)
            {
                if (act == MireLich.Act.Warning)
                {
                    float point = queen.Marker?.x ?? queen.Position.x;
                    dodgeSide = Mathf.Abs(point - at.x) > 0.65f ? Sign(at.x - point) : queen.Facing;
                    if (Mathf.Abs(at.x) > 11.5f)
                        dodgeSide = -Sign(at.x);
                }
                else if (act == MireLich.Act.LarvaWarning)
                {
                    foreach (MireLich.Hazard hazard in queen.Hazards)
                        if (hazard.Kind == MireLich.HazardKind.LarvaWarning)
                            dodgeSide = Mathf.Abs(at.x) > 12.1f ? -Sign(at.x) : Sign(hazard.Direction.x);
                }
            }
            lastAct = act;
            lastMove = move;

            int run = 0;
            bool jump = false, strike = false, heavy = false;
            if ((act == MireLich.Act.Warning && move is MireLich.Move.Dive or MireLich.Move.Probe)
                || act is MireLich.Act.Dive or MireLich.Act.Probe)
            {
                run = dodgeSide;
            }
            else if ((act == MireLich.Act.Warning && move == MireLich.Move.Jab) || act == MireLich.Act.Jab)
            {
                float front = (at.x - queen.Position.x) * queen.Facing;
                if (front is > -0.1f and < 3.8f)
                    run = queen.Facing;
            }
            else if (act is MireLich.Act.LarvaWarning or MireLich.Act.LarvaDart)
            {
                run = dodgeSide;
                jump = Mathf.Abs(at.x) > 14.5f;
            }
            else if (move == MireLich.Move.Spray && act is MireLich.Act.Warning or MireLich.Act.AirRecovery)
            {
                // Move beneath an overhead platform before the globs arrive.
                float shelter = FindCover(queen, at);
                run = Steer(shelter - at.x, vx);
                foreach (MireLich.Hazard hazard in queen.Hazards)
                {
                    float dx = hazard.Position.x - at.x;
                    if (hazard.Kind == MireLich.HazardKind.Puddle && Mathf.Abs(hazard.Position.y - at.y) < 0.7f
                        && Mathf.Abs(dx) < 1.9f && run == Sign(dx))
                        jump = true;
                }
            }
            else if (move == MireLich.Move.Bubbles && act == MireLich.Act.AirRecovery)
            {
                float nearest = 6.1f;
                foreach (MireLich.Hazard hazard in queen.Hazards)
                {
                    if (hazard.Kind is not (MireLich.HazardKind.Bubble or MireLich.HazardKind.Cloud))
                        continue;
                    float distance = Vector2.Distance(hazard.Position, at);
                    if (distance >= nearest)
                        continue;
                    nearest = distance;
                    run = Mathf.Abs(at.x) > 12.1f ? -Sign(at.x) : Sign(at.x - hazard.Position.x);
                }
            }
            else if ((move == MireLich.Move.Feed && act == MireLich.Act.Warning) || act == MireLich.Act.Descend)
            {
                if (queen.Marker is Vector2 mark && mark.y < at.y + 1.8f)
                    run = Steer(mark.x - queen.Facing * 2.7f - at.x, vx);
            }
            else if (queen.Opening)
                AimForAbdomen(queen, at, vx, reachBonus, heavyReady, ref run, ref strike, ref heavy);

            if (Mathf.Abs(at.x) > 15.35f && run == Sign(at.x))
                run = 0;
            bool jumpPressed = jump && grounded && jumpTime == 0;
            if (jumpPressed)
                jumpTime = 38;
            bool hold = jumpTime > 0;
            jumpTime = Mathf.Max(0, jumpTime - 1);
            return new Input(run, jumpPressed, hold, strike, heavy);
        }

        private static void AimForAbdomen(MireLich queen, Vector2 at, float vx, float bonus, bool heavyReady,
            ref int run, ref bool strike, ref bool heavy)
        {
            bool canReach = false;
            float nearest = float.MaxValue;
            Hurtbox goal = default;
            foreach (Hurtbox part in queen.Parts)
            {
                if (part.Multiplier != 2)
                    continue;
                Vector2 delta = part.Center - at;
                if (Mathf.Abs(delta.y) <= part.Radius + (heavyReady ? 0.77f : 0.34f))
                    canReach = true;
                float score = delta.sqrMagnitude;
                if (score >= nearest)
                    continue;
                nearest = score;
                goal = part;
            }
            if (!canReach)
                return;

            float dx = goal.Center.x - at.x;
            float clear = Mathf.Abs(dx);
            float reach = (heavyReady ? 2.1f : 1.3f) + bonus + goal.Radius;
            if (clear > reach * 0.78f)
                run = Steer(dx - Sign(dx) * reach * 0.63f, vx);
            if (clear >= 0.15f && clear < reach - 0.08f)
            {
                strike = Mathf.Abs(goal.Center.y - at.y) <= goal.Radius + (heavyReady ? 0.79f : 0.36f);
                heavy = strike && heavyReady;
                if (strike)
                    run = Sign(dx);
            }
            else if (clear < 0.3f)
                run = -queen.Facing;
        }

        private static float FindCover(MireLich queen, Vector2 at)
        {
            float closest = at.x;
            float distance = float.MaxValue;
            for (int i = -2; i <= 2; i++)
            {
                float x = i * 6.8f;
                if (queen.Surface(x, queen.Position.y + 0.5f).y < at.y + 2.5f)
                    continue;
                float dx = Mathf.Abs(at.x - x);
                if (dx >= distance)
                    continue;
                distance = dx;
                closest = x;
            }
            return closest;
        }

        private static int Steer(float dx, float velocity)
        {
            float stoppingDistance = velocity * Mathf.Abs(velocity) / 130f;
            return Mathf.Abs(dx - stoppingDistance) > 0.22f ? Sign(dx - stoppingDistance) : 0;
        }

        private static int Sign(float value) => value < 0f ? -1 : 1;
    }
}
