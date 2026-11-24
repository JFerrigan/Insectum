using System;
using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    public sealed class HealthPool
    {
        public int Maximum { get; private set; }
        public int Current { get; private set; }
        public bool Alive => Current > 0;

        public HealthPool(int maximum)
        {
            Maximum = Mathf.Max(1, maximum);
            Current = Maximum;
        }

        public void Restore(int hearts) => Current += Mathf.Clamp(hearts, 0, Maximum - Current);

        public void IncreaseMaximum()
        {
            Maximum++;
            Restore(1);
        }

        public int TakeDamage(int hearts)
        {
            int applied = Mathf.Clamp(hearts, 0, Current);
            Current -= applied;
            return applied;
        }
    }

    public enum DamageSource
    {
        Melee,
        Projectile,
        Trap,
        Fall,
        Crush,
        Explosion,
        Burn,
        Contact
    }

    public readonly struct DamageHit
    {
        public DamageSource Source { get; }
        public int Hearts { get; }
        public Vector2 Knockback { get; }

        public DamageHit(DamageSource source, int hearts, Vector2 knockback = default)
        {
            Source = source;
            Hearts = hearts;
            Knockback = knockback;
        }
    }

    public enum DamageOutcome
    {
        Ignored,
        Absorbed,
        Applied
    }

    public sealed class DamageReceiver
    {
        private readonly Func<Vector2> position;
        private readonly Action<Vector2> applyImpulse;
        private readonly Func<DamageHit, bool> absorb;
        private readonly int invulnerabilityDuration;

        public HealthPool Health { get; }
        public float Radius { get; }
        public Vector2 Position => position();
        public int InvulnerabilitySteps { get; private set; }
        public event Action<DamageHit, DamageOutcome> Resolved;

        public DamageReceiver(HealthPool health, float radius, Func<Vector2> position, int invulnerabilitySteps,
            Action<Vector2> applyImpulse = null, Func<DamageHit, bool> absorb = null)
        {
            Health = health;
            Radius = radius;
            this.position = position;
            this.applyImpulse = applyImpulse;
            this.absorb = absorb;
            invulnerabilityDuration = Mathf.Max(0, invulnerabilitySteps);
        }

        public void ProtectFor(int steps) => InvulnerabilitySteps = Mathf.Max(InvulnerabilitySteps, steps);

        public void Step()
        {
            if (InvulnerabilitySteps > 0)
                InvulnerabilitySteps--;
        }

        public DamageOutcome Receive(DamageHit hit)
        {
            if (!Health.Alive || hit.Hearts <= 0 || InvulnerabilitySteps > 0)
                return DamageOutcome.Ignored;

            InvulnerabilitySteps = invulnerabilityDuration;
            if (absorb != null && absorb(hit))
            {
                Resolved?.Invoke(hit, DamageOutcome.Absorbed);
                return DamageOutcome.Absorbed;
            }

            Health.TakeDamage(hit.Hearts);
            if (hit.Knockback != Vector2.zero)
                applyImpulse?.Invoke(hit.Knockback);

            Resolved?.Invoke(hit, DamageOutcome.Applied);
            return DamageOutcome.Applied;
        }
    }

    public readonly struct MeleeStrike
    {
        public Vector2 Origin { get; }
        public Vector2 Direction { get; }
        public float Reach { get; }
        public float HalfWidth { get; }
        public DamageHit Hit { get; }

        public MeleeStrike(Vector2 origin, Vector2 direction, float reach, float halfWidth, int damage, float force)
        {
            Origin = origin;
            Direction = direction;
            Reach = reach;
            HalfWidth = halfWidth;
            Hit = new DamageHit(DamageSource.Melee, damage, direction * force + Vector2.up * 1.2f);
        }
    }

    public sealed class CombatArena
    {
        private readonly List<DamageReceiver> targets = new();

        public IReadOnlyList<DamageReceiver> Targets { get; }
        public event Action<MeleeStrike> MeleePerformed;

        public CombatArena() => Targets = targets.AsReadOnly();

        public void AddTarget(DamageReceiver target) => targets.Add(target);

        public void Step()
        {
            foreach (DamageReceiver target in targets)
                target.Step();
        }

        public bool Melee(MeleeStrike strike)
        {
            bool hit = false;
            int count = targets.Count;
            var across = new Vector2(-strike.Direction.y, strike.Direction.x);
            for (int i = 0; i < count; i++)
            {
                DamageReceiver target = targets[i];
                if (!target.Health.Alive)
                    continue;

                Vector2 offset = target.Position - strike.Origin;
                float forward = Vector2.Dot(strike.Direction, offset);
                float lateral = Mathf.Abs(Vector2.Dot(across, offset));
                if (forward >= 0f && forward - target.Radius <= strike.Reach && lateral <= strike.HalfWidth + target.Radius)
                {
                    target.Receive(strike.Hit);
                    hit = true;
                }
            }

            MeleePerformed?.Invoke(strike);
            return hit;
        }

        public void Crush(Vector2 previous, Vector2 current, float attackerRadius, int damage)
        {
            if (current.y >= previous.y)
                return;

            float previousFoot = previous.y - attackerRadius;
            float currentFoot = current.y - attackerRadius;
            int count = targets.Count;
            for (int i = 0; i < count; i++)
            {
                DamageReceiver target = targets[i];
                if (!target.Health.Alive)
                    continue;

                Vector2 center = target.Position;
                float top = center.y + target.Radius;
                if (previousFoot < top || currentFoot > top)
                    continue;

                float t = (previousFoot - top) / (previousFoot - currentFoot);
                float crossing = Mathf.Lerp(previous.x, current.x, t);
                if (Mathf.Abs(crossing - center.x) <= attackerRadius + target.Radius)
                    target.Receive(new DamageHit(DamageSource.Crush, damage, Vector2.down * 3f));
            }
        }
    }
}
