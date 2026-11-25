using System;
using System.Collections.Generic;
using Insectum.Game;
using UnityEngine;

// Runs boss logic headless, no Unity needed: tools/bosscheck.sh
//   - hurtboxes: one hit per swing, armor absorbs but counts, weak points double
//   - every attack follows a warning of at least Boss.MinimumWarningSteps, and nothing hurts during one
//   - openings never hurt on contact, and the boss stays inside the hall
//   - the share of time airborne, per phase
//   - a scripted player that dodges and hits during openings wins, per seed
internal static class BossCheck
{
    private const float StepSeconds = 1f / 60f;
    private static readonly BossArena Arena = BossArena.Hall;
    private static int failures;

    internal static int Run(string[] args)
    {

        int seeds = args.Length > 1 && int.TryParse(args[1], out int parsed) ? Math.Max(1, parsed) : 12;
        if (args.Length < 3 || args[2] != "mire")
        {
            CheckHurtboxes();
            for (uint seed = 1; seed <= seeds; seed++)
                CheckWarnings(seed);
            CheckSlam();
            CheckAirTime(seeds);
            CheckFights(seeds);
        }
        MireLichCheck.Run(seeds, Require);
        Console.WriteLine(failures == 0 ? "Boss check passed." : $"Boss check FAILED: {failures} problem(s).");
        return failures == 0 ? 0 : 1;
    }

    private static void Require(bool condition, string message)
    {
        if (condition)
            return;
        failures++;
        Console.WriteLine("  FAIL " + message);
    }

    private static void CheckHurtboxes()
    {
        var armor = new Hurtbox(new Vector2(1f, 0f), 0.5f, armored: true);
        var plain = new Hurtbox(new Vector2(1.4f, 0f), 0.5f);
        var weak = new Hurtbox(new Vector2(3f, 0f), 0.5f, 2);
        IReadOnlyList<Hurtbox> parts = new[] { armor, plain, weak };
        var arena = new CombatArena();
        var boss = new DamageReceiver(new HealthPool(10), 1f, () => new Vector2(2f, 0f), 0, parts: () => parts);
        arena.AddTarget(boss);
        Require(arena.Melee(new MeleeStrike(Vector2.zero, Vector2.right, 1.3f, 0.38f, 1, 4f)) && boss.Health.Current == 9,
            "hurtboxes: a swing over armor and a plain part did not land once on the plain part");
        parts = new[] { armor };
        Require(arena.Melee(new MeleeStrike(Vector2.zero, Vector2.right, 1.3f, 0.38f, 1, 4f)) && boss.Health.Current == 9,
            "hurtboxes: armor did not absorb a hit that still counts");
        parts = new[] { weak };
        Require(arena.Melee(new MeleeStrike(new Vector2(2f, 0f), Vector2.right, 1.3f, 0.38f, 1, 4f)) && boss.Health.Current == 7,
            "hurtboxes: a weak point did not double the damage");
        Console.WriteLine("Hurtboxes checked.");
    }

    // ---------- warnings, openings, bounds ----------
    private static readonly HashSet<StagBeetle.Act> Attacks = new()
    {
        StagBeetle.Act.ChargeRun, StagBeetle.Act.Snap, StagBeetle.Act.RearSnap, StagBeetle.Act.DiveDrop, StagBeetle.Act.GoreDash,
        StagBeetle.Act.Slam
    };

    private static readonly HashSet<StagBeetle.Act> Warnings = new()
    {
        StagBeetle.Act.ChargeWindup, StagBeetle.Act.SnapWindup, StagBeetle.Act.RearWindup, StagBeetle.Act.DiveLock,
        StagBeetle.Act.GoreAim, StagBeetle.Act.SlamWindup
    };

    private static void CheckWarnings(uint seed)
    {
        var beetle = new StagBeetle(Arena, new Vector2(BossArenaLayout.BossX, Arena.FloorY), seed);
        var wanderer = new Wanderer(seed);
        StagBeetle.Act previous = beetle.CurrentAct;
        int warningSteps = 0;
        int attacks = 0;
        for (int step = 0; step < 60 * 60 * 6; step++)
        {
            if (step == 60 * 60 * 2)
                beetle.DevSetPhase(2);
            if (step == 60 * 60 * 4)
                beetle.DevSetPhase(3);
            wanderer.Step();
            beetle.Step(wanderer.Body);
            beetle.ClearCues();
            StagBeetle.Act act = beetle.CurrentAct;
            if (act != previous)
            {
                // The wanderer never digs in or strikes upward, so it never earns a body slam.
                Require(act != StagBeetle.Act.SlamWindup, $"seed {seed}: a body slam began with nobody beneath");
                if (Attacks.Contains(act))
                {
                    attacks++;
                    Require(Warnings.Contains(previous) && warningSteps >= Boss.MinimumWarningSteps,
                        $"seed {seed}: {act} began after {previous} with only {warningSteps} warning steps");
                }
                // A dive's lock follows its hover, so the whole hover counts toward its warning.
                warningSteps = act == StagBeetle.Act.DiveLock ? warningSteps : 0;
                previous = act;
            }
            warningSteps++;

            bool waves = beetle.Shockwaves.Count > 0 || beetle.Shards.Count > 0;
            if (Warnings.Contains(act) && !waves)
                Require(beetle.Hitboxes.Count == 0, $"seed {seed}: a hitbox was live during {act}");
            if (beetle.Opening)
                Require(!beetle.ContactHurts, $"seed {seed}: contact hurt during the opening {act}");
            // Hits land a little past the art and contact stays a little inside it.
            for (int i = 0; i < beetle.Body.Count; i++)
                Require(beetle.Parts[i].Radius > beetle.Body[i].Radius + 0.15f,
                    $"seed {seed}: a hurtbox is not bigger than its contact circle during {act}");
            // A charge stays low enough to jump: its body hitboxes top out under a jump's clearance.
            if (act == StagBeetle.Act.ChargeRun)
                foreach (BossHitbox hitbox in beetle.Hitboxes)
                    Require(hitbox.Area.yMax <= Arena.FloorY + 1.45f, $"seed {seed}: the charge stands {hitbox.Area.yMax - Arena.FloorY:F2} tall");
            Vector2 at = beetle.Position;
            Require(Mathf.Abs(at.x) <= 16f && at.y >= Arena.FloorY - 0.01f && at.y + 3f <= Arena.CeilingY,
                $"seed {seed}: the beetle left the hall at {at} during {act}");
            if (failures > 20)
                return;
        }
        Require(attacks > 40, $"seed {seed}: only {attacks} attacks in six minutes");
    }

    // A player dug into the floor under the beetle gets body-slammed, and so does one who strikes up at it
    // from under its jaws.
    private static void CheckSlam()
    {
        foreach (bool dug in new[] { true, false })
        {
            var beetle = new StagBeetle(Arena, new Vector2(0f, Arena.FloorY), 3);
            // It faces left, so x < 0 is in front: under its thorax when dug in, under its jaws when standing.
            Vector2 position = dug ? new Vector2(-0.8f, 0.2f) : new Vector2(-2.6f, Arena.FloorY + 0.43f);
            var body = new DamageReceiver(new HealthPool(99), 0.34f, () => position, 36);
            var combat = new CombatArena();
            combat.AddTarget(beetle.Damage);
            int hits = 0;
            body.Resolved += (_, outcome) =>
            {
                if (outcome == DamageOutcome.Applied && beetle.CurrentAct == StagBeetle.Act.Slam)
                    hits++;
            };
            bool slammed = false, struck = false;
            for (int step = 0; step < 60 * 6 && !slammed; step++)
            {
                body.Step();
                beetle.Damage.Step();
                if (!dug && !struck && beetle.State == Boss.Mode.Fighting && beetle.CurrentAct == StagBeetle.Act.Idle)
                    struck = combat.Melee(new MeleeStrike(position, Vector2.up, 1.3f, 0.38f, 1, 4f));
                beetle.Step(body);
                beetle.ClearCues();
                slammed |= beetle.CurrentAct == StagBeetle.Act.Slam && hits > 0;
            }
            for (int step = 0; step < 12; step++)
            {
                beetle.Step(body);
                beetle.ClearCues();
            }
            string who = dug ? "a player dug in under it" : "a player striking up from under its jaws";
            Require((dug || struck) && hits > 0, $"slam: {who} was not body-slammed");
        }
        Console.WriteLine("Body slam checked.");
    }

    private static void CheckAirTime(int seeds)
    {
        for (int phase = 1; phase <= 3; phase++)
        {
            int airborne = 0, total = 0;
            for (uint seed = 1; seed <= seeds; seed++)
            {
                var beetle = new StagBeetle(Arena, new Vector2(BossArenaLayout.BossX, Arena.FloorY), seed);
                beetle.DevSetPhase(phase);
                var wanderer = new Wanderer(seed);
                for (int step = 0; step < 60 * 180; step++)
                {
                    wanderer.Step();
                    beetle.Step(wanderer.Body);
                    beetle.ClearCues();
                    if (beetle.State != Boss.Mode.Fighting)
                        continue;
                    total++;
                    if (beetle.Airborne)
                        airborne++;
                }
            }
            float share = airborne / (float)Math.Max(1, total);
            Console.WriteLine($"Phase {phase}: airborne {share:P0} of the time.");
            Require(share < 0.25f, $"phase {phase}: airborne {share:P0}, over a quarter of the fight");
        }
    }

    // ---------- scripted fights ----------
    private static void CheckFights(int seeds)
    {
        int totalSteps = 0, totalHits = 0, wins = 0;
        var hitsBy = new Dictionary<StagBeetle.Act, int>();
        for (uint seed = 1; seed <= seeds; seed++)
        {
            var beetle = new StagBeetle(Arena, new Vector2(BossArenaLayout.BossX, Arena.FloorY), seed);
            var fighter = new Fighter(beetle);
            int step = 0;
            for (; step < 60 * 240 && beetle.State != Boss.Mode.Dead; step++)
            {
                fighter.Step();
                beetle.Step(fighter.Body);
                beetle.ClearCues();
            }
            bool won = beetle.State == Boss.Mode.Dead;
            wins += won ? 1 : 0;
            totalSteps += step;
            totalHits += fighter.HitsTaken;
            foreach (var pair in fighter.HitsBy)
                hitsBy[pair.Key] = hitsBy.TryGetValue(pair.Key, out int count) ? count + pair.Value : pair.Value;
            Console.WriteLine($"  seed {seed}: {(won ? "won" : "LOST")} in {step / 60f:F0} s, took {fighter.HitsTaken} hits, "
                + $"landed {fighter.Landed} ({fighter.Pogos} pogos)");
        }
        Console.WriteLine($"Fights: {wins}/{seeds} won, {totalSteps / 60f / seeds:F0} s and {totalHits / (float)seeds:F1} hits taken on average.");
        var breakdown = new List<string>();
        foreach (var pair in hitsBy)
            breakdown.Add($"{pair.Key} {pair.Value}");
        Console.WriteLine("Hits taken by move: " + string.Join(", ", breakdown));
        Require(wins == seeds, $"the scripted player lost {seeds - wins} fight(s)");
    }

    // A target that walks the floor, climbs to the platforms' heights and jumps now and then, for the
    // warning and air-time checks. It never attacks.
    private sealed class Wanderer
    {
        private SeededRandom random;
        private Vector2 position = new(-15.5f, 1.43f);
        private float goalX;
        private float goalY = 1.43f;
        private int wait;

        internal DamageReceiver Body { get; }

        internal Wanderer(uint seed)
        {
            random = new SeededRandom(seed * 7919u);
            Body = new DamageReceiver(new HealthPool(9999), 0.43f, () => position, 0);
        }

        internal void Step()
        {
            if (--wait <= 0)
            {
                goalX = random.Next(330) / 10f - 16.5f;
                goalY = random.Next(5) switch { 0 => 7.2f, 1 => 10.2f, _ => 1.43f };
                wait = 60 + random.Next(180);
            }
            position = new Vector2(Mathf.MoveTowards(position.x, goalX, 6.5f * StepSeconds), Mathf.MoveTowards(position.y, goalY, 8f * StepSeconds));
        }
    }

    // A player on the floor with the game's run, jump and strike numbers. It keeps a little distance,
    // jumps charges, dives and shockwaves, backs off from snaps, drops under dashes, and strikes
    // during openings or a pogo off the back.
    private sealed class Fighter
    {
        private const float Floor = 1f;
        private const float Radius = 0.43f;
        private const float RunSpeed = 6.5f;
        private const float JumpSpeed = 12.5f;
        private const float Gravity = 38f;
        private readonly StagBeetle beetle;
        private readonly CombatArena combat = new();
        private Vector2 position = new(-15.5f, Floor + Radius);
        private Vector2 velocity;
        private int cooldown;
        private int facing = 1;

        internal DamageReceiver Body { get; }
        internal int HitsTaken { get; private set; }
        internal int Landed { get; private set; }
        internal int Pogos { get; private set; }
        internal readonly Dictionary<StagBeetle.Act, int> HitsBy = new();

        internal Fighter(StagBeetle beetle)
        {
            this.beetle = beetle;
            combat.AddTarget(beetle.Damage);
            Body = new DamageReceiver(new HealthPool(9999), Radius, () => position, 36, impulse => velocity = impulse);
            Body.Resolved += (_, outcome) =>
            {
                if (outcome != DamageOutcome.Applied)
                    return;
                HitsTaken++;
                StagBeetle.Act act = beetle.Shockwaves.Count > 0 && beetle.CurrentAct == StagBeetle.Act.Land
                    ? StagBeetle.Act.Land : beetle.CurrentAct;
                HitsBy[act] = HitsBy.TryGetValue(act, out int count) ? count + 1 : 1;
            };
            beetle.Damage.Resolved += (_, outcome) =>
            {
                if (outcome == DamageOutcome.Applied)
                    Landed++;
            };
        }

        private bool Grounded => position.y <= Floor + Radius + 0.001f;

        internal void Step()
        {
            Body.Step();
            beetle.Damage.Step();
            if (cooldown > 0)
                cooldown--;

            Vector2 boss = beetle.Position;
            float dx = boss.x - position.x;
            float distance = Mathf.Abs(dx);
            int toward = dx < 0f ? -1 : 1;
            StagBeetle.Act act = beetle.CurrentAct;
            int move = 0;
            bool jump = false;

            switch (act)
            {
                case StagBeetle.Act.ChargeRun:
                case StagBeetle.Act.ChargeSkid:
                    // Jump when the jaw tips, 3.7 units ahead of the middle, are under 2 units away.
                    jump = (position.x - boss.x) * beetle.Facing > 0f && (position.x - boss.x) * beetle.Facing < 5.8f;
                    // Once up, cross over toward its back rather than running ahead of the jaws.
                    if (!Grounded)
                        move = -beetle.Facing;
                    break;
                case StagBeetle.Act.ChargeWindup:
                    // Back off while it winds up; if there's no room, jump on the white flash.
                    move = distance < 6.5f && Mathf.Abs(position.x) < 16.5f ? -toward : 0;
                    jump = beetle.CurrentGlow == StagBeetle.Glow.Flash && (position.x - boss.x) * beetle.Facing is > 0f and < 6.5f;
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
                    // Run out from under the locked dive, toward the middle when right below it.
                    int away = Mathf.Abs(dx) > 0.3f ? -toward : position.x > 0f ? -1 : 1;
                    move = distance < 4.6f ? away : 0;
                    break;
                case StagBeetle.Act.GoreDash:
                    float ahead = (position.x - boss.x) * beetle.Facing;
                    jump = ahead > 0f && ahead < 4.8f && Mathf.Abs(beetle.Position.y + 1f - position.y) < 1.6f;
                    break;
                default:
                    // Close in during an opening, and step back before it ends.
                    bool open = beetle.Opening && beetle.ActProgress < 0.75f;
                    float keep = open ? 2.2f : 4.5f;
                    move = distance > keep + 0.3f ? toward : distance < keep - 0.3f && !open ? -toward : 0;
                    break;
            }

            foreach (StagBeetle.Shard shard in beetle.Shards)
            {
                float side = position.x - shard.Position.x;
                if (Mathf.Abs(side) < 1.1f)
                    move = side < 0f ? -1 : 1;
            }
            bool charging = act is StagBeetle.Act.ChargeRun or StagBeetle.Act.ChargeSkid;
            if (!Grounded && velocity.y < 0f && !beetle.Opening && !charging && distance < 3.2f)
                move = -toward;
            foreach (StagBeetle.Shockwave wave in beetle.Shockwaves)
            {
                float ahead = (position.x - wave.X) * wave.Direction;
                if (ahead > 0f && ahead < 2.8f)
                    jump = true;
                if (ahead > 0f && move == wave.Direction * -1)
                    move = 0;
            }

            if (move != 0)
                facing = move;
            velocity.x = Grounded || move != 0 ? move * RunSpeed : velocity.x;
            if (jump && Grounded)
                velocity.y = JumpSpeed;
            velocity.y -= Gravity * StepSeconds;
            position += velocity * StepSeconds;
            position.x = Mathf.Clamp(position.x, -17f, 17f);
            if (position.y < Floor + Radius)
            {
                position.y = Floor + Radius;
                velocity.y = 0f;
            }

            if (cooldown > 0)
                return;
            bool above = !Grounded && velocity.y < 0f && Mathf.Abs(dx) < 2.5f && position.y > boss.y + 1.6f;
            if (above && combat.Melee(new MeleeStrike(position, Vector2.down, 1.3f, 0.38f, 1, 4f)))
            {
                velocity.y = 10.5f;
                Pogos++;
                cooldown = 16;
                return;
            }
            if (beetle.Opening && distance < 3.4f)
            {
                facing = toward;
                combat.Melee(new MeleeStrike(position, Vector2.right * facing, 1.3f, 0.38f, 1, 4f));
                cooldown = 16;
            }
        }
    }
}
