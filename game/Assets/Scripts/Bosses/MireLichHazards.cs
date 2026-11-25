using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal sealed partial class MireLich
    {
        internal enum HazardKind { Glob, Puddle, Bubble, Cloud, Larva, LarvaWarning, LarvaDart }

        internal readonly struct Hazard
        {
            internal readonly HazardKind Kind;
            internal readonly Vector2 Position;
            internal readonly float Radius;
            internal readonly int Age;
            internal readonly Vector2 Direction;

            internal Hazard(HazardKind kind, Vector2 position, float radius, int age = 0, Vector2 direction = default)
            {
                Kind = kind;
                Position = position;
                Radius = radius;
                Age = age;
                Direction = direction;
            }
        }

        private sealed class Bubble
        {
            internal Vector2 Position;
            internal int Age;
            internal readonly DamageReceiver Body;

            internal Bubble(Vector2 position)
            {
                Position = position;
                Body = new DamageReceiver(new HealthPool(1), 0.54f, () => Position, 0);
            }
        }

        private sealed class Larva
        {
            internal Vector2 Position;
            internal Vector2 Direction;
            internal int Facing = 1;
            internal readonly DamageReceiver Body;

            internal Larva(Vector2 position)
            {
                Position = position;
                Body = new DamageReceiver(new HealthPool(2), 0.47f, () => Position, 9);
            }
        }

        internal const float PuddleWidth = 1.4f;
        internal const float PuddleHeight = 0.2f;
        private const int PuddleLife = 90;
        private const int BubbleLife = 240;
        private const int CloudLife = 32;
        private const float GlobGravity = 19f;
        private readonly List<BossProjectile> globs = new();
        private readonly List<Hazard> patches = new();
        private readonly List<Bubble> bubbles = new();
        private readonly List<Larva> larvae = new();
        private Larva dart;

        internal IEnumerable<Hazard> Hazards
        {
            get
            {
                foreach (BossProjectile glob in globs)
                    yield return new Hazard(HazardKind.Glob, glob.Position, 0.23f, direction: glob.Velocity.normalized);
                foreach (Hazard patch in patches)
                    yield return patch;
                foreach (Bubble bubble in bubbles)
                    yield return new Hazard(HazardKind.Bubble, bubble.Position, 0.54f, bubble.Age);
                foreach (Larva larva in larvae)
                {
                    HazardKind kind = larva != dart ? HazardKind.Larva
                        : CurrentAct == Act.LarvaWarning ? HazardKind.LarvaWarning : HazardKind.LarvaDart;
                    yield return new Hazard(kind, larva.Position, 0.47f, larva == dart ? age : tick,
                        larva == dart ? larva.Direction : Vector2.right * larva.Facing);
                }
            }
        }

        private void LaunchGlobs(Vector2 target)
        {
            int count = Phase * 2 + 1;
            Vector2 from = Position + new Vector2(-Facing * (0.55f + Stage * 0.55f), 1.25f);
            float center = Mathf.Clamp(target.x, -Lane, Lane);
            const float upward = 2.5f;
            float height = Mathf.Max(0.5f, from.y - target.y + 0.4f);
            float travel = (upward + Mathf.Sqrt(upward * upward + 2f * GlobGravity * height)) / GlobGravity;
            for (int i = 0; i < count; i++)
            {
                const float spacing = 3.05f;
                float x = Mathf.Clamp(center, -17f + (count - 1) * spacing * 0.5f, 17f - (count - 1) * spacing * 0.5f)
                    + (i - (count - 1) * 0.5f) * spacing;
                globs.Add(new BossProjectile(from, new Vector2((x - from.x) / travel, upward + NextFloat())));
            }
        }

        private void LaunchBubbles(Vector2 target)
        {
            int count = Phase == 3 ? 3 : 2;
            for (int i = 0; i < count; i++)
            {
                // Release down toward the lane, with visible gaps between the bubbles.
                Vector2 from = Position + new Vector2((i - (count - 1) * 0.5f) * 1.8f, -0.75f - i * 0.24f);
                var bubble = new Bubble(from);
                bubbles.Add(bubble);
                combat.AddTarget(bubble.Body);
            }
        }

        private void SpawnLarvae()
        {
            int count = Mathf.Min(2, 3 - larvae.Count);
            for (int i = 0; i < count; i++)
            {
                var larva = new Larva(Position + new Vector2(-Facing * (0.65f + i * 0.85f), -0.35f - i * 0.55f));
                larvae.Add(larva);
                combat.AddTarget(larva.Body);
            }
        }

        private void CullMinions()
        {
            for (int i = larvae.Count - 1; i >= 0; i--)
            {
                Larva larva = larvae[i];
                if (larva.Body.Health.Alive)
                    continue;
                combat.RemoveTarget(larva.Body);
                larvae.RemoveAt(i);
                if (dart == larva)
                    dart = null;
            }
        }

        private void TrackLarvae(Vector2 target)
        {
            for (int i = 0; i < larvae.Count; i++)
            {
                Larva larva = larvae[i];
                if (larva == dart)
                    continue;

                int side = larva.Position.x < target.x ? -1 : 1;
                Vector2 perch = new(Mathf.Clamp(target.x + side * (4.1f + 0.4f * i), -16f, 16f),
                    Mathf.Clamp(target.y + 1.2f + 0.4f * i, 1.8f, AirY));
                Vector2 next = Vector2.MoveTowards(larva.Position, perch, 5f * StepSeconds);
                if (!terrain.Sweep(larva.Position, next, out _))
                    larva.Position = next;
                else
                {
                    // Skirt the point of a shelf until there is a clear descent.
                    float direction = Mathf.Abs(larva.Position.x) < 5.6f ? side : -Mathf.Sign(larva.Position.x);
                    next = larva.Position + Vector2.right * (direction * 4.5f * StepSeconds);
                    if (!terrain.Sweep(larva.Position, next, out _))
                        larva.Position = next;
                }
                larva.Facing = target.x < larva.Position.x ? -1 : 1;
            }
        }

        private bool StartDart(Vector2 target)
        {
            foreach (Larva larva in larvae)
            {
                Vector2 offset = target - larva.Position;
                if (offset.magnitude is < 2.2f or > 6.6f || terrain.Sweep(larva.Position, target, out _))
                    continue;
                dart = larva;
                dart.Direction = offset.normalized;
                Enter(Act.LarvaWarning, 24);
                return true;
            }
            return false;
        }

        private void StepDart()
        {
            if (dart == null)
                return;
            Vector2 start = dart.Position;
            Vector2 next = start + dart.Direction * (16f * StepSeconds);
            bool hit = terrain.Sweep(start, next, out Vector2 impact);
            dart.Position = hit ? impact - dart.Direction * 0.035f : next;
            AddSweep(start, hit ? impact : next, 0.32f, dart.Direction * 4.5f + Vector2.up * 2f);
            if (hit)
                FinishDart();
        }

        private void FinishDart()
        {
            if (dart == null)
                return;
            larvae.Remove(dart);
            larvae.Add(dart);
            dart = null;
        }

        private void StepAirHazards(Vector2 target)
        {
            for (int i = patches.Count - 1; i >= 0; i--)
            {
                Hazard patch = patches[i];
                int life = patch.Kind == HazardKind.Puddle ? PuddleLife : CloudLife;
                if (patch.Age + 1 >= life)
                    patches.RemoveAt(i);
                else
                    patches[i] = new Hazard(patch.Kind, patch.Position, patch.Radius, patch.Age + 1);
            }

            for (int i = globs.Count - 1; i >= 0; i--)
            {
                BossProjectile glob = globs[i];
                if (!glob.Step(terrain, GlobGravity))
                    continue;
                // Show and place the puddle along the nearest surface below the entry point.
                Vector2 inside = glob.Position + glob.Velocity.normalized * 0.025f;
                float top = HexGrid.Center(HexGrid.CellAt(inside)).y + 1.02f;
                Vector2 surface = Surface(glob.Position.x, top);
                patches.Add(new Hazard(HazardKind.Puddle, surface, PuddleWidth * 0.5f));
                globs.RemoveAt(i);
            }

            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                Bubble bubble = bubbles[i];
                bool popped = !bubble.Body.Health.Alive;
                if (!popped && ++bubble.Age < BubbleLife)
                {
                    Vector2 next = Vector2.MoveTowards(bubble.Position, target, 3.1f * StepSeconds);
                    if (!terrain.Sweep(bubble.Position, next, out _))
                        bubble.Position = next;
                    else
                    {
                        next = bubble.Position + Vector2.right * (target.x < bubble.Position.x ? -1f : 1f) * (2.1f * StepSeconds);
                        if (!terrain.Sweep(bubble.Position, next, out _))
                            bubble.Position = next;
                    }
                    continue;
                }

                if (!popped)
                    patches.Add(new Hazard(HazardKind.Cloud, bubble.Position, 0.78f));
                Cue(BossCueKind.WingBeat, bubble.Position);
                combat.RemoveTarget(bubble.Body);
                bubbles.RemoveAt(i);
            }
        }

        private void PoseAirHazards()
        {
            foreach (BossProjectile glob in globs)
                AddSweep(glob.Position, glob.Position, 0.2f, new Vector2(0f, 2.5f));
            foreach (Hazard patch in patches)
            {
                Vector2 at = patch.Position;
                float bottom = patch.Kind == HazardKind.Puddle ? at.y : at.y - patch.Radius * 0.65f;
                float top = patch.Kind == HazardKind.Puddle ? at.y + PuddleHeight : at.y + patch.Radius * 0.65f;
                AddWorldHitbox(Rect.MinMaxRect(at.x - patch.Radius, bottom, at.x + patch.Radius, top), Vector2.up * 3f);
            }
        }

        private void ClearAirHazards()
        {
            globs.Clear();
            patches.Clear();
            foreach (Bubble bubble in bubbles)
                combat.RemoveTarget(bubble.Body);
            bubbles.Clear();
        }
    }
}
