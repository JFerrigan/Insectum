using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Insectum.Game.Editor.ProductionVerification;

namespace Insectum.Game.Editor
{
    // Drives the player through a tunnel level the way it is built: landing to landing along the room graph,
    // jumping where a door starts from a ledge or a room marks a leap over a hazard, and striking enemies that
    // stand in the way.
    internal static class TunnelWalker
    {
        private const int SegmentLimit = 420;
        // A walker crossing a take-off point leaps on its first grounded step inside this window.
        private const float LeapWindow = 0.6f;

        // Walks toward the level exit until reached() holds.
        internal static void Walk(PhysicsFixture fixture, Func<bool> reached, bool useHeavy = false)
        {
            GameSession session = fixture.Session;
            Travel(fixture, session.Layout.Tunnel.Route[^1], reached, useHeavy);
            Require(reached(), $"{Where(session)}: the tunnel walk reached the exit without meeting its goal.");
        }

        // Walks through the rooms to the landing nearest the goal, then steps over to the goal itself.
        internal static void WalkTo(PhysicsFixture fixture, Vector2 goal, float radius, bool useHeavy = false)
        {
            GameSession session = fixture.Session;
            bool Near() => (session.Motor.Body.position - goal).sqrMagnitude < radius * radius;
            Travel(fixture, Nearest(session.Layout.Tunnel, goal), Near, useHeavy);
            for (int step = 0; step < 240 && !Near() && session.Phase == RunPhase.Playing; step++)
            {
                float dx = goal.x - session.Motor.Body.position.x;
                int move = Mathf.Abs(dx) > 0.11f ? Math.Sign(dx) : 0;
                fixture.Step(Fight(session, move, useHeavy) ?? new PlayerCommand(move: move));
            }
            Require(Near(), $"{Where(session)}: could not reach {goal} from {session.Motor.Body.position}.");
        }

        // Crosses the main route without content, optionally visiting every side tunnel on the way.
        internal static int CrossRoute(PhysicsFixture fixture, bool explore)
        {
            GameSession session = fixture.Session;
            ForestTunnelSpace tunnel = session.Layout.Tunnel;
            int start = Steps;
            fixture.Advance(8);
            for (int i = 0; i < tunnel.Route.Count; i++)
            {
                Vector2 here = tunnel.Route[i];
                if (explore)
                {
                    foreach (var branch in tunnel.Branches.Where(branch => branch.source == i))
                    {
                        Segment(fixture, here, branch.first, Never, false);
                        Segment(fixture, branch.first, branch.end, Never, false);
                        Segment(fixture, branch.end, branch.first, Never, false);
                        Segment(fixture, branch.first, here, Never, false);
                    }
                }
                if (i + 1 < tunnel.Route.Count)
                    Segment(fixture, here, tunnel.Route[i + 1], Never, false);
            }
            Require(session.AtExit, $"{Where(session)}: the crossing ended beyond the exit at {session.Motor.Body.position}.");
            return Steps - start + 8;
        }

        private static int Steps { get; set; }

        private static bool Never() => false;

        private static void Travel(PhysicsFixture fixture, Vector2 target, Func<bool> reached, bool useHeavy)
        {
            GameSession session = fixture.Session;
            ForestTunnelSpace tunnel = session.Layout.Tunnel;
            List<Vector2> path = Path(tunnel, Nearest(tunnel, Feet(session)), target);
            for (int i = 1; i < path.Count && !reached() && session.Phase == RunPhase.Playing; i++)
                Segment(fixture, path[i - 1], path[i], reached, useHeavy);
        }

        private static void Segment(PhysicsFixture fixture, Vector2 from, Vector2 to, Func<bool> reached, bool useHeavy)
        {
            GameSession session = fixture.Session;
            bool jump = session.Layout.Tunnel.Jumps.ContainsKey((from, to));
            bool jumped = false;
            bool leaping = false;
            if (jump && !reached())
            {
                // Settle on the landing so the jump starts from rest, as a player would.
                fixture.Advance(12);
                Steps += 12;
            }

            for (int step = 0; step < SegmentLimit; step++)
            {
                if (reached() || session.Phase != RunPhase.Playing || !session.Run.Alive)
                    return;
                Vector2 feet = Feet(session);
                if (Mathf.Abs(feet.x - to.x) <= 0.36f && Mathf.Abs(feet.y - to.y) <= 0.40f && session.Motor.Grounded)
                    return;

                float dx = to.x - feet.x;
                int move = Mathf.Abs(dx) > 0.13f ? Math.Sign(dx) : 0;
                PlayerCommand command;
                // Mid-leap the walker keeps running, since stopping to strike would drop it short.
                bool airborne = leaping && !session.Motor.Grounded;
                if (!airborne && Fight(session, move, useHeavy) is PlayerCommand strike)
                    command = strike;
                else
                {
                    bool press = jump && !jumped && session.Motor.Grounded;
                    jumped |= press;
                    bool leap = !press && AtLeap(session, to);
                    leaping = leap || (leaping && !session.Motor.Grounded);
                    command = new PlayerCommand(move: move, jumpPressed: press || leap, jumpHeld: jump || leaping);
                }
                fixture.Step(command);
                Steps++;
            }

            Require(false, $"{Where(session)}: stuck between landings {from} and {to}, jump {jump}, at "
                + $"{session.Motor.Body.position} with {session.Run.Health} hearts.");
        }

        // Strikes a live enemy that blocks the way ahead or hovers just above, using the heavy strike when asked.
        private static PlayerCommand? Fight(GameSession session, int move, bool useHeavy)
        {
            if (session.Content == null)
                return null;
            Vector2 position = session.Motor.Body.position;
            int facing = move != 0 ? move : session.Motor.Facing;
            bool heavy = useHeavy && session.Run.Character.Insect == Insect.FireAnt
                && session.Run.Energy >= CharacterTuning.Current.fireAntAttackEnergy;
            float reach = heavy ? 2.1f : 1.3f;
            float halfWidth = heavy ? 0.83f : 0.38f;
            foreach (LevelEnemy enemy in session.Content.Enemies)
            {
                if (!enemy.Damage.Health.Alive || enemy.Boss)
                    continue;
                Vector2 offset = enemy.Position - position;
                float ahead = facing * offset.x;
                float radius = enemy.Damage.Radius;
                if (ahead >= 0f && ahead < reach + radius && Mathf.Abs(offset.y) < halfWidth + radius)
                    return new PlayerCommand(move: session.Motor.Facing == facing ? 0 : facing,
                        attackPressed: true, abilityPressed: heavy);
                if (Mathf.Abs(offset.x) < halfWidth + radius && offset.y > 0f && offset.y < reach + radius)
                    return new PlayerCommand(aim: 1, attackPressed: true, abilityPressed: heavy);
            }
            return null;
        }

        // True when the grounded player has just crossed a take-off point that a room marks toward this goal.
        internal static bool AtLeap(GameSession session, Vector2 goal)
        {
            if (!session.Motor.Grounded)
                return false;
            Vector2 feet = Feet(session);
            foreach (var (takeoff, direction) in session.Layout.Tunnel.Leaps)
            {
                float along = (feet.x - takeoff.x) * direction;
                if (along >= -0.02f && along < LeapWindow && Mathf.Abs(feet.y - takeoff.y) < 0.5f
                    && (goal.x - takeoff.x) * direction > 0f)
                    return true;
            }
            return false;
        }

        private static List<Vector2> Path(ForestTunnelSpace tunnel, Vector2 start, Vector2 goal)
        {
            var links = new Dictionary<Vector2, List<Vector2>>();
            void Link(Vector2 a, Vector2 b)
            {
                (links.TryGetValue(a, out var fromA) ? fromA : links[a] = new List<Vector2>()).Add(b);
                (links.TryGetValue(b, out var fromB) ? fromB : links[b] = new List<Vector2>()).Add(a);
            }
            for (int i = 1; i < tunnel.Route.Count; i++)
                Link(tunnel.Route[i - 1], tunnel.Route[i]);
            foreach (var branch in tunnel.Branches)
            {
                Link(tunnel.Route[branch.source], branch.first);
                Link(branch.first, branch.end);
            }

            var previous = new Dictionary<Vector2, Vector2> { [start] = start };
            var open = new Queue<Vector2>();
            open.Enqueue(start);
            while (open.TryDequeue(out Vector2 node) && node != goal)
                foreach (Vector2 next in links[node])
                    if (previous.TryAdd(next, node))
                        open.Enqueue(next);
            Require(previous.ContainsKey(goal), $"Tunnel landing {goal} is not connected to {start}.");

            var path = new List<Vector2> { goal };
            while (path[^1] != start)
                path.Add(previous[path[^1]]);
            path.Reverse();
            return path;
        }

        private static Vector2 Nearest(ForestTunnelSpace tunnel, Vector2 point) =>
            tunnel.Route.Concat(tunnel.Branches.SelectMany(branch => new[] { branch.first, branch.end }))
                .OrderBy(landing => (landing - point).sqrMagnitude).First();

        private static Vector2 Feet(GameSession session) =>
            session.Motor.Body.position - Vector2.up * session.Run.Character.Radius;

        private static string Where(GameSession session) =>
            $"{session.Run.World} seed {session.Run.Seed}/{session.Run.LevelNumber} {session.Run.Character.Name}";
    }
}
