using System;
using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal enum BossKind
    {
        None,
        StagBeetle,
        MireLich
    }

    // The sealed hall every boss fights in, in world units.
    internal readonly struct BossArena
    {
        // The floor's walking surface.
        internal float FloorY { get; }
        // Where the lowest terrace step meets the floor, on each side: the end of the boss lane.
        internal float TerraceFace { get; }
        // The underside of the ceiling.
        internal float CeilingY { get; }

        internal BossArena(float floorY, float terraceFace, float ceilingY)
        {
            FloorY = floorY;
            TerraceFace = terraceFace;
            CeilingY = ceilingY;
        }

        internal static BossArena Hall => new(BossArenaLayout.FloorY, BossArenaLayout.TerraceFace, BossArenaLayout.CeilingY);
    }

    // Something a boss did this step that the view shows: dust, a wall hit, a landing, a roar.
    internal enum BossCueKind
    {
        Dust,
        WallHit,
        Land,
        Roar,
        Clank,
        Thud,
        WingBeat,
        Shatter
    }

    internal readonly struct BossCue
    {
        internal BossCueKind Kind { get; }
        internal Vector2 Position { get; }
        internal int Direction { get; }

        internal BossCue(BossCueKind kind, Vector2 position, int direction = 0)
        {
            Kind = kind;
            Position = position;
            Direction = direction;
        }
    }

    // A box that hurts the player while it's live.
    internal readonly struct BossHitbox
    {
        internal Rect Area { get; }
        internal Vector2 Knockback { get; }

        internal BossHitbox(Rect area, Vector2 knockback)
        {
            Area = area;
            Knockback = knockback;
        }
    }

    // A boss's logic, free of rendering so it runs headless. Each fixed step the subclass poses its parts
    // (hurtboxes), switches hitboxes on, and says whether touching its body hurts. The base resolves those
    // against the player, tracks the three phases, and absorbs hits during the intro, roars and death.
    internal abstract class Boss
    {
        internal enum Mode
        {
            Intro,
            Fighting,
            Dying,
            Dead
        }

        protected const float StepSeconds = 1f / 60f;
        // Telegraphs last at least this long before a hitbox goes live.
        internal const int MinimumWarningSteps = 24;
        private const int PogoWindowSteps = 90;
        // Hurtboxes reach a little past the art (3 px), so a hit that looks like it lands does. Contact and
        // body hitboxes stay a little inside it, so brushing the edge of the art doesn't hurt.
        internal const float HurtPadding = 3f / 16f;
        internal const float ContactShrink = 0.85f;

        private readonly List<Hurtbox> parts = new();
        private readonly List<Hurtbox> body = new();
        private readonly List<BossHitbox> hitboxes = new();
        private readonly List<BossCue> cues = new();
        private SeededRandom random;
        private int lastPogoStep = -PogoWindowSteps;
        private int lastUpwardStep = -PogoWindowSteps;
        private int steps;

        protected readonly BossArena Arena;

        internal DamageReceiver Damage { get; }
        internal HealthPool Health => Damage.Health;
        internal abstract string Name { get; }
        // Move names for the dev panel, in the order ForceMove takes them.
        internal abstract IReadOnlyList<string> MoveNames { get; }
        // The ground point under the body's center, or that point carried into the air.
        internal Vector2 Position { get; private protected set; }
        internal int Facing { get; private protected set; } = -1;
        internal int Phase { get; private set; } = 1;
        internal Mode State { get; private set; } = Mode.Intro;
        // Where strikes land: the art's circles, padded.
        internal IReadOnlyList<Hurtbox> Parts => parts;
        // Where touching the boss hurts: the art's circles, shrunk.
        internal IReadOnlyList<Hurtbox> Body => body;
        internal IReadOnlyList<BossHitbox> Hitboxes => hitboxes;
        // Cues pile up until the view (or a headless caller) takes them, so a clank from the player's strike
        // before this step isn't lost.
        internal IReadOnlyList<BossCue> Cues => cues;
        internal bool ContactHurts { get; private set; }
        internal bool Frozen { get; set; }
        internal float IntroProgress { get; private protected set; }
        internal bool DeathFinished => State == Mode.Dead;
        // Set by the subclass while it can't be hurt (a roar between phases).
        private protected bool Invulnerable { get; set; }
        internal float ShakeAmount { get; private set; }
        internal int ShakeSteps { get; private set; }
        // True once when two downward strikes land within 1.5 s of each other.
        private protected bool PogoSpam { get; set; }
        // True for 0.75 s after an upward strike lands: someone below is hitting it.
        private protected bool StruckFromBelow => steps - lastUpwardStep <= 45;

        protected Boss(BossArena arena, Vector2 home, int hearts, float radius, uint seed)
        {
            Arena = arena;
            Position = home;
            random = new SeededRandom(seed ^ 0x5BD1E995u);
            Damage = new DamageReceiver(new HealthPool(hearts), radius, () => Position, 9,
                absorb: _ => State != Mode.Fighting || Invulnerable, parts: () => parts);
            Damage.Resolved += OnResolved;
        }

        internal void Step(DamageReceiver player)
        {
            steps++;
            parts.Clear();
            body.Clear();
            hitboxes.Clear();
            ContactHurts = false;
            ShakeSteps = 0;
            if (!Health.Alive && State is Mode.Intro or Mode.Fighting)
            {
                State = Mode.Dying;
                BeginDeath();
            }

            switch (State)
            {
                case Mode.Intro:
                    if (StepIntro())
                        State = Mode.Fighting;
                    break;
                case Mode.Fighting:
                    if (Frozen)
                        Hold();
                    else
                        StepFight(player);
                    break;
                case Mode.Dying:
                    if (StepDeath())
                        State = Mode.Dead;
                    break;
                case Mode.Dead:
                    Hold();
                    break;
            }

            if (State == Mode.Fighting && player != null)
                Resolve(player);
        }

        // The phase health calls for: 2 at two thirds, 3 at one third.
        private protected int PhaseForHealth =>
            Health.Current * 3 <= Health.Maximum ? 3 : Health.Current * 3 <= Health.Maximum * 2 ? 2 : 1;

        private protected void EnterPhase(int phase) => Phase = Mathf.Clamp(phase, 1, 3);

        // Dev: jumps health down to just inside the phase.
        internal void DevSetPhase(int phase)
        {
            int target = phase switch
            {
                2 => Health.Maximum * 2 / 3,
                3 => Health.Maximum / 3,
                _ => Health.Maximum
            };
            if (Health.Current > target)
                Health.TakeDamage(Health.Current - target);
        }

        internal void ClearCues() => cues.Clear();

        internal abstract void ForceMove(int move);

        // Returns true when the intro is over.
        private protected abstract bool StepIntro();
        private protected abstract void StepFight(DamageReceiver player);
        private protected abstract void BeginDeath();
        // Returns true when the death animation has finished.
        private protected abstract bool StepDeath();
        // Keeps the current pose without acting (dev freeze, or lying dead).
        private protected abstract void Hold();

        private protected int NextRandom(int count) => random.Next(count);

        private protected float NextFloat() => random.Next(10000) / 10000f;

        // Picks an index with chance in proportion to its weight, or -1 when every weight is zero.
        private protected int Pick(params float[] weights)
        {
            float total = 0f;
            foreach (float weight in weights)
                total += Mathf.Max(0f, weight);
            if (total <= 0f)
                return -1;
            float roll = NextFloat() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= Mathf.Max(0f, weights[i]);
                if (roll < 0f)
                    return i;
            }
            return weights.Length - 1;
        }

        // A circle fitted to the art, at a point in the boss's own frame: x forward along its facing, y up
        // from Position. It becomes a padded hurtbox and a shrunk contact circle.
        private protected void AddPart(float x, float y, float radius, int multiplier = 1, bool armored = false)
        {
            Vector2 center = Position + new Vector2(x * Facing, y);
            parts.Add(new Hurtbox(center, radius + HurtPadding, multiplier, armored));
            body.Add(new Hurtbox(center, radius * ContactShrink, multiplier, armored));
        }

        // A hitbox spanning x0..x1 forward and y0..y1 up, in the boss's own frame.
        private protected void AddHitbox(float x0, float x1, float y0, float y1, Vector2 knockback)
        {
            float a = Position.x + x0 * Facing;
            float b = Position.x + x1 * Facing;
            hitboxes.Add(new BossHitbox(Rect.MinMaxRect(Mathf.Min(a, b), Position.y + y0, Mathf.Max(a, b), Position.y + y1),
                knockback));
        }

        // A hitbox in world space.
        private protected void AddWorldHitbox(Rect area, Vector2 knockback) => hitboxes.Add(new BossHitbox(area, knockback));

        // The body hurts, as boxes inside its contact circles, with this knockback. A ram passes only the
        // parts at least `fromForward` ahead of the boss's middle.
        private protected void PartsHurt(Vector2 knockback, float fromForward = float.NegativeInfinity)
        {
            foreach (Hurtbox part in body)
            {
                if ((part.Center.x - Position.x) * Facing < fromForward)
                    continue;
                float half = part.Radius * 0.9f;
                hitboxes.Add(new BossHitbox(Rect.MinMaxRect(part.Center.x - half, part.Center.y - half,
                    part.Center.x + half, part.Center.y + half), knockback));
            }
        }

        private protected void SetContact(bool hurts) => ContactHurts = hurts;

        private protected void Cue(BossCueKind kind, Vector2 position, int direction = 0) =>
            cues.Add(new BossCue(kind, position, direction));

        private protected void Shake(float amount, int length)
        {
            ShakeAmount = Mathf.Max(amount, ShakeSteps > 0 ? ShakeAmount : 0f);
            ShakeSteps = Mathf.Max(ShakeSteps, length);
        }

        private void Resolve(DamageReceiver player)
        {
            Vector2 center = player.Position;
            float radius = player.Radius;
            foreach (BossHitbox hitbox in hitboxes)
            {
                Rect area = hitbox.Area;
                float nearestX = Mathf.Clamp(center.x, area.xMin, area.xMax);
                float nearestY = Mathf.Clamp(center.y, area.yMin, area.yMax);
                if ((new Vector2(nearestX, nearestY) - center).sqrMagnitude > radius * radius)
                    continue;
                player.Receive(new DamageHit(DamageSource.Melee, 1, hitbox.Knockback));
                return;
            }

            if (!ContactHurts)
                return;
            foreach (Hurtbox part in body)
            {
                float reach = part.Radius + radius;
                Vector2 offset = center - part.Center;
                if (offset.sqrMagnitude > reach * reach)
                    continue;
                player.Receive(new DamageHit(DamageSource.Contact, 1, new Vector2(offset.x < 0f ? -6f : 6f, 3.5f)));
                return;
            }
        }

        private void OnResolved(DamageHit hit, DamageOutcome outcome)
        {
            if (outcome == DamageOutcome.Absorbed && Damage.LastPart.Armored)
                Cue(BossCueKind.Clank, Damage.LastPart.Center);
            // An upward strike pushes its target up.
            if (hit.Source == DamageSource.Melee && hit.Knockback.y > 3f && outcome != DamageOutcome.Ignored)
                lastUpwardStep = steps;
            // A downward strike pushes its target down.
            if (hit.Source != DamageSource.Melee || hit.Knockback.y > -1f || outcome == DamageOutcome.Ignored)
                return;
            if (steps - lastPogoStep <= PogoWindowSteps)
                PogoSpam = true;
            lastPogoStep = steps;
        }
    }
}
