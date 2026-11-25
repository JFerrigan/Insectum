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

    // One part of a body that strikes and blasts can land on. A boss is several of these: an armored part
    // turns hits aside, and a weak point multiplies their damage.
    public readonly struct Hurtbox
    {
        public Vector2 Center { get; }
        public float Radius { get; }
        public int Multiplier { get; }
        public bool Armored { get; }

        public Hurtbox(Vector2 center, float radius, int multiplier = 1, bool armored = false)
        {
            Center = center;
            Radius = radius;
            Multiplier = Mathf.Max(1, multiplier);
            Armored = armored;
        }

        // An unarmored part beats an armored one, and a bigger multiplier beats a smaller one.
        internal bool Outranks(Hurtbox other) =>
            Armored != other.Armored ? !Armored : Multiplier > other.Multiplier;
    }

    public sealed class DamageReceiver
    {
        private readonly Func<Vector2> position;
        private readonly Action<Vector2> applyImpulse;
        private readonly Func<DamageHit, bool> absorb;
        private readonly Func<IReadOnlyList<Hurtbox>> parts;
        private readonly int invulnerabilityDuration;

        public HealthPool Health { get; }
        public float Radius { get; }
        public Vector2 Position => position();
        public int InvulnerabilitySteps { get; private set; }
        // True when the body is made of parts rather than one circle at Position.
        public bool HasParts => parts != null;
        // The part the latest strike or blast landed on, for sparks and flashes.
        public Hurtbox LastPart { get; private set; }
        public event Action<DamageHit, DamageOutcome> Resolved;

        public DamageReceiver(HealthPool health, float radius, Func<Vector2> position, int invulnerabilitySteps,
            Action<Vector2> applyImpulse = null, Func<DamageHit, bool> absorb = null,
            Func<IReadOnlyList<Hurtbox>> parts = null)
        {
            Health = health;
            Radius = radius;
            this.position = position;
            this.applyImpulse = applyImpulse;
            this.absorb = absorb;
            this.parts = parts;
            invulnerabilityDuration = Mathf.Max(0, invulnerabilitySteps);
        }

        // The best part the test overlaps. A body without parts is one plain circle at Position.
        public bool TryFindPart(Func<Vector2, float, bool> overlaps, out Hurtbox part)
        {
            part = default;
            if (parts == null)
            {
                part = new Hurtbox(Position, Radius);
                return overlaps(Position, Radius);
            }

            bool found = false;
            foreach (Hurtbox candidate in parts())
            {
                if (!overlaps(candidate.Center, candidate.Radius) || found && !candidate.Outranks(part))
                    continue;
                part = candidate;
                found = true;
            }
            return found;
        }

        // Lands a hit on one part: armor absorbs it, and a weak point multiplies its damage.
        public DamageOutcome Receive(DamageHit hit, Hurtbox part)
        {
            LastPart = part;
            if (!part.Armored)
                return Receive(part.Multiplier == 1 ? hit : new DamageHit(hit.Source, hit.Hearts * part.Multiplier, hit.Knockback));
            if (!Health.Alive || hit.Hearts <= 0 || InvulnerabilitySteps > 0)
                return DamageOutcome.Ignored;

            InvulnerabilitySteps = invulnerabilityDuration;
            Resolved?.Invoke(hit, DamageOutcome.Absorbed);
            return DamageOutcome.Absorbed;
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

        // Lands the strike on everything in reach, once per body. A hit on armor still counts, so a downward
        // strike bounces off it.
        public bool Melee(MeleeStrike strike)
        {
            bool hit = false;
            int count = targets.Count;
            var across = new Vector2(-strike.Direction.y, strike.Direction.x);
            bool InReach(Vector2 center, float radius)
            {
                Vector2 offset = center - strike.Origin;
                float forward = Vector2.Dot(strike.Direction, offset);
                float lateral = Mathf.Abs(Vector2.Dot(across, offset));
                return forward >= 0f && forward - radius <= strike.Reach && lateral <= strike.HalfWidth + radius;
            }

            for (int i = 0; i < count; i++)
            {
                DamageReceiver target = targets[i];
                if (!target.Health.Alive || !target.TryFindPart(InReach, out Hurtbox part))
                    continue;

                target.Receive(strike.Hit, part);
                hit = true;
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
            bool Crossed(Vector2 center, float radius)
            {
                float top = center.y + radius;
                if (previousFoot < top || currentFoot > top)
                    return false;

                float t = (previousFoot - top) / (previousFoot - currentFoot);
                float crossing = Mathf.Lerp(previous.x, current.x, t);
                return Mathf.Abs(crossing - center.x) <= attackerRadius + radius;
            }

            int count = targets.Count;
            for (int i = 0; i < count; i++)
            {
                DamageReceiver target = targets[i];
                if (target.Health.Alive && target.TryFindPart(Crossed, out Hurtbox part))
                    target.Receive(new DamageHit(DamageSource.Crush, damage, Vector2.down * 3f), part);
            }
        }
    }
}
