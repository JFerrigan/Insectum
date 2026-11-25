using System;
using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal sealed partial class MireLich : Boss
    {
        internal enum Act
        {
            Intro, Rise, Hover, Warning, Dive, DiveStuck, AirRecovery,
            Descend, Feed, ProbeApproach, Jab, Buffet, Probe, GroundRecovery,
            LarvaWarning, LarvaDart, Stagger, Swell, Rupture, Fall, Fade
        }

        internal enum Move
        {
            Dive, Spray, Bubbles, Larvae, Feed, Jab, Buffet, Probe, Takeoff
        }

        internal const int Hearts = 40;
        internal const string Title = "Mire Lich";
        private const float AirY = 12.6f;
        private const float DiveSpeed = 18f;
        private const float Lane = 13.6f;
        private static readonly string[] moves =
        {
            "Proboscis Dive", "Blood Spray", "Bog Bubbles", "Larva Call", "Land and Feed",
            "Proboscis Jab", "Wing Buffet", "Subsurface Probe", "Takeoff"
        };

        private readonly BossTerrain terrain;
        private readonly CombatArena combat;
        private readonly Action<int, float> push;
        private readonly float driftSeed;
        private int age;
        private int duration = 75;
        private int tick;
        private int airMoves;
        private int groundHits;
        private Move lastAir = Move.Larvae;
        private int queuedMove = -1;
        private bool groundVisit;
        private bool initialLanding;
        private bool probeVisit;
        private bool probeRequested;
        private float probeX;
        private bool groundMoveDue;
        private bool dartDue;
        private int divePose;
        private bool diveImpact;
        private float lockedX;
        private float deathVelocity;
        private bool bounced;
        private Move pendingArrival;

        internal MireLich(BossArena arena, Vector2 home, uint seed, BossTerrain terrain, CombatArena combat,
            Action<int, float> push = null) : base(arena, home, Hearts, 0.8f, seed)
        {
            this.terrain = terrain;
            this.combat = combat;
            this.push = push;
            driftSeed = NextFloat() * 6.28f;
            Damage.Resolved += OnDamage;
            PoseParts();
        }

        internal override string Name => Title;
        internal override IReadOnlyList<string> MoveNames => moves;
        internal Act CurrentAct { get; private set; }
        internal Move CurrentMove { get; private set; }
        internal float ActProgress => Mathf.Clamp01(age / (float)duration);
        internal int Stage { get; private set; } = 1;
        internal int Bounces => bounced ? 1 : 0;
        internal bool InWarning => CurrentAct is Act.Warning or Act.LarvaWarning;
        internal bool Flash => InWarning && duration - age <= 3;
        internal bool Opening => CurrentAct is Act.DiveStuck or Act.AirRecovery or Act.Descend or Act.Feed
            or Act.ProbeApproach or Act.GroundRecovery;
        internal bool Airborne => State == Mode.Fighting && !groundVisit && CurrentAct != Act.DiveStuck;
        internal Vector2? Marker { get; private set; }
        internal int ArtFrame => (Stage - 1) * MireLichArt.FramesPerStage + (int)Pose();

        internal override void ForceMove(int move)
        {
            if (State == Mode.Fighting && move >= 0 && move < moves.Length)
                queuedMove = move;
        }

        private protected override bool StepIntro()
        {
            tick++;
            IntroProgress = Mathf.Clamp01(++age / (float)duration);
            PoseParts();
            if (age < duration)
                return false;
            Enter(Act.Rise, 1);
            return true;
        }

        private protected override void Hold()
        {
            if (State != Mode.Dead)
                PoseParts();
        }

        private protected override void StepFight(DamageReceiver player)
        {
            tick++;
            age++;
            Vector2 target = player?.Position ?? new Vector2(0f, Arena.FloorY + 0.43f);
            float footing = groundVisit ? Position.y : Arena.FloorY;
            bool burrowed = target.y < footing - 0.28f && target.y >= footing - 3.5f
                && Mathf.Abs(target.x - Position.x) < 4.2f;
            if (burrowed && CurrentMove != Move.Probe && !probeVisit)
            {
                probeRequested = true;
                probeX = target.x;
            }

            CullMinions();
            TrackLarvae(target);
            // The sixth hit cuts short the visit, including a ground warning already underway.
            if (groundVisit && groundHits >= 6 && CurrentMove != Move.Takeoff
                && CurrentAct is Act.Feed or Act.Warning or Act.GroundRecovery or Act.ProbeApproach)
                BeginWarning(Move.Takeoff, 24);

            switch (CurrentAct)
            {
                case Act.Rise:
                    Position = Vector2.MoveTowards(Position, new Vector2(Position.x, AirY), 10f * StepSeconds);
                    if (Position.y >= AirY)
                        EnterHover();
                    break;
                case Act.Hover:
                    Drift(target);
                    if (age >= duration || queuedMove >= 0 || probeRequested)
                        Choose(target);
                    break;
                case Act.Warning:
                    UpdateMarker();
                    if (age >= duration)
                        Execute(target, player);
                    break;
                case Act.Dive:
                    if (diveImpact)
                    {
                        Marker = null;
                        Enter(Act.DiveStuck, 36);
                        break;
                    }
                    StepDive();
                    break;
                case Act.DiveStuck:
                    if (age >= duration)
                        Enter(Act.Rise, 1);
                    break;
                case Act.AirRecovery:
                    StepAirHazards(target);
                    if (age >= duration && globs.Count == 0 && patches.Count == 0 && bubbles.Count == 0)
                        Choose(target);
                    break;
                case Act.Descend:
                    StepDescent(target);
                    break;
                case Act.Feed:
                    if (age >= duration)
                    {
                        Stage = Mathf.Min(Stage + 1, Phase);
                        groundMoveDue = true;
                        Choose(target);
                    }
                    break;
                case Act.ProbeApproach:
                    float x = Mathf.MoveTowards(Position.x, lockedX, 5f * StepSeconds);
                    Position = new Vector2(x, Surface(x, Position.y + 0.25f).y);
                    if (x == lockedX)
                        BeginWarning(Move.Probe, 42);
                    break;
                case Act.Jab:
                    if (age >= duration)
                        Enter(Act.GroundRecovery, 48);
                    break;
                case Act.Buffet:
                    Enter(Act.GroundRecovery, 36);
                    break;
                case Act.Probe:
                    if (age >= duration)
                        Enter(Act.GroundRecovery, 48);
                    break;
                case Act.GroundRecovery:
                    if (age >= duration)
                        Choose(target);
                    break;
                case Act.LarvaWarning:
                    if (age >= duration)
                        Enter(Act.LarvaDart, 26);
                    break;
                case Act.LarvaDart:
                    if (age >= duration)
                    {
                        FinishDart();
                        Choose(target);
                    }
                    break;
                case Act.Stagger:
                    if (age >= duration)
                    {
                        Invulnerable = false;
                        Choose(target);
                    }
                    break;
            }

            PoseParts();
            switch (CurrentAct)
            {
                case Act.Jab:
                    AddHitbox(1.125f, 2.625f, 0.29f, 0.83f, new Vector2(Facing * 5f, 2.5f));
                    break;
                case Act.Probe:
                    AddWorldHitbox(Rect.MinMaxRect(lockedX - 0.9f, Position.y - 3.4f, lockedX + 0.9f, Position.y + 0.2f),
                        new Vector2(target.x < lockedX ? -5f : 5f, 1.5f));
                    break;
                case Act.AirRecovery:
                    PoseAirHazards();
                    break;
                case Act.LarvaDart:
                    StepDart();
                    break;
            }
            SetContact(CurrentAct == Act.Hover);
        }

        private void Choose(Vector2 target)
        {
            Marker = null;
            if (Phase < PhaseForHealth)
            {
                EnterPhase(PhaseForHealth);
                Invulnerable = true;
                Cue(BossCueKind.Thud, Position);
                Shake(0.16f, 26);
                Enter(Act.Stagger, 45);
                return;
            }

            if (queuedMove >= 0)
            {
                Move forced = (Move)queuedMove;
                queuedMove = -1;
                ClearAirHazards();
                StartMove(forced, target, forced: true);
                return;
            }

            if (groundVisit)
            {
                if (groundHits < 6 && groundMoveDue)
                {
                    groundMoveDue = false;
                    StartMove(probeRequested ? Move.Probe : NextRandom(2) == 0 ? Move.Jab : Move.Buffet, target);
                }
                else
                    StartMove(Move.Takeoff, target);
                return;
            }

            if (dartDue)
            {
                dartDue = false;
                if (StartDart(target))
                    return;
            }

            if (probeRequested)
            {
                StartMove(Move.Probe, target);
                return;
            }

            if (airMoves >= (Phase == 2 ? 3 : 2))
            {
                StartMove(Move.Feed, target);
                return;
            }

            int pick = Pick(lastAir != Move.Dive ? 5f : 0f, lastAir != Move.Spray ? 4f : 0f,
                Phase >= 2 && lastAir != Move.Bubbles ? 2f : 0f,
                Phase == 3 && larvae.Count < 3 && lastAir != Move.Larvae ? 2.4f : 0f);
            StartMove((Move)pick, target);
        }

        private void StartMove(Move move, Vector2 target, bool forced = false)
        {
            if (move is Move.Jab or Move.Buffet or Move.Probe)
            {
                if (!groundVisit)
                {
                    probeVisit = move == Move.Probe;
                    initialLanding = true;
                    BeginLanding(target, move);
                    return;
                }

                if (move == Move.Probe)
                {
                    lockedX = Mathf.Clamp(probeRequested ? probeX : target.x, -Lane, Lane);
                    lockedX = Mathf.Clamp(lockedX, Position.x - 4f, Position.x + 4f);
                    probeRequested = false;
                    if (Mathf.Abs(lockedX - Position.x) > 0.05f)
                    {
                        CurrentMove = move;
                        Enter(Act.ProbeApproach, 1);
                        return;
                    }
                    lockedX = Position.x;
                }
                else
                    Face(target.x);
                BeginWarning(move, move == Move.Buffet ? 24 : move == Move.Jab ? 36 : 42);
                return;
            }

            if (move == Move.Feed)
            {
                initialLanding = false;
                BeginLanding(target, move);
                return;
            }

            if (move == Move.Takeoff)
            {
                BeginWarning(move, 24);
                return;
            }

            if (forced && groundVisit)
            {
                queuedMove = (int)move;
                BeginWarning(Move.Takeoff, 24);
                return;
            }

            lastAir = move;
            airMoves++;
            dartDue = true;
            Face(target.x);
            if (move == Move.Dive)
            {
                lockedX = Mathf.Clamp(target.x, -Lane, Lane);
                float dx = Mathf.Abs(lockedX - Position.x);
                divePose = dx > 10f ? 0 : dx > 4.5f ? 1 : 2;
                Vector2 offset = DiveOffset;
                diveImpact = false;
                if (Position.y + offset.y < Arena.FloorY + 0.55f)
                    Position += Vector2.up * (Arena.FloorY + 0.55f - (Position.y + offset.y));
            }
            BeginWarning(move, move == Move.Dive ? 42 : move == Move.Spray ? 36 : 30);
        }

        private void BeginWarning(Move move, int steps)
        {
            CurrentMove = move;
            Enter(Act.Warning, steps);
            UpdateMarker();
        }

        private void Execute(Vector2 target, DamageReceiver player)
        {
            switch (CurrentMove)
            {
                case Move.Dive:
                    Enter(Act.Dive, 1);
                    StepDive();
                    break;
                case Move.Spray:
                    LaunchGlobs(target);
                    Enter(Act.AirRecovery, 36);
                    break;
                case Move.Bubbles:
                    LaunchBubbles(target);
                    Enter(Act.AirRecovery, 36);
                    break;
                case Move.Larvae:
                    SpawnLarvae();
                    Enter(Act.AirRecovery, 36);
                    break;
                case Move.Feed:
                    Enter(Act.Descend, 1);
                    break;
                case Move.Jab:
                    Enter(Act.Jab, 8);
                    break;
                case Move.Buffet:
                    float front = (target.x - Position.x) * Facing;
                    if (player != null && front is > 0.6f and < 5.25f && target.y - Position.y is > -0.5f and < 2.25f)
                        push?.Invoke(Facing, 3f * Mathf.Clamp01((5.25f - front) / 3f));
                    Cue(BossCueKind.WingBeat, Position + new Vector2(Facing * 1.5f, 1f), Facing);
                    Enter(Act.Buffet, 1);
                    break;
                case Move.Probe:
                    Marker = null;
                    Enter(Act.Probe, 10);
                    Shake(0.18f, 10);
                    break;
                case Move.Takeoff:
                    groundVisit = false;
                    probeVisit = false;
                    airMoves = 0;
                    groundMoveDue = false;
                    Enter(Act.Rise, 1);
                    break;
            }
        }

        private void BeginLanding(Vector2 target, Move arrival)
        {
            float preferredX = probeVisit && probeRequested ? probeX : target.x;
            float desiredY = probeVisit || target.y < 4f ? Arena.FloorY : target.y - 0.55f;
            lockedX = BestLandingX(preferredX, desiredY);
            if (arrival == Move.Probe)
                probeRequested = false;
            pendingArrival = arrival;
            BeginWarning(Move.Feed, 36);
        }

        private void StepDescent(Vector2 target)
        {
            float x = Mathf.MoveTowards(Position.x, lockedX, 12f * StepSeconds);
            if (Mathf.Abs(x - lockedX) > 0.001f)
            {
                Position = new Vector2(x, Position.y);
                return;
            }

            Vector2 surface = Surface(lockedX, Position.y + 0.015f);
            Marker = surface;
            Position = Vector2.MoveTowards(Position, surface, 15f * StepSeconds);
            if (Position != surface)
                return;

            Marker = null;
            groundVisit = true;
            groundHits = 0;
            Face(target.x);
            Cue(BossCueKind.Land, Position);
            if (initialLanding)
            {
                initialLanding = false;
                groundMoveDue = false;
                if (pendingArrival == Move.Probe)
                    BeginWarning(Move.Probe, 42);
                else
                    StartMove(pendingArrival, target);
            }
            else
                Enter(Act.Feed, 150);
        }

        private float BestLandingX(float preferred, float desiredY)
        {
            float x = Mathf.Clamp(preferred, -Lane, Lane);
            float fromY = Mathf.Max(Position.y, Arena.FloorY + 0.02f);
            float best = x;
            float score = float.MaxValue;
            for (int i = -28; i <= 28; i++)
            {
                float candidate = Mathf.Clamp(x + i * 0.5f, -Lane, Lane);
                float top = Surface(candidate, fromY).y;
                float cost = Mathf.Abs(candidate - x) + 4f * Mathf.Abs(top - desiredY);
                if (cost < score)
                {
                    best = candidate;
                    score = cost;
                }
            }
            return best;
        }

        private Vector2 DiveOffset => new(MireLichArt.DiveTips[divePose].x * Facing, MireLichArt.DiveTips[divePose].y);

        private void StepDive()
        {
            Vector2 start = Position + DiveOffset;
            float y = Surface(lockedX, start.y + 0.02f).y;
            Vector2 end = new(lockedX, Mathf.Min(start.y - 0.01f, y - 0.04f));
            Vector2 next = Vector2.MoveTowards(start, end, DiveSpeed * StepSeconds);
            diveImpact = terrain.Sweep(start, next, out Vector2 impact);
            if (diveImpact)
                next = impact;
            float knock = Mathf.Abs(next.x - start.x) > 0.01f ? Facing * 5f : 0f;
            Vector2 basePoint = MireLichArt.DiveBases[divePose];
            Vector2 shaft = Position + new Vector2(basePoint.x * Facing, basePoint.y);
            Vector2 travel = next - start;
            int slices = Mathf.CeilToInt((start - shaft).magnitude / 0.24f);
            for (int i = 0; i <= slices; i++)
            {
                Vector2 point = Vector2.Lerp(shaft, start, i / (float)slices);
                AddSweep(point, point + travel, 0.16f, new Vector2(knock, 4f));
            }
            Position = next - DiveOffset;
            if (diveImpact || Position.y < -2f)
            {
                diveImpact = true;
                Cue(BossCueKind.Land, next);
                Shake(0.25f, 13);
            }
        }

        private void UpdateMarker()
        {
            if (CurrentMove == Move.Dive)
            {
                Vector2 origin = Position + DiveOffset;
                Vector2 end = Surface(lockedX, origin.y + 0.02f) + Vector2.down * 0.04f;
                Marker = terrain.Sweep(origin, end, out Vector2 impact) ? impact : end;
            }
            else if (CurrentMove == Move.Feed)
                Marker = Surface(lockedX, Position.y + 0.015f);
            else if (CurrentMove == Move.Probe)
            {
                Position = Surface(lockedX, Position.y + 0.12f);
                Marker = Position;
            }
            else
                Marker = null;
        }

        internal Vector2 Surface(float x, float top) =>
            terrain.Sweep(new Vector2(x, top), new Vector2(x, -7f), out Vector2 hit)
                ? hit : new Vector2(x, Mathf.Min(Arena.FloorY, top));

        internal bool SweepTerrain(Vector2 from, Vector2 to, out Vector2 hit) => terrain.Sweep(from, to, out hit);

        private void Face(float x) => Facing = x < Position.x ? -1 : 1;

        private void Drift(Vector2 target)
        {
            float t = tick * StepSeconds;
            float x = Mathf.Clamp(target.x * 0.68f + 5f * Mathf.Sin(t * 0.92f + driftSeed), -Lane, Lane);
            float y = AirY + 0.9f * Mathf.Sin(t * 1.84f + driftSeed);
            Position = Vector2.MoveTowards(Position, new Vector2(x, y), 5f * StepSeconds);
            Face(target.x);
        }

        private void EnterHover() => Enter(Act.Hover, (Phase == 3 ? 78 : 96) + NextRandom(25));

        private void Enter(Act act, int steps)
        {
            CurrentAct = act;
            duration = steps;
            age = 0;
        }

        private void PoseParts()
        {
            MireLichArt.Part[] parts = MireLichArt.Parts[ArtFrame];
            if (parts == null)
                return;
            foreach (MireLichArt.Part part in parts)
                AddPart(part.X, part.Y, part.Radius, part.Multiplier);
        }

        private MireLichPose Pose()
        {
            MireLichPose hover = MireLichPose.Hover0 + tick / 3 % 4;
            return CurrentAct switch
            {
                Act.Intro or Act.Descend => MireLichPose.Land,
                Act.Warning => CurrentMove switch
                {
                    Move.Dive => MireLichPose.DiveAim0 + divePose,
                    Move.Spray => MireLichPose.Spray,
                    Move.Bubbles => MireLichPose.Bubble,
                    Move.Larvae => MireLichPose.Call,
                    Move.Feed => MireLichPose.Land,
                    Move.Jab => MireLichPose.JabWind,
                    Move.Buffet => MireLichPose.Buffet,
                    Move.Probe => MireLichPose.ProbeWind,
                    _ => MireLichPose.Takeoff
                },
                Act.Dive or Act.DiveStuck => MireLichPose.Dive0 + divePose,
                Act.Feed => MireLichPose.Feed0 + tick / 9 % 3,
                Act.Jab => MireLichPose.Jab,
                Act.Buffet => MireLichPose.Buffet,
                Act.Probe => MireLichPose.Probe,
                Act.ProbeApproach => MireLichPose.ProbeWind,
                Act.GroundRecovery => CurrentMove == Move.Probe ? MireLichPose.Probe : MireLichPose.JabWind,
                Act.Stagger => MireLichPose.Stagger,
                Act.Swell => MireLichPose.Swell0 + Mathf.Min(3, age * 4 / duration),
                Act.Rupture => MireLichPose.Rupture0 + Mathf.Min(2, age * 3 / duration),
                Act.Fall or Act.Fade => MireLichPose.Corpse,
                _ => hover
            };
        }

        private void OnDamage(DamageHit hit, DamageOutcome outcome)
        {
            if (outcome != DamageOutcome.Applied)
                return;
            if (groundVisit)
                groundHits++;
            if (hit.Source == DamageSource.Melee && hit.Knockback.y > 3f && CurrentMove != Move.Probe && !probeVisit)
            {
                probeRequested = true;
                probeX = Position.x;
            }
        }

        private protected override void BeginDeath()
        {
            ClearAirHazards();
            foreach (Larva larva in larvae)
                combat.RemoveTarget(larva.Body);
            larvae.Clear();
            dart = null;
            Marker = null;
            Enter(Act.Swell, 30);
            age = -1;
        }

        private protected override bool StepDeath()
        {
            if (++age >= duration)
            {
                if (CurrentAct == Act.Swell)
                {
                    Enter(Act.Rupture, 9);
                    Cue(BossCueKind.Shatter, Position + new Vector2(-Facing * (0.95f + Stage * 0.45f), 1.45f));
                    Shake(0.55f, 28);
                }
                else if (CurrentAct == Act.Rupture)
                    Enter(Act.Fall, int.MaxValue);
                else if (CurrentAct == Act.Fade)
                    return true;
            }

            if (CurrentAct != Act.Fall)
                return false;

            deathVelocity -= 31f * StepSeconds;
            Vector2 next = Position + Vector2.up * (deathVelocity * StepSeconds);
            if (deathVelocity < 0f && terrain.Sweep(Position + Vector2.up * 0.02f, next, out Vector2 impact))
            {
                Position = impact;
                if (bounced)
                    Enter(Act.Fade, 28);
                else
                {
                    bounced = true;
                    deathVelocity = 2.7f;
                    Cue(BossCueKind.Thud, Position);
                }
            }
            else
                Position = next;
            return false;
        }

        private void AddSweep(Vector2 from, Vector2 to, float radius, Vector2 knockback) =>
            AddWorldHitbox(Rect.MinMaxRect(Mathf.Min(from.x, to.x) - radius, Mathf.Min(from.y, to.y) - radius,
                Mathf.Max(from.x, to.x) + radius, Mathf.Max(from.y, to.y) + radius), knockback);
    }
}
