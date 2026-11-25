using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    // Forest's boss: a long, low stag beetle that mostly fights on the ground. It charges along the floor and
    // sticks its mandibles in the terrace at the end, snaps at anyone close in front, rears up to snap at
    // anyone above it, and now and then flies, either to slam down from above or to dash across the arena.
    internal sealed class StagBeetle : Boss
    {
        internal enum Act
        {
            IntroRest, IntroRoar,
            Idle, Walk, Turn, Roar,
            ChargeWindup, ChargeRun, ChargeSkid, Stuck, Wrench, ChargeRecover,
            SnapWindup, Snap, SnapRecover,
            RearWindup, RearSnap, RearRecover,
            WingOpen, LiftOff, HoverTrack, DiveLock, DiveDrop, Land,
            GoreRise, GoreAim, GoreDash, SoftDrop, SoftLand,
            DeathFall, DeathKneel, DeathPause, DeathDrop, DeathFade
        }

        internal enum Glow
        {
            None,
            Amber,
            Flash
        }

        internal enum Pose
        {
            Stand,
            Crouch,
            Rear,
            Air,
            Dive,
            Dash,
            Down
        }

        // A chunk of bark shaken loose on a bombing run, falling to the floor.
        internal readonly struct Shard
        {
            internal Vector2 Position { get; }
            internal Vector2 Velocity { get; }

            internal Shard(Vector2 position, Vector2 velocity)
            {
                Position = position;
                Velocity = velocity;
            }
        }

        internal readonly struct Shockwave
        {
            internal float X { get; }
            internal int Direction { get; }
            internal float Travelled { get; }
            internal float Range { get; }

            internal Shockwave(float x, int direction, float travelled, float range)
            {
                X = x;
                Direction = direction;
                Travelled = travelled;
                Range = range;
            }
        }

        internal const int Hearts = 30;
        private const float Radius = 1.4f;
        private const float WalkSpeed = 2.5f;
        private const float ChargeSpeed = 11f;
        private const float DiveSpeed = 22f;
        private const float DashSpeed = 18f;
        private const float WaveSpeed = 12f;
        // How far the mandible tips reach ahead of Position.
        private const float FrontReach = 2.9f;
        private const float HoverHeight = 10.5f;
        private const float GoreSide = 15.5f;
        private const int AirCooldownSteps = 600;
        private const int IntroRestSteps = 40;
        private const int IntroRoarSteps = 80;
        private const int ChainedWarningSteps = 30;
        // A dash aimed at a player on the floor flies this high above it and drops bark instead.
        private const float BombingHeight = 5.2f;
        private const float ShardSpacing = 3.4f;
        private const float ShardGravity = 30f;
        private const float ShardHalfSize = 0.25f;

        private static readonly string[] Moves = { "CHARGE", "SNAP", "REAR SNAP", "DIVE SLAM", "AIR GORE" };

        private readonly List<Shockwave> shockwaves = new();
        private readonly List<Shard> shards = new();
        private readonly float maxX;
        private int actLength;
        private int chargeCooldown;
        private int rearCooldown;
        private int stepsSinceAir = 240;
        private int forcedMove = -1;
        private bool chained;
        private bool chainUsed;
        private bool pogoPunish;
        private float chargeLimit;
        private int gorePasses;
        private int goreSide;
        private Vector2 riseFrom;
        private float aimFromY;
        private bool bombing;
        private float sinceShard;

        internal StagBeetle(BossArena arena, Vector2 home, uint seed) : base(arena, home, Hearts, Radius, seed)
        {
            maxX = arena.TerraceFace - FrontReach;
            Facing = -1;
            Enter(Act.IntroRest, IntroRestSteps);
            PoseParts();
        }

        internal override string Name => "Old Oakhorn";
        internal override IReadOnlyList<string> MoveNames => Moves;

        internal Act CurrentAct { get; private set; }
        internal int ActSteps { get; private set; }
        internal int ActLength => actLength;
        internal float ActProgress => actLength > 0 ? Mathf.Clamp01(ActSteps / (float)actLength) : 1f;
        internal IReadOnlyList<Shockwave> Shockwaves => shockwaves;
        internal IReadOnlyList<Shard> Shards => shards;
        // True when the current dash passes high and drops bark rather than ramming at the player's height.
        internal bool Bombing => bombing;
        internal bool Airborne => Position.y > Arena.FloorY + 0.05f;
        internal int GorePasses => gorePasses;
        // Where the dive will land and how close it is, while the beetle hovers; null otherwise.
        internal float? DiveMarker => CurrentAct switch
        {
            Act.HoverTrack => ActProgress * 0.8f,
            Act.DiveLock => 1f,
            _ => null
        };
        // How far the charge streak has drawn along the floor during the wind-up.
        internal float? ChargeLine => CurrentAct == Act.ChargeWindup ? ActProgress : null;
        internal float ChargeTarget => chargeLimit;

        internal Glow CurrentGlow => CurrentAct switch
        {
            Act.DiveLock => Glow.Flash,
            Act.ChargeWindup or Act.SnapWindup or Act.RearWindup or Act.GoreAim =>
                actLength - ActSteps <= 3 ? Glow.Flash : Glow.Amber,
            _ => Glow.None
        };

        // An opening: touching the body is safe, and it's slow to act.
        internal bool Opening => CurrentAct is Act.Stuck or Act.Wrench or Act.ChargeRecover or Act.SnapRecover
            or Act.RearRecover or Act.Land or Act.SoftLand;

        internal Pose CurrentPose => CurrentAct switch
        {
            Act.ChargeRun or Act.ChargeSkid or Act.Stuck or Act.Wrench => Pose.Crouch,
            Act.ChargeWindup => ActProgress > 0.66f ? Pose.Crouch : Pose.Stand,
            Act.RearSnap or Act.Roar => Pose.Rear,
            Act.IntroRoar => ActProgress is > 0.2f and < 0.8f ? Pose.Rear : Pose.Stand,
            Act.RearRecover => ActProgress < 0.5f ? Pose.Rear : Pose.Stand,
            Act.WingOpen => ActProgress >= 0.5f ? Pose.Air : Pose.Stand,
            Act.LiftOff or Act.HoverTrack or Act.DiveLock or Act.GoreRise or Act.SoftDrop => Pose.Air,
            Act.DiveDrop => Pose.Dive,
            Act.GoreAim or Act.GoreDash => Pose.Dash,
            Act.DeathFall or Act.DeathKneel or Act.DeathPause or Act.DeathDrop or Act.DeathFade => Pose.Down,
            _ => Pose.Stand
        };

        internal override void ForceMove(int move)
        {
            forcedMove = Mathf.Clamp(move, 0, Moves.Length - 1);
            if (CurrentAct is Act.Idle or Act.Walk)
                ActSteps = actLength;
        }

        private void Enter(Act act, int length)
        {
            CurrentAct = act;
            ActSteps = 0;
            actLength = length;
        }

        private bool Done => ActSteps >= actLength;

        private void Move(float x, float y) => Position = new Vector2(x, y);

        private void MoveX(float x) => Position = new Vector2(Mathf.Clamp(x, -maxX, maxX), Position.y);

        private float Floor => Arena.FloorY;

        // ---------- intro ----------
        private protected override bool StepIntro()
        {
            ActSteps++;
            if (CurrentAct == Act.IntroRest && Done)
                Enter(Act.IntroRoar, IntroRoarSteps);
            else if (CurrentAct == Act.IntroRoar && ActSteps == 24)
            {
                Cue(BossCueKind.Roar, Position + Vector2.up * 2f, Facing);
                Shake(0.18f, 30);
            }
            IntroProgress = (CurrentAct == Act.IntroRest ? ActSteps : IntroRestSteps + ActSteps)
                / (float)(IntroRestSteps + IntroRoarSteps);
            PoseParts();
            if (CurrentAct != Act.IntroRoar || !Done)
                return false;
            IntroProgress = 1f;
            Think(null);
            return true;
        }

        // ---------- fighting ----------
        private protected override void StepFight(DamageReceiver player)
        {
            ActSteps++;
            chargeCooldown--;
            rearCooldown--;
            if (Position.y <= Floor + 0.05f)
                stepsSinceAir++;
            if (PogoSpam)
            {
                PogoSpam = false;
                pogoPunish = true;
            }
            StepShockwaves();
            StepShards();

            Vector2 target = player.Position;
            float dx = target.x - Position.x;
            switch (CurrentAct)
            {
                case Act.Idle:
                case Act.Walk:
                    if (React(target))
                        break;
                    if (CurrentAct == Act.Walk && Mathf.Abs(dx) > 3.2f && Mathf.Sign(dx) == Facing)
                        MoveX(Position.x + Facing * WalkSpeed * StepSeconds);
                    if (Done)
                        Choose(target);
                    break;
                case Act.Turn:
                    if (Done)
                    {
                        Facing = -Facing;
                        Choose(target);
                    }
                    break;
                case Act.Roar:
                    if (ActSteps == 12)
                    {
                        Cue(BossCueKind.Roar, Position + Vector2.up * 2.6f, Facing);
                        Shake(0.2f, 30);
                    }
                    if (Done)
                        Think(target);
                    break;

                case Act.ChargeWindup:
                    if (ActSteps % 14 == 1 && ActProgress < 0.6f)
                        Cue(BossCueKind.Dust, Position + new Vector2(Facing * 1.6f, 0f), -Facing);
                    if (Done)
                    {
                        float limit = chained ? Position.x + Facing * 8f : Facing * maxX;
                        chargeLimit = Mathf.Clamp(limit, -maxX, maxX);
                        Enter(Act.ChargeRun, int.MaxValue);
                    }
                    break;
                case Act.ChargeRun:
                    StepCharge();
                    break;
                case Act.ChargeSkid:
                    MoveX(Position.x + Facing * ChargeSpeed * StepSeconds * (1f - ActProgress));
                    if (ActSteps % 4 == 0)
                        Cue(BossCueKind.Dust, Position + new Vector2(-Facing * 1.2f, 0f), -Facing);
                    if (Done)
                        Enter(Act.ChargeRecover, 36);
                    break;
                case Act.Stuck:
                    if (ActSteps % 10 == 0)
                        Cue(BossCueKind.Dust, Position + new Vector2(Facing * FrontReach, 0.6f), -Facing);
                    if (Done)
                        Enter(Act.Wrench, 21);
                    break;
                case Act.Wrench:
                    MoveX(Position.x - Facing * 0.8f / 21f);
                    if (Done)
                    {
                        Facing = -Facing;
                        if (Phase >= 2 && !chainUsed && Mathf.Abs(dx) < 6f)
                        {
                            chained = chainUsed = true;
                            Enter(Act.ChargeWindup, ChainedWarningSteps);
                        }
                        else
                            Think(target);
                    }
                    break;
                case Act.ChargeRecover:
                    if (Done)
                        Think(target);
                    break;

                case Act.SnapWindup:
                    if (Done)
                        Enter(Act.Snap, 8);
                    break;
                case Act.Snap:
                    if (Done)
                        Enter(Act.SnapRecover, 42);
                    break;
                case Act.SnapRecover:
                    if (Done)
                        Think(target);
                    break;

                case Act.RearWindup:
                    if (Done)
                    {
                        Enter(Act.RearSnap, 12);
                        Cue(BossCueKind.Dust, Position + new Vector2(-Facing * 1.4f, 0f), -Facing);
                    }
                    break;
                case Act.RearSnap:
                    if (Done)
                        Enter(Act.RearRecover, 48);
                    break;
                case Act.RearRecover:
                    if (ActSteps == actLength / 2)
                    {
                        Cue(BossCueKind.Dust, Position + new Vector2(Facing * 1.4f, 0f), Facing);
                        Shake(0.08f, 6);
                    }
                    if (Done)
                    {
                        rearCooldown = 90;
                        Think(target);
                    }
                    break;

                case Act.WingOpen:
                    if (ActSteps % 9 == 0)
                        Cue(BossCueKind.WingBeat, Position, Facing);
                    if (Done)
                        BeginFlight(target);
                    break;
                case Act.LiftOff:
                    float rise = 1f - (1f - ActProgress) * (1f - ActProgress);
                    Move(Mathf.MoveTowards(Position.x, ClampLane(target.x), 3f * StepSeconds), Floor + HoverHeight * rise);
                    if (Done)
                        Enter(Act.HoverTrack, Phase == 3 ? 36 : 54);
                    break;
                case Act.HoverTrack:
                    MoveX(Mathf.MoveTowards(Position.x, ClampLane(target.x), 8f * StepSeconds));
                    if (Done)
                        Enter(Act.DiveLock, 12);
                    break;
                case Act.DiveLock:
                    if (Done)
                        Enter(Act.DiveDrop, int.MaxValue);
                    break;
                case Act.DiveDrop:
                    Move(Position.x, Position.y - DiveSpeed * StepSeconds);
                    if (Position.y <= Floor)
                        LandDive();
                    break;
                case Act.Land:
                    if (Done)
                        Think(target);
                    break;

                case Act.GoreRise:
                    float lift = 1f - (1f - ActProgress) * (1f - ActProgress);
                    var goal = new Vector2(goreSide * GoreSide, Floor + BombingHeight - 1f);
                    Position = Vector2.LerpUnclamped(riseFrom, goal, lift);
                    if (Done)
                        BeginAim(target);
                    break;
                case Act.GoreAim:
                    float settle = Mathf.Clamp01(ActSteps / 15f);
                    float aimY = GoreLineHeight - 1f;
                    Move(Position.x, Mathf.Lerp(aimFromY, aimY, settle));
                    if (Done)
                        Enter(Act.GoreDash, int.MaxValue);
                    break;
                case Act.GoreDash:
                    Move(Position.x + Facing * DashSpeed * StepSeconds, Position.y);
                    sinceShard += DashSpeed * StepSeconds;
                    if (bombing && sinceShard >= ShardSpacing && Mathf.Abs(Position.x) < maxX)
                    {
                        sinceShard = 0f;
                        shards.Add(new Shard(Position + new Vector2(-Facing * 0.6f, 0.5f), new Vector2(Facing * 1.5f, 0f)));
                    }
                    if (Position.x * Facing >= GoreSide)
                    {
                        Move(Facing * GoreSide, Position.y);
                        gorePasses++;
                        if (gorePasses < (Phase == 3 ? 3 : 2))
                        {
                            Facing = -Facing;
                            BeginAim(target);
                        }
                        else
                            Enter(Act.SoftDrop, int.MaxValue);
                    }
                    break;
                case Act.SoftDrop:
                    Move(Mathf.MoveTowards(Position.x, Mathf.Clamp(Position.x, -maxX, maxX), 6f * StepSeconds),
                        Mathf.Max(Floor, Position.y - 9f * StepSeconds));
                    if (Position.y <= Floor && Mathf.Abs(Position.x) <= maxX)
                    {
                        Enter(Act.SoftLand, 60);
                        Cue(BossCueKind.Land, Position, 0);
                        Shake(0.12f, 8);
                        stepsSinceAir = 0;
                    }
                    break;
                case Act.SoftLand:
                    if (Done)
                        Think(target);
                    break;
            }

            // Hits between steps land before the next one, so the roar's guard is set for the steps after.
            Invulnerable = CurrentAct == Act.Roar;
            PoseParts();
            ApplyHitboxes(target);
        }

        private float GoreLineHeight { get; set; }

        // A player on the floor gets a bombing run; one up on the platforms gets rammed at their height.
        private float GoreHeight(float playerY, out bool high)
        {
            high = playerY < Floor + 2.6f;
            return high ? Floor + BombingHeight : Mathf.Clamp(playerY, Floor + 3f, Floor + 9.5f);
        }

        private float ClampLane(float x) => Mathf.Clamp(x, -maxX, maxX);

        // Jumps the queue when the player is above it, or has pogoed off it twice in quick succession.
        private bool React(Vector2 target)
        {
            if (!pogoPunish && (rearCooldown > 0 || !Above(target)))
                return false;
            Enter(Act.RearWindup, pogoPunish ? MinimumWarningSteps : 30);
            pogoPunish = false;
            return true;
        }

        private bool Above(Vector2 target)
        {
            float forward = (target.x - Position.x) * Facing;
            float height = target.y - Position.y;
            return forward > -2.2f && forward < 2.8f && height > 2.6f && height < 6.5f;
        }

        // A short pause, walking toward the player when far, before the next move.
        private void Think(Vector2? target)
        {
            int length = Phase switch
            {
                3 => 14 + NextRandom(12),
                2 => 20 + NextRandom(14),
                _ => 24 + NextRandom(18)
            };
            bool far = target.HasValue && Mathf.Abs(target.Value.x - Position.x) > 3.2f;
            Enter(far ? Act.Walk : Act.Idle, length);
        }

        private void Choose(Vector2 target)
        {
            if (PhaseForHealth > Phase)
            {
                EnterPhase(PhaseForHealth);
                Enter(Act.Roar, 60);
                return;
            }

            float dx = target.x - Position.x;
            int toward = dx < 0f ? -1 : 1;
            if (toward != Facing && Mathf.Abs(dx) > 1f)
            {
                Enter(Act.Turn, 10);
                return;
            }

            chainUsed = false;
            int move = forcedMove;
            forcedMove = -1;
            if (move < 0)
            {
                float forward = dx * Facing;
                bool close = forward > 0.5f && forward < FrontReach + 0.6f
                    && Mathf.Abs(target.y - (Position.y + 1f)) < 1.8f;
                float room = maxX - Position.x * Facing;
                bool airReady = stepsSinceAir >= AirCooldownSteps;
                float walk = Mathf.Abs(dx) > 3.2f ? (Phase == 1 ? 3f : Phase == 2 ? 2f : 1.5f) : 1f;
                float snap = close ? 6f : 0f;
                float charge = chargeCooldown <= 0 && room > 3f ? (Phase == 3 ? 4.5f : 4f) : 0f;
                float dive = airReady ? (Phase == 1 ? 1.2f : 1.8f) : 0f;
                float gore = airReady ? (Phase == 1 ? 0.8f : 1.4f) : 0f;
                // Picks map onto the dev panel's move order: walk, snap, charge, dive, gore.
                move = Pick(walk, snap, charge, dive, gore) switch
                {
                    1 => 1,
                    2 => 0,
                    3 => 3,
                    4 => 4,
                    _ => -1
                };
            }

            switch (move)
            {
                case 0:
                    chained = false;
                    Enter(Act.ChargeWindup, Phase switch { 3 => 36, 2 => 40, _ => 45 });
                    break;
                case 1:
                    Enter(Act.SnapWindup, Phase == 3 ? MinimumWarningSteps : 27);
                    break;
                case 2:
                    Enter(Act.RearWindup, 30);
                    break;
                case 3:
                case 4:
                    gorePasses = move == 4 ? 0 : -1;
                    Enter(Act.WingOpen, 36);
                    break;
                default:
                    Think(target);
                    break;
            }
        }

        private void StepCharge()
        {
            MoveX(Position.x + Facing * ChargeSpeed * StepSeconds);
            if (ActSteps % 5 == 0)
                Cue(BossCueKind.Dust, Position + new Vector2(-Facing * 2.2f, 0f), -Facing);
            if ((Position.x - chargeLimit) * Facing < 0f)
                return;

            Move(chargeLimit, Position.y);
            chargeCooldown = 90;
            if (Mathf.Abs(chargeLimit) >= maxX - 0.01f)
            {
                chained = false;
                Enter(Act.Stuck, Phase switch { 3 => 60, 2 => 78, _ => 96 });
                Cue(BossCueKind.WallHit, Position + new Vector2(Facing * FrontReach, 0.5f), Facing);
                Shake(0.3f, 14);
            }
            else
            {
                chained = false;
                Enter(Act.ChargeSkid, 18);
            }
        }

        private void BeginFlight(Vector2 target)
        {
            if (gorePasses < 0)
            {
                Enter(Act.LiftOff, 48);
                return;
            }
            goreSide = Position.x >= 0f ? 1 : -1;
            Facing = -goreSide;
            riseFrom = Position;
            Enter(Act.GoreRise, 54);
        }

        private void BeginAim(Vector2 target)
        {
            GoreLineHeight = GoreHeight(target.y, out bombing);
            aimFromY = Position.y;
            sinceShard = ShardSpacing * 0.5f;
            Enter(Act.GoreAim, 36);
        }

        private void LandDive()
        {
            Move(Position.x, Floor);
            Cue(BossCueKind.Land, Position, 0);
            Shake(0.4f, 18);
            float range = Phase == 3 ? 10f : 6f;
            shockwaves.Add(new Shockwave(Position.x - 2.2f, -1, 0f, range));
            shockwaves.Add(new Shockwave(Position.x + 2.2f, 1, 0f, range));
            stepsSinceAir = 0;
            Enter(Act.Land, 72);
        }

        private void StepShards()
        {
            for (int i = shards.Count - 1; i >= 0; i--)
            {
                Shard shard = shards[i];
                Vector2 velocity = shard.Velocity + Vector2.down * (ShardGravity * StepSeconds);
                Vector2 position = shard.Position + velocity * StepSeconds;
                if (position.y - ShardHalfSize <= Floor)
                {
                    shards.RemoveAt(i);
                    Cue(BossCueKind.Shatter, new Vector2(position.x, Floor), 0);
                }
                else
                    shards[i] = new Shard(position, velocity);
            }
        }

        private void StepShockwaves()
        {
            for (int i = shockwaves.Count - 1; i >= 0; i--)
            {
                Shockwave wave = shockwaves[i];
                float step = WaveSpeed * StepSeconds;
                var next = new Shockwave(wave.X + wave.Direction * step, wave.Direction, wave.Travelled + step, wave.Range);
                if (next.Travelled > next.Range || Mathf.Abs(next.X) > Arena.TerraceFace)
                    shockwaves.RemoveAt(i);
                else
                    shockwaves[i] = next;
            }
        }

        // ---------- body ----------
        // Circles fitted to each pose's frame by ArtSource/boss_forest_pixels.py (it prints these, and draws
        // them over the art in Builds/BossPreviews/forest_hurtboxes.png): shell, thorax, head, then the
        // armored jaws, then the lifted wing case in poses that fly.
        private void PoseParts()
        {
            int head = CurrentAct == Act.Stuck ? 2 : 1;
            switch (CurrentPose)
            {
                case Pose.Stand:
                    AddPart(-1.94f, 1.5f, 0.72f);
                    AddPart(-0.75f, 1.56f, 0.78f);
                    AddPart(0.44f, 1.62f, 0.69f);
                    AddPart(1.38f, 1.69f, 0.62f, head);
                    AddPart(2.25f, 1.69f, 0.38f, armored: true);
                    AddPart(3.19f, 2.06f, 0.44f, armored: true);
                    break;
                case Pose.Crouch:
                    AddPart(-1.88f, 0.63f, 0.72f);
                    AddPart(-0.69f, 0.59f, 0.78f);
                    AddPart(0.49f, 0.55f, 0.69f);
                    AddPart(1.42f, 0.49f, 0.62f, head);
                    AddPart(2.28f, 0.39f, 0.38f, armored: true);
                    AddPart(3.24f, 0.7f, 0.44f, armored: true);
                    break;
                case Pose.Rear:
                    AddPart(-2.31f, 1.33f, 0.72f);
                    AddPart(-1.41f, 2.11f, 0.78f);
                    AddPart(-0.51f, 2.89f, 0.69f);
                    AddPart(0.15f, 3.57f, 0.62f, head);
                    AddPart(0.73f, 4.22f, 0.38f, armored: true);
                    AddPart(1.08f, 5.17f, 0.44f, armored: true);
                    break;
                case Pose.Air:
                    AddPart(-1.94f, 1.5f, 0.72f);
                    AddPart(-0.75f, 1.56f, 0.78f);
                    AddPart(0.44f, 1.62f, 0.69f);
                    AddPart(1.38f, 1.71f, 0.62f, head);
                    AddPart(2.25f, 1.77f, 0.38f, armored: true);
                    AddPart(3.16f, 2.21f, 0.44f, armored: true);
                    AddPart(-1.03f, 2.95f, 0.62f);
                    AddPart(-1.67f, 3.74f, 0.44f);
                    break;
                case Pose.Dive:
                    AddPart(-1.7f, 1.48f, 0.72f);
                    AddPart(-0.57f, 1.09f, 0.78f);
                    AddPart(0.55f, 0.7f, 0.69f);
                    AddPart(1.4f, 0.36f, 0.62f, head);
                    AddPart(2.19f, 0.01f, 0.38f, armored: true);
                    AddPart(3.19f, 0.04f, 0.44f, armored: true);
                    AddPart(-0.32f, 2.49f, 0.62f);
                    AddPart(-0.61f, 3.45f, 0.44f);
                    break;
                case Pose.Dash:
                    AddPart(-1.94f, 1.12f, 0.72f);
                    AddPart(-0.75f, 1.19f, 0.78f);
                    AddPart(0.44f, 1.25f, 0.69f);
                    AddPart(1.37f, 1.3f, 0.62f, head);
                    AddPart(2.23f, 1.37f, 0.38f, armored: true);
                    AddPart(3.1f, 1.89f, 0.44f, armored: true);
                    AddPart(-1.03f, 2.58f, 0.62f);
                    AddPart(-1.67f, 3.36f, 0.44f);
                    break;
            }
        }

        private void ApplyHitboxes(Vector2 target)
        {
            int away = target.x < Position.x ? -1 : 1;
            switch (CurrentAct)
            {
                case Act.ChargeRun:
                case Act.ChargeSkid:
                    // Only the head and jaws ram; the thorax and shell behind them pass under a jump or a pogo.
                    PartsHurt(new Vector2(Facing * 9f, 4.5f), fromForward: 1f);
                    break;
                case Act.Snap:
                    AddHitbox(1.5f, 3.5f, 0.1f, 2f, new Vector2(Facing * 6f, 4f));
                    SetContact(true);
                    break;
                case Act.RearSnap:
                    AddHitbox(-1f, 2.6f, 1.7f, 5.6f, new Vector2(Facing * 2f, 8f));
                    SetContact(true);
                    break;
                case Act.DiveDrop:
                    PartsHurt(new Vector2(away * 6f, 5f));
                    break;
                case Act.GoreDash:
                    PartsHurt(new Vector2(Facing * 8f, 4f));
                    break;
                default:
                    SetContact(!Opening);
                    break;
            }

            foreach (Shard shard in shards)
                AddWorldHitbox(Rect.MinMaxRect(shard.Position.x - ShardHalfSize, shard.Position.y - ShardHalfSize,
                    shard.Position.x + ShardHalfSize, shard.Position.y + ShardHalfSize), new Vector2(shard.Velocity.x, 3f));
            foreach (Shockwave wave in shockwaves)
                AddWorldHitbox(Rect.MinMaxRect(wave.X - 0.35f, Floor, wave.X + 0.35f, Floor + 0.6f),
                    new Vector2(wave.Direction * 5f, 5f));
        }

        // ---------- death ----------
        private protected override void BeginDeath()
        {
            shockwaves.Clear();
            shards.Clear();
            if (Airborne)
                Enter(Act.DeathFall, int.MaxValue);
            else
                Enter(Act.DeathKneel, 54);
        }

        private protected override bool StepDeath()
        {
            ActSteps++;
            switch (CurrentAct)
            {
                case Act.DeathFall:
                    Move(Mathf.MoveTowards(Position.x, Mathf.Clamp(Position.x, -maxX, maxX), 6f * StepSeconds),
                        Mathf.Max(Floor, Position.y - 14f * StepSeconds));
                    if (Position.y <= Floor)
                    {
                        Cue(BossCueKind.Thud, Position, 0);
                        Shake(0.25f, 10);
                        Enter(Act.DeathKneel, 54);
                    }
                    break;
                case Act.DeathKneel:
                    if (Done)
                        Enter(Act.DeathPause, 24);
                    break;
                case Act.DeathPause:
                    if (Done)
                        Enter(Act.DeathDrop, 30);
                    break;
                case Act.DeathDrop:
                    if (Done)
                    {
                        Cue(BossCueKind.Thud, Position, 0);
                        Shake(0.4f, 20);
                        Enter(Act.DeathFade, 60);
                    }
                    break;
                case Act.DeathFade:
                    return Done;
            }
            return false;
        }

        private protected override void Hold() => PoseParts();
    }
}
